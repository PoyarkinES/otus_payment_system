# Bounded Contexts & Domain Boundaries

## Executive Summary

This document defines the bounded context map, aggregate boundaries, domain events, and integration patterns for the Otus Payment System. The system is modeled as a **distributed domain** with 6 bounded contexts. The **Payments context is the core domain**, Identity is the **upstream context** for all others, and Fraud, Notifications, and Audit are **downstream supporting contexts**.

**Key architectural decisions:**
- Payments context owns the transfer lifecycle end-to-end; Wallets is a supporting context providing balance operations
- Fraud is called synchronously during transfer validation (blocking decision), with async follow-up for alerts
- All contexts publish events to RabbitMQ for Audit and Notifications (fire-and-forget)
- Identity is the single upstream context; all other contexts reference only `UserId` (never the full User model)
- A new **Audit context** is recommended as a dedicated concern, separate from Notifications

---

## 1. Bounded Context Map

### 1.1 Context Relationships

```
                    ┌──────────────┐
                    │   IDENTITY   │  (UPSTREAM)
                    │ Users, Roles │
                    └──────┬───────┘
                           │ ACL (UserId only)
              ┌────────────┼────────────┐
              │            │            │
        ┌─────▼─────┐ ┌───▼────┐  ┌────▼─────┐
        │  WALLETS   │ │PAYMENTS│  │  FRAUD   │
        │ Supporting │ │  CORE  │  │Supporting│
        └─────┬──────┘ └───┬────┘  └────┬─────┘
              │            │             │
              │            │   ┌─────────▼──────────┐
              │            │   │  NOTIFICATIONS      │
              │            │   │ Supporting          │
              └────────────┼────────────┐
                    EVENT DRIVEN  │
              ┌────────────▼─────▼─────┐
              │        AUDIT           │
              │ Supporting             │
              └────────────────────────┘
```

### 1.2 Relationship Types

| From Context | To Context | Pattern | Rationale |
|---|---|---|---|
| Identity -> All | Conformist | All contexts use only `UserId` |
| Wallets -> Payments | Customer-Supplier | Payments "orders" balance operations from Wallets |
| Payments -> Fraud | ACL | Fraud has its own risk model; Payments must not leak internal transfer domain into Fraud |
| All -> Notifications | Publish-Subscribe | Notifications subscribes to events without coupling |
| All -> Audit | Publish-Subscribe | Audit subscribes to all events for immutable logging |

### 1.3 Context Ownership Matrix

| Context | Project Path | Domain Type | Phase |
|---|---|---|---|
| Identity | `src/Identity/` | **Supporting Subdomain** (foundational) | Phase 1 |
| Wallets | `src/Wallets/` | **Supporting Subdomain** | Phase 2 |
| Payments | `src/Payments/` | **CORE DOMAIN** | Phase 3, 6 |
| Fraud | `src/Fraud/` | **Supporting Subdomain** | Phase 5 |
| Notifications | `src/Notifications/` | **Supporting Subdomain** | Phase 4 |
| Audit | `src/Audit/` | **Supporting Subdomain** | Phase 4 |

---

## 2. Detailed Context Analysis

---

### 2.1 Identity Context

**Path:** `src/Identity/`

**Description:** Manages user lifecycle, authentication, authorization, and token lifecycle. This is the upstream context -- every other context depends on it. All external contexts reference users only by `UserId` (a GUID).

#### Aggregate Roots

**User** (Aggregate Root)
- **Responsibilities:** User registration, authentication state, profile data, status management (Active/Blocked)
- **Key invariants:**
  - Email must be unique
  - Password is always hashed (never stored in plaintext)
  - Status transitions: Active -> Blocked (irreversible without Admin action)
- **Fields:**
  - `Id` (Guid) - primary key
  - `Email` (string) - unique, lowercase-normalized
  - `PasswordHash` (string) - ASP.NET Core Identity-compatible hash
  - `Status` (UserStatus enum: Active, Blocked)
  - `CreatedAt` (DateTimeOffset)
  - `UpdatedAt` (DateTimeOffset)
  - `Version` (int) - for optimistic concurrency
- **Domain Events:** `UserRegistered`, `UserBlocked`, `UserUnblocked`, `PasswordChanged`

**RefreshToken** (Aggregate Root -- separate aggregate)
- **Responsibilities:** Token rotation, revocation, expiration
- **Key invariants:**
  - Each token has a single parent user
  - Tokens can be revoked individually or all at once (on password change)
  - Rotation chain: `ReplacedByTokenId` links old to new
- **Fields:**
  - `Id` (Guid)
  - `UserId` (Guid)
  - `Token` (string) - stored as hash
  - `ExpiresAt` (DateTimeOffset)
  - `RevokedAt` (DateTimeOffset?)
  - `ReplacedByTokenId` (Guid?)
  - `CreatedAt` (DateTimeOffset)

#### Domain Events Published

| Event | Payload | Routing Key |
|---|---|---|
| `UserRegistered` | UserId, Email, Timestamp | `identity.user.registered` |
| `UserBlocked` | UserId, BlockedBy, Timestamp | `identity.user.blocked` |
| `UserUnblocked` | UserId, UnblockedBy, Timestamp | `identity.user.unblocked` |
| `PasswordChanged` | UserId, Timestamp | `identity.password.changed` |

---

### 2.2 Wallets Context

**Path:** `src/Wallets/`

**Description:** Manages digital wallets, balance operations (top-up, withdrawal), and operation history. This is a supporting subdomain. Wallets is upstream for Payments because transfers debit/credit wallets.

#### Aggregate Roots

**Wallet** (Aggregate Root)
- **Responsibilities:** Wallet lifecycle, balance management, currency management
- **Key invariants:**
  - Each user can have multiple wallets (one per currency, or unlimited)
  - Balance must never go negative (unless overdraft is enabled)
  - Balance changes only through explicit operations (top-up, withdrawal, transfer in/out)
  - Version field for optimistic locking
- **Fields:**
  - `Id` (Guid)
  - `UserId` (Guid) - FK to Identity
  - `Currency` (string, ISO 4217: RUB, USD, EUR)
  - `Balance` (decimal, 18.4 precision)
  - `Status` (WalletStatus: Active, Frozen, Closed)
  - `Version` (int) - optimistic concurrency token
  - `CreatedAt` (DateTimeOffset)
  - `UpdatedAt` (DateTimeOffset)
- **Domain Events:** `WalletCreated`, `BalanceDeposited`, `BalanceWithdrawn`, `BalanceTransferredIn`, `BalanceTransferredOut`, `WalletFrozen`, `WalletClosed`

**WalletOperation** (Entity within Wallet aggregate)
- **Responsibilities:** Records every balance-changing operation with full audit trail
- **Fields:**
  - `Id` (Guid)
  - `WalletId` (Guid)
  - `Type` (OperationType: TopUp, Withdrawal, TransferIn, TransferOut, Fee, Refund)
  - `Amount` (decimal)
  - `Currency` (string)
  - `BalanceBefore` (decimal)
  - `BalanceAfter` (decimal)
  - `ReferenceId` (Guid?) - links to TransferId or external reference
  - `Description` (string?)
  - `CreatedAt` (DateTimeOffset)
- **Invariant:** BalanceAfter must equal BalanceBefore +/- Amount

#### Domain Events Published

| Event | Payload | Routing Key |
|---|---|---|
| `WalletCreated` | WalletId, UserId, Currency, Timestamp | `wallet.created` |
| `BalanceDeposited` | WalletId, Amount, Currency, OperationId, Timestamp | `wallet.balance.deposited` |
| `BalanceWithdrawn` | WalletId, Amount, Currency, OperationId, Timestamp | `wallet.balance.withdrawn` |
| `BalanceTransferredIn` | WalletId, Amount, Currency, TransferId, Timestamp | `wallet.balance.transferred.in` |
| `BalanceTransferredOut` | WalletId, Amount, Currency, TransferId, Timestamp | `wallet.balance.transferred.out` |
| `WalletFrozen` | WalletId, FrozenBy, Timestamp | `wallet.frozen` |
| `WalletClosed` | WalletId, ClosedBy, Timestamp | `wallet.closed` |

---

### 2.3 Payments Context (CORE DOMAIN)

**Path:** `src/Payments/`

**Description:** The core domain of the system. Manages wallet-to-wallet transfers, fees, refunds, merchant payments, and the payment gateway API. This is where the primary business value is created.

#### Aggregate Roots

**Transfer** (Aggregate Root)
- **Responsibilities:** End-to-end lifecycle of a wallet-to-wallet transfer
- **Key invariants:**
  - Source and destination wallets must belong to different users (or same user, for self-transfers)
  - Source wallet must have sufficient balance (checked before commit)
  - Transfer status transitions: Pending -> Processing -> Completed OR Failed OR RolledBack
  - Once Completed or RolledBack, status is terminal
  - Fees are calculated at creation time and cannot change
- **Fields:**
  - `Id` (Guid)
  - `SourceWalletId` (Guid)
  - `DestinationWalletId` (Guid)
  - `Amount` (decimal)
  - `Currency` (string)
  - `FeeAmount` (decimal)
  - `FeeWalletId` (Guid?) - which wallet pays the fee (source or separate merchant wallet)
  - `Status` (TransferStatus: Pending, Processing, Completed, Failed, RolledBack)
  - `FraudCheckStatus` (FraudCheckStatus: NotChecked, Passed, Blocked)
  - `ErrorMessage` (string?)
  - `CreatedAt` (DateTimeOffset)
  - `CompletedAt` (DateTimeOffset?)
  - `UpdatedAt` (DateTimeOffset)
- **Domain Events:** `TransferCreated`, `TransferProcessing`, `TransferCompleted`, `TransferFailed`, `TransferRolledBack`, `TransferBlockedByFraud`

**Payment** (Aggregate Root) -- Phase 6 (Payment Gateway)
- **Responsibilities:** Merchant-initiated payments, payment links, refunds
- **Key invariants:**
  - Payment is linked to a Transfer (creates one internally)
  - Payment has a status lifecycle: Created -> Paid -> Refunded OR Failed
  - Refund amount cannot exceed original payment amount
- **Fields:**
  - `Id` (Guid)
  - `MerchantId` (Guid)
  - `TransferId` (Guid?) - linked transfer
  - `Amount` (decimal)
  - `Currency` (string)
  - `Status` (PaymentStatus: Created, Paid, Refunded, Failed, Expired)
  - `PaymentLink` (string?) - unique URL for customer payment
  - `ExpiresAt` (DateTimeOffset?)
  - `WebhookUrl` (string?) - merchant callback URL
  - `CreatedAt` (DateTimeOffset)
  - `UpdatedAt` (DateTimeOffset)
- **Domain Events:** `PaymentCreated`, `PaymentPaid`, `PaymentRefunded`, `PaymentExpired`

**Merchant** (Aggregate Root) -- Phase 6
- **Responsibilities:** Merchant registration, API key management, webhook configuration
- **Fields:**
  - `Id` (Guid)
  - `Name` (string)
  - `Email` (string)
  - `Status` (MerchantStatus: Active, Suspended, Closed)
  - `WebhookUrl` (string?)
  - `CreatedAt` (DateTimeOffset)
  - `UpdatedAt` (DateTimeOffset)

**MerchantApiKey** (Entity within Merchant aggregate)
- **Responsibilities:** API key management for merchant authentication
- **Fields:**
  - `Id` (Guid)
  - `MerchantId` (Guid)
  - `KeyHash` (string) - hashed API key
  - `KeyPrefix` (string) - first 8 chars for display
  - `Name` (string) - friendly name
  - `IsActive` (bool)
  - `LastUsedAt` (DateTimeOffset?)
  - `CreatedAt` (DateTimeOffset)

#### Domain Services

- `TransferService` -- orchestrates the full transfer lifecycle (validate -> debit -> credit -> complete)
- `FeeCalculationService` -- calculates transfer fees based on amount, currency, merchant tier
- `RefundService` -- handles partial and full refunds for payments
- `PaymentGatewayService` -- exposes merchant-facing payment creation API

#### Domain Events Published

| Event | Payload | Routing Key |
|---|---|---|
| `TransferCreated` | TransferId, SourceWalletId, DestWalletId, Amount, Currency, FeeAmount, Timestamp | `payment.transfer.created` |
| `TransferProcessing` | TransferId, Timestamp | `payment.transfer.processing` |
| `TransferCompleted` | TransferId, SourceWalletId, DestWalletId, Amount, Currency, Timestamp | `payment.transfer.completed` |
| `TransferFailed` | TransferId, ErrorMessage, Timestamp | `payment.transfer.failed` |
| `TransferRolledBack` | TransferId, Timestamp | `payment.transfer.rolledback` |
| `TransferBlockedByFraud` | TransferId, FraudReason, Timestamp | `payment.transfer.blocked` |
| `PaymentCreated` | PaymentId, MerchantId, Amount, Currency, Timestamp | `payment.payment.created` |
| `PaymentPaid` | PaymentId, TransferId, Timestamp | `payment.payment.paid` |
| `PaymentRefunded` | PaymentId, RefundAmount, Timestamp | `payment.payment.refunded` |

---

### 2.4 Fraud Context

**Path:** `src/Fraud/`

**Description:** Detects suspicious transactions, manages risk scoring, blacklists, and operational limits. This is a supporting subdomain. Fraud is called **synchronously** during transfer validation (blocking decision) and also processes events asynchronously for alert generation.

#### Aggregate Roots

**FraudRule** (Aggregate Root)
- **Responsibilities:** Defines rules for fraud detection (amount thresholds, frequency limits, blacklist entries)
- **Fields:**
  - `Id` (Guid)
  - `Name` (string)
  - `Type` (FraudRuleType: MaxSingleAmount, MaxDailyAmount, MaxFrequency, Blacklist, RiskScore)
  - `Parameters` (JSON) - rule-specific parameters
  - `Severity` (Severity: Low, Medium, High, Critical)
  - `Action` (RuleAction: Alert, Block, Review)
  - `IsActive` (bool)
  - `CreatedAt` (DateTimeOffset)
  - `UpdatedAt` (DateTimeOffset)

**FraudAlert** (Aggregate Root)
- **Responsibilities:** Records fraud alerts generated by rule evaluation
- **Fields:**
  - `Id` (Guid)
  - `TransferId` (Guid?)
  - `UserId` (Guid)
  - `RuleId` (Guid)
  - `Severity` (Severity)
  - `Status` (AlertStatus: Open, Acknowledged, Resolved, FalsePositive)
  - `Details` (string)
  - `CreatedAt` (DateTimeOffset)
  - `ResolvedAt` (DateTimeOffset?)

#### Domain Services

- `FraudEvaluationService` -- evaluates a transfer against all active rules, computes risk score
- `RiskScoreCalculatorService` -- calculates rolling risk score based on recent transfer history

#### Domain Events Published

| Event | Payload | Routing Key |
|---|---|---|
| `FraudAlertRaised` | AlertId, TransferId, UserId, RuleId, Severity, Timestamp | `fraud.alert.raised` |
| `FraudAlertResolved` | AlertId, ResolvedBy, Timestamp | `fraud.alert.resolved` |
| `TransferBlockedByFraud` | TransferId, RuleId, Reason, Timestamp | `fraud.transfer.blocked` |

---

### 2.5 Notifications Context

**Path:** `src/Notifications/`

**Description:** Manages notification delivery (email, SMS, push) to users. This is a supporting subdomain. It subscribes to events from all other contexts and dispatches appropriate notifications.

#### Aggregate Roots

**NotificationTemplate** (Aggregate Root)
- **Responsibilities:** Defines notification content templates for different event types
- **Fields:**
  - `Id` (Guid)
  - `EventType` (string) - e.g., "payment.transfer.completed"
  - `Channel` (Channel: Email, SMS, Push)
  - `Subject` (string) - for email
  - `BodyTemplate` (string) - with placeholders like {{UserName}}, {{Amount}}
  - `Locale` (string) - language code
  - `IsActive` (bool)
  - `CreatedAt` (DateTimeOffset)
  - `UpdatedAt` (DateTimeOffset)

**NotificationMessage** (Aggregate Root)
- **Responsibilities:** Tracks individual notification delivery attempts
- **Fields:**
  - `Id` (Guid)
  - `UserId` (Guid)
  - `EventType` (string)
  - `Channel` (Channel)
  - `Status` (NotificationStatus: Pending, Sent, Failed, Retry)
  - `Payload` (JSON) - rendered template with resolved placeholders
  - `RetryCount` (int)
  - `LastAttemptAt` (DateTimeOffset?)
  - `SentAt` (DateTimeOffset?)
  - `CreatedAt` (DateTimeOffset)

#### Domain Events Consumed (but not published)

| Event | Routing Key | Action |
|---|---|---|
| `UserRegistered` | `identity.user.registered` | Send welcome email |
| `TransferCompleted` | `payment.transfer.completed` | Send transfer confirmation |
| `TransferFailed` | `payment.transfer.failed` | Send failure notification |
| `TransferBlockedByFraud` | `payment.transfer.blocked` | Send security alert |
| `UserBlocked` | `identity.user.blocked` | Send account status notification |
| `FraudAlertRaised` | `fraud.alert.raised` | Send admin alert (internal) |

---

### 2.6 Audit Context (NEW)

**Path:** `src/Audit/`

**Description:** Immutable audit logging for all system operations. This is a supporting subdomain that subscribes to events from ALL other contexts. Separate from Notifications because audit is about compliance and traceability, not user communication.

#### Aggregate Roots

**AuditLog** (Aggregate Root)
- **Responsibilities:** Immutable record of every significant system operation
- **Key invariants:**
  - Once written, audit entries are NEVER modified or deleted
  - Each entry captures: who, what, when, where, before/after state
  - Entries are append-only
- **Fields:**
  - `Id` (Guid)
  - `AggregateType` (string) - e.g., "Transfer", "User", "Wallet"
  - `AggregateId` (Guid)
  - `EventType` (string) - e.g., "Created", "Completed", "Blocked"
  - `UserId` (Guid?) - who performed the action (null for system actions)
  - `IpAddress` (string?)
  - `CorrelationId` (string) - for tracing across contexts
  - `BeforeState` (JSON?) - state before the change
  - `AfterState` (JSON?) - state after the change
  - `Timestamp` (DateTimeOffset)
  - `ContextName` (string) - which bounded context generated this

#### Domain Events Consumed

All events from all contexts. Routing keys prefixed by context name:
- `identity.*`
- `wallet.*`
- `payment.*`
- `fraud.*`

---

## 3. Cross-Context Communication

### 3.1 Synchronous Boundaries (REST/gRPC)

| Caller | Callee | Purpose | Method | Details |
|---|---|---|---|---|
| Payments | Wallets | Debit source wallet | `POST /api/wallets/{walletId}/debit` | Returns success/failure; Payments handles rollback |
| Payments | Wallets | Credit destination wallet | `POST /api/wallets/{walletId}/credit` | Returns success/failure; Payments handles rollback |
| Payments | Fraud | Evaluate transfer risk | `POST /api/fraud/evaluate` | Blocking call; returns Pass/Block with reason |
| Payments | Identity | Validate user exists | `GET /api/users/{userId}` | Lightweight check; returns 404 if not found |
| Fraud | Identity | Resolve user details | `GET /api/users/{userId}` | For alert context (name, email) |

**Protocol:** REST over HTTP/JSON (gRPC is optional for high-throughput internal calls)

**Key design:** Payments is the orchestrator. It calls Wallets synchronously for debit/credit operations within a saga pattern. If debit succeeds but credit fails, Payments initiates a compensating transaction (rollback).

### 3.2 Asynchronous Boundaries (RabbitMQ Events)

#### Event Publishing Matrix

| Publisher | Events Published | Queues/Exchanges |
|---|---|---|
| Identity | UserRegistered, UserBlocked, PasswordChanged | `otus.identity` exchange |
| Wallets | WalletCreated, BalanceDeposited, BalanceWithdrawn, BalanceTransferredIn, BalanceTransferredOut | `otus.wallets` exchange |
| Payments | TransferCreated, TransferProcessing, TransferCompleted, TransferFailed, TransferRolledBack, TransferBlockedByFraud, PaymentCreated, PaymentPaid, PaymentRefunded | `otus.payments` exchange |
| Fraud | FraudAlertRaised, FraudAlertResolved, TransferBlockedByFraud | `otus.fraud` exchange |

#### Event Subscriptions

| Consumer | Subscribes To | Purpose |
|---|---|---|
| Notifications | All events from all exchanges | Send user-facing notifications |
| Audit | All events from all exchanges | Immutable audit logging |
| Fraud | `payment.transfer.created`, `wallet.balance.*` | Async risk scoring and alert generation |

#### Exchange Strategy

Use **topic exchanges** with hierarchical routing keys:
```
otus.{context}.{entity}.{action}
```

Examples:
- `otus.identity.user.registered`
- `otus.wallets.wallet.balance.deposited`
- `otus.payments.transfer.completed`
- `otus.fraud.alert.raised`

Each context publishes to its own exchange. Consumers declare bindings to the exchanges they need.

### 3.3 Event Schema Convention

All events follow this structure:

```json
{
  "eventId": "guid",
  "eventType": "fully.qualified.event.name",
  "timestamp": "ISO 8601 UTC",
  "correlationId": "guid",       // traces request across contexts
  "causationId": "guid?",        // links to the domain event that caused this
  "context": "Identity|Wallets|Payments|Fraud",
  "data": {
    // Event-specific payload
  }
}
```

**Naming convention:**
- Event class: `PascalCase` (e.g., `TransferCompleted`)
- Routing key: `snake_case` with context prefix (e.g., `payment.transfer.completed`)
- JSON property: `camelCase` (e.g., `transferId`, `amount`)

---

## 4. Anti-Corruption Layers

### 4.1 Payments ACL for Identity

**Problem:** Payments must validate that a `UserId` exists but must not depend on Identity's User entity, roles, or password logic.

**Solution:** Payments defines its own minimal `ExternalUser` DTO with only `Id` and `Email`. It calls Identity's `GET /api/users/{userId}` endpoint. The response is mapped to `ExternalUser`. Payments never imports Identity's domain model.

**Implementation:**
```
Payments.Application/
  DTOs/
    ExternalUser.cs        // { Id, Email }
  Interfaces/
    IIdentityClient.cs     // HttpClient wrapper
  Services/
    IdentityValidationService.cs  // Calls IIdentityClient, maps to ExternalUser
```

### 4.2 Payments ACL for Wallets

**Problem:** Payments orchestrates transfers but must not depend on Wallets' internal balance calculation or operation recording.

**Solution:** Payments defines `WalletBalance` DTO with only `WalletId`, `Balance`, `Currency`. It calls Wallets' debit/credit APIs. The Wallets context owns the balance logic entirely.

**Implementation:**
```
Payments.Application/
  DTOs/
    WalletBalance.cs       // { WalletId, Balance, Currency }
  Interfaces/
    IWalletsClient.cs      // HttpClient wrapper for Wallets API
  Services/
    WalletValidationService.cs  // Calls IWalletsClient
```

### 4.3 Fraud ACL for Payments

**Problem:** Fraud has its own risk model (rules, scoring, blacklists) that Payments must not understand.

**Solution:** Payments calls Fraud's evaluation API with a minimal request DTO. Fraud returns a simple `Pass`/`Block` decision with a reason code. Payments does not know about FraudRules, RiskScores, or Blacklists.

**Implementation:**
```
Payments.Application/
  DTOs/
    FraudEvaluationRequest.cs  // { TransferId, Amount, Currency, UserId, IpAddress }
    FraudEvaluationResponse.cs // { Decision: Pass|Block, Reason: string }
  Interfaces/
    IFraudClient.cs          // HttpClient wrapper for Fraud API
  Services/
    FraudEvaluationService.cs  // Calls IFraudClient
```

---

## 5. Recommended Project Structure

### Clean Architecture for each context

```
Otus.PaymentSystem.{Context}/
├── src/
│   ├── Otus.PaymentSystem.{Context}.Domain/
│   │   ├── Aggregates/           # Aggregate roots and entities
│   │   ├── Events/               # Domain events
│   │   ├── Services/             # Domain services
│   │   ├── ValueObjects/         # Value objects
│   │   └── Exceptions/           # Domain exceptions
│   ├── Otus.PaymentSystem.{Context}.Application/
│   │   ├── Commands/             # CQRS commands
│   │   ├── Queries/              # CQRS queries
│   │   ├── DTOs/                 # Data transfer objects
│   │   ├── Interfaces/           # External service interfaces
│   │   └── Services/             # Application services
│   └── Otus.PaymentSystem.{Context}.Infrastructure/
│       ├── Data/                 # EF Core DbContext, migrations
│       ├── Repositories/         # Repository implementations
│       ├── ExternalServices/     # HTTP clients, message brokers
│       └── Events/               # Event handlers for domain events
└── tests/
    ├── Otus.PaymentSystem.{Context}.Domain.Tests/
    ├── Otus.PaymentSystem.{Context}.Application.Tests/
    └── Otus.PaymentSystem.{Context}.Integration.Tests/
```

### Solution structure

```
src/
├── Identity/
│   ├── Otus.PaymentSystem.Identity.csproj
│   ├── Domain/
│   │   ├── Otus.PaymentSystem.Identity.Domain.csproj
│   │   └── Events/
│   ├── Application/
│   │   └── Otus.PaymentSystem.Identity.Application.csproj
│   └── Infrastructure/
│       └── Otus.PaymentSystem.Identity.Infrastructure.csproj
├── Wallets/
│   ├── Otus.PaymentSystem.Wallets.csproj
│   ├── Domain/
│   │   ├── Otus.PaymentSystem.Wallets.Domain.csproj
│   │   └── Events/
│   ├── Application/
│   │   └── Otus.PaymentSystem.Wallets.Application.csproj
│   └── Infrastructure/
│       └── Otus.PaymentSystem.Wallets.Infrastructure.csproj
├── Payments/
│   ├── Otus.PaymentSystem.Payments.csproj
│   ├── Domain/
│   │   ├── Otus.PaymentSystem.Payments.Domain.csproj
│   │   └── Events/
│   ├── Application/
│   │   └── Otus.PaymentSystem.Payments.Application.csproj
│   └── Infrastructure/
│       └── Otus.PaymentSystem.Payments.Infrastructure.csproj
├── Fraud/
│   ├── Otus.PaymentSystem.Fraud.csproj
│   ├── Domain/
│   │   ├── Otus.PaymentSystem.Fraud.Domain.csproj
│   │   └── Events/
│   ├── Application/
│   │   └── Otus.PaymentSystem.Fraud.Application.csproj
│   └── Infrastructure/
│       └── Otus.PaymentSystem.Fraud.Infrastructure.csproj
├── Notifications/
│   ├── Otus.PaymentSystem.Notifications.csproj
│   ├── Domain/
│   │   ├── Otus.PaymentSystem.Notifications.Domain.csproj
│   │   └── Events/
│   ├── Application/
│   │   └── Otus.PaymentSystem.Notifications.Application.csproj
│   └── Infrastructure/
│       └── Otus.PaymentSystem.Notifications.Infrastructure.csproj
└── Audit/
    ├── Otus.PaymentSystem.Audit.csproj
    ├── Domain/
    │   ├── Otus.PaymentSystem.Audit.Domain.csproj
    │   └── Events/
    ├── Application/
    │   └── Otus.PaymentSystem.Audit.Application.csproj
    └── Infrastructure/
        └── Otus.PaymentSystem.Audit.Infrastructure.csproj
```

---

## 6. Key Design Decisions

### Why Payments is the Core Domain?
- This is where the primary business value is created (money transfers)
- Payments orchestrates the entire money movement flow
- Wallets, Fraud, Notifications — supporting subdomains

### Why Wallets is upstream for Payments?
- Payments "orders" balance operations from Wallets
- Wallets defines the contract for debit/credit
- Payments does not know Wallets internals (only through API)

### Why Fraud is sync + async?
- **Sync:** Blocking fraud check during transfer validation (required for allow/deny decision in real-time)
- **Async:** Alert generation, risk scoring updates (does not block main flow)

### Why Audit is a separate context?
- Audit is a compliance concern, not user communication
- Notifications is user-facing, can retry, has templates
- Audit is immutable, append-only, regulatory requirement
- Separation of concerns: Audit does not delete/modify data

### Why all contexts use only UserId?
- Identity is the single source of truth for users
- Avoids duplicating user data
- Consistency through ACL pattern

---

## 7. Verification Checklist

- [ ] All 6 bounded contexts defined with clear responsibilities
- [ ] Aggregate Roots defined for each context
- [ ] Domain Events defined and named by convention
- [ ] Synchronous boundaries defined (REST API)
- [ ] Asynchronous boundaries defined (RabbitMQ events)
- [ ] Anti-Corruption Layers defined for all external dependencies
- [ ] Event schema convention documented
- [ ] Project structure recommended for each context
- [ ] Audit context added as new project
- [ ] Solution file updated with new project
- [ ] Roadmap updated with Audit context

---

## 8. Success Criteria

- Each context has a clear single responsibility
- No circular dependencies between contexts
- Cross-context communication is explicitly defined (sync vs async)
- Domain events serve as contracts between contexts
- ACL pattern prevents domain model leakage
