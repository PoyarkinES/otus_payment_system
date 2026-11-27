# Демо-сценарий M2: Wallets через Gateway

Сценарий проходит через публичный Gateway: регистрация пользователя, создание кошелька, пополнение, hold, confirm и просмотр истории операций. Wallets и PostgreSQL не публикуют порты наружу.

## Предусловия и запуск

- Запущен Docker Desktop, порт `8080` свободен.
- В корне есть `.env`; если его нет, создай из примера: `Copy-Item .env.example .env`.
- При наличии локального `docker-compose.override.yml` используй `-f docker-compose.yml`, как в командах ниже.

```powershell
docker compose -f docker-compose.yml pull
docker compose -f docker-compose.yml up -d
docker compose -f docker-compose.yml ps -a
```

Ожидаются четыре работающих сервиса в статусе `healthy`: `postgres`, `identity`, `wallets`, `gateway`; `wallets-db-bootstrap` должен завершиться с кодом `0`. Bootstrap создаёт `otus_wallets`, если БД ещё нет, и сохраняет существующий volume с `otus_identity`.

Gateway стартует независимо от Wallets: пока Wallets поднимается, маршруты `/api/wallets/**` могут временно отвечать `502/503`. Это ожидаемо; дождись `healthy` у Wallets.

Проверки:

```powershell
Invoke-RestMethod http://localhost:8080/health | ConvertTo-Json
Invoke-RestMethod http://localhost:8080/bff/health | ConvertTo-Json
docker compose -f docker-compose.yml exec wallets curl -fsS http://localhost:8080/health
```

`/bff/health` проверяет Gateway и Identity; Wallets проверяется отдельно по внутреннему `/health`.

## Регистрация и вход

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

## Создание кошелька и пополнение

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

## Hold, confirm и история

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

Для короткого демо `recipientUserId` — случайный GUID: при confirm Wallets создаёт для получателя кошелёк в той же валюте. Пополнение требует уникальный `Idempotency-Key`.

## Остановка

```powershell
docker compose -f docker-compose.yml down
```

Обычный `down` сохраняет данные. Не используй `down -v`, если нужно сохранить БД Identity и Wallets: обе находятся в volume PostgreSQL.