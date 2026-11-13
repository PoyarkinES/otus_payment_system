# Otus Payment System — путеводитель по проекту

> **Статус:** актуально на 13.11.2026. Это навигационный документ, а не замена архитектуре и схеме БД.
>
> **Зачем он нужен:** перед началом issue быстро ответить на вопросы «в каком сервисе делать», «какие сущности и поля», «где строка подключения», «кто кого вызывает», «какие тесты и docs обновить».
>
> **Связанные документы:** [architecture.ru.md](./architecture.ru.md) · [db-schema.ru.md](./db-schema.ru.md) · [bounded-contexts.md](./bounded-contexts.md) · [api-contracts.ru.md](./api-contracts.ru.md) · [api-errors.ru.md](./api-errors.ru.md) · [auth-requirements.ru.md](./auth-requirements.ru.md) · [testing-strategy.ru.md](./testing-strategy.ru.md) · [roadmap.ru.md](./roadmap.ru.md)

## 0. Как пользоваться этим документом

| Если нужно… | Читай |
|---|---|
| Понять, из каких сервисов состоит система | 1 |
| Понять, куда класть код | 2 |
| Запустить локально / поднять стек | 3.1–3.3 |
| Найти строку подключения или ключ конфигурации | 3.4–3.5 |
| Понять, какие сущности и поля есть/планируются | 4 |
| Понять, кто кого вызывает и какие есть API | 5 |
| Сделать новый сервис по шаблону | 6 |
| Начать issue и не забыть ничего | 7 |
| Понять, что уже готово и что дальше | 8 |
| Найти подробный документ | 9 |

**Порядок источников истины:**

1. Код и EF-миграции — что реально работает сейчас.
2. [db-schema.ru.md](./db-schema.ru.md) — целевая схема БД и принятые решения.
3. [architecture.ru.md](./architecture.ru.md) — границы сервисов и сценарии.
4. [auth-requirements.ru.md](./auth-requirements.ru.md) — требования безопасности.
5. Issue — объём конкретной задачи.
6. Этот документ — навигация и процесс; если он противоречит коду, прав код.

## 1. Карта проекта

| Контекст | Папка | Роль | Статус | БД | Host |
|---|---|---|---|---|---|
| Identity | `Identity/` | upstream: пользователи, JWT, refresh | ✅ M1 | PostgreSQL | ✅ `Identity.Host` |
| Wallets | `Wallets/` | supporting: балансы, холды, ledger | 🟡 M2 | PostgreSQL | ✅ `Wallets.Host` |
| Payments | `Payments/` | core: жизненный цикл перевода | ⚪ каркас | PostgreSQL (план) | ⚪ нет |
| Fraud | `Fraud/` | supporting: правила, fallback | ⚪ каркас | PostgreSQL (план) | ⚪ нет |
| Notifications | `Notifications/` | downstream: email/push | ⚪ каркас | PostgreSQL (план) | ⚪ нет |
| Audit | `Audit/` | downstream: append-only журнал | ⚪ каркас | PostgreSQL (план) | ⚪ нет |
| Gateway | `Gateway/` | edge/BFF, не bounded context | ✅ M1 | нет | ✅ `Gateway.Host` |
| Shared | `Shared/` | SharedKernel + общий ProblemDetails | ✅ | нет | — |

Легенда: ✅ готово · 🟡 в работе · ⚪ план/каркас.

Ключевые принципы:

1. Деньги живут только в Wallets; другие сервисы баланс не меняют.
2. Payments решает — Wallets исполняет.
3. Клиент ходит только через Gateway/BFF.
4. У каждого сервиса своя БД; межсервисных FK нет.
5. Идемпотентность и optimistic concurrency — часть контракта, а не «потом».
6. Сначала контракт (API/DB/события), потом код.

## 2. Репозиторий и слои

```text
.
├── Directory.Build.props          # общие настройки MSBuild
├── global.json                    # .NET SDK
├── docker-compose.yml             # postgres + identity + gateway (M1)
├── docker-compose.dev.yml         # только postgres для локальной разработки
├── docker-compose.override.yml    # локальный override (не коммитить)
├── openapi/                       # сгенерированные контракты, руками не править
├── Shared/
│   ├── src/Otus.PaymentSystem.SharedKernel/
│   ├── src/Otus.PaymentSystem.AspNetCore/
│   └── test/
├── Identity/
│   ├── src/Otus.PaymentSystem.Identity.{Domain,Application,Infrastructure,Host}/
│   ├── test/…
│   └── Dockerfile
├── Wallets/                       # Domain/Application/Infrastructure, Host пока нет
├── Payments/                      # каркас Domain/Application/Infrastructure
├── Fraud/                         # каркас
├── Notifications/                 # каркас
├── Audit/                         # каркас
├── Gateway/                       # Host + тесты
└── docs/
```

### 2.1. Слои и ответственность

| Слой | Что здесь | Чего здесь быть не должно |
|---|---|---|
| `Domain` | агрегаты, value objects, инварианты, доменные события, доменные исключения | EF, HTTP, DI, JSON, криптография |
| `Application` | команды/запросы, handler'ы, порты (интерфейсы), DTO результатов | `DbContext`, SQL, ASP.NET Core |
| `Infrastructure` | EF-контекст, конфигурации, миграции, репозитории, JWT/хэширование, внешние клиенты, DI | бизнес-правила, HTTP-эндпоинты |
| `Host` | `Program.cs`, endpoints, middleware, auth, Swagger, health, валидаторы | прямые запросы к БД в обход Application |
| `test` | тесты того слоя, который проверяют | «всё в одном» тесте |

### 2.2. Куда класть код

| Задача | Куда |
|---|---|
| Новый агрегат / value object / инвариант | `<Service>/src/...Domain` |
| Новый сценарий (команда/запрос) | `...Application/Commands` или `...Application/Queries` |
| Новый порт (репозиторий, токен, внешний клиент) | `...Application/Interfaces` |
| EF-конфигурация, миграция, репозиторий | `...Infrastructure/Persistence` |
| JWT/хэш/внешний клиент | `...Infrastructure/Security` или `...Infrastructure/Clients` |
| Эндпоинт, middleware, DI | `...Host/Endpoints`, `...Host/Middleware`, `...Host/Program.cs` |
| Общий ProblemDetails/фильтр | `Shared/src/Otus.PaymentSystem.AspNetCore` |
| Документация и ADR | `docs/` |

> **Правило решения:** в `<Service>.sln` входят только проекты этого сервиса. Общие библиотеки `Shared/*` **не** добавляются в `.sln`; сервисы подключают их через `PackageReference` на NuGet-пакеты: [shared-nuget.ru.md](./shared-nuget.ru.md).

> **Что это значит на практике:** `.sln` — список проектов для IDE и `dotnet build`, а не механизм зависимостей. Сервис собирается из NuGet-пакетов, а не из исходников `Shared`; перед локальной сборкой выполните `scripts/pack-shared.ps1`. Решение о NuGet реализовано ([ADR 0004](./adr/0004-shared-nuget.md), issue #118). `Shared.sln` остаётся источником пакетов и тестов.

## 3. Запуск и конфигурация

### 3.1. Что нужно

- .NET SDK из `global.json` (сейчас .NET 10).
- Docker Desktop / Docker Engine — для PostgreSQL и полного стека.
- PowerShell 7 (примеры команд) — опционально.
- Для Identity: PostgreSQL 17 (контейнер `otus-postgres` или compose).

### 3.2. Быстрый старт

Полный стек M1:

```powershell
docker compose up --build -d
docker compose ps
Invoke-RestMethod http://localhost:8080/bff/health | ConvertTo-Json
```

Локальная БД для Identity:

```powershell
docker compose -f docker-compose.dev.yml up -d
```

Сборка и тесты:

```powershell
.\scripts\pack-shared.ps1
dotnet build Shared/Otus.PaymentSystem.Shared.sln
dotnet build Gateway/Otus.PaymentSystem.Gateway.sln
dotnet build Identity/Otus.PaymentSystem.Identity.sln
dotnet test Identity/Otus.PaymentSystem.Identity.sln --filter "FullyQualifiedName!~Postgres"
```

`pack-shared.ps1` создаёт `artifacts/packages`; без него сервисы не восстановят `Otus.PaymentSystem.*` из локального feed.

### 3.3. Порты

| Что | Локально | В compose | Примечание |
|---|---|---|---|
| Gateway | 8080 | 8080 | единственная публичная точка входа |
| Identity | 5100 | 8080 (внутри) | снаружи недоступен |
| PostgreSQL (dev) | 5433 | — | `docker-compose.dev.yml` |
| PostgreSQL (compose) | — | 5432 (внутри) | наружу не публикуется |
| PostgreSQL (override) | 5434 | — | локальный `docker-compose.override.yml` |
| Frontend | 5173 | — | CORS в Gateway |
| Wallets | план | план | Host ещё не создан |

### 3.4. Строки подключения

| Сервис | Ключ | Где задаётся сейчас | Статус |
|---|---|---|---|
| Identity | `ConnectionStrings:IdentityDb` | user-secrets локально; `ConnectionStrings__IdentityDb` в compose/окружении | ✅ работает |
| Wallets | `ConnectionStrings:WalletsDb` | пока нигде; появится вместе с Wallets.Host | ⚪ план |
| Payments | `ConnectionStrings:PaymentsDb` | пока нигде | ⚪ план |
| Fraud | `ConnectionStrings:FraudDb` | пока нигде | ⚪ план |
| Notifications | `ConnectionStrings:NotificationsDb` | пока нигде | ⚪ план |
| Audit | `ConnectionStrings:AuditDb` | пока нигде | ⚪ план |
| Gateway | — | БД нет | ✅ |

PostgreSQL-параметры для compose — `POSTGRES_DB`, `POSTGRES_USER`, `POSTGRES_PASSWORD` в `.env` (см. `.env.example`).

Пример локальной строки Identity (порт dev-БД 5433):

```text
Host=localhost;Port=5433;Database=otus_identity;Username=otus;Password=<из .env>
```

### 3.5. Каталог конфигурации

| Ключ | Сервис | Назначение | Где сейчас |
|---|---|---|---|
| `Jwt:Issuer` / `Jwt:Audience` | Identity, Gateway | проверка `iss`/`aud` | Identity `appsettings.Development.json`; Gateway `appsettings.json` |
| `Jwt:LifetimeMinutes` | Identity | TTL access-токена | 15 в Development, 60 по умолчанию |
| `Jwt:PrivateKeyPem` | Identity | RS256-ключ в non-Development | `Jwt__PrivateKeyPem` / secrets; в Development — эфемерный |
| `Jwt:JwksUri` | Gateway | адрес JWKS Identity | `appsettings.json` / `Jwt__JwksUri` |
| `RefreshTokens:LifetimeDays` | Identity | срок refresh-токена | 30 |
| `RefreshTokens:SizeBytes` | Identity | энтропия refresh-токена | 32 |
| `Seed:Admin:Email` / `Password` | Identity | dev-админ | `appsettings.Development.json` / `Seed__Admin__*` |
| `Database:AutoMigrate` | Identity | миграции на старте | только compose |
| `Swagger:Enabled` | Identity, Gateway | включить Swagger вне Development | CI/локально |
| `ReverseProxy:Clusters:*` | Gateway | адреса сервисов | `appsettings.json` / env |
| `Cors:AllowedOrigins` | Gateway | origins фронтенда | `appsettings.json` |
| — (код Host) | Identity, Gateway | `IncludeExceptionDetails` в Development | `AddApiProblems` в Program |

> `Jwt:Secret` остался в старом конфиге Identity, но после перехода на RS256 не используется. Не добавляй его в новые сервисы.

### 3.6. Секреты

- Пароли/ключи — в user-secrets локально и в `Jwt__*`/`ConnectionStrings__*` через окружение в compose/CI.
- `.env` и `docker-compose.override.yml` — локальные, не коммитить.
- `Jwt:PrivateKeyPem` — только через секреты; в репозиторий не попадает.
- В логах и ошибках не должно быть паролей, токенов и PII.

### 3.7. Миграции

```powershell
dotnet ef database update \
  --project Identity/src/Otus.PaymentSystem.Identity.Infrastructure \
  --startup-project Identity/src/Otus.PaymentSystem.Identity.Host
```

Для нового сервиса нужен свой `DbContext`, миграции и startup-проект (Host), иначе `dotnet ef` не сможет собрать модель.

### 3.8. Тесты

| Уровень | Где | Что проверяет |
|---|---|---|
| Domain | `<Service>/test/...Domain.Tests` | инварианты агрегатов и VO |
| Application | `...Application.Tests` | handler'ы на фейковых портах |
| Host | `...Host.Tests` | middleware, валидаторы, endpoint-хелперы |
| Infrastructure | `...Infrastructure.Tests` | EF-маппинг, репозитории; Postgres-тесты — отдельно |
| Contract | `openapi/*.json` + CI drift | спека соответствует коду |

Postgres-тесты запускаются с `IDENTITY_TEST_DB` (см. [testing-strategy.ru.md](./testing-strategy.ru.md)).

## 4. Каталог сущностей

> Подробные колонки/индексы — [db-schema.ru.md](./db-schema.ru.md). Здесь — «что это и кто владеет».
>
> `✅` — есть в коде, `⚪` — план.

### 4.1. Identity ✅

| Сущность | Роль | Ключевые поля | Инварианты/заметки |
|---|---|---|---|
| `User` (aggregate root) | пользователь | `Id`, `Email` (VO, unique), `PasswordHash`, `Status`, `Role`, `CreatedAt/UpdatedAt`, `Version` | только `User.Register`; `Block/Unblock`; `ChangeEmail/ChangePassword`; `Version` пока не concurrency token |
| `RefreshToken` (aggregate root) | сессия | `Id`, `UserId`, `TokenHash` (unique), `ExpiresAt`, `RevokedAt`, `ReplacedByTokenId`, `CreatedAt` | только `Issues`; ротация и reuse-детект; raw-токен не хранится |

Подробно: [identity-entity-interactions.ru.md](./identity-entity-interactions.ru.md).

### 4.2. Wallets 🟡

| Сущность | Роль | Ключевые поля | Инварианты/заметки |
|---|---|---|---|
| `Wallet` (aggregate root) | кошелёк | `Id`, `UserId`, `Currency`, `Available`, `Held`, `Status`, `Version`, timestamps | `Create`; `Available/Held ≥ 0`; одна валюта; `UNIQUE(UserId, Currency)`; `Version` — concurrency token |
| `Hold` (внутри Wallet) | заморозка | `Id`, `WalletId`, `TransferId` (unique), `UserId`, `Amount`, `Status`, timestamps | `Active/Completed/Released/Expired`; создаётся только `PlaceHold` |
| `WalletTransaction` (ledger) | история | `Id`, `WalletId`, `Type`, `Amount`, `Currency`, `BalanceBefore/After`, `ReferenceId`, `Description`, `CreatedAt` | append-only; `BalanceAfter = BalanceBefore ± Amount`; hold/release ledger не пишут |
| `Outbox` | события (M4) | `Id`, `EventType`, `Payload`, `OccurredAt`, `PublishedAt` | фабрики/писателя пока нет |

Подробно: [wallets-entity-interactions.ru.md](./wallets-entity-interactions.ru.md).

### 4.3. Payments ⚪ (M3)

| Сущность | Роль | Ключевые поля | Заметки |
|---|---|---|---|
| `Transfer` (aggregate root) | перевод | `Id`, `SourceWalletId`, `DestinationWalletId`, `Amount`, `Currency`, `Status`, `IdempotencyKey`, `ErrorMessage`, timestamps, `CompletedAt` | статусы `Created/Held/Completed/Failed/Confirming` |
| `IdempotencyKey` | идемпотентность | `Key` (PK), `TransferId`, `CreatedAt` | альтернатива — unique-ключ в `Transfers`; решение в M3 |
| `Outbox` | события | как в Wallets | `TransferCreated/Completed/Failed` |

### 4.4. Fraud ⚪ (M5)

| Сущность | Роль | Ключевые поля |
|---|---|---|
| `Rules` | правила | пороги, частота, blacklist, risk score |
| `FraudSettings` | fallback | `fallbackThreshold` |
| `Decisions` | решение | `TransferId`, `Decision` (Passed/Blocked), причина |
| `Blacklist` | блокировки | `UserId`/карта/реквизиты |

### 4.5. Notifications ⚪ (M4)

| Сущность | Роль | Ключевые поля |
|---|---|---|
| `Templates` | шаблоны | `EventType`, `Channel`, `Locale`, тело |
| `SentMessages` | доставка | `EventId` + `Channel` (unique), `Status`, `RetryCount`, `SentAt` |
| `UserPreferences` | opt-in | каналы пользователя |

### 4.6. Audit ⚪ (M4)

| Сущность | Роль | Ключевые поля |
|---|---|---|
| `Events` | append-only журнал | `Id`, `EventId` (unique), `EventType`, `AggregateType`, `AggregateId`, `UserId`, `CorrelationId`, `Payload`, `OccurredAt` |

### 4.7. Gateway

Своих сущностей нет. Gateway хранит только конфигурацию маршрутов, JWT/JWKS-кэш и BFF-представления (`/bff/me`, `/bff/health`).

### 4.8. Общие правила сущностей

- ID — `uuid`; деньги — `numeric(18,4)`; время — `timestamptz`/`DateTimeOffset`.
- В БД enum хранится числом, наружу отдаётся строкой (`JsonStringEnumConverter`).
- Межсервисных FK нет: ссылки по `UserId`/`WalletId`/`TransferId`.
- Дочерние сущности создаются только методами агрегата; EF — единственное исключение (private ctor).
- Доменные события накапливаются в агрегате; публикация — через outbox (M4), не напрямую из handler'а.

## 5. Кто кого вызывает

### 5.1. Публичные маршруты

| Публичный путь | Куда | Auth | Статус |
|---|---|---|---|
| `/api/identity/**` | Identity.Host | JWT для защищённых | ✅ |
| `/api/wallets/**` | Wallets.Host | JWT + ownership в Wallets | ⚪ M2W5 |
| `/api/transfers/**` | Payments.Host | JWT | ⚪ M3 |
| `/api/payments/**` | Payments.Host | API key/merchant | ⚪ M6 |
| `/api/fraud/**` | Fraud.Host | service/admin | ⚪ M5 |
| `/api/notifications/**` | Notifications.Host | admin/read-only | ⚪ M4 |
| `/api/audit/**` | Audit.Host | admin/read-only | ⚪ M4 |
| `/bff/me` | Gateway | JWT | ✅ |
| `/bff/health` | Gateway | anonymous | ✅ |
| `/bff/wallets` | Gateway | JWT | ⚪ M2W7 |
| `/bff/transfers` | Gateway | JWT | ⚪ M3 |

Клиент **не ходит** напрямую в Identity/Wallets/Payments: только через Gateway/BFF.

### 5.2. Синхронные вызовы

| Откуда | Куда | Зачем | Статус |
|---|---|---|---|
| Client | Gateway | все запросы | ✅ |
| Gateway | Identity | `/api/identity/**`, `/bff/health` | ✅ |
| Gateway | Wallets | `/api/wallets/**` | ⚪ |
| Payments | Wallets | `hold`/`confirm`/`release`/`getHold` | ⚪ M3 |
| Payments | Fraud | `check`, `getSettings` | ⚪ M3/M5 |
| Payments | Redis | кэш `user:{id}` | ⚪ M3 |
| Payments | Identity | `GET /users/{id}` при cache miss | ⚪ M3 |
| Notifications | Identity | email получателя | ⚪ M4 |
| Gateway | Identity | `/bff/health` → `/health` | ✅ |

### 5.3. Асинхронные события

| Публикует | События | Подписчики | Транспорт | Статус |
|---|---|---|---|---|
| Payments | `TransferCreated/Completed/Failed` | Notifications, Audit | RabbitMQ + outbox | ⚪ M4 |
| Wallets | `WalletCreated`, `BalanceDeposited/Withdrawn/TransferredIn/Out` | Notifications, Audit | RabbitMQ + outbox | ⚪ M4 |
| Identity | в MVP не публикует | — | — | ✅ (осознанно) |

События холдов не публикуются. Доставка at-least-once, потребители идемпотентны.

### 5.4. Инициализация и вызовы на уровне сущностей

| Сервис | Кто создаёт агрегат | Кто вызывает методы | Подробно |
|---|---|---|---|
| Identity | `User.Register`, `RefreshToken.Issues` | handler'ы через MediatR; EF — материализация | [identity-entity-interactions.ru.md](./identity-entity-interactions.ru.md) |
| Wallets | `Wallet.Create`, `Wallet.PlaceHold` | handler'ы через MediatR; Payments — через API | [wallets-entity-interactions.ru.md](./wallets-entity-interactions.ru.md) |
| Payments | TBD в M3 | TBD | [architecture.ru.md](./architecture.ru.md) |
| Fraud/Notifications/Audit | TBD в M4–M5 | TBD | [bounded-contexts.md](./bounded-contexts.md) |

Общее правило: **endpoint → MediatR → handler → репозиторий/агрегат**; endpoint не работает с `DbContext`, handler не создаёт дочерние сущности вручную.

### 5.5. Перевод: высокоуровневый поток

1. Клиент → Gateway: `POST /api/transfers` + `Idempotency-Key`.
2. Gateway → Payments: JWT, `sub` = отправитель.
3. Payments: проверка ключа → валидация получателя (Redis/Identity) → `Transfer(Created)`.
4. Payments → Wallets: `hold` (идемпотентно по `transferId`).
5. Payments → Fraud: `check` (Approve/Block).
6. Approve → Wallets `confirm`; Block → Wallets `release`.
7. Payments: `Completed`/`Failed`/`Confirming`, outbox-событие.
8. Notifications/Audit получают событие через RabbitMQ.

### 5.6. Auth и ownership

- Gateway валидирует JWT по JWKS Identity и маршрутизирует запрос.
- Каждый сервис **сам** валидирует JWT (issuer/audience/signature/lifetime) и берёт `sub` из токена.
- `X-User-Id` добавляется Gateway только для внутренних вызовов; публичные endpoint'ы ему не доверяют.
- Ownership проверяется в Application/Domain: чужой существующий ресурс → `403`, несуществующий → `404`.
- `401` — нет/невалиден токен; `403` — токен есть, но прав нет; `409` — конфликт состояния.
- Service-to-service аутентификация в MVP не реализована — известный security-долг.

## 6. Типовой сервис: шаблон

### 6.1. Domain

- агрегат с private ctor для EF и публичной фабрикой (`Create/Register/Issues`);
- value objects с валидацией в конструкторе;
- инварианты — в методах, а не в handler'е;
- доменные события — `IApplicationEvent`/`IDomainEvent`;
- доменные ошибки — `DomainError`/`DomainException`.

### 6.2. Application

- команда/запрос — record с параметрами;
- handler — `IRequestHandler`, зависимости только через порты;
- порты — `I*Repository`, `I*Factory`, `I*Service`;
- результат — record/DTO, без EF-сущностей наружу;
- транзакция — через scoped `DbContext`/`SaveChanges`, отдельный `IUnitOfWork` только если нужен.

### 6.3. Infrastructure

- `DbContext` + `IEntityTypeConfiguration<T>` на каждую сущность;
- конвертации VO, `numeric`, `timestamptz`, enum → `int`;
- индексы, unique, FK, `IsConcurrencyToken`;
- репозитории — тонкие обёртки над `DbSet`;
- миграции — через `dotnet ef`;
- DI-расширение `Add<Service>Infrastructure`.

### 6.4. Host

- `Program.cs`: Serilog, `AddApiProblems`, Swagger, auth, DI, health;
- endpoints — minimal API, `RequireAuthorization`, `Produces`/`ProducesProblem`;
- `DomainExceptionMiddleware` — маппинг ошибок;
- валидаторы — `ValidationFilter<T>`;
- `Database:AutoMigrate` — только compose.

### 6.5. Gateway

- маршрут `/api/<service>/{**catch-all}`;
- политика `authenticated` или anonymous;
- path transform без `/api/<service>`;
- `X-User-Id` — только для внутренних вызовов;
- BFF-агрегаты — отдельно, только read-only.

### 6.6. Общие правила для всех сервисов

| Тема | Правило |
|---|---|
| JWT | Сервис валидирует JWT сам: issuer/audience/signature/lifetime, RS256, JWKS Identity. Без OIDC-discovery; кэш 10 минут + refresh по unknown `kid`. См. [ADR 0005](./adr/0005-jwt-validation-in-services.md). |
| DTO | Результаты handler'ов — в `Application` (рядом с командой/запросом или в `Application/Contracts`). HTTP-запросы — в `Host/Contracts`. |
| DomainError | `Validation→400`, `Unauthorized→401`, `Forbidden→403`, `NotFound→404`, `Conflict→409`. |
| IUnitOfWork | Не вводим, пока репозиторий один: `I*Repository.SaveChangesAsync` + scoped `DbContext`. Появится при нескольких репозиториях в одной транзакции. |
| EF read-only nav | Для read-only коллекций — явный `UsePropertyAccessMode(Field)` + runtime-тест с `Include`. |
| Миграции | Если Host готов — Host как startup-project; если нет — `IDesignTimeDbContextFactory<T>` в `Infrastructure/Persistence`, строка из env/secrets. |
| Эндпоинты | Extension-метод `Map<Service>Endpoints()`, группа внутри сервиса (например, `/wallets`), `Program.cs` только вызывает его и `/health`. |
| `.sln` | Только проекты сервиса; `Shared` не добавляется, подключается через `PackageReference` на локальный feed. |
| Конфигурация | Строки подключения — `ConnectionStrings:<Service>Db`; секреты — env/user-secrets, не код. |

## 7. Как делать issue

### 7.1. Definition of Ready: паспорт issue

Перед началом заполни (в issue или локально):

```text
## Паспорт issue
- Сервис/контекст:
- Агрегат/сущности:
- Пользовательский сценарий:
- API (метод, путь, request/response, ошибки):
- Изменения БД (таблицы, колонки, индексы):
- Auth/ownership:
- Конфигурация (connection string, ключи, порты):
- События/outbox:
- Тесты (domain/application/host/infrastructure):
- Какие docs обновить:
- DoD:
```

Если на любой пункт нет ответа — сначала найди его в разделах 4–5 или документах, а не в коде «на глаз».

### 7.2. Пошаговый алгоритм

0. **Ветка + чтение.** `git switch -c feature/<issue>-<slug>`; прочитай issue, разделы 4–5, [db-schema.ru.md](./db-schema.ru.md), [auth-requirements.ru.md](./auth-requirements.ru.md).
1. **Контракт.** Зафиксируй API/DB/события в docs; при изменении DTO — не забудь про OpenAPI drift.
2. **Domain.** Агрегат/VO/инварианты/события + Domain.Tests. Никакого EF/HTTP.
3. **Application.** Команда/запрос, handler, порты, DTO + Application.Tests на фейках.
4. **Infrastructure.** EF-конфигурация, миграция, репозиторий, DI + Infrastructure.Tests.
5. **Host.** `Program.cs`, endpoints, auth/ownership, ProblemDetails, Swagger, health.
6. **Gateway.** Маршрут `/api/<service>/**`, политика, `X-User-Id` (если внутренний вызов).
7. **Тесты.** Domain/Application/Host/Infrastructure; Postgres — отдельным фильтром.
8. **Docs + issue.** Обнови разделы 4–5 этого гайда, db-schema, api-contracts, interaction-док; отметь чекбоксы issue.
9. **PR + CI.** `dotnet build`, `dotnet test`, drift, Docker build; PR с описанием «что/зачем/как проверить».
10. **Demo.** Если сценарий пользовательский — добавь шаг в demo/README.

### 7.3. Definition of Done

- [ ] Код собирается, тесты зелёные.
- [ ] Инварианты в Domain, а не в endpoint/handler.
- [ ] Нет прямых `DbContext`/SQL в Host или Domain.
- [ ] Миграция создаёт/меняет схему; применена локально.
- [ ] API соответствует `Produces`/`ProducesProblem` и [api-errors.ru.md](./api-errors.ru.md).
- [ ] Auth/ownership проверены; `401/403/404/409` различимы.
- [ ] Конфигурация не содержит секретов; ключи описаны в 3.5.
- [ ] Тесты покрывают happy path и ключевые ошибки.
- [ ] Docs/issue обновлены; OpenAPI перегенерирован.
- [ ] Нет `.env`, `docker-compose.override.yml`, `bin/obj`, `.vs` в коммите.

### 7.4. Частые ошибки

| Симптом | Правило |
|---|---|
| `X-User-Id` используют как доказательство личности | Публичный endpoint валидирует JWT и берёт `sub`; `X-User-Id` — только внутренний |
| Бизнес-логика в endpoint'е | Только MediatR → handler → агрегат |
| `new Wallet/User/Hold` в handler'е | Только фабрика агрегата; EF — private ctor |
| `SaveChanges` в середине сценария | Одна транзакция на сценарий; исключения — осознанно |
| Нет миграции после изменения EF | `dotnet ef migrations add` + `database update` |
| Строка подключения «зашита» в код | `ConnectionStrings:*` + env/secrets |
| Событие публикуется из handler'а напрямую | Outbox в той же транзакции (M4) |
| `DateTimeOffset.UtcNow` в тестах | `TimeProvider`/фиксированное время (где появится) |
| OpenAPI правится руками | Генерируется из кода/CI drift |
| Чужой ресурс отдаёт `404` «для безопасности» | По политике: `403` для существующего чужого, `404` для отсутствующего |

## 8. Текущий статус и что дальше

| Направление | Статус | Следующий шаг |
|---|---|---|
| Identity | ✅ M1 | поддержка, события — M4 |
| Gateway/BFF | ✅ M1 | маршрут Wallets и 5 E2E добавлены; зелёный CI — при PR в main |
| PostgreSQL + миграции | ✅ Identity + Wallets | миграция Wallets применена; CHECK-ограничения — позже |
| Wallets | 🟡 домен + Host + Gateway + 36 тестов | зелёный CI (PR в main), CHECK, техдолг |
| Payments | ⚪ каркас | M3 |
| Fraud | ⚪ каркас | M5 |
| Notifications/Audit | ⚪ каркас | M4 |
| Web UI | ⚪ | параллельно M2–M6 |

Текущие пробелы (на 13.11.2026):

1. CI workflow добавлен, но `on: push/pull_request: branches: [main]` — на feature-ветке `50ab97f` прогона нет; зелёный CI не подтверждён.
2. `WalletsDb`/`JwksUri` в `appsettings.Development` — compose-стиль; Wallets в compose нет, локально нужны env.
3. `CHECK (Available >= 0)`/`Held >= 0` в БД нет; `TimeProvider`, `Outbox`, `Freeze/Close`, `DbUpdateConcurrencyException` — открыты.
5. У Identity доменные события не публикуются и не очищаются; outbox появится в M4.
6. `User.Version` не concurrency token; гонки профиля возможны.
7. Service-to-service auth отсутствует; `X-User-Id` доверяется внутри сети.

Зависимости:

- Не начинать Wallets, пока не готовы PostgreSQL/Gateway (M1) — уже готовы.
- Не начинать Transfers, пока Wallets и hold/confirm/release не закрыты.
- Не включать RabbitMQ/outbox, пока не готовы агрегаты Payments/Wallets.

## 9. Где что искать

| Документ | Когда читать |
|---|---|
| [architecture.ru.md](./architecture.ru.md) | границы сервисов, сценарий перевода, модели данных |
| [db-schema.ru.md](./db-schema.ru.md) | таблицы, колонки, индексы, решения по схеме |
| [bounded-contexts.md](./bounded-contexts.md) | агрегаты и ответственность контекстов (EN) |
| [api-contracts.ru.md](./api-contracts.ru.md) | OpenAPI-артефакты, contract-first |
| [api-errors.ru.md](./api-errors.ru.md) | ProblemDetails, коды, traceId |
| [auth-requirements.ru.md](./auth-requirements.ru.md) | JWT, роли, ownership, политики |
| [testing-strategy.ru.md](./testing-strategy.ru.md) | уровни тестов, CI, Postgres |
| [roadmap.ru.md](./roadmap.ru.md) | месяцы, недели, issue, exit criteria |
| [identity-entity-interactions.ru.md](./identity-entity-interactions.ru.md) | кто создаёт/вызывает сущности Identity |
| [wallets-entity-interactions.ru.md](./wallets-entity-interactions.ru.md) | кто создаёт/вызывает сущности Wallets |
| [shared-nuget.ru.md](./shared-nuget.ru.md) | Shared как NuGet: пакеты, версии, feed, CI, миграция |
| [adr/0004-shared-nuget.md](./adr/0004-shared-nuget.md) | решение о NuGet и альтернативы |
| [adr/0005-jwt-validation-in-services.md](./adr/0005-jwt-validation-in-services.md) | единый механизм JWT-валидации через JWKS |
| [adr/0001-api-gateway.md](./adr/0001-api-gateway.md) | зачем Gateway и какие маршруты |
| `openapi/identity.v1.json`, `openapi/gateway.v1.json` | сгенерированные контракты (не править руками) |

## 10. Глоссарий

- **Bounded context** — сервис со своей моделью и БД.
- **Aggregate root** — сущность, через которую только и можно менять состояние.
- **Hold** — заморозка суммы: `Available → Held` без списания.
- **Ledger** — `WalletTransactions`: append-only история операций.
- **Outbox** — таблица событий, сохраняемая в одной транзакции с бизнес-данными.
- **BFF** — backend-for-frontend: read-only агрегаты Gateway.
- **JWKS** — публичные ключи Identity, по которым Gateway проверяет JWT.
- **Idempotency-Key** — ключ клиента, защищающий от повторного перевода.
- **Optimistic concurrency** — `Version` + `WHERE Version = @old`; конфликт → `409`/retry.
- **Ownership** — пользователь видит только свои ресурсы; проверка в Application/Domain.
