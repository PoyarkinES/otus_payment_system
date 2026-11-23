# Gateway

API Gateway / BFF для Otus Payment System. Это **edge-сервис**, а не bounded context:
он не содержит бизнес-логики, не хранит данные и не знает про агрегаты.

Подробнее: [ADR 0001](../docs/adr/0001-api-gateway.md).

## Что делает

- маршрутизирует запросы к внутренним сервисам через YARP;
- валидирует JWT локально (issuer/audience/подпись/срок/алгоритм);
- добавляет `X-User-Id` из claim `sub` и удаляет клиентский `X-User-Id`;
- настраивает CORS для фронтенда;
- прокидывает `X-Correlation-Id`;
- предоставляет read-only BFF-эндпоинты `/bff/me`, `/bff/health` и `/bff/wallets`;
- aggregates `/bff/wallets` from the user's wallet list, per-currency totals, and recent Wallets operations;
- отдаёт `/health` самого Gateway.

## Локальный запуск

Нужен запущенный Identity на `http://localhost:5100`.

```bash
dotnet run --project Gateway/src/Otus.PaymentSystem.Gateway.Host
```

Gateway слушает `http://localhost:8080`.

Проверка:

```bash
curl.exe -i http://localhost:8080/health
```

Ожидаемо:

```json
{"status":"ok","service":"gateway"}
```

## Маршруты

| Публичный путь | Куда | Защита |
|---|---|---|
| `/api/identity/auth/me` | Identity `/auth/me` | JWT обязателен |
| `/api/identity/users/**` | Identity `/users/**` | JWT обязателен |
| `/api/identity/**` | Identity `/**` | без политики на Gateway (login/register анонимны) |
| `/api/wallets/**` | Wallets `/wallets/**` | JWT обязателен |
| `/bff/me` | Gateway claims | JWT обязателен |
| `/bff/health` | Gateway + Identity | анонимно |
| `/bff/wallets` | Gateway + Wallets | JWT обязателен |
| `/health` | Gateway | анонимно |

Маршруты Payments появятся в M3.

`GET /bff/wallets?recentOperations=5` принимает `recentOperations` в диапазоне `0..20` (по умолчанию `5`). Значение `0` возвращает кошельки и totals без запросов истории. Upstream non-2xx преобразуется в `502`, недоступность и таймаут Wallets — в `503`; частичный агрегат не возвращается.

## Конфигурация

`appsettings.json`:

- `Jwt:Issuer` / `Jwt:Audience` — должны совпадать с Identity;
- `Cors:AllowedOrigins` — адрес фронтенда;
- `ReverseProxy` — маршруты, кластеры, destinations.

`appsettings.Development.json`:

- `Jwt:Secret` — только dev-секрет, совпадает с Identity.

В production секрет задаётся через переменные окружения/secret store, не через файл в репозитории.

## Тесты

```bash
dotnet test Gateway/Otus.PaymentSystem.Gateway.sln --arch x64
```

Покрыто:

- `UserIdHeaderMiddleware`: удаление клиентского заголовка, подстановка `sub`,
  невалидный/отсутствующий `sub`;
- `CorrelationIdMiddleware`: сохранение, генерация, невалидные и слишком длинные значения.
- `/bff/wallets`: валидация параметра, валютные totals, операции, forwarded headers и ошибки upstream;
- Gateway E2E: BFF получает настоящие кошельки и историю через in-process Wallets Host и PostgreSQL.

### Почему `--arch x64`

На Windows VSTest иногда поднимает `testhost.x86`, который падает с
`Win32Exception (5): Access denied` при попытке открыть handle родительского процесса.
В тестовом проекте отключён parent-process query:

```xml
<_MSTestEnableParentProcessQuery>false</_MSTestEnableParentProcessQuery>
```

и добавлен `runtimeconfig.template.json` с `"MSTest.EnableParentProcessQuery": false`.
`--arch x64` дополнительно фиксирует архитектуру testhost.

## CI

Gateway включён в матрицу `.github/workflows/ci.yml` и собирается как отдельное решение.
