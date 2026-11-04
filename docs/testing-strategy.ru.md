# Стратегия тестирования

Документ закрывает issue #16: какие тесты есть, где они живут, что проверяет CI и где мы сознательно недокрыты.

Принцип: **тест живёт в проекте того слоя, который он проверяет.** Доменный инвариант не должен проверяться из `Host.Tests`, а middleware — из `Domain.Tests`. Иначе тесты начинают зависеть от инфраструктуры и перестают быть быстрыми.

## Уровни и где они лежат

| Уровень | Проект | Что проверяется | Зависимости |
|---|---|---|---|
| Доменные инварианты | `Identity/test/Otus.PaymentSystem.Identity.Domain.Tests` | агрегаты `User`, `RefreshToken`, value objects: переходы статусов, ротация и reuse-детект, запреты | нет |
| Сценарии приложения | `Identity/test/Otus.PaymentSystem.Identity.Application.Tests` | хендлеры MediatR: login/refresh/logout/register/block/update, отзыв цепочки при смене пароля | фейковые репозитории и `ITokenService` |
| Host-уровень | `Identity/test/Otus.PaymentSystem.Identity.Host.Tests` | middleware (`CorrelationId`, `DomainException`), валидаторы контрактов, `ClaimsPrincipalExtensions` | `DefaultHttpContext` |
| Инфраструктура | `Identity/test/Otus.PaymentSystem.Identity.Infrastructure.Tests` | EF-маппинг, репозитории, формат refresh-токена, `JwtTokenService` на RS256 | PostgreSQL для части тестов |
| Edge / BFF | `Gateway/test/Otus.PaymentSystem.Gateway.Host.Tests` | `UserIdHeaderMiddleware`, `CorrelationIdMiddleware`, хендлеры `/bff/me` и `/bff/health` | подменённый `HttpMessageHandler` |
| HTTP-конвейер хоста | `Gateway.Host.Tests`, `Identity.Host.Tests` (`WebApplicationFactory`) | 401/403/200 через реальные auth-политики, агрегат health, формат ProblemDetails | TestServer, тестовая auth-схема, фейковый репозиторий |
| Общая библиотека | `Shared/test/Otus.PaymentSystem.AspNetCore.Tests` | `ApiProblems`, `ValidationFilter`, middleware ответов | нет |
| Контракт | `openapi/*.v1.json` + jobs `openapi-drift`, `openapi-gateway-drift` | спеки соответствуют коду | запуск Host |
| Смоук | [demo-m1.ru.md](./demo-m1.ru.md) | стек в Docker: логин, BFF, админский `GET /users/{id}` | docker compose |

Проверенные числа на 2026-11-04: Identity — **106 тестов** (без Postgres-тестов), Gateway — **20 тестов**.

## Правила размещения новых тестов

- Инвариант сущности или value object → `Domain.Tests`. Без БД, без HTTP.
- Логика команды/запроса → `Application.Tests`, зависимости — через интерфейсы и фейки.
- Маппинг EF, миграции, SQL-специфика → `Infrastructure.Tests`, имя теста содержит `Postgres`, если нужна реальная БД.
- Middleware, валидаторы, хендлеры эндпоинтов → `Host.Tests`; сеть подменяется `HttpMessageHandler`, не мокается `IHttpClientFactory` целиком.
- Новый публичный сценарий без теста **не проходит ревью**. Исключение — только с явной записью в разделе «Пробелы» ниже.

## Тесты с PostgreSQL

Часть тестов инфраструктуры требует реальной БД: `EfUserRepositoryPostgresTests`.

- Локально: поднять `otus-postgres`, задать `IDENTITY_TEST_DB` и запустить точечно.

  ```powershell
  $env:IDENTITY_TEST_DB = 'Host=localhost;Port=5434;Database=otus_identity_test;Username=otus;Password=otus'
  dotnet test Identity\test\Otus.PaymentSystem.Identity.Infrastructure.Tests `
      --filter "FullyQualifiedName~EfUserRepositoryPostgresTests"
  ```

- В CI это отдельный job `identity-integration-tests` с сервис-контейнером `postgres:17`; остальные job гоняют фильтр `FullyQualifiedName!~Postgres`, чтобы не требовать БД.

## Контракт OpenAPI

`openapi/identity.v1.json` — артефакт, а не ручная правка. Любое изменение DTO, кодов ответа или маршрутов требует перегенерации, иначе job `openapi-drift` падает на `git diff --exit-code -- openapi`. Процедура — в [api-contracts.ru.md](./api-contracts.ru.md), шаг 2.

## Что проверяет CI

| Job | Что делает | Что ловит |
|---|---|---|
| `Build` (матрица 8 решений) | `dotnet build -c Release` | ошибки компиляции в любом контексте |
| `Test (Shared/Gateway/Identity)` | `dotnet test` + coverage | регрессии юнит- и host-тестов |
| `identity-integration-tests` | тесты с реальным PostgreSQL | расхождения EF-маппинга и SQL |
| `openapi-drift` | генерация спеки Identity и `git diff` | контракт Identity разошёлся с кодом |
| `openapi-gateway-drift` | генерация спеки Gateway и `git diff` | контракт BFF разошёлся с кодом |
| `Docker build (Identity/Gateway)` | `docker build -f <Dockerfile> .` | Dockerfile и контекст сборки разъехались с кодом |

## Команды локально

```powershell
dotnet test Shared\Otus.PaymentSystem.Shared.sln
dotnet test Gateway\Otus.PaymentSystem.Gateway.sln
dotnet test Identity\Otus.PaymentSystem.Identity.sln --filter "FullyQualifiedName!~Postgres"
```

Сборка всех решений — то же, что делает `Build`:

```powershell
Get-ChildItem -Recurse -Filter *.sln | ForEach-Object { dotnet build $_.FullName -c Release }
```

## Пробелы (осознанные)

1. **Нет E2E через UI** — фронтенда пока нет.
2. **Wallets/Payments/Fraud/Notifications/Audit** содержат только доменные заготовки; их тесты появятся вместе с кодом в M2–M3.
3. **Нет тестов на rate limiting** кроме ручной проверки 429 — лимитер живёт в конфигурации Gateway, а не в коде.
4. **HTTP-тесты Identity используют фейковый репозиторий**, а не реальную БД: EF-путь проверяется отдельно в `Infrastructure.Tests` (Postgres).

## Definition of Done для теста

- Тест падает, если откатить исправление (проверяется вручную при ревью).
- Имя теста читается как утверждение: `GetMe_NoRole_ReturnsUnauthorized`.
- Нет `Thread.Sleep`, нет обращений во внешнюю сеть, время не зависит от часов машины.
- Тест не требует порядка запуска: любой тест можно запустить отдельно.
