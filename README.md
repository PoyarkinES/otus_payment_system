# Otus Payment System

[![CI](https://github.com/PoyarkinES/otus_payment_system/actions/workflows/ci.yml/badge.svg)](https://github.com/PoyarkinES/otus_payment_system/actions/workflows/ci.yml)

Fintech portfolio project for a .NET developer.

## Project goal
Build a realistic payment platform that demonstrates skills in:
- ASP.NET Core
- Entity Framework Core
- PostgreSQL
- JWT authentication
- RabbitMQ
- CQRS / MediatR
- Clean Architecture
- DDD
- Microservices
- Docker
- GitHub Actions
- observability and monitoring

## Project scope
This repository is a learning and portfolio project.
The goal is to grow it step by step over several months without implementing everything at once.

## Repository structure
The repository is organized as a set of independent microservice solutions.
Each bounded context lives in its own folder and has its own `.sln` file,
following a common `src` / `test` layout.

```
.
├── Directory.Build.props        # shared MSBuild settings (TFM, nullable, analyzers)
├── global.json                  # pinned .NET SDK version
├── Shared/
│   ├── Otus.PaymentSystem.Shared.sln
│   ├── src/Otus.PaymentSystem.SharedKernel/
│   └── test/
├── Identity/
│   ├── Otus.PaymentSystem.Identity.sln
│   ├── src/Otus.PaymentSystem.Identity.{Domain,Application,Infrastructure}/
│   └── test/
├── Wallets/                    # same layout
├── Payments/                   # same layout
├── Fraud/                      # same layout
├── Notifications/              # same layout
└── Audit/                      # same layout (with test project placeholders)
```

Each context follows Clean Architecture with the following layers:

- **Domain** — entities, value objects, domain events, no external dependencies
  (references only `SharedKernel`)
- **Application** — use cases, commands/queries, DTOs (references `Domain`)
- **Infrastructure** — persistence, external services (references `Domain`)

`SharedKernel` is a standalone solution and is referenced by every `*.Domain` project.

## Planned bounded contexts
- Identity
- Wallets
- Payments
- Audit
- Fraud
- Notifications

## Development roadmap
1. Base platform
   - registration
   - login
   - JWT
   - roles
   - PostgreSQL
   - EF Core migrations

2. Wallets
   - create wallet
   - top up
   - withdraw
   - history
   - database transactions
   - optimistic locking

3. Transfers between users
   - wallet to wallet transfer
   - fees
   - rollback
   - audit log

4. Async processing
   - RabbitMQ
   - domain events
   - Audit Service
   - Notification Service
   - Fraud Service

5. Anti-fraud
   - operation limits
   - suspicious amounts
   - high-frequency transfers
   - blacklist
   - risk scoring

6. Payment gateway
   - merchant API
   - payment creation endpoint
   - API keys
   - webhooks
   - refunds
   - payment links

## Tech stack
- .NET
- ASP.NET Core
- Entity Framework Core
- PostgreSQL
- RabbitMQ
- MediatR
- Docker
- GitHub Actions

## Status
Initial scaffold only. Solutions are split by bounded context; no business logic implemented yet.

## License
This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.

## Roadmap
See [docs/roadmap.md](docs/roadmap.md) for the weekly plan.
