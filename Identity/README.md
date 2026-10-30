# Identity

## Локальный запуск

1. Поднять PostgreSQL:

```powershell
docker compose -f docker-compose.dev.yml up -d
```

2. Создать локальный `.env`:

```powershell
Copy-Item .env.example .env
```

3. Записать connection string в user-secrets (пароль — из `.env`):

```powershell
dotnet user-secrets set "ConnectionStrings:IdentityDb" `
  "Host=localhost;Port=5433;Database=otus_identity;Username=otus;Password=<пароль из .env>" `
  --project Identity/src/Otus.PaymentSystem.Identity.Host
```

4. Применить миграции:

```powershell
dotnet ef database update `
  --project Identity/src/Otus.PaymentSystem.Identity.Infrastructure `
  --startup-project Identity/src/Otus.PaymentSystem.Identity.Host
```

5. Запустить:

```powershell
dotnet run --project Identity/src/Otus.PaymentSystem.Identity.Host
```

## Интеграционные тесты

Нужна отдельная тестовая БД:

```powershell
docker exec otus-postgres psql -U otus -d postgres -c 'CREATE DATABASE otus_identity_test;'
```

```powershell
$env:IDENTITY_TEST_DB = 'Host=localhost;Port=5433;Database=otus_identity_test;Username=otus;Password=<пароль из .env>'

dotnet test Identity/test/Otus.PaymentSystem.Identity.Infrastructure.Tests/Otus.PaymentSystem.Identity.Infrastructure.Tests.csproj `
  --arch x64 --filter "FullyQualifiedName~EfUserRepositoryPostgresTests"
```
