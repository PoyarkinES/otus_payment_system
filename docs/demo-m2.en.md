# M2 demo: Wallets through the Gateway

This walkthrough uses the public Gateway for registration, wallet creation, deposit, hold, confirmation, and operation history. Wallets and PostgreSQL are not exposed on host ports.

## Prerequisites and startup

- Docker Desktop is running and port `8080` is available.
- A root `.env` file exists; create it from the example if needed: `Copy-Item .env.example .env`.
- If the local `docker-compose.override.yml` is present, pass `-f docker-compose.yml` as shown below.

```powershell
docker compose -f docker-compose.yml pull
docker compose -f docker-compose.yml up -d
docker compose -f docker-compose.yml ps -a
```

Expect four running services in `healthy` state: `postgres`, `identity`, `wallets`, and `gateway`; `wallets-db-bootstrap` should exit with code `0`. The bootstrap creates `otus_wallets` only when it is missing and preserves the existing `otus_identity` volume.

Gateway starts independently of Wallets: while Wallets is coming up, `/api/wallets/**` may temporarily return `502/503`. This is expected; wait for Wallets to become `healthy`.

Health checks:

```powershell
Invoke-RestMethod http://localhost:8080/health | ConvertTo-Json
Invoke-RestMethod http://localhost:8080/bff/health | ConvertTo-Json
docker compose -f docker-compose.yml exec wallets curl -fsS http://localhost:8080/health
```

`/bff/health` checks Gateway and Identity; Wallets has its own internal `/health` endpoint.

## Register and log in

```powershell
$email = "wallet-demo-$([guid]::NewGuid().ToString('N'))@otus.local"
$password = 'WalletP@ssw0rd1'
$authBody = @{ email = $email; password = $password } | ConvertTo-Json

Invoke-RestMethod -Method Post http://localhost:8080/api/identity/auth/register `
    -ContentType 'application/json' -Body $authBody | Out-Null

$login = Invoke-RestMethod -Method Post http://localhost:8080/api/identity/auth/login `
    -ContentType 'application/json' -Body $authBody
$headers = @{ Authorization = "Bearer $($login.accessToken)" }
```

## Create and fund a wallet

```powershell
$wallet = Invoke-RestMethod -Method Post http://localhost:8080/api/wallets `
    -Headers $headers -ContentType 'application/json' `
    -Body (@{ currency = 'RUB' } | ConvertTo-Json)
$walletId = $wallet.id

$depositHeaders = $headers.Clone()
$depositHeaders['Idempotency-Key'] = [guid]::NewGuid().ToString()
Invoke-RestMethod -Method Post "http://localhost:8080/api/wallets/$walletId/deposit" `
    -Headers $depositHeaders -ContentType 'application/json' `
    -Body (@{ amount = 100.00 } | ConvertTo-Json) | ConvertTo-Json
```

## Place and confirm a hold, then view history

```powershell
$hold = Invoke-RestMethod -Method Post "http://localhost:8080/api/wallets/$walletId/holds" `
    -Headers $headers -ContentType 'application/json' `
    -Body (@{
        transferId = [guid]::NewGuid().ToString()
        amount = 25.00
        recipientUserId = [guid]::NewGuid().ToString()
    } | ConvertTo-Json)

Invoke-RestMethod -Method Post "http://localhost:8080/api/wallets/holds/$($hold.id)/confirm" `
    -Headers $headers | Out-Null

Invoke-RestMethod "http://localhost:8080/api/wallets/$walletId/operations" `
    -Headers $headers | ConvertTo-Json -Depth 10
Invoke-RestMethod http://localhost:8080/bff/wallets -Headers $headers | ConvertTo-Json -Depth 10
```

For this short demo, `recipientUserId` is a random GUID; on confirmation, Wallets creates a recipient wallet in the same currency. Deposits require a unique `Idempotency-Key`.

## Stop the stack

```powershell
docker compose -f docker-compose.yml down
```

Plain `down` preserves data. Do not use `down -v` if you need to keep Identity and Wallets data; both databases live in the PostgreSQL volume.