# Otus Payment System — Roadmap

## Goal
Build a realistic fintech system in .NET over 6 months, step by step.

---

## Phase 1. Base platform
**Duration:** 1 month

### Week 1
- Create the repository scaffold
- Define project structure
- Prepare Clean Architecture folders
- Clarify bounded contexts

### Week 2
- Implement registration
- Implement login
- Add JWT authentication

### Week 3
- Add user roles
- Connect PostgreSQL
- Configure Entity Framework Core
- Create migrations

### Week 4
- Add Swagger / OpenAPI
- Add validation
- Add basic tests
- Close the phase

**Result:** a user can register and log in.

---

## Phase 2. Wallets
**Duration:** 1 month

### Week 5
- Design the Wallet entity
- Implement wallet creation

### Week 6
- Implement top-up
- Implement withdrawal
- Validate sufficient balance

### Week 7
- Add operation history
- Add DB transactions
- Add optimistic locking

### Week 8
- Test wallet scenarios
- Improve API behavior
- Close the phase

**Result:** a simple digital wallet.

---

## Phase 3. Transfers between users
**Duration:** 1 month

### Week 9
- Implement wallet-to-wallet transfer

### Week 10
- Add transfer fees
- Add rollback on failure

### Week 11
- Add audit logging
- Introduce Repository and Unit of Work

### Week 12
- Add integration tests
- Close the phase

**Result:** safe money transfer between users.

---

## Phase 4. Async processing
**Duration:** 1 month

### Week 13
- Introduce RabbitMQ

### Week 14
- Publish `TransferCreated` events

### Week 15
- Add Audit Service (separate bounded context)
- Add Notification Service
- Define bounded contexts and domain events

### Week 16
- Add Fraud Service
- Improve error handling
- Close the phase

**Result:** transfers generate events for multiple services.

---

## Phase 5. Anti-fraud
**Duration:** 1 month

### Week 17
- Add operation limits
- Add suspicious amount checks

### Week 18
- Add transfer frequency checks
- Create Fraud Alert handling

### Week 19
- Add blacklist support
- Implement risk scoring

### Week 20
- Add tests
- Close the phase

**Result:** risky operations can be detected and blocked.

---

## Phase 6. Payment gateway
**Duration:** 1 month

### Week 21
- Design merchant-facing API
- Implement `POST /payments/create`

### Week 22
- Add API keys
- Secure merchant access

### Week 23
- Add webhooks
- Add refunds
- Add payment links

### Week 24
- Finalize documentation
- Close the phase

**Result:** a basic external payment gateway.

---

## Additional topics
- CQRS
- MediatR
- Clean Architecture
- DDD
- Event Sourcing
- CI/CD
- Monitoring: Serilog, OpenTelemetry, Prometheus, Grafana

---

## Final outcome
After 6 months the project should resemble a real fintech system:
- 5–8 microservices
- PostgreSQL
- RabbitMQ
- Redis
- CQRS
- Clean Architecture
- Docker
- CI/CD
- monitoring
- anti-fraud
- merchant payment API
