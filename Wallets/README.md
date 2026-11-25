# Otus.PaymentSystem.Wallets

Сервис кошельков: создание кошелька, чтение баланса, пополнение и списание.

## Состав

```text
Wallets/
├── src/
│   ├── Otus.PaymentSystem.Wallets.Domain/          # Wallet, Hold, WalletTransaction, Money
│   ├── Otus.PaymentSystem.Wallets.Application/     # MediatR handlers, IWalletRepository
│   ├── Otus.PaymentSystem.Wallets.Infrastructure/  # EF Core, PostgreSQL, миграции
│   └── Otus.PaymentSystem.Wallets.Host/            # ASP.NET Core minimal API
└── test/
    ├── Otus.PaymentSystem.Wallets.Domain.Tests/
    ├── Otus.PaymentSystem.Wallets.Host.Tests/
    ├── Otus.PaymentSystem.Wallets.Infrastructure.Tests/
    └── Otus.PaymentSystem.Wallets.Host.IntegrationTests/
```

## Требования

- .NET SDK из `global.json` (сейчас .NET 10).
- PostgreSQL 17.
- Docker Desktop / Docker Engine для локальной БД.
- Shared-пакеты: доступ к GitHub Packages (токен с `read:packages`), см. ниже.

## Локальный запуск

### 1. PostgreSQL

Из корня репозитория:

```powershell
docker compose -f docker-compose.dev.yml up -d
```

Контейнер: `otus-postgres`, порт `5433`.

Если базы `otus_wallets` ещё нет:

```powershell
docker exec -e PGPASSWORD=payments -it otus-postgres psql -U otus -d postgres -c "CREATE DATABASE otus_wallets;"
```

### 2. Shared NuGet

Один раз на ПК:

```bash
gh auth login
gh auth refresh -s read:packages
```

Перед сборкой в PowerShell:

```powershell
$user = gh api user --jq .login
$token = gh auth token
$env:NuGetPackageSourceCredentials_github = "Username=$user;Password=$token;ValidAuthenticationTypes=Basic"
```

Пакеты `Otus.PaymentSystem.SharedKernel` и `Otus.PaymentSystem.AspNetCore` восстанавливаются из GitHub Packages. `scripts/pack-shared.ps1` для restore не нужен.

### 3. Переменные окружения

В PowerShell:

```powershell
$env:ConnectionStrings__WalletsDb="Host=localhost;Port=5433;Database=otus_wallets;Username=otus;Password=payments"
$env:Jwt__JwksUri="http://localhost:5100/.well-known/jwks.json"
$env:Database__AutoMigrate="true"
```

В cmd:

```cmd
set "ConnectionStrings__WalletsDb=Host=localhost;Port=5433;Database=otus_wallets;Username=otus;Password=payments"
set "Jwt__JwksUri=http://localhost:5100/.well-known/jwks.json"
set "Database__AutoMigrate=true"
```

`Jwt:Issuer` и `Jwt:Audience` уже заданы в `appsettings.json`; `Jwt:JwksUri` должен указывать на Identity.

### 4. Запуск

```powershell
dotnet run --project Wallets/src/Otus.PaymentSystem.Wallets.Host
```

Сервис: `http://localhost:5200`.

- `GET /health` → 200;
- `GET /swagger` → Swagger UI в Development.

Порт 5200 задан в `launchSettings.json`.

### 5. Gateway

Публичный вход — Gateway на `http://localhost:8080`:

```text
POST /api/wallets
GET  /api/wallets/{id}
POST /api/wallets/{id}/deposit
POST /api/wallets/{id}/withdraw
```

Gateway трансформирует `/api/wallets/**` → `/wallets/**` и проксирует на `http://localhost:5200/`.

## API

### Создать кошелёк

```http
POST /api/wallets
Authorization: Bearer <token>
Content-Type: application/json

{ "currency": "RUB" }
```

### Получить кошелёк

```http
GET /api/wallets/{walletId}
Authorization: Bearer <token>
```

### Пополнить

```http
POST /api/wallets/{walletId}/deposit
Authorization: Bearer <token>
Idempotency-Key: <uuid>
Content-Type: application/json

{ "amount": 1000.00 }
```

### Списать

```http
POST /api/wallets/{walletId}/withdraw
Authorization: Bearer <token>
Idempotency-Key: <uuid>
Content-Type: application/json

{ "amount": 100.00 }
```

`Idempotency-Key` обязателен для deposit/withdraw. Повтор с тем же ключом не создаёт вторую операцию.

## Тесты

Нужны две тестовые БД:

- `otus_wallets_api_test` — Host HTTP и Gateway E2E;
- `otus_wallets_repo_test` — Infrastructure/Postgres.

```powershell
$env:WALLETS_API_TEST_DB="Host=localhost;Port=5433;Database=otus_wallets_api_test;Username=otus;Password=payments"
$env:WALLETS_REPO_TEST_DB="Host=localhost;Port=5433;Database=otus_wallets_repo_test;Username=otus;Password=payments"

dotnet test Wallets/Otus.PaymentSystem.Wallets.sln
```

Отдельно:

```powershell
dotnet test Wallets/test/Otus.PaymentSystem.Wallets.Host.IntegrationTests
dotnet test Gateway/test/Otus.PaymentSystem.Gateway.Host.IntegrationTests
```

## Документация

- [wallets-entity-interactions.ru.md](../docs/wallets-entity-interactions.ru.md)
- [db-schema.ru.md](../docs/db-schema.ru.md)
- [ADR 0006 — deposit/withdraw, idempotency](../docs/adr/0006-wallet-operations-idempotency.md)
- [project-guide.ru.md](../docs/project-guide.ru.md)
