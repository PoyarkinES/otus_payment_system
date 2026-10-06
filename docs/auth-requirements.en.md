# Authentication and Authorization Requirements

## 1. Purpose

Define baseline requirements for authentication and authorization in **Otus Payment System** to protect user accounts, wallets, payments, and administrative operations.

## 2. Scope

This document covers:

- User authentication (sign-up, sign-in, token lifecycle)
- API authorization (role- and policy-based access)
- Security controls for identity-related operations
- Audit and observability requirements

Out of scope (for this issue):

- External IdP integration (Google, Azure AD, etc.)
- MFA implementation details
- Merchant API key model (covered in payment gateway phase)

## 3. Definitions

- **Authentication**: verifying user identity.
- **Authorization**: checking if an authenticated user can perform an action.
- **Access Token**: short-lived JWT used for API access.
- **Refresh Token**: long-lived token used to obtain a new access token.
- **RBAC**: role-based access control.

## 4. Actors and Roles

Minimum roles:

- `User` — standard account operations.
- `Admin` — operational/admin endpoints.
- `Support` (optional for initial phase) — read-only operational access.

Future role expansion must be supported without breaking existing tokens.

## 5. Functional Requirements — Authentication

### 5.1 Registration

1. System shall allow user registration with unique email.
2. System shall store password as a strong one-way hash (never plaintext).
3. System shall validate password policy (min length and complexity).
4. System shall return clear validation errors without exposing internal details.

### 5.2 Login

1. System shall authenticate by email + password.
2. On success, system shall issue:
   - JWT access token
   - Refresh token
3. On failure, system shall return generic auth error (`invalid credentials`).

### 5.3 Token Lifecycle

1. Access token should be short-lived (e.g., 15 minutes).
2. Refresh token should be revocable and rotatable.
3. System shall provide token refresh endpoint.
4. System shall support logout by refresh token revocation.
5. System shall invalidate refresh tokens after password change.

### 5.4 Password Management

1. System shall support password change for authenticated users.
2. System should support password reset flow (email-based) in later iteration.
3. Password history/reuse restrictions are optional for initial release.

## 6. Functional Requirements — Authorization

### 6.1 API Access Control

1. All protected endpoints shall require valid JWT bearer token.
2. Authorization shall be policy-based with role checks where applicable.
3. Anonymous access shall be explicitly allowed only for:
   - registration
   - login
   - health/public endpoints (if needed)

### 6.2 Resource Ownership

1. User shall only access own wallets, transfers, and profile data.
2. Admin role may access cross-user operational endpoints.
3. Ownership checks shall be enforced in application/domain layer, not only controller layer.

### 6.3 Minimum Endpoint Policy Matrix

- `POST /auth/register` — anonymous
- `POST /auth/login` — anonymous
- `POST /auth/refresh` — anonymous (requires valid refresh token)
- `POST /auth/logout` — authenticated user
- Wallet/payment endpoints — authenticated user + ownership policy
- Admin endpoints — `Admin` role

## 7. Security Requirements

1. JWT signing key must be stored securely (no hardcoding, use configuration secrets).
2. Use HTTPS in all non-local environments.
3. Implement brute-force protection for login (rate limiting / lockout strategy).
4. Do not leak sensitive details in auth error responses.
5. Include `sub`, `email`, `role`, `jti`, `exp` claims in access token.
6. Validate issuer/audience/signature/lifetime for every token.
7. Refresh tokens must be stored server-side with revocation status.
8. Security events must be auditable (login success/failure, token refresh, logout, role changes).

## 8. Non-Functional Requirements

1. Authentication endpoints shall be observable (logs + metrics).
2. Authorization failures (`403`) and authentication failures (`401`) shall be distinguishable.
3. Auth subsystem should be stateless for access token validation.
4. Token validation should add minimal overhead to request processing.

## 9. Data and Persistence Requirements

1. Identity data shall include:
   - User ID
   - Email
   - PasswordHash
   - Roles
   - Status (Active/Blocked)
   - CreatedAt/UpdatedAt
2. Refresh token data shall include:
   - Token ID / hash
   - User ID
   - ExpiresAt
   - RevokedAt
   - ReplacedByTokenId (for rotation chains)

## 10. Audit and Compliance Requirements

1. System shall log:
   - registration attempts
   - login success/failure
   - token refresh/revocation
   - role assignment changes
2. Logs shall avoid storing secrets/passwords/tokens in plaintext.
3. Correlation ID should be present for auth-related requests.

## 11. Acceptance Criteria (Issue #15)

Issue is considered complete when:

1. `docs/auth-requirements.md` is created and agreed.
2. Required roles and access model are documented.
3. JWT + refresh token lifecycle requirements are documented.
4. Security and audit minimums are documented.
5. Clear boundary between in-scope and out-of-scope is documented.

## 12. Implementation Notes (for next issues)

Recommended next tasks:

1. Implement ASP.NET Core Identity (or equivalent) entity model.
2. Add JWT bearer authentication setup.
3. Introduce policy-based authorization handlers for ownership checks.
4. Implement refresh token storage + rotation + revocation.
5. Add integration tests for `401/403`, role checks, and ownership checks.

## 13. Follow-up Implementation Checklist

> Status legend: `[ ]` not started, `[~]` in progress, `[x]` done.

### A. Identity Model and Storage (links: 4, 5.1, 5.4, 9)
- [ ] Create `User` entity with fields from section 9 (id, email, password hash, roles, status, timestamps).
- [ ] Enforce unique email at DB level and application level.
- [ ] Add role model (`User`, `Admin`, optional `Support`) and seed baseline roles.
- [ ] Implement password hashing via framework-approved hasher (no custom crypto).
- [ ] Add migrations for identity and refresh-token tables.

### B. Authentication API (links: 5.1, 5.2, 5.3, 6.3)
- [ ] Implement `POST /auth/register` (anonymous).
- [ ] Implement `POST /auth/login` (anonymous).
- [ ] Implement `POST /auth/refresh` (anonymous + refresh token validation).
- [ ] Implement `POST /auth/logout` (authenticated + refresh token revocation).
- [ ] Add consistent auth error responses (`invalid credentials`, no sensitive details).

### C. JWT and Refresh Token Lifecycle (links: 5.3, 7, 8, 9)
- [ ] Configure JWT issuance with required claims: `sub`, `email`, `role`, `jti`, `exp`.
- [ ] Validate issuer, audience, signature, and lifetime on each request.
- [ ] Set short access-token TTL (target: ~15 minutes).
- [ ] Implement refresh-token rotation and revocation chain (`ReplacedByTokenId`).
- [ ] Revoke all active refresh tokens on password change.

### D. Authorization and Policies (links: 6.1, 6.2, 6.3)
- [ ] Configure auth middleware and protect all non-public endpoints by default.
- [ ] Add role-based policies for admin-only operations.
- [ ] Add ownership policy/handler for wallet/payment/profile resources.
- [ ] Ensure ownership checks are enforced in app/domain layer, not only controllers.
- [ ] Verify endpoint-policy matrix from section 6.3 is implemented.

### E. Security Hardening (links: 7)
- [ ] Move JWT secrets to secure configuration (user-secrets/dev, vault/env for non-local).
- [ ] Enforce HTTPS in non-local environments.
- [ ] Add login brute-force mitigation (rate limiting or lockout).
- [ ] Ensure logs and responses never expose passwords/tokens/secrets.

### F. Observability and Audit (links: 8, 10)
- [ ] Add structured logs for registration, login success/failure, refresh, logout, role changes.
- [ ] Add correlation ID propagation for auth requests.
- [ ] Add metrics for auth endpoints (request count, latency, failure rate).
- [ ] Ensure `401` and `403` are clearly distinguishable in API behavior and logs.

### G. Testing and Definition of Done (links: 11, 12)
- [ ] Unit tests for token generation/validation and policy handlers.
- [ ] Integration tests for `register/login/refresh/logout` happy-path and failure-path.
- [ ] Integration tests for `401` (unauthenticated) and `403` (unauthorized) scenarios.
- [ ] Integration tests for ownership boundaries (own vs other users' resource access).
- [ ] Review checklist against section 11 acceptance criteria and mark completed.
