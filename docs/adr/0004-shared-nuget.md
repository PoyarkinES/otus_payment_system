# ADR 0004. Shared-библиотеки как NuGet-пакеты

- **Статус:** Accepted, реализовано в #118
- **Дата:** 2026-11-13
- **Контекст:** монорепо, per-context решения, M2W5 и подготовка к независимой сборке сервисов
- **Связанные документы:** [shared-nuget.ru.md](../shared-nuget.ru.md), [project-guide.ru.md](../project-guide.ru.md)

## Контекст

Сейчас `SharedKernel` и `AspNetCore` подключены к сервисам через `ProjectReference`. Это нормально для монорепо, но:

- сборка сервиса физически требует папку `Shared` рядом;
- сервис нельзя вынести в отдельный репозиторий без копирования/сабмодуля Shared;
- нет версионирования: все сервисы всегда на одной версии;
- CI не проверяет сборку сервиса с опубликованной версией Shared.

Обсуждение началось с частного случая: `AspNetCore` был добавлен в `Wallets.sln` как проект решения. Это нарушает per-context структуру, но удаление из `.sln` **не** убирает зависимость — `ProjectReference` в `csproj` остаётся. Нужно решение, которое действительно развязывает сборки.

## Решение

1. `Otus.PaymentSystem.SharedKernel` и `Otus.PaymentSystem.AspNetCore` упаковываются в NuGet-пакеты.
2. Сервисы подключают их через `PackageReference` с фиксированной версией.
3. `Shared.sln` остаётся в монорепо как источник пакетов и тестов; он не добавляется в `.sln` сервисов.
4. Версионирование — SemVer; общая версия на оба пакета на старте.
5. Локально — `artifacts/packages` + `NuGet.config`; в CI — artifact или GitHub Packages/Azure Artifacts.
6. Миграция выполнена: Identity → Gateway → Wallets → остальные сервисы.

## Рассмотренные альтернативы

| Вариант | Почему нет |
|---|---|
| Оставить `ProjectReference` | Просто, но нет физической изоляции и версионирования; вынос сервиса в отдельный repo ломается |
| Git submodule / копия Shared | Дублирование или сложная синхронизация; всё равно нет версий пакетов |
| Отдельный репозиторий Shared | Больше инфраструктуры, чем нужно для учебного монорепо |
| NuGet-пакеты | Выбран: версии, feed, независимая сборка, монорепо сохраняется |

## Последствия

**Плюсы**

- Сервис собирается по `PackageReference`, без исходников Shared рядом.
- Явные версии и контроль breaking changes.
- CI может проверить сборку с опубликованным пакетом.
- Подготовка к выносу сервисов в отдельные репозитории.

**Минусы / риски**

- Медленнее inner loop: изменение Shared требует `pack`/publish.
- Нужно управлять версиями и обновлять `PackageReference`.
- Появляется feed и его доступность/аутентификация.
- Риск версионного расхождения между сервисами.
- `AspNetCore` тянет `Microsoft.AspNetCore.App`; пакет предназначен только для Host.

**Меры**

- `Shared/Directory.Build.props` — единая версия.
- Central Package Management — опционально.
- CI job `pack-shared` перед сборкой сервисов.
- Версии фиксированные; обновление — отдельным PR.
- Не смешивать миграцию с фичами (M2W5).

## План внедрения

| Фаза | Результат |
|---|---|
| 0 | ADR + `shared-nuget.ru.md` |
| 1 | Метаданные пакетов, `IsPackable`, `NuGet.config`, локальный `pack` — ✅ |
| 2 | Identity на `PackageReference` — ✅ |
| 3 | Gateway на `PackageReference` — ✅ |
| 4 | Wallets на `PackageReference` — ✅ |
| 5 | Payments/Fraud/Notifications/Audit — ✅ |
| 6 | CI `pack-shared` + feed/artifact; `ProjectReference` на Shared удалены — ✅ |

## Что этот ADR не решает

- Конкретный feed для production (GitHub Packages vs Azure Artifacts) — решается при настройке CI.
- Публикация символов/SourceLink — опционально.
- Версионирование через GitVersion/NBGV — опционально.
- Service-to-service аутентификация — отдельный security-долг.

## Ссылки

- [Shared как NuGet-пакеты](../shared-nuget.ru.md)
- [Путеводитель по проекту](../project-guide.ru.md)
- [Архитектура](../architecture.ru.md)
