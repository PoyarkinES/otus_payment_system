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
| `/health` | Gateway | анонимно |

Маршруты Wallets и Payments появятся в M2–M3.

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
