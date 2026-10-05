# Otus Payment System — Roadmap

This roadmap outlines six one-month phases (24 weeks total). It describes planned work only; no business logic is implemented in the current scaffold.

## Phase 1: Base platform (Weeks 1–4)

| Week | Goal | Expected result |
| --- | --- | --- |
| 1 | Confirm project boundaries, bounded contexts, and Clean Architecture conventions. | Documented structure and development conventions. |
| 2 | Establish the .NET solution and API/application project boundaries. | A buildable platform skeleton with no payment features. |
| 3 | Plan persistence and configuration boundaries; add initial infrastructure setup. | A documented persistence approach and locally runnable development setup. |
| 4 | Establish API error handling, health checks, and an automated test baseline. | A testable platform foundation ready for feature work. |

## Phase 2: Wallets (Weeks 5–8)

| Week | Goal | Expected result |
| --- | --- | --- |
| 5 | Model wallet ownership, currency, and lifecycle. | Wallet domain model and creation flow. |
| 6 | Define balance-changing operations and validation rules. | Wallet operations with explicit balance invariants. |
| 7 | Record wallet operation history and handle concurrent updates. | Traceable operations and concurrency-safe balance updates. |
| 8 | Cover wallet behavior with automated tests and document the API. | Tested wallet capabilities with documented request and response contracts. |

## Phase 3: Transfers between users (Weeks 9–12)

| Week | Goal | Expected result |
| --- | --- | --- |
| 9 | Define transfer states and the user-to-user transfer flow. | Transfer model and documented lifecycle. |
| 10 | Implement the debit/credit flow with atomicity and sufficient-funds checks. | Transfers that either complete fully or leave balances unchanged. |
| 11 | Define idempotency and audit requirements for transfer requests. | Duplicate-safe requests and traceable transfer records. |
| 12 | Test success, rejection, concurrency, and failure scenarios. | A verified transfer capability with documented behavior. |

## Phase 4: Async processing (Weeks 13–16)

| Week | Goal | Expected result |
| --- | --- | --- |
| 13 | Define domain events and message contracts for completed transfers. | Versionable event contracts and messaging boundaries. |
| 14 | Introduce message publishing and consumer foundations. | Transfer events can be published and consumed asynchronously. |
| 15 | Add audit and notification consumers. | Independent consumers react to transfer events. |
| 16 | Define retry, duplicate delivery, and dead-letter handling. | Documented and tested failure-recovery behavior. |

## Phase 5: Anti-fraud (Weeks 17–20)

| Week | Goal | Expected result |
| --- | --- | --- |
| 17 | Define fraud signals, evaluation outcomes, and configurable limits. | An extensible fraud-rule model. |
| 18 | Evaluate transfer amount and frequency patterns. | Initial rules identify transactions that need review. |
| 19 | Add risk scoring and a review/block decision flow. | Explainable risk outcomes integrated with transfer processing. |
| 20 | Test rule combinations and document operational considerations. | A verified initial anti-fraud capability and rule documentation. |

## Phase 6: Payment gateway (Weeks 21–24)

| Week | Goal | Expected result |
| --- | --- | --- |
| 21 | Define merchant-facing payment contracts and payment lifecycle. | Documented external API and payment states. |
| 22 | Design merchant authentication and request protection. | A reviewed security model for API access. |
| 23 | Add webhook delivery and payment reversal flows. | Documented integration flows with delivery and failure handling. |
| 24 | Complete API documentation, end-to-end tests, and project review. | A presentable learning-project gateway milestone and a plan for future improvements. |
