# Otus Payment System

Otus Payment System is a learning and portfolio project exploring the design of a fintech payment platform. This repository currently contains only the project scaffold and development plan; it does not implement business functionality.

## Goal

Build the project incrementally to demonstrate practical .NET development and software architecture skills, including secure identity, wallet operations, transfers, asynchronous processing, fraud checks, and payment integrations.

## Planned technology stack

- .NET and ASP.NET Core
- PostgreSQL and Entity Framework Core
- RabbitMQ for asynchronous messaging
- Redis for caching and supporting distributed workloads
- Clean Architecture and domain-driven design
- Docker and GitHub Actions
- Automated unit and integration testing
- OpenTelemetry-based observability

Technology choices and versions will be confirmed as development progresses.

## Development phases

1. **Base platform** — establish architecture, API foundations, persistence, and tests.
2. **Wallets** — introduce wallet accounts, balance operations, and operation history.
3. **Transfers between users** — support atomic transfers and auditability.
4. **Async processing** — introduce events, message consumers, retries, and notifications.
5. **Anti-fraud** — add risk rules, limits, and review outcomes.
6. **Payment gateway** — design an external payment API and related integrations.

See the [six-month weekly roadmap](docs/roadmap.md) for goals and expected results for each phase.

## Project structure

- `src/` — future bounded contexts and application code
- `tests/` — future automated tests
- `docs/` — project documentation and roadmap

This is an educational portfolio project, not a production payment system.
