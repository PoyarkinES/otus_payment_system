# M2 demo: Wallets through the UI

This walkthrough is shown in the browser: registration/login, wallet, deposit/withdraw, hold, confirmation, and operation history. Do not show Gateway, curl, or Swagger — they are not part of the demo.

## Prerequisites

- The Docker stack is already running: `postgres`, `identity`, `wallets`, `gateway` — `healthy`; `wallets-db-bootstrap` — `Exited (0)`.
- The client is running separately:
  ```powershell
  cd Client
  npm run dev
  ```
- Open `http://localhost:5173`.

## Scenario

### 1. Sign in

Use the demo admin:

```
admin@otus.local
AdminP@ssw0rd
```

Or open `/register`, create a new user, and sign in as that user.

### 2. Wallet

Open “Wallets”. If there is no wallet, click “Create wallet” (RUB). Show:

- available;
- held;
- currency and status.

### 3. Deposit

Enter an amount, for example `100`, and click “Deposit”. Show that available grows while held stays unchanged.

### 4. Withdraw

Enter an amount, for example `10`, and click “Withdraw”. Show available decreasing.

For a negative path, try withdrawing more than the balance — the UI shows an error and the balance stays unchanged.

### 5. Hold

Enter an amount, for example `25`, and a recipient UUID (any valid UUID). Create the hold. Show:

- available decreased;
- held increased.

Then:

- **Confirm** — the hold is settled and held decreases;
- or **Release** — the funds return to available.

### 6. Operation history

Open “Operation history”. Show:

- deposit, withdrawal, and hold entries;
- type and date filters;
- loading the next pages on scroll.

### 7. Optional

- Profile: email, role, logout.
- A repeated “Deposit” click must not create a duplicate: the button is disabled while pending, and `Idempotency-Key` protects retries.

## If the UI does not open

Show pre-recorded screenshots or a recording of the scenario. Do not switch to the console or debug the stack during the retro.

## After the demo

Stop the stack with:

```powershell
docker compose -f docker-compose.yml down
```

`down` without `-v` preserves Identity and Wallets data in the PostgreSQL volume.
