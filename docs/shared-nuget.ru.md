# Shared как NuGet-пакеты

> **Статус:** решение реализовано в #118; публикация вынесена в GitHub Packages в #133, Docker-сборка переведена на BuildKit secret в #134, демо-стек — на GHCR-образы в #135.
>
> Связанные: [project-guide.ru.md](./project-guide.ru.md) · [ADR 0004](./adr/0004-shared-nuget.md)

## 1. Решение

`SharedKernel` и `AspNetCore` упаковываются в NuGet-пакеты и публикуются в **GitHub Packages**. Сервисы подключают их через `PackageReference` с фиксированной версией.

Монорепо остаётся: `Shared.sln` — источник пакетов и тестов, а не набор проектов внутри каждого сервисного `.sln`. Для Docker и сервисов папка `Shared` больше не нужна: они восстанавливают пакет из feed.

## 2. Пакеты

| Пакет | Проект | Кто использует | Зависимости |
|---|---|---|---|
| `Otus.PaymentSystem.SharedKernel` | `Shared/src/Otus.PaymentSystem.SharedKernel` | все `*.Domain` | нет |
| `Otus.PaymentSystem.AspNetCore` | `Shared/src/Otus.PaymentSystem.AspNetCore` | все `*.Host` | `Microsoft.AspNetCore.App`, `FluentValidation` |

Тестовые проекты не пакуются: `IsPackable=false`.

Текущее состояние:

- `AspNetCore.csproj`: `IsPackable=true`, `PackageId=Otus.PaymentSystem.AspNetCore`.
- `SharedKernel.csproj`: `PackageId=Otus.PaymentSystem.SharedKernel`; версия — `Shared/Directory.Build.props`.
- Identity, Gateway, Wallets, Payments, Fraud, Notifications, Audit используют `PackageReference 1.0.0`.
- `NuGet.config` содержит только GitHub Packages и nuget.org; локального `artifacts/packages` в источниках нет.

## 3. Feed

- Источник: `https://nuget.pkg.github.com/PoyarkinES/index.json`.
- Пакеты версии `1.0.0` опубликованы по тегу `shared-v1.0.0`.
- `NuGet.config` в корне:

```xml
<configuration>
    <packageSources>
        <clear />
        <add key="github" value="https://nuget.pkg.github.com/PoyarkinES/index.json" />
        <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
    </packageSources>
</configuration>
```

Пароль в `NuGet.config` не хранится. Учётные данные передаются через `NuGetPackageSourceCredentials_github` (локально и в CI) или через BuildKit secret (в Docker).

## 4. Версионирование

SemVer:

- `MAJOR` — breaking change в публичном API;
- `MINOR` — новая обратная совместимая функциональность;
- `PATCH` — исправления;
- prerelease — `1.1.0-preview.1`.

Правила:

1. Одна версия на оба пакета.
2. Сервисы фиксируют точную версию, без `*`.
3. GitHub Packages **не перезаписывает** уже опубликованную версию.
4. Изменил Shared — подними версию, обнови `PackageReference` во всех сервисах, собери и опубликуй пакеты. Для массового обновления есть `scripts/bump-shared.ps1`.
5. Публикация — только по тегу `shared-v<version>`; версия тега должна совпадать с версией в `Shared/Directory.Build.props`.
6. `Directory.Packages.props` **не используем**. Версия остаётся явной в каждом `PackageReference`; синхронность проверяет `scripts/bump-shared.ps1 -Check`.

## 5. Локальная разработка

### 5.1. Один раз на ПК

```bash
gh auth login
gh auth refresh -s read:packages
```

Токен хранится в `gh` на машине. В проект его класть не нужно.

### 5.2. Перед сборкой

`NuGet.config` не содержит credentials, поэтому `dotnet restore` нужно передать их через переменную окружения:

```powershell
$user = gh api user --jq .login
$token = gh auth token
$env:NuGetPackageSourceCredentials_github = "Username=$user;Password=$token;ValidAuthenticationTypes=Basic"

dotnet build Shared/Otus.PaymentSystem.Shared.sln
dotnet build Gateway/Otus.PaymentSystem.Gateway.sln
dotnet build Identity/Otus.PaymentSystem.Identity.sln
```

В новой сессии PowerShell переменную нужно выставить снова. Если не хочется повторять — добавьте эти строки в профиль PowerShell или в локальный скрипт, который не коммитится.

### 5.3. `scripts/pack-shared.ps1`

Скрипт собирает `.nupkg` в `artifacts/packages`. Для restore он **не нужен**: `NuGet.config` больше не смотрит в локальный feed. Используйте его только для проверки состава пакетов или ручной публикации.

### 5.4. Docker

`Identity/Dockerfile` и `Gateway/Dockerfile` восстанавливают Shared из GitHub Packages через BuildKit secret:

```dockerfile
RUN --mount=type=secret,id=github_token \
    export NuGetPackageSourceCredentials_github="Username=${NUGET_USERNAME};Password=$(cat /run/secrets/github_token);ValidAuthenticationTypes=Basic" \
    && dotnet restore ... --configfile NuGet.config
```

Локальная сборка образа:

```powershell
$tokenFile = Join-Path $env:TEMP "github_token"
Set-Content -NoNewline -Path $tokenFile -Value (gh auth token)
docker build --secret id=github_token,src=$tokenFile -f Identity/Dockerfile -t otus-identity:local .
Remove-Item $tokenFile
```

В CI секрет передаёт `docker/build-push-action`.

### 5.5. Демо-стек

`docker-compose.yml` использует готовые публичные образы Identity/Gateway из GHCR. Для запуска токен не нужен:

```powershell
docker compose pull
docker compose up -d
```

Образы закреплены по `sha-…`; после изменения кода публикуется новый образ и тег в compose обновляется отдельным PR.

## 6. CI

1. **Публикация пакетов** — `.github/workflows/publish-shared.yml`, триггер `shared-v*`:
   - проверяет, что версия тега совпадает с версией пакетов;
   - пакует два проекта;
   - публикует в GitHub Packages с `packages: write`.
2. **Build/test** — `permissions: packages: read`; jobs получают `NuGetPackageSourceCredentials_github` из `GITHUB_TOKEN` и восстанавливают пакеты из GitHub Packages.
3. **Docker build** — job `docker` передаёт `GITHUB_TOKEN` в BuildKit secret; токен не попадает в образ.
4. **Образы** — `.github/workflows/publish-images.yml` (ручной запуск) собирает Identity/Gateway и публикует в GHCR теги `latest` и `sha-<commit>`.

В CI остался legacy job `Pack Shared NuGet packages`, который пакует Shared и загружает artifact. Сейчас artifact не участвует в restore (в `NuGet.config` нет локального источника); это вынесено в техдолг #137.

## 7. Изменение Shared

1. Изменить код в `Shared/`.
2. Обновить версию и все ссылки одной командой:
   ```powershell
   .\scripts\bump-shared.ps1 -Version 1.0.1
   ```
   Скрипт меняет `<Version>` в `Shared/Directory.Build.props` и `Version=` во всех `PackageReference`.
3. Проверить синхронность:
   ```powershell
   .\scripts\bump-shared.ps1 -Check
   ```
4. Прогнать build/test.
5. Смержить PR.
6. Поставить тег `shared-v<version>` — workflow опубликует пакеты.
7. Если менялся код сервисов — опубликовать новые GHCR-образы и обновить `sha-…` в `docker-compose.yml`.

## 8. Миграция

| Фаза | Что сделано | Проверка |
|---|---|---|
| 0 | ADR + этот документ | решение зафиксировано |
| 1 | Метаданные, `IsPackable`, `NuGet.config` | `dotnet pack` создаёт `.nupkg` |
| 2 | Identity → `PackageReference` | ✅ |
| 3 | Gateway → `PackageReference` | ✅ |
| 4 | Wallets → `PackageReference` | ✅ |
| 5 | Payments/Fraud/Notifications/Audit | ✅ |
| 6 | GitHub Packages + BuildKit secret + GHCR | ✅ для Identity/Gateway |
| 7 | Wallets Docker/Compose | ⏳ #128 |

## 9. Риски и меры

| Риск | Мера |
|---|---|
| Забыли поднять версию | версия в `Shared/Directory.Build.props`, checklist в PR |
| Версии разъехались | `scripts/bump-shared.ps1 -Check` |
| Нет токена локально | `gh auth login` + `gh auth refresh -s read:packages` |
| Токен попал в образ | только BuildKit secret, не `ARG`/`--build-arg` |
| CI проверяет не тот Shared | PR с изменением Shared требует bump/publish; текущий artifact не используется — техдолг #137 |
| Образ устарел | после публикации обновить `sha-…` в compose; автоматизация — техдолг #139 |
| Breaking change без major | SemVer + ревью публичного API |

## 10. FAQ

**`pack-shared.ps1` всё ещё нужен?**
Нет, для restore не нужен. Только для проверки пакетов или ручной публикации.

**Почему локальный feed убрали?**
Чтобы сервисы и Docker не зависели от структуры папок монорепо. Единственный источник — GitHub Packages.

**Можно одновременно `ProjectReference` и `PackageReference`?**
На один проект — нет. Все сервисы уже на `PackageReference`.

**Почему `dotnet restore` отвечает 401?**
Нет `NuGetPackageSourceCredentials_github` или токен без `read:packages`. Выполните `gh auth refresh -s read:packages` и выставьте переменную.

**Можно переопубликовать `1.0.0`?**
Нет, GitHub Packages не перезаписывает версию. Поднимите версию и обновите `PackageReference`.

## 11. Ссылки

- [ADR 0004](./adr/0004-shared-nuget.md)
- [project-guide.ru.md](./project-guide.ru.md)
- [architecture.ru.md](./architecture.ru.md)
- [testing-strategy.ru.md](./testing-strategy.ru.md)
