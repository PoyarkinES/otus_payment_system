# Otus Payment System

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
- Docker
- GitHub Actions
- observability and monitoring

## Project scope
This repository is a learning and portfolio project.
The goal is to grow it step by step over several months without implementing everything at once.

## Planned bounded contexts
- Identity
- Wallet
- Payments
- Merchants
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
Initial scaffold only. No business logic implemented yet.

## License
This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.

## Roadmap
See [docs/roadmap.md](docs/roadmap.md) for the weekly plan.
