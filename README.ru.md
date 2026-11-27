# Otus Payment System

[![CI](https://github.com/PoyarkinES/otus_payment_system/actions/workflows/ci.yml/badge.svg)](https://github.com/PoyarkinES/otus_payment_system/actions/workflows/ci.yml)

Учебный fintech-проект для портфолио .NET-разработчика.

> **Разработчику:** единая точка входа — [docs/project-guide.ru.md](./docs/project-guide.ru.md): запуск, конфигурация, сущности, взаимодействия и порядок работы над issue.

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

**Текущий план:** 6 месяцев core-разработки (ноябрь 2026 — апрель 2027). Web UI идёт параллельно M2–M6 и входит в релиз 1.0; мобильное приложение — отдельный post-1.0 трек.

## Структура репозитория

Репозиторий организован как набор независимых микросервисных решений. Каждый bounded context живёт в своей папке и имеет собственный `.sln`-файл с единой структурой `src` / `test`.

```
.
├── Directory.Build.props        # общие настройки MSBuild (TFM, nullable, анализаторы)
├── global.json                  # зафиксированная версия .NET SDK
├── docker-compose.yml           # демо-стек M1 из публичных GHCR-образов
├── .dockerignore
├── openapi/                     # OpenAPI-контракты Identity и Gateway (генерируются, не правятся руками)
├── Client/                      # веб-клиент: React SPA, ходит только через Gateway (см. Client/README.md)
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
    ├── demo-m2.ru.md
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

## Демо-стек (Docker Compose, GHCR)

`docker-compose.yml` запускает PostgreSQL 17 и Identity, Wallets и Gateway. Три образа приложений — **публичные GHCR-образы**, закреплённые по commit SHA. Одноразовый `wallets-db-bootstrap` создаёт отдельную БД `otus_wallets` в уже работающем PostgreSQL и не требует удалять существующий volume. Локальный код при этом не собирается; токены GitHub/NuGet для запуска не нужны. Наружу открыт только Gateway — сервисы и БД доступны лишь внутри сети compose.

```powershell
# если .env ещё нет
if (-not (Test-Path .env)) { Copy-Item .env.example .env }

docker compose -f docker-compose.yml pull
docker compose -f docker-compose.yml up -d
docker compose -f docker-compose.yml ps -a
Invoke-RestMethod http://localhost:8080/bff/health | ConvertTo-Json
```

- Вход: `http://localhost:8080`, демо-админ `admin@otus.local` / `AdminP@ssw0rd`.
- Bootstrap пропускает создание `otus_wallets`, если эта БД уже существует; `Wallets` применяет её схему на старте (`Database__AutoMigrate=true`). Миграции Identity также применяются при старте; в обычном `dotnet run` их по-прежнему накатывают через `dotnet ef database update`.
- Используй явный `-f docker-compose.yml`, чтобы не подмешивать `docker-compose.override.yml` (он публикует postgres на 5434 и может конфликтовать с dev-контейнером `otus-postgres`).
- Для локальных изменений кода используй `docker compose -f docker-compose.dev.yml up -d` (только PostgreSQL) и `dotnet run`; `docker-compose.yml` — только готовые образы.
- После изменения кода публикуется новый образ, тег `sha-…` в `docker-compose.yml` обновляется отдельным PR.

Пошаговые сценарии: [M1 — Identity и Gateway](./docs/demo-m1.ru.md) и [M2 — регистрация, кошелёк, пополнение, hold, confirm и история](./docs/demo-m2.ru.md). Уровни тестов и что проверяет CI: [docs/testing-strategy.ru.md](./docs/testing-strategy.ru.md).

## Веб-клиент

`Client/` — SPA на React + TypeScript с компонентами MUI. Все запросы идут через Gateway.
После входа можно создать рублёвый кошелёк, посмотреть доступный остаток, резерв и последние операции, скрыть суммы. На телефоне нижняя панель переключает между балансами и операциями.

```bash
cd Client
npm ci
npm run dev          # http://localhost:5173, нужен запущенный docker compose
npm run dev:mocks    # то же без бэка, на моках
```

Карта папок, команды и что делать после изменения API — в [Client/README.md](./Client/README.md).

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
  - Gateway: YARP, валидация JWT по JWKS, `X-User-Id`, CORS, rate limiting (429 + `Retry-After`), BFF `/bff/me`, `/bff/health` и `/bff/wallets`.
  - Инфраструктура: Serilog + correlation ID, единый ProblemDetails, валидация DTO, Docker Compose полного стека.
- **Wallets (#52):** реализованы балансы, holds, история операций и список кошельков; Gateway агрегирует список, totals по валютам и последние операции.
- **Проверенные Gateway-тесты:** 34 Host/unit/web и 11 E2E через in-process Wallets + PostgreSQL.
- **CI** собирает решения, запускает тесты и проверяет OpenAPI drift; Gateway E2E проверены локально с `WALLETS_API_TEST_DB`.
- **Веб-клиент (#82, #83, #85):** авторизация, создание и обзор кошельков и проверки в CI.
- **Дальше:** Payments/Transfers и экраны пополнения, вывода и истории операций; RabbitMQ, Redis, Fraud и Merchant Payments остаются будущими этапами.
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
- React + TypeScript, MUI (веб-клиент)
- **Post-1.0:** MongoDB, MinIO/S3, gRPC (KYC)

## Лицензия

Проект распространяется под лицензией MIT. Подробнее см. файл [LICENSE](LICENSE).

## Roadmap

Подробный план по месяцам и неделям: [docs/roadmap.ru.md](docs/roadmap.ru.md).
