# Otus Payment System

[![CI](https://github.com/PoyarkinES/otus_payment_system/actions/workflows/ci.yml/badge.svg)](https://github.com/PoyarkinES/otus_payment_system/actions/workflows/ci.yml)

Fintech portfolio project for a .NET developer.

## Project goal

Build a realistic payment platform that demonstrates skills in:

- ASP.NET Core
- Entity Framework Core
- PostgreSQL
- JWT authentication
- API Gateway / BFF (YARP)
- RabbitMQ
- CQRS / MediatR
- Clean Architecture
- DDD
- Microservices
- Docker
- GitHub Actions
- observability and monitoring

## Project scope

This is a learning and portfolio project. It grows step by step without trying to implement everything at once.

**Current plan:** 6 months of core development (November 2026 — April 2027). A full frontend UI is a separate post-1.0 track so that backend goals are not derailed.

## Repository structure

The repository is organized as a set of independent microservice solutions. Each bounded context lives in its own folder and has its own `.sln` file with a common `src` / `test` layout.

```
.
├── Directory.Build.props        # shared MSBuild settings (TFM, nullable, analyzers)
├── global.json                  # pinned .NET SDK version
├── Shared/
│   ├── Otus.PaymentSystem.Shared.sln
│   ├── src/Otus.PaymentSystem.SharedKernel/
│   └── test/
├── Gateway/                     # API Gateway / BFF (edge, not a bounded context; created in M1)
│   ├── Otus.PaymentSystem.Gateway.sln
│   └── src/Otus.PaymentSystem.Gateway.Host/
├── Identity/
│   ├── Otus.PaymentSystem.Identity.sln
│   ├── src/Otus.PaymentSystem.Identity.{Domain,Application,Infrastructure,Host}/
│   └── test/
├── Wallets/                     # same layout
├── Payments/                    # same layout
├── Fraud/                       # same layout
├── Notifications/               # same layout
├── Audit/                       # same layout (with test project placeholders)
└── docs/
    ├── roadmap.md
    ├── architecture.md
    ├── bounded-contexts.md
    └── adr/0001-api-gateway.md
```

Each context follows Clean Architecture with the following layers:

- **Domain** — entities, value objects, domain events, no external dependencies (references only `SharedKernel`).
- **Application** — use cases, commands/queries, DTOs (references `Domain`).
- **Infrastructure** — persistence, external services (references `Domain`).
- **Host** — ASP.NET Core Web API, wiring, middleware.

`SharedKernel` is a standalone solution and is referenced by every `*.Domain` project.

## Bounded contexts and the edge

- Identity
- Wallets
- Payments (core domain)
- Audit
- Fraud
- Notifications
- **Gateway** — not a bounded context; an edge/BFF for routing, JWT, CORS, rate limiting, and frontend aggregation.

Do not confuse:

- **API Gateway** — the single entry point between the frontend and the services (month 1).
- **Payment Gateway** — merchant acquiring: API keys, payments, webhooks, refunds (month 6).

See [ADR 0001](./docs/adr/0001-api-gateway.md).

## Development roadmap

| Month | Goal |
|---|---|
| M0 (October 2026) | Scaffold, docs, Identity MVP — **actuals** |
| M1 (November 2026) | API Gateway/BFF, PostgreSQL, refresh/logout, CI tests, Docker |
| M2 (December 2026) | Wallets: `available`/`held`, top-up/withdraw, holds, history, optimistic locking |
| M3 (January 2027) | Transfers: idempotency, Redis recipient cache, hold/confirm/release, audit, outbox |
| M4 (February 2027) | RabbitMQ, Audit Service, Notifications Service |
| M5 (March 2027) | Fraud: limits, velocity, fallback, blacklist, risk scoring |
| M6 (April 2027) | Merchant Payments: API keys, webhooks, refunds, release 1.0 |

Critical path:

```
Gateway → PostgreSQL → Wallets (+ holds) → Transfers (Redis, hold, Fraud sync) → Outbox/RabbitMQ → Audit/Notifications → Merchant Payments → Release 1.0
```

## Status

- **Identity MVP** is done: registration, login, JWT, roles, profile, block/unblock, unit tests.
- **M1** starts on 2 November 2026: Gateway, PostgreSQL, refresh tokens, CI tests, Docker.
- **Missing:** PostgreSQL, migrations, Gateway/BFF, refresh/logout, rate limiting, structured logging, tests in CI.
- Wallets/Transfers/Fraud/Notifications/Audit currently contain domain events only.

Current status and task order: pinned issue [#70](https://github.com/PoyarkinES/otus_payment_system/issues/70).

## Tech stack

- .NET 10
- ASP.NET Core
- Entity Framework Core
- PostgreSQL
- YARP (API Gateway)
- RabbitMQ
- Redis (recipient validation cache)
- MediatR
- Docker
- GitHub Actions

## License

This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.

## Roadmap

See [docs/roadmap.md](docs/roadmap.md) for the month/week plan.
