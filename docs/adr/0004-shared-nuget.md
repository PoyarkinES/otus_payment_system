# ADR 0004. Shared-библиотеки как NuGet-пакеты

- **Статус:** Accepted, реализовано в #118; feed переведён на GitHub Packages в #133/#134
- **Дата:** 2026-11-13
- **Обновлено:** 2026-11-27
- **Контекст:** монорепо, per-context решения, M2W5 и подготовка к независимой сборке сервисов
- **Связанные документы:** [shared-nuget.ru.md](../shared-nuget.ru.md), [project-guide.ru.md](../project-guide.ru.md)

## Контекст

`SharedKernel` и `AspNetCore` подключены к сервисам через `ProjectReference`. Это нормально для монорепо, но:

- сборка сервиса физически требует папку `Shared` рядом;
- сервис нельзя вынести в отдельный репозиторий без копирования/сабмодуля Shared;
- нет версионирования: все сервисы всегда на одной версии;
- CI не проверяет сборку сервиса с опубликованной версией Shared.

Обсуждение началось с частного случая: `AspNetCore` был добавлен в `Wallets.sln` как проект решения. Это нарушает per-context структуру, но удаление из `.sln` **не** убирает зависимость — `ProjectReference` в `csproj` остаётся. Нужно решение, которое действительно развязывает сборки.

## Решение

1. `Otus.PaymentSystem.SharedKernel` и `Otus.PaymentSystem.AspNetCore` упаковываются в NuGet-пакеты.
2. Сервисы подключают их через `PackageReference` с фиксированной версией.
3. `Shared.sln` остаётся в монорепо как источник пакетов и тестов; он не добавляется в `.sln` сервисов.
4. Версионирование — SemVer; общая версия на оба пакета.
5. Пакеты публикуются в **GitHub Packages** (`https://nuget.pkg.github.com/PoyarkinES/index.json`), а не в локальный feed.
6. Локально разработчик использует свой GitHub-токен через `gh auth` и `NuGetPackageSourceCredentials_github`; токен не хранится в репозитории.
7. Docker-сборка получает токен через BuildKit secret и восстанавливает пакеты из GitHub Packages.
8. Демо-стек использует готовые GHCR-образы и не требует токена.

## Рассмотренные альтернативы

| Вариант | Почему нет |
|---|---|
| Оставить `ProjectReference` | Просто, но нет физической изоляции и версионирования; вынос сервиса в отдельный repo ломается |
| Git submodule / копия Shared | Дублирование или сложная синхронизация; всё равно нет версий пакетов |
| Отдельный репозиторий Shared | Больше инфраструктуры, чем нужно для учебного монорепо |
| Локальный `artifacts/packages` | Требует предварительного `pack` на каждой машине и привязывает сборку к структуре папок |
| GitHub Packages | Выбран: версии, внешний feed, независимая сборка, монорепо сохраняется |

## Последствия

**Плюсы**

- Сервис собирается по `PackageReference`, без исходников Shared рядом.
- Явные версии и контроль breaking changes.
- Docker-образы не содержат исходников Shared и токенов.
- Демо-стек не требует GitHub/NuGet-токена.
- Подготовка к выносу сервисов в отдельные репозитории.

**Минусы / риски**

- Медленнее inner loop: изменение Shared требует bump/publish и обновления `PackageReference`.
- Нужно управлять версиями и обновлять `PackageReference` во всех сервисах.
- GitHub Packages не перезаписывает версии; ошибка с версией требует нового релиза.
- Нужен токен с `read:packages` для локального restore и Docker-сборки.
- Риск версионного расхождения между сервисами.
- `AspNetCore` тянет `Microsoft.AspNetCore.App`; пакет предназначен только для Host.

**Меры**

- `Shared/Directory.Build.props` — единая версия.
- Тег `shared-v<version>` задаёт версию пакетов; после публикации `scripts/bump-shared.ps1` синхронизирует `<Version>` и consumer-ссылки.
- Локально — `gh auth login` + `gh auth refresh -s read:packages`.
- В CI — `packages: read` и `GITHUB_TOKEN`; в Docker — BuildKit secret.
- Версии фиксированные; обновление — отдельным PR.
- Массовый bump — `scripts/bump-shared.ps1`; `Directory.Packages.props` не используем.

## Обновление 2026-11-25

- #133: добавлен `publish-shared` по тегу `shared-v*`; пакеты `1.0.0` опубликованы.
- #134: Identity/Gateway Dockerfile’ы переведены на GitHub Packages + BuildKit secret; CI передаёт токен.
- #135: `docker-compose.yml` переведён на GHCR-образы Identity/Gateway; демо не требует токена.
- В CI остался legacy job `Pack Shared NuGet packages` с artifact, который больше не участвует в restore; вынесено в техдолг #137.
- Wallets уже на `PackageReference`, но Docker/Compose для него ещё не сделаны — #128.

## Обновление 2026-11-27

- #137: удалён локальный Shared artifact из CI и Docker; CI восстанавливает только опубликованные пакеты. Для Shared выбран release-first порядок.
- #138: CI запускает `scripts/bump-shared.ps1 -Check` и проверяет синхронность всех consumer-ссылок.
- #139: CI проверяет Compose и GHCR-манифесты; #157 перевёл Compose на мутабельный `latest` + `pull_policy: always`, SHA-PR убран.
- #128: Wallets включён в Compose; отдельная БД создаётся bootstrap-сервисом.
- Shared `1.0.1` опубликован; Identity, Gateway и Wallets пересобраны с согласованным IdentityModel (#142/#143).

## План внедрения

| Фаза | Результат |
|---|---|
| 0 | ADR + `shared-nuget.ru.md` |
| 1 | Метаданные пакетов, `IsPackable`, `NuGet.config` — ✅ |
| 2 | Identity на `PackageReference` — ✅ |
| 3 | Gateway на `PackageReference` — ✅ |
| 4 | Wallets на `PackageReference` — ✅ |
| 5 | Payments/Fraud/Notifications/Audit — ✅ |
| 6 | GitHub Packages + BuildKit secret + GHCR для Identity/Gateway/Wallets — ✅ |
| 7 | Wallets Docker/Compose — ✅ #128 |

## Что этот ADR не решает

- Автоматизация версий — `scripts/bump-shared.ps1`; `Directory.Packages.props` не вводим.
- JWT blocking/JWKS — закрыт (#126/#150).
- Публикация символов/SourceLink — опционально.
- Service-to-service аутентификация — отдельный security-долг.

## Ссылки

- [Shared как NuGet-пакеты](../shared-nuget.ru.md)
- [Путеводитель по проекту](../project-guide.ru.md)
- [Архитектура](../architecture.ru.md)
