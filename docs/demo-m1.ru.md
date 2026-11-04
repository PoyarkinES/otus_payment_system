# Демо-сценарий M1

Что показывает демо: единая точка входа (Gateway), BFF-агрегаты для фронтенда, админский доступ к чужому профилю, healthcheck всего стека. Всё — в Docker, без локальных `dotnet run`.

## Предусловия

- Docker Desktop запущен, `docker compose version` отвечает.
- В корне репозитория есть `.env` (`POSTGRES_DB`, `POSTGRES_USER`, `POSTGRES_PASSWORD`) — compose читает его автоматически.
- Порт `8080` свободен. Локально запущенные `Otus.PaymentSystem.*` процессы остановлены:

  ```powershell
  Get-Process | Where-Object { $_.ProcessName -like 'Otus.PaymentSystem*' } |
      Select-Object Id, Path
  ```

- Если в корне есть локальный `docker-compose.override.yml` (он публикует postgres на 5434), запускать нужно с явным `-f`: иначе Docker подмешает override и конфликтует портом с dev-контейнером `otus-postgres`.

## Шаг 1. Подъём стека

```powershell
docker compose -f docker-compose.yml up --build -d
docker compose -f docker-compose.yml ps
```

Ожидаем три сервиса в статусе `healthy`:

```
otus-payment-system-postgres-1    postgres:17    (healthy)   5432/tcp
otus-payment-system-identity-1    ...identity    (healthy)   8080/tcp
otus-payment-system-gateway-1     ...gateway     (healthy)   0.0.0.0:8080->8080/tcp
```

Первый запуск 2–5 минут: собираются два образа. Наружу открыт только Gateway, БД и Identity доступны лишь внутри сети compose.

## Шаг 2. Healthcheck

```powershell
Invoke-RestMethod http://localhost:8080/health | ConvertTo-Json
Invoke-RestMethod http://localhost:8080/bff/health | ConvertTo-Json
```

Ожидаем:

```json
{ "status": "ok", "service": "gateway" }
{ "status": "ok", "services": { "gateway": "ok", "identity": "ok" } }
```

`/bff/health` — агрегат: Gateway отвечает за себя, а статус Identity получает единственным внутренним вызовом `http://identity:8080/health`.

## Шаг 3. Логин и BFF

```powershell
$t  = Invoke-RestMethod -Method Post http://localhost:8080/api/identity/auth/login `
      -ContentType 'application/json' -Body '{"email":"admin@otus.local","password":"AdminP@ssw0rd"}'
$h  = @{ Authorization = "Bearer $($t.accessToken)" }
$me = Invoke-RestMethod http://localhost:8080/bff/me -Headers $h
$me | ConvertTo-Json
```

```json
{ "userId": "…", "email": "admin@otus.local", "role": "Admin" }
```

`/bff/me` не ходит в Identity: `sub`, `email` и `role` берутся из claims токена.

Redis/кеша нет, поэтому токены живут в памяти Identity: подпись RS256, ключ эфемерный (Development). Gateway валидирует подпись по `http://identity:8080/.well-known/jwks.json` и переживает рестарт Identity за счёт обновления по `kid`.

## Шаг 4. Админский доступ к чужому профилю

```powershell
Invoke-RestMethod "http://localhost:8080/api/identity/users/$($me.userId)" -Headers $h | ConvertTo-Json
```

```json
{ "userId": "…", "email": "admin@otus.local", "status": 0, "createdAt": "2026-11-04T17:48:31.805248+00:00" }
```

## Шаг 5. Негативные сценарии

**5.1. Без токена — 401:**

```powershell
try { Invoke-WebRequest http://localhost:8080/bff/me -UseBasicParsing } catch {
    "status: $([int]$_.Exception.Response.StatusCode)"
}
```

**5.2. Обычный пользователь — 403 на админский маршрут:**

```powershell
Invoke-RestMethod -Method Post http://localhost:8080/api/identity/auth/register `
    -ContentType 'application/json' -Body '{"email":"user1@otus.local","password":"UserP@ssw0rd1"}' | Out-Null

$u = Invoke-RestMethod -Method Post http://localhost:8080/api/identity/auth/login `
     -ContentType 'application/json' -Body '{"email":"user1@otus.local","password":"UserP@ssw0rd1"}'

try {
    Invoke-RestMethod "http://localhost:8080/api/identity/users/$($me.userId)" `
        -Headers @{ Authorization = "Bearer $($u.accessToken)" }
} catch { "status: $([int]$_.Exception.Response.StatusCode)" }
```

Ожидаем `status: 403`.

**5.3. Rate limiting на login — 429:**

```powershell
1..6 | ForEach-Object {
    try {
        Invoke-RestMethod -Method Post http://localhost:8080/api/identity/auth/login `
            -ContentType 'application/json' -Body '{"email":"nobody@otus.local","password":"wrong"}' |
            Out-Null
        "$_ : 200"
    } catch {
        "$_ : $([int]$_.Exception.Response.StatusCode)"
    }
}
```

Политика `login` — 5 попыток в минуту с одного IP, ожидаем 429 на шестой.

**5.4. Отказ зависимости — 503:**

```powershell
docker compose -f docker-compose.yml stop identity
try { Invoke-WebRequest http://localhost:8080/bff/health -UseBasicParsing } catch {
    $resp = $_.Exception.Response
    $reader = New-Object IO.StreamReader($resp.GetResponseStream())
    "{0} {1}" -f [int]$resp.StatusCode, $reader.ReadToEnd()
}
```

Ожидаем `503` и `"identity": "unavailable"`. Gateway при этом продолжает отвечать. Вернуть: `docker compose -f docker-compose.yml start identity`.

## Шаг 6. Уборка

```powershell
docker compose -f docker-compose.yml down          # контейнеры, сеть; данные БД остаются
docker compose -f docker-compose.yml down -v       # плюс удалить volume с БД
```

## Если что-то не поднялось

| Симптом | Причина и что делать |
|---|---|
| `port is already allocated` на 8080 | локальный Gateway держит порт — остановить процесс из предусловий |
| `port is already allocated` на 5434 | подхватился `docker-compose.override.yml` — перезапустить с `-f docker-compose.yml` |
| `identity` в `restarting` | `docker compose -f docker-compose.yml logs --tail 100 identity`; чаще всего неверный `POSTGRES_PASSWORD` в `.env` |
| `identity healthy`, но `/bff/me` → 401 | Gateway не увидел JWKS: проверить `Jwt__JwksUri` и лог старта Gateway |
| админ не логинится | сидер не сработал: проверить, что `Seed__Admin__Email`/`Seed__Admin__Password` дошли до контейнера (`docker compose -f docker-compose.yml exec identity env`) |
| таблиц нет | `Database__AutoMigrate` не включён: в compose это `"true"`, в обычном запуске схему применяет `dotnet ef database update` |
