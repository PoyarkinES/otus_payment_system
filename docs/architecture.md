Otus Payment System Architecture
1. Overview
The system is built as a set of microservices. Each service owns its database and communicates with others in two ways:

Synchronously (HTTP) — when an immediate response is needed (e.g., Payments waits for a response from Wallets).

Asynchronously (RabbitMQ) — when you can "fire and forget" (e.g., notifications and audit).

1.1. System composition
Service	Role	Responsibility
Identity	upstream	Authentication, JWT issuance, roles, contact data (email)
Wallets	supporting	User balances, holds
Payments	core	Transfer lifecycle, all business decisions, idempotency
Fraud	supporting	Synchronous transfer validation, fallback settings
Notifications	downstream	Email / push notifications
Audit	downstream	Log of all system events
Infrastructure:

API Gateway — single entry point for clients (JWT, routing)

RabbitMQ — message broker

Redis — cache for recipient validation (protection of Identity from SPOF)

PostgreSQL — own database for each service

1.2. Key principles
Money lives only in Wallets. No other service changes balances.

Payments decides — Wallets executes. All transfer lifecycle logic is in Payments.

Hold before check. Money is reserved before the Fraud verdict.

Sync where a decision is needed now. Async where you can wait.

Own DB for each service. Communication only via API or events.

Idempotency above all. A repeated request with the same key always returns the saved result, regardless of the current state of the world.

2. Interaction diagram
https://./images/architecture.png

Diagram: see file docs/images/architecture.png (or docs/architecture.drawio).

2.1. Connections between components
Synchronous calls (HTTP):

From	To	Purpose
Client	API Gateway	All requests
API Gateway	Identity	/auth/* — login, registration
API Gateway	Wallets	/wallets/* — balance, top-up, withdrawal
API Gateway	Payments	/transfers/* — transfers (JWT validation and routing only)
Payments	Redis	Check recipient validation cache
Payments	Identity	Validate recipient (on cache miss)
Payments	Wallets	hold / confirm / release / getHold (for reconciliation)
Payments	Fraud	check (Approve / Block)
Payments	Fraud	getSettings (get fallback threshold)
Notifications	Identity	Get recipient's email for sending letter
Asynchronous events (RabbitMQ):

Publisher	Subscriber	Events
Payments	Notifications, Audit	TransferCreated, TransferCompleted, TransferFailed
Wallets	Notifications, Audit	WalletCredited, WalletDebited
3. How a transfer works (step by step)
Example: Alice transfers 1000 ₽ to Boris.

3.1. Preliminary steps
Registration. Client → Gateway → Identity. Identity saves the user, returns UserId.

Login. Client → Gateway → Identity. Identity verifies the password and issues a JWT.

Top-up. Client → Gateway → Wallets. Wallets creates/tops up the balance: available += 5000.

3.2. Transfer
Step 1. Client request.
The client generates an Idempotency-Key and sends POST /transfers with body {toUserId, amount, currency}. The body does not contain fromUserId — the sender is determined from the JWT.

Step 2. API Gateway.

Validates the JWT locally using Identity's public key.

Extracts UserId (which is fromUserId) from the token.

Routes the request to Payments, adding header X-User-Id: {fromUserId}.

Trust in header: in MVP Payments trusts X-User-Id because calls go only through the internal network. In production — mTLS or signed internal JWT (see Known Limitations).

Step 3. Payments — transfer creation and validation.
Here is a strict order of actions (protection against race conditions and duplicates):

Idempotency-Key check. Payments looks for the key in its DB. If the key already exists — immediately returns the saved result (success or error) and finishes. Recipient validation is not performed.

Recipient validation (Redis/Identity). If the key is new:

Payments checks in Redis the key user:{toUserId}.

If present in cache — takes from there (fast, doesn't call Identity).

If not (cache miss) — synchronously calls Identity (GET /users/{toUserId}). Saves the response in Redis (TTL 60 sec for existing, 10 sec for non-existing).

If the recipient does not exist — Transfer → Failed, client receives an error.

Transfer creation. Creates a Transfer with status Created, saves the Idempotency-Key.

Responds to the client with 202 Accepted.

Why the key check comes first: If the client repeats the request with the same key, and the recipient was deleted from Identity in the meantime, we must still return the original successful result, not a validation error.

Step 4. Payments → Wallets: hold.
Payments asks Wallets to freeze the amount for Alice, passing transferId as the idempotency key.

Wallets checks: is there already a hold with this transferId? If yes — returns the existing holdId (idempotency).

Checks balance: available >= 1000?

If no → error InsufficientFunds, Transfer → Failed.

If yes → creates Hold{transferId, userId, amount} with UNIQUE(transferId), decreases available, increases held. Returns holdId.

Payments saves holdId, Transfer → Held.

Step 5. Payments → Fraud: check.
Payments synchronously calls Fraud and waits for the verdict.

Step 6. Fraud — verdict.
Fraud applies rules and returns Approve or Block.
(Note: if Fraud is unavailable or did not respond within N seconds — this is a call failure, handled in Step 7).

Step 7. Payments handles the verdict.

Outcome	Payments action
Approve	→ Wallets: confirm(holdId), Transfer → Completed
Block	→ Wallets: release(holdId), Transfer → Failed
Timeout / Fraud unavailable	Fallback: if amount < threshold → as Approve, else → as Block
Fallback threshold is stored in Fraud. Payments gets it via GET /fraud/settings on startup and caches with TTL (e.g., 5 minutes). If Fraud is unavailable and cache is empty — the default threshold is used (0 = block everything).

Step 8. Wallets — finalization.
On confirm command, Wallets in a single transaction:

debits from Alice (held -= 1000),

credits to Boris (available += 1000). If Boris has no wallet — Wallets creates it automatically (since the user is validated).

marks the hold as Completed.

On release command, Wallets returns the money to Alice's available and marks the hold as Released.

Idempotency:

confirm for a Completed hold → returns OK (does not debit again).

confirm for a Released hold → returns error Conflict.

release for a Released hold → returns OK.

release for a Completed hold → returns error Conflict.

Step 9. Payments publishes an event.
An event TransferCompleted or TransferFailed is sent to RabbitMQ (via Outbox).

Subscribers:

Notifications — sends emails. To send an email to Boris, Notifications goes to Identity (gets email by toUserId).

Audit — records the event in the log.

Step 10. Client gets the result.
The client periodically polls GET /transfers/{id} until the status becomes Completed or Failed.

3.3. Handling unknown confirm result
Scenario: Payments called confirm, Wallets debited and credited, but the response was lost.
Solution:

After retry without response — Transfer transitions to status Confirming (not Failed!).

In MVP there is no background reconciliation process. A transfer in Confirming requires manual intervention (checking the hold status in Wallets). In production — a background process calls getHold(holdId) and completes the transfer.

4. Internal patterns
4.1. Hold — in Wallets
Reserving money (available -= amount, held += amount). Data model: Holds (transferId UNIQUE). State machine: Active → Completed / Released / Expired. Repeated hold/confirm/release are idempotent.

4.2. Idempotency-Key — in Payments
Client key. Protects against double clicks and retries. Saved together with the Transfer.
Strict rule: Key check always precedes any other business logic (including recipient validation).

4.3. Outbox — in Payments and in Wallets
The event is saved in the DB in the same transaction with business data. A background process publishes to RabbitMQ. Guarantees that the event is not lost if the service crashes.

4.4. Fallback when Fraud is unavailable — in Payments
If Fraud is down — we only let small transfers through. Timeout 2 sec, Circuit Breaker (Polly). The threshold is obtained from Fraud (getSettings) and cached.

4.5. Watchdog for stuck holds — in Wallets (not implemented in MVP)
A background process that cancels holds older than N minutes without confirmation. Not implemented in MVP: if Payments crashes between hold and confirm, the money remains frozen, manual intervention is required.

4.6. Cache-Aside for recipient validation — in Payments + Redis
Problem: A synchronous call to Identity for each transfer makes it a single point of failure (SPOF).
Solution: Payments caches the validation result in Redis.

Key: user:{toUserId}.

TTL: 60 seconds (existing), 10 seconds (negative caching for non-existing).

If Identity is down, Payments continues to work with those recently validated.

4.7. Handling failures between steps
hold failure: retry (protected by transferId). If unsuccessful — Failed.

confirm failure: retry. If unsuccessful — status Confirming (manual reconciliation in MVP).

release failure: retry. If unsuccessful — Failed, hold hangs (manual cleanup in MVP).

Payments failure as a whole: transfer remains in Created / Held / Confirming (manual reconciliation).

5. Events
5.1. Who publishes
Payments (via Outbox): TransferCreated, TransferCompleted, TransferFailed.

Wallets (via Outbox): WalletCredited, WalletDebited. Does not publish hold events.

Identity and Fraud do not publish events in MVP.

5.2. Who subscribes
Notifications — to all Payments and Wallets events.

Audit — to all Payments and Wallets events.

Fraud — not subscribed to anything in MVP.

5.3. Delivery guarantees
At-least-once: an event may arrive twice.

Order not guaranteed: Audit sorts by createdAt.

Subscribers are idempotent: Notifications stores SentMessages with eventId; Audit — unique eventId.

6. Data models (by service)
Identity DB
Users (UserId, email, password, status), Roles, UserRoles.

Wallets DB
Wallets (UserId, available, held, version), Holds (holdId, transferId UNIQUE, status), WalletTransactions, Outbox.

Payments DB
Transfers (statuses: Created / Held / Completed / Failed / Confirming), IdempotencyKeys, Outbox.

Fraud DB
Rules, FraudSettings (fallbackThreshold), Decisions, Blacklist.

Notifications DB
Templates, SentMessages (eventId UNIQUE, channel, status), UserPreferences.

Audit DB
Events (append-only, eventId UNIQUE, correlationId nullable).

Redis (Cache)
user:{userId} → { exists: bool, status: string } (TTL: 60s/10s).

7. Key decisions
Decision	Rationale
Strict order: Key → Validation → Creation	Protection against race condition. Retry with a known key always returns the result, even if the recipient was deleted.
Redis cache for recipient validation	Removes Identity from the critical path of each transfer (protection against SPOF), reduces latency and load.
Hold before Fraud	Money is reserved before the verdict. There is no window in which Alice can spend it.
Sync Fraud	Decision is needed immediately.
Async Notifications / Audit	Transfer is completed, email is not critical.
Idempotency of hold/confirm/release	Protection against double debits on retry.
Outbox in Payments and Wallets	Guaranteed event publication.
Status Confirming	On unknown confirm result, we do not mark Failed to avoid losing money.
Local JWT validation on Gateway	Gateway only verifies the signature and routes. It does not know about business logic.
Auto-creation of wallet	Safe because Payments previously validated the recipient via Redis/Identity.
8. Differences from original documents
8.1. Added
API Gateway, Hold pattern, Idempotency-Key, Outbox (Payments + Wallets), Fallback, Audit, Redis cache, Confirming status, auto-creation of wallet.

8.2. Deferred
Merchants, Async follow-up for Fraud, Review flow, UserRegistered event from Identity.

9. Known limitations (MVP)
Merchants

Review flow

Async follow-up for Fraud

Watchdog for holds — if Payments crashes between hold and confirm, the hold hangs, manual intervention is needed.

Reconciliation for Confirming — transfers in Confirming require manual reconciliation with Wallets.

UserRegistered event — in MVP validation goes via Redis+Identity (sync). In production — event from Identity and local projection in Payments/Notifications.

Service-to-service authentication — (Security concern). Payments trusts the X-User-Id header from Gateway.

Integration with external payment provider

External notification channels (SMTP, FCM/APNs)

Correlation ID (field exists but is not populated)

Multi-currency (RUB only)

Monitoring (Prometheus, Grafana, Jaeger)

10. Technology stack
.NET / ASP.NET Core

Entity Framework Core + PostgreSQL

RabbitMQ

Redis

YARP

JWT

MediatR / CQRS

FluentValidation

Polly

Docker + docker-compose

GitHub Actions

11. References
Bounded Contexts

Roadmap

README