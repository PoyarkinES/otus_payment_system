# Testing strategy

This document closes issue #16: which tests exist, where they live, what is mandatory for a PR, and where we deliberately leave gaps.

Principle: **a test lives in the project of the layer it verifies.** A domain invariant must not be checked from `Host.Tests`, and middleware must not be checked from `Domain.Tests`. Otherwise tests start depending on infrastructure and stop being fast.

## Levels and where tests live

| Level | Project | What it verifies | Dependencies |
|---|---|---|---|
| Domain invariants | `Identity.Domain.Tests` | `User`, `RefreshToken`, value objects: status transitions, rotation and reuse detection, forbidden states | none |
| Domain invariants | `Wallets.Domain.Tests` | `Wallet`, `Hold`, `Money`: status transitions, limits, balances, hold idempotency | none |
| Application scenarios | `Identity.Application.Tests` | MediatR handlers: login/refresh/logout/register/block/update, chain revocation on password change | fake repositories, `NSubstitute` for `ITokenService` |
| Host level | `Identity.Host.Tests` | middleware (`CorrelationId`, `DomainException`), contract validators, `ClaimsPrincipalExtensions` | `DefaultHttpContext` |
| HTTP pipeline | `Wallets.Host.IntegrationTests` | wallet endpoints, ownership, hold/confirm/release, operations history, paging | `WebApplicationFactory`, test auth scheme, PostgreSQL |
| Middleware | `Wallets.Host.Tests` | `DomainExceptionMiddleware`, error code mapping | `DefaultHttpContext` |
| Infrastructure | `Identity.Infrastructure.Tests` | EF mapping, repositories, refresh token format, `JwtTokenService` on RS256 | PostgreSQL for part of the tests |
| Infrastructure | `Wallets.Infrastructure.Tests` | EF mapping, database constraints, versioning, paging, unique-violation mapping | PostgreSQL |
| Edge / BFF | `Gateway.Host.Tests` | `/bff/me`, `/bff/health`, `/bff/wallets`, totals/operations, upstream headers, 502/503 | substituted `HttpMessageHandler` |
| E2E through Gateway | `Gateway.Host.IntegrationTests` | YARP and BFF on top of real `Wallets.Host`/`Identity.Host` through TestServer | PostgreSQL |
| Shared library | `Shared.AspNetCore.Tests` | `ApiProblems`, `ValidationFilter`, response middleware | none |
| UI | `Client` (`Vitest` + Testing Library) | components, router, `apiFetch`, ProblemDetails handling | `jsdom`, MSW |
| Contract | `openapi/*.v1.json` + drift jobs | specs match the code | running Host |
| Smoke | [demo-m1.ru.md](./demo-m1.ru.md) | Docker stack: login, BFF, admin `GET /users/{id}` | docker compose |

There is no separate `Wallets.Application.Tests`: command logic is verified by HTTP tests in `Host.IntegrationTests`. This is a deliberate saving, not a model to copy — see "Gaps".

## Rules for placing new tests

- Entity or value object invariant → `Domain.Tests`. No database, no HTTP.
- Command/query logic → `Application.Tests`, dependencies through interfaces and fakes.
- EF mapping, migrations, SQL specifics → `Infrastructure.Tests`; the test name contains `Postgres` when a real database is required.
- Middleware, validators, endpoint handlers → `Host.Tests`; the network is substituted with `HttpMessageHandler`, not by mocking `IHttpClientFactory` entirely.
- End-to-end scenario through Gateway → `Gateway.Host.IntegrationTests`; Wallets is hosted in-process, but real HTTP pipelines are preserved.
- A concurrency test does not use `Thread.Sleep` and does not depend on execution order: the result is verified by database state and response code.
- `Wallets.Host.IntegrationTests` classes run sequentially (`AssemblyInfo.cs`): they share one test database, and parallel migrations cause a `42P07`/`42710` race.
- A new public scenario without a test **does not pass review**. The only exception is an explicit entry in "Gaps".

## Critical scenarios

| Scenario | Where it is verified | CI job |
|---|---|---|
| Authentication: login, refresh, rotation, reuse, block/unblock | `Identity.Application.Tests`, `Identity.Domain.Tests`, `Identity.Host.Tests` | `Test (Identity)`, `Identity integration tests` |
| Wallet ownership: foreign wallet → 403, missing wallet → 404 | `WalletsApiTests` | `Wallets integration tests` |
| Balance after deposit/withdraw and `Idempotency-Key` idempotency | `WalletsApiTests` | `Wallets integration tests` |
| Hold/confirm/release: balances, repeated call, concurrency | `WalletHoldsApiTests` | `Wallets integration tests` |
| Transfer rollback: `confirm` after `release` and vice versa → 409 | `WalletHoldsApiTests` | `Wallets integration tests` |
| Operations history: order, pages, `pageSize`, 400 on invalid parameters | `WalletOperationsApiTests`, `EfWalletRepositoryPostgresTests` | `Wallets integration tests` |
| BFF: wallets and operations aggregation, `recentOperations` 0..20, 502/503 | `BffWalletsEndpointTests`, `WalletsGatewayE2ETests` | `Test (Gateway)`, `Gateway E2E` |
| Fraud block | not implemented (M5), no tests | — |
| Rate limiting | manual 429 check | — |

## Tooling

| Tool | Where it is used | Comment |
|---|---|---|
| xUnit 2.9 + `Assert` | all .NET tests | base runner and assertions |
| `NSubstitute` | `Identity.Application.Tests` | substitutes `ITokenService` and external dependencies |
| `WebApplicationFactory` / TestServer | `Wallets.Host.IntegrationTests`, `Gateway.Host.IntegrationTests` | real HTTP pipeline without network |
| PostgreSQL 17 | infrastructure tests, Wallets Host, Gateway E2E | docker compose locally, service container in CI |
| `coverlet.collector` | `Test (...)` jobs, `coverage-*` artifacts | cobertura, no gate |
| `Vitest` + Testing Library + MSW | `Client` | component tests, API mocked at fetch level |
| `Orval` | `Client` | client generation from `openapi/gateway.v1.json` |

Deliberately **not used**:

- `FluentAssertions` — plain `Assert` is enough, and it avoids an extra dependency in every test project;
- `Testcontainers` — CI already runs a PostgreSQL service container, and locally the same container comes from docker compose; Testcontainers would add startup time and a second database configuration;
- `Moq` — Identity uses `NSubstitute`, and the rest needs only hand-written fakes.

The decision is revisited if a multi-database matrix appears or a message broker has to be started in tests.

## Coverage

Layer targets are guidance for review, not a hard gate: CI collects cobertura and stores `coverage-*` artifacts, but does not enforce a threshold.

| Layer | Target |
|---|---|
| Domain | ≥ 90% |
| Application | ≥ 85% |
| Infrastructure | ≥ 70% |
| Host / API | ≥ 70% |
| Client | not measured |

We will enable a CI threshold after M3 stabilizes, when Payments and Fraud exist. Until then coverage is reviewed by eye: a drop in the touched layer is a reason to ask a question, not to block a merge.

## What CI runs

| Job | What it does | What it catches |
|---|---|---|
| `Pack Shared NuGet packages` | `dotnet pack` for two Shared projects + artifact | NuGet package versions and contents |
| `Build` (matrix of 8 solutions) | `dotnet build -c Release` | compilation errors in any context |
| `Test (Shared/Gateway/Identity/Wallets)` | `dotnet test` + coverage | regressions in unit/host tests |
| `Identity integration tests (PostgreSQL)` | tests against real PostgreSQL | EF mapping and SQL discrepancies |
| `Wallets integration tests (PostgreSQL)` | Wallets Host HTTP tests and repository Postgres tests | races, database constraints, ownership, holds, history |
| `Gateway E2E (Wallets through Gateway)` | YARP + BFF through TestServer | end-to-end scenario through Gateway |
| `OpenAPI contract drift` | Identity spec generation and `git diff` | Identity contract diverged from code |
| `OpenAPI gateway drift` | Gateway spec generation and `git diff` | BFF contract diverged from code |
| `Docker build (Identity/Gateway)` | `docker build -f <Dockerfile> .` | Dockerfile and build context diverged from code |
| `Client (lint, test, build)` | client generation, lint, format, typecheck, Vitest, `vite build` | stale generated client, type errors, UI regressions |

Filters in the `Test` matrix:

- Shared — no filter;
- Gateway — `FullyQualifiedName!~IntegrationTests`;
- Identity — `FullyQualifiedName!~Postgres`;
- Wallets — `FullyQualifiedName!~Postgres & FullyQualifiedName!~IntegrationTests`.

What is mandatory for a PR: every job above. A merge is blocked if any job is red: branch protection requires every mandatory check, including drift, integration tests, and the client.

There is no nightly workflow yet. Candidates for it: a full run of all Postgres tests, a five-fold repeat of concurrency tests (`hold`/`confirm`/`deposit`/`withdraw`), a `docker compose` smoke run, and dependency vulnerability scanning. We will add it as a separate task when the first paid runner appears or PR run time grows.

## Running locally

```powershell
.\scripts\pack-shared.ps1
dotnet test Shared\Otus.PaymentSystem.Shared.sln
dotnet test Gateway\Otus.PaymentSystem.Gateway.sln
dotnet test Identity\Otus.PaymentSystem.Identity.sln --filter "FullyQualifiedName!~Postgres"
dotnet test Wallets\Otus.PaymentSystem.Wallets.sln --filter "FullyQualifiedName!~Postgres & FullyQualifiedName!~IntegrationTests"
```

Postgres tests:

```powershell
$env:IDENTITY_TEST_DB  = 'Host=localhost;Port=5434;Database=otus_identity_test;Username=otus;Password=payments'
$env:WALLETS_API_TEST_DB  = 'Host=localhost;Port=5433;Database=otus_wallets_api_test;Username=otus;Password=payments'
$env:WALLETS_REPO_TEST_DB = 'Host=localhost;Port=5433;Database=otus_wallets_repo_test;Username=otus;Password=payments'

dotnet test Identity\test\Otus.PaymentSystem.Identity.Infrastructure.Tests --filter "FullyQualifiedName~EfUserRepositoryPostgresTests"
dotnet test Wallets\test\Otus.PaymentSystem.Wallets.Infrastructure.Tests --filter "FullyQualifiedName~Postgres"
dotnet test Wallets\test\Otus.PaymentSystem.Wallets.Host.IntegrationTests
dotnet test Gateway\test\Otus.PaymentSystem.Gateway.Host.IntegrationTests
```

Client:

```powershell
cd Client
npm ci
npm run api:generate
npm run lint
npm run typecheck
npm run test
npm run build
```

Run `scripts/pack-shared.ps1` before building the services; CI does it in the `Pack Shared NuGet packages` job and shares the artifact.

## Gaps (deliberate)

1. **`Wallets.Application.Tests` does not exist.** The logic of `PlaceHold`/`ConfirmHold`/`ReleaseHold` (retries, auto-creating the recipient wallet) is verified only by HTTP tests. Add the project when the scenarios grow.
2. **No UI E2E.** Client has component tests, but there is no browser-level scenario (Playwright).
3. **Payments/Fraud/Notifications/Audit are skeletons.** Their tests will appear in M3–M5; `Audit` currently contains project placeholders only.
4. **Rate limiting is not covered by tests** — the limiter lives in Gateway configuration and is checked manually.
5. **Identity HTTP tests use a fake repository**, not a real database; the EF path is verified separately in `Identity.Infrastructure.Tests`.
6. **No nightly run** — concurrency tests execute once per PR.
7. **Wallets Host integration classes are sequential** because they share a database; as the number of classes grows, split databases per collection to restore parallelism.

## Definition of Done for a test

- The test fails if the fix is reverted (checked manually during review).
- The test name reads as a statement: `GetMe_NoRole_ReturnsUnauthorized`.
- No `Thread.Sleep`, no external network calls, no dependency on the machine clock.
- The test does not require a run order: any test can be run on its own.
- A database test cleans up after itself or uses unique identifiers so it does not affect neighbouring tests.
