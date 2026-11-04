# Otus Payment System

[![CI](https://github.com/PoyarkinES/otus_payment_system/actions/workflows/ci.yml/badge.svg)](https://github.com/PoyarkinES/otus_payment_system/actions/workflows/ci.yml)

Учебный fintech-проект для портфолио .NET-разработчика.

## Цель проекта

Создать реалистичную платёжную платформу, которая демонстрирует навыки:

- ASP.NET Core
- Entity Framework Core
- PostgreSQL
- JWT-аутентификация
- API Gateway / BFF (YARP)
- RabbitMQ
- CQRS / MediatR
- Clean Architecture
- DDD
- Микросервисы
- Docker
- GitHub Actions
- мониторинг и observability

## Формат проекта

Это учебный и портфолио-проект. Его цель — развиваться поэтапно, без попытки реализовать всё сразу.

**Текущий план:** 6 месяцев core-разработки (ноябрь 2026 — апрель 2027). Полный frontend UI — отдельный post-1.0 трек, чтобы не сорвать backend-цели.

## Структура репозитория

Репозиторий организован как набор независимых микросервисных решений. Каждый bounded context живёт в своей папке и имеет собственный `.sln`-файл с единой структурой `src` / `test`.

```
.
├── Directory.Build.props        # общие настройки MSBuild (TFM, nullable, анализаторы)
├── global.json                  # зафиксированная версия .NET SDK
├── docker-compose.yml           # полный стек M1: postgres + identity + gateway
├── .dockerignore
├── openapi/                     # OpenAPI-контракт Identity (генерируется, не правится руками)
├── Shared/
│   ├── Otus.PaymentSystem.Shared.sln
│   ├── src/Otus.PaymentSystem.SharedKernel/
│   ├── src/Otus.PaymentSystem.AspNetCore/   # единый ProblemDetails для Host-проектов
│   └── test/
├── Gateway/                     # API Gateway / BFF (edge, не bounded context)
│   ├── Otus.PaymentSystem.Gateway.sln
│   ├── Dockerfile
│   └── src/Otus.PaymentSystem.Gateway.Host/
├── Identity/
│   ├── Otus.PaymentSystem.Identity.sln
│   ├── Dockerfile
│   ├── src/Otus.PaymentSystem.Identity.{Domain,Application,Infrastructure,Host}/
│   └── test/
├── Wallets/                     # такая же структура
├── Payments/                    # такая же структура
├── Fraud/                       # такая же структура
├── Notifications/               # такая же структура
├── Audit/                       # такая же структура (с заготовками тестовых проектов)
└── docs/
    ├── roadmap.ru.md
    ├── architecture.ru.md
    ├── api-contracts.ru.md
    ├── api-errors.ru.md
    ├── bounded-contexts.md
    ├── db-schema.ru.md
    ├── testing-strategy.ru.md
    ├── demo-m1.ru.md
    ├── retro-m1.ru.md
    └── adr/0001-api-gateway.md
```

Каждый контекст следует Clean Architecture со следующими слоями:

- **Domain** — сущности, value objects, доменные события, без внешних зависимостей (ссылается только на `SharedKernel`).
- **Application** — сценарии использования, команды/запросы, DTO (ссылается на `Domain`).
- **Infrastructure** — персистентность, внешние сервисы (ссылается на `Domain`).
- **Host** — ASP.NET Core Web API, wiring, middleware.

`SharedKernel` — самостоятельное решение, на него ссылается каждый `*.Domain`-проект.

## Bounded contexts и edge

- Identity
- Wallets
- Payments (core domain)
- Audit
- Fraud
- Notifications
- **Gateway** — не bounded context, а edge/BFF: маршрутизация, JWT, CORS, rate limiting, агрегация для фронтенда.

Важно не путать:

- **API Gateway** — единая точка входа между фронтендом и сервисами (месяц 1).
- **Payment Gateway** — merchant acquiring: API keys, платежи, webhooks, возвраты (месяц 6).

Подробнее: [ADR 0001](./docs/adr/0001-api-gateway.md).

## Полный стек (Docker Compose)

Один `docker compose up` поднимает PostgreSQL, Identity и Gateway. Наружу открыт только Gateway — Identity и БД доступны лишь внутри сети compose.

```powershell
docker compose up --build -d
docker compose ps
Invoke-RestMethod http://localhost:8080/bff/health | ConvertTo-Json
```

- Вход: `http://localhost:8080`, демо-админ `admin@otus.local` / `AdminP@ssw0rd`.
- Схема БД применяется на старте контейнера (`Database__AutoMigrate=true`); в обычном `dotnet run` миграции по-прежнему накатываются через `dotnet ef database update`.
- Если в корне лежит локальный `docker-compose.override.yml`, запускай явно: `docker compose -f docker-compose.yml up --build`.

Пошаговый сценарий с негативными проверками: [docs/demo-m1.ru.md](./docs/demo-m1.ru.md). Уровни тестов и что проверяет CI: [docs/testing-strategy.ru.md](./docs/testing-strategy.ru.md).

## План развития

| Месяц | Цель |
|---|---|
| M0 (октябрь 2026) | Каркас, документация, Identity MVP — **факт** |
| M1 (ноябрь 2026) | API Gateway/BFF, PostgreSQL, refresh/logout, CI-тесты, Docker — **факт** |
| M2 (декабрь 2026) | Wallets: `available`/`held`, пополнение/списание, holds, история, optimistic locking |
| M3 (январь 2027) | Transfers: идемпотентность, Redis-кэш получателя, hold/confirm/release, аудит, outbox |
| M4 (февраль 2027) | RabbitMQ, Audit Service, Notifications Service |
| M5 (март 2027) | Fraud: лимиты, velocity, fallback, blacklist, risk scoring |
| M6 (апрель 2027) | Merchant Payments: API keys, webhooks, refunds, релиз 1.0 |
| Web UI (параллельно M2–M6) | Вход, профиль, кошелёк, переводы, E2E через Gateway |
| M7 (апрель–май 2027) | **Post-1.0:** KYC/Onboarding — MongoDB, MinIO, gRPC, безопасность PII |
| M8 (май–июнь 2027) | **Post-1.0:** Mobile MVP — auth, кошелёк, переводы, push |

Критический путь:

```
Gateway → PostgreSQL → Wallets (+ holds) → Transfers (Redis, hold, Fraud sync) → Outbox/RabbitMQ → Audit/Notifications → Merchant Payments → Release 1.0
```

## Статус

- **M1 закрыт (эпик #40):**
  - Identity: регистрация, логин, профиль, роли, block/unblock, refresh/logout с ротацией и reuse-детектом, RS256 + JWKS, PostgreSQL + миграции.
  - Gateway: YARP, валидация JWT по JWKS, `X-User-Id`, CORS, rate limiting (429 + `Retry-After`), BFF `/bff/me` и `/bff/health`.
  - Инфраструктура: Serilog + correlation ID, единый ProblemDetails, валидация DTO, Docker Compose полного стека.
- **Тесты:** 101 в Identity, 17 в Gateway, отдельно `Shared`; тесты с PostgreSQL — отдельным job.
- **CI** собирает 8 решений, запускает тесты Shared/Gateway/Identity, интеграцию с PostgreSQL и проверяет контракт OpenAPI (`openapi-drift`).
- **Не сделано (M2+):** Wallets, Transfers, RabbitMQ, Redis, Fraud, Merchant Payments, frontend UI. Wallets/Payments/Fraud/Notifications/Audit пока содержат только доменные события.
- **Post-1.0:** KYC/Onboarding (MongoDB + MinIO + gRPC), не блокирует 1.0.

Актуальный статус и порядок задач — в pinned issue [#70](https://github.com/PoyarkinES/otus_payment_system/issues/70).

## Стек технологий

- .NET 10
- ASP.NET Core
- Entity Framework Core
- PostgreSQL
- YARP (API Gateway)
- RabbitMQ
- Redis (кэш валидации получателя)
- MediatR
- Docker
- GitHub Actions
- **Post-1.0:** MongoDB, MinIO/S3, gRPC (KYC)

## Лицензия

Проект распространяется под лицензией MIT. Подробнее см. файл [LICENSE](LICENSE).

## Roadmap

Подробный план по месяцам и неделям: [docs/roadmap.ru.md](docs/roadmap.ru.md).
