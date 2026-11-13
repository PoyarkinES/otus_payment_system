# Otus Payment System

[![CI](https://github.com/PoyarkinES/otus_payment_system/actions/workflows/ci.yml/badge.svg)](https://github.com/PoyarkinES/otus_payment_system/actions/workflows/ci.yml)

Fintech portfolio project for a .NET developer.

> **Developer guide (RU):** [docs/project-guide.ru.md](./docs/project-guide.ru.md) — setup, configuration, entities, interactions, and the issue workflow.

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

**Current plan:** 6 months of core development (November 2026 — April 2027). The web UI runs in parallel with M2–M6 and is part of release 1.0; the mobile app is a separate post-1.0 track.

## Repository structure

The repository is organized as a set of independent microservice solutions. Each bounded context lives in its own folder and has its own `.sln` file with a common `src` / `test` layout.

```
.
├── Directory.Build.props        # shared MSBuild settings (TFM, nullable, analyzers)
├── global.json                  # pinned .NET SDK version
├── openapi/                     # Identity OpenAPI contract (generated, do not edit)
├── Client/                      # web client: React SPA, talks only to the Gateway (see Client/README.md)
├── Shared/
│   ├── Otus.PaymentSystem.Shared.sln
│   ├── src/Otus.PaymentSystem.SharedKernel/
│   ├── src/Otus.PaymentSystem.AspNetCore/   # shared ProblemDetails for Host projects
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
    ├── api-contracts.ru.md
    ├── api-errors.ru.md
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

## Web client

`Client/` is a React + TypeScript SPA built with MUI components. It talks only to the Gateway,
and its API client is generated from `openapi/gateway.v1.json`. It currently has the app
scaffold and a "System status" page (`/bff/health`); sign-in, profile and wallets follow in
epic #80.

```bash
cd Client
npm ci
npm run dev          # http://localhost:5173, needs docker compose running
npm run dev:mocks    # same, without the backend, on mocks
```

Folder map, commands and what to do after an API change: [Client/README.md](./Client/README.md)
(in Russian).

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
| Web UI (parallel M2–M6) | Login, profile, wallet, transfers, E2E via Gateway |
| M7 (April–May 2027) | **Post-1.0:** KYC/Onboarding — MongoDB, MinIO, gRPC, PII security |
| M8 (May–June 2027) | **Post-1.0:** Mobile MVP — auth, wallet, transfers, push |

Critical path:

```
Gateway → PostgreSQL → Wallets (+ holds) → Transfers (Redis, hold, Fraud sync) → Outbox/RabbitMQ → Audit/Notifications → Merchant Payments → Release 1.0
```

## Status

- **Identity MVP** is done: registration, login, JWT, roles, profile, block/unblock, PostgreSQL + migrations, unit tests.
- **Gateway** is done: YARP, JWT validation, `X-User-Id`, CORS, `/health`, 8 tests, CI.
- **CI** builds every solution, runs Shared/Gateway/Identity tests and checks the OpenAPI contract (`openapi-drift`).
- **M1 in progress:** next are refresh/JWKS, rate limiting, Docker Compose.
- **Missing:** refresh/logout, JWKS, rate limiting, Docker Compose, BFF `/bff/me`.
- **Web client (#82):** React + MUI scaffold, API client generated from the Gateway OpenAPI, system status page, `client` CI job.
- Wallets/Transfers/Fraud/Notifications/Audit currently contain domain events only.
- **Post-1.0:** KYC/Onboarding (MongoDB + MinIO + gRPC), does not block 1.0.

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
- React + TypeScript, MUI (web client)
- **Post-1.0:** MongoDB, MinIO/S3, gRPC (KYC)

## License

This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.

## Roadmap

See [docs/roadmap.md](docs/roadmap.md) for the month/week plan.
