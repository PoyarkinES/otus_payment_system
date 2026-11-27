# Стратегия тестирования

Документ закрывает issue #16: какие тесты есть, где они живут, что обязательно для PR и где мы сознательно недокрыты.

Принцип: **тест живёт в проекте того слоя, который он проверяет.** Доменный инвариант не должен проверяться из `Host.Tests`, а middleware — из `Domain.Tests`. Иначе тесты начинают зависеть от инфраструктуры и перестают быть быстрыми.

## Уровни и где лежат тесты

| Уровень | Проект | Что проверяет | Зависимости |
|---|---|---|---|
| Доменные инварианты | `Identity.Domain.Tests` | агрегаты `User`, `RefreshToken`, value objects: переходы статусов, ротация и reuse-детект, запреты | нет |
| Доменные инварианты | `Wallets.Domain.Tests` | `Wallet`, `Hold`, `Money`: переходы статусов, лимиты, балансы, идемпотентность холда | нет |
| Сценарии приложения | `Identity.Application.Tests` | хендлеры MediatR: login/refresh/logout/register/block/update, отзыв цепочки при смене пароля | фейковые репозитории, `NSubstitute` для `ITokenService` |
| Host-уровень | `Identity.Host.Tests` | middleware (`CorrelationId`, `DomainException`), валидаторы контрактов, `ClaimsPrincipalExtensions` | `DefaultHttpContext` |
| HTTP-конвейер | `Wallets.Host.IntegrationTests` | эндпоинты кошелька, ownership, hold/confirm/release, история операций, пагинация | `WebApplicationFactory`, тестовая auth-схема, PostgreSQL |
| Middleware | `Wallets.Host.Tests` | `DomainExceptionMiddleware`, маппинг кодов ошибок | `DefaultHttpContext` |
| Инфраструктура | `Identity.Infrastructure.Tests` | EF-маппинг, репозитории, формат refresh-токена, `JwtTokenService` на RS256 | PostgreSQL для части тестов |
| Инфраструктура | `Wallets.Infrastructure.Tests` | EF-маппинг, ограничения БД, версионирование, пагинация, маппинг ошибок уникальности | PostgreSQL |
| Edge / BFF | `Gateway.Host.Tests` | `/bff/me`, `/bff/health`, `/bff/wallets`, суммы/операции, upstream-заголовки, 502/503 | подменённый `HttpMessageHandler` |
| E2E через Gateway | `Gateway.Host.IntegrationTests` | YARP и BFF поверх реальных `Wallets.Host`/`Identity.Host` через TestServer | PostgreSQL |
| Общая библиотека | `Shared.AspNetCore.Tests` | `ApiProblems`, `ValidationFilter`, middleware ответов | нет |
| UI | `Client` (`Vitest` + Testing Library) | компоненты, роутер, `apiFetch`, обработка ProblemDetails | `jsdom`, MSW |
| Контракт | `openapi/*.v1.json` + drift-джобы | спеки соответствуют коду | запуск Host |
| Смоук | [demo-m1.ru.md](./demo-m1.ru.md) | стек в Docker: логин, BFF, админский `GET /users/{id}` | docker compose |

Отдельного `Wallets.Application.Tests` нет: логика команд проверяется HTTP-тестами через `Host.IntegrationTests`. Это осознанная экономия, а не образец — см. «Пробелы».

## Правила размещения новых тестов

- Инвариант сущности или value object → `Domain.Tests`. Без БД, без HTTP.
- Логика команды/запроса → `Application.Tests`, зависимости — через интерфейсы и фейки.
- Маппинг EF, миграции, SQL-специфика → `Infrastructure.Tests`; имя теста содержит `Postgres`, если нужна реальная БД.
- Middleware, валидаторы, хендлеры эндпоинтов → `Host.Tests`; сеть подменяется `HttpMessageHandler`, а не мокается `IHttpClientFactory` целиком.
- Сквозной сценарий через Gateway → `Gateway.Host.IntegrationTests`; Wallets поднимается in-process, но сохраняются реальные HTTP-конвейеры.
- Тест на конкурентность не использует `Thread.Sleep` и не зависит от порядка запуска: результат проверяется по состоянию в БД и коду ответа.
- Классы `Wallets.Host.IntegrationTests` выполняются последовательно (`AssemblyInfo.cs`): они делят одну тестовую БД, и параллельное применение миграций даёт гонку `42P07`/`42710`.
- Новый публичный сценарий без теста **не проходит ревью**. Исключение — только с явной записью в разделе «Пробелы».

## Критические сценарии

| Сценарий | Где проверяется | Джоба CI |
|---|---|---|
| Аутентификация: логин, refresh, ротация, reuse, block/unblock | `Identity.Application.Tests`, `Identity.Domain.Tests`, `Identity.Host.Tests` | `Test (Identity)`, `Identity integration tests` |
| Ownership кошелька: чужой → 403, несуществующий → 404 | `WalletsApiTests` | `Wallets integration tests` |
| Баланс после deposit/withdraw и идемпотентность по `Idempotency-Key` | `WalletsApiTests` | `Wallets integration tests` |
| Hold/confirm/release: балансы, повторный вызов, конкурентность | `WalletHoldsApiTests` | `Wallets integration tests` |
| Откат перевода: `confirm` после `release` и наоборот → 409 | `WalletHoldsApiTests` | `Wallets integration tests` |
| История операций: порядок, страницы, `pageSize`, 400 на невалидных параметрах | `WalletOperationsApiTests`, `EfWalletRepositoryPostgresTests` | `Wallets integration tests` |
| BFF: агрегация кошельков и операций, `recentOperations` 0..20, 502/503 | `BffWalletsEndpointTests`, `WalletsGatewayE2ETests` | `Test (Gateway)`, `Gateway E2E` |
| Fraud block | не реализовано (M5), тестов нет | — |
| Rate limiting | ручная проверка 429 | — |

## Инструменты

| Инструмент | Где используется | Комментарий |
|---|---|---|
| xUnit 2.9 + `Assert` | все .NET-тесты | базовый раннер и утверждения |
| `NSubstitute` | `Identity.Application.Tests` | подмена `ITokenService` и внешних зависимостей |
| `WebApplicationFactory` / TestServer | `Wallets.Host.IntegrationTests`, `Gateway.Host.IntegrationTests` | реальный HTTP-конвейер без сети |
| PostgreSQL 17 | инфраструктурные тесты, Wallets Host, Gateway E2E | локально — docker compose, в CI — service container |
| `coverlet.collector` | job `Test (...)`, артефакт `coverage-*` | cobertura, без гейта |
| `Vitest` + Testing Library + MSW | `Client` | компонентные тесты, API мокается на уровне fetch |
| `Orval` | `Client` | генерация клиента из `openapi/gateway.v1.json` |

Осознанно **не используем**:

- `FluentAssertions` — стандартного `Assert` достаточно, лишняя зависимость в каждом тестовом проекте;
- `Testcontainers` — в CI уже есть service-контейнер PostgreSQL, а локально тот же контейнер поднимает docker compose; Testcontainers добавит время старта и вторую конфигурацию БД;
- `Moq` — в Identity используется `NSubstitute`, в остальных местах хватает ручных фейков.

Решение пересматривается, если появится матрица из нескольких СУБД или понадобится поднимать брокер сообщений в тестах.

## Покрытие

Цели по слоям — ориентир для ревью, а не жёсткий гейт: CI собирает cobertura и складывает артефакты `coverage-*`, но порог не проверяет.

| Слой | Цель |
|---|---|
| Domain | ≥ 90% |
| Application | ≥ 85% |
| Infrastructure | ≥ 70% |
| Host / API | ≥ 70% |
| Client | не измеряется |

Порог в CI включим после стабилизации M3, когда появятся Payments и Fraud. До этого покрытие смотрим глазами на ревью: падение покрытия в затронутом слое — повод для вопроса, а не для блокировки.

## Что запускает CI

| Джоба | Что делает | Что ловит |
|---|---|---|
| `Shared package version check` | `scripts/bump-shared.ps1 -Check` | расхождение версии Shared и consumer-ссылок |
| `Compose image tags` | `docker compose config` + `docker manifest inspect` для мутабельного `latest` | неверный Compose, отсутствующий manifest или не-`latest` тег |
| `Build` (матрица 8 решений) | `dotnet build -c Release` | ошибки компиляции в любом контексте |
| `Test (Shared/Gateway/Identity/Wallets)` | `dotnet test` + coverage | регрессии unit/host-тестов |
| `Identity integration tests (PostgreSQL)` | тесты с реальным PostgreSQL | расхождения EF-маппинга и SQL |
| `Wallets integration tests (PostgreSQL)` | Host HTTP-тесты Wallets и Postgres-тесты репозитория | гонки, ограничения БД, ownership, holds, история |
| `Gateway E2E (Wallets through Gateway)` | YARP + BFF через TestServer | сквозной сценарий через Gateway |
| `OpenAPI contract drift` | генерация спеки Identity и `git diff` | контракт Identity разошёлся с кодом |
| `OpenAPI gateway drift` | генерация спеки Gateway и `git diff` | контракт BFF разошёлся с кодом |
| `Docker build (Identity/Gateway)` | `docker build` с BuildKit secret для GitHub Packages | Dockerfile, контекст и токен сборки разъехались с кодом |
| `Client (lint, test, build)` | генерация клиента, lint, format, typecheck, Vitest, `vite build` | устаревший сгенерированный клиент, ошибки типов, регрессии UI |

Фильтры в матрице `Test`:

- Shared — без фильтра;
- Gateway — `FullyQualifiedName!~IntegrationTests`;
- Identity — `FullyQualifiedName!~Postgres`;
- Wallets — `FullyQualifiedName!~Postgres & FullyQualifiedName!~IntegrationTests`.

Что обязательно для PR: все перечисленные джобы. Мерж блокируется, если хотя бы одна красная: branch protection требует все обязательные чеки, включая drift, интеграционные тесты и клиент.

Публикация образов запускается вручную; после публикации workflow проверяет GHCR manifests и открывает или обновляет PR с новыми Compose SHA. Для этого PR CI запускается отдельно.

Nightly-воркфлоу пока нет. Кандидаты на него: полный прогон всех Postgres-тестов, пятикратный повтор конкурентных тестов (`hold`/`confirm`/`deposit`/`withdraw`), смоук `docker compose` и проверка уязвимостей зависимостей. Заведём отдельной задачей, когда появится первый платный раннер или вырастет время PR-прогона.

## Локальный запуск

```powershell
$user = gh api user --jq .login
$token = gh auth token
$env:NuGetPackageSourceCredentials_github = "Username=$user;Password=$token;ValidAuthenticationTypes=Basic"

dotnet test Shared\Otus.PaymentSystem.Shared.sln
dotnet test Gateway\Otus.PaymentSystem.Gateway.sln
dotnet test Identity\Otus.PaymentSystem.Identity.sln --filter "FullyQualifiedName!~Postgres"
dotnet test Wallets\Otus.PaymentSystem.Wallets.sln --filter "FullyQualifiedName!~Postgres & FullyQualifiedName!~IntegrationTests"
```

Postgres-тесты:

```powershell
$env:IDENTITY_TEST_DB  = 'Host=localhost;Port=5434;Database=otus_identity_test;Username=otus;Password=payments'
$env:WALLETS_API_TEST_DB  = 'Host=localhost;Port=5433;Database=otus_wallets_api_test;Username=otus;Password=payments'
$env:WALLETS_REPO_TEST_DB = 'Host=localhost;Port=5433;Database=otus_wallets_repo_test;Username=otus;Password=payments'

dotnet test Identity\test\Otus.PaymentSystem.Identity.Infrastructure.Tests --filter "FullyQualifiedName~EfUserRepositoryPostgresTests"
dotnet test Wallets\test\Otus.PaymentSystem.Wallets.Infrastructure.Tests --filter "FullyQualifiedName~Postgres"
dotnet test Wallets\test\Otus.PaymentSystem.Wallets.Host.IntegrationTests
dotnet test Gateway\test\Otus.PaymentSystem.Gateway.Host.IntegrationTests
```

Клиент:

```powershell
cd Client
npm ci
npm run api:generate
npm run lint
npm run typecheck
npm run test
npm run build
```

Перед локальной сборкой нужен GitHub-токен с `read:packages` и переменная `NuGetPackageSourceCredentials_github` (см. блок выше). `scripts/pack-shared.ps1` для restore больше не нужен: пакеты берутся из GitHub Packages.

## Пробелы (осознанные)

1. **`Wallets.Application.Tests` отсутствует.** Логика `PlaceHold`/`ConfirmHold`/`ReleaseHold` (ретраи, автосоздание кошелька получателя) проверяется только HTTP-тестами. При усложнении сценариев — завести проект.
2. **Нет UI E2E.** Компонентные тесты Client есть, сквозного сценария в браузере (Playwright) нет.
3. **Payments/Fraud/Notifications/Audit — каркас.** Их тесты появятся в M3–M5; `Audit` пока содержит только заготовки проектов.
4. **Rate limiting не покрыт тестами** — лимитер живёт в конфигурации Gateway, проверяется руками.
5. **Identity HTTP-тесты используют фейковый репозиторий**, а не реальную БД; EF-путь проверяется отдельно в `Identity.Infrastructure.Tests`.
6. **Нет nightly-прогона** — конкурентные тесты выполняются один раз на PR.
7. **Wallets Host integration-классы последовательны** из-за общей БД; при росте числа классов стоит развести базы по коллекциям, чтобы вернуть параллельность.

## Definition of Done для теста

- Тест падает, если откатить исправление (проверяется вручную при ревью).
- Имя теста читается как утверждение: `GetMe_NoRole_ReturnsUnauthorized`.
- Нет `Thread.Sleep`, нет обращений во внешнюю сеть, время не зависит от часов машины.
- Тест не требует порядка запуска: любой тест можно запустить отдельно.
- Тест с БД убирает за собой данные или использует уникальные идентификаторы, чтобы не влиять на соседние тесты.
