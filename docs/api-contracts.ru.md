# Контракты API: что сделано и с чего начать

> **Обновлено 02.11.2026.** Ветка `feature/m1-w3-problemdetails` (M1W3), коммит `5efd0a0`.
> **Связанные:** [#22](https://github.com/PoyarkinES/otus_payment_system/issues/22) · [#21](https://github.com/PoyarkinES/otus_payment_system/issues/21) · [#48](https://github.com/PoyarkinES/otus_payment_system/issues/48) · [#49](https://github.com/PoyarkinES/otus_payment_system/issues/49) · [ADR 0001](./adr/0001-api-gateway.md) · [Единый формат ошибок](./api-errors.ru.md)

## 0. С чего начать

1. ~~Закоммитить документацию~~ — ✅ выполнено в `5efd0a0`: [api-errors.ru.md](./api-errors.ru.md), правки [db-schema.ru.md](./db-schema.ru.md), этот файл.
2. ~~Закрыть #22~~ — ✅ выполнено: ошибки Identity и Gateway приведены к контракту (`Shared.AspNetCore`, `DomainError`, middleware), тесты зелёные.
3. **Отдельная ветка `feature/openapi-artifact`** — довести метаданные Identity, сгенерировать `openapi/identity.v1.json`, добавить drift-check в CI. Это шаг, с которого начинается собственно «контракт как артефакт».
4. Дальше — #21 (валидация), #48 (refresh/JWKS/rate limiting), #49 (BFF contract-first).

Шаг 3 — точка входа во всю работу по контрактам. Всё, что до него, — подготовка.

## 1. Что уже сделано

| Что | Где | Статус |
|---|---|---|
| Структурное логирование, correlation ID, `X-Correlation-Id` на Gateway и Identity | #20, PR #97 | ✅ в `main` |
| Единый контракт ошибок: поля, content-type, маппинг статусов, `traceId` | [api-errors.ru.md](./api-errors.ru.md) | ✅ задокументирован и реализован (#22) |
| Решение по хранению refresh-токена (SHA-256, не HMAC) | [db-schema.ru.md](./db-schema.ru.md):46, 332 | ✅ зафиксировано |
| Целевые маршруты Gateway и BFF | [ADR 0001](./adr/0001-api-gateway.md):38–53 | ✅ есть, но неполные |
| Матрица auth-политик по эндпоинтам | [auth-requirements.ru.md](./auth-requirements.ru.md):88–95 | ✅ нормативная |
| Внутренние контракты контекстов (агрегаты, события, JSON-конверт) | [bounded-contexts.md](./bounded-contexts.md):116, 560–580 | ✅ частично |

## 2. Что уже есть как «контракт», а что нет

| Артефакт | Что фиксирует | Это контракт? |
|---|---|---|
| ADR 0001, раздел «Маршруты» | Публичные пути → сервисы, порты | Целевой, без тел запросов/ответов |
| auth-requirements, §6.3 | anonymous / authenticated / Admin | Нормативный для политик |
| api-errors.ru.md | Ошибки: `type`, `title`, `status`, `detail`, `traceId`, `errors` | **Да**, единственный полный |
| bounded-contexts.md | `RefreshToken`, события, конверт с `correlationId` | Внутренний, не HTTP |
| Swagger Identity ([Program.cs:25](Identity/src/Otus.PaymentSystem.Identity.Host/Program.cs:25)) | Эндпоинты, DTO, коды ответов | Генерируется в рантайме, только Development |
| [roadmap.ru.md](./roadmap.ru.md):86 | Требование contract-first для UI | Требование, не контракт |

**Чего нет:**

1. Спецификации Gateway/BFF — а именно он точка входа для UI. Для Identity артефакт уже есть: [openapi/identity.v1.json](../openapi/identity.v1.json), его проверяет job `openapi-drift`.
2. Описания DTO в доках — только в коде и в сгенерированной спеке.
3. Contract-тестов на поведение (не только drift): формат проверяется unit-тестами middleware, а не схемой.

## 3. Принятые решения

| Вопрос | Решение | Почему |
|---|---|---|
| Contract-first или code-first | Code-first + закоммиченный артефакт для сервисов; contract-first для BFF (#49) | У Identity 7 эндпоинтов, писать YAML руками — дублирование. BFF нужен фронтенду для моков до готовности бэкенда |
| Генератор | Swashbuckle 7.2.0 (уже в Identity), второй не добавляем | Миграция на `Microsoft.AspNetCore.OpenApi` — отдельная задача, не совмещаем с #22 |
| Где лежит артефакт | `openapi/{service}.v1.json`, генерируется, руками не правится | Дифф виден в PR, имя файла задаёт версию |
| Как держим актуальность | CI: regenerate → `git diff --exit-code -- openapi` | Без проверки файл устаревает молча |
| Источник формата ошибок | [api-errors.ru.md](./api-errors.ru.md) | Один формат для Gateway и Identity |

## 4. План по шагам

### Шаг 0. Коммит документации — ✅ `5efd0a0`

Файлы: `docs/api-errors.ru.md`, `docs/db-schema.ru.md`, `docs/api-contracts.ru.md`, ссылка в `docs/roadmap.ru.md`.

**DoD:** ветка чистая, в `main` есть контракт ошибок и решение по хэшу. Выполнено; коммит не запушен.

### Шаг 1. #22 — ошибки по контракту — ✅

- `DomainError` вместо маппинга по тексту сообщения; «не найден» → 404.
- `Shared/src/Otus.PaymentSystem.AspNetCore`: `ApiProblems`, `CorrelationIdHeader`, `UnhandledExceptionMiddleware`, `EmptyResponseProblemMiddleware`.
- Gateway: 401/403 (JwtBearer), 404 (маршрут), 502/504 (YARP), 500; ответы Identity не перезаписываются.
- Тесты: 400/401/403/404/409/500, `traceId == X-Correlation-Id`, 500 без stack trace вне Development.

**DoD:** прямой вызов Identity и вызов через Gateway дают одинаковый JSON.

### Шаг 2. Артефакт OpenAPI — ✅ (`openapi/identity.v1.json`)

1. `IncludeXmlComments` + `<GenerateDocumentationFile>` в Identity.Host.
2. Стабильные `OperationId` (из `WithName`), полный набор `ProducesProblem`/`ProducesValidationProblem`, Bearer security scheme, `servers` = адрес Gateway, флаг `Swagger:Enabled` для запуска без Development.
3. Генерация — запуск Host и снятие `/swagger/v1/swagger.json`:
   ```powershell
   $env:ASPNETCORE_ENVIRONMENT='Production'
   $env:Swagger__Enabled='true'
   $env:Jwt__PrivateKeyPem='<PEM приватного ключа RS256>'   # в Production Identity без ключа не стартует
   $env:ConnectionStrings__IdentityDb='Host=localhost;Port=5433;Database=otus_identity;Username=otus;Password=otus'

   dotnet run --project Identity/src/Otus.PaymentSystem.Identity.Host --no-launch-profile --urls http://127.0.0.1:5100

   curl.exe -sS -f http://127.0.0.1:5100/swagger/v1/swagger.json -o openapi/identity.v1.json
   ```

   С 2026-11-04 Identity подписывает токены RS256 и публикует `/.well-known/jwks.json`; симметричный `Jwt:Secret` удалён. Для самой спеки ключ не нужен, но Host в Production без `Jwt:PrivateKeyPem` не стартует. В CI ключ генерируется на время джоба (`openssl genpkey`), локально проще запускать в `Development` — там ключ создаётся эфемерно.
   Swashbuckle CLI (`dotnet swagger tofile`) не используется: установка инструмента требует nuget.org, недоступного в среде разработки.
4. CI: job `openapi-drift` повторяет те же шаги и падает на `git diff --exit-code -- openapi`.

**DoD:** файл в репозитории, CI краснеет при изменении DTO без перегенерации.

### Шаг 3. #21 — валидация DTO — ✅

Единый `ValidationFilter<T>` в `Shared.AspNetCore`, правила в валидаторах, `errors` в формате api-errors, тесты валидаторов.

**DoD:** 400 с `errors` по полям, схема в спеке обновлена.

### Шаг 4. #48 — refresh/JWKS/rate limiting

Новые эндпоинты `POST /auth/refresh`, `POST /auth/logout` и 429 входят в спеку; после этого спека Identity считается полной на M1.

### Шаг 5. #49 — BFF contract-first

Спека `/bff/me`, `/bff/health` пишется до кода, фронтенд получает моки. Только здесь появляется спека Gateway.

## 5. Артефакт OpenAPI: как устроен

**Файлы:** `Identity/src/Otus.PaymentSystem.Identity.Host/Program.cs`, `.../Endpoints/*.cs`, `.config/dotnet-tools.json`, `.github/workflows/ci.yml`, `openapi/identity.v1.json`.

**Проверка руками:**

```powershell
dotnet run --project Identity/src/Otus.PaymentSystem.Identity.Host
# http://localhost:5100/swagger/v1/swagger.json — глазами: есть ProblemDetails, Bearer, все коды
```

**Типичные ошибки:**

- `Produces<ProblemDetails>` и `Results.ValidationProblem` дают разные схемы → в спеке два разных формата ошибок;
- `servers` указывает на Identity, а клиент ходит через Gateway → сгенерированный клиент ходит мимо edge;
- нестабильные `OperationId` (`Register` vs `RegisterAsync`) → ломается кодогенерация при переименовании метода;
- сгенерированный файл правят руками → следующий regenerate затирает правки.

## 6. Риски

| Риск | Мера |
|---|---|
| Спека устаревает | drift-check в CI, обязательный regenerate в PR с изменением DTO |
| Расхождение формата ошибок между Gateway и Identity | общий `ApiProblems` + тест на одинаковый JSON |
| Contract-first расползается на все сервисы | только BFF (#49); сервисы — code-first + артефакт |
| Версионирование вводят слишком рано | до 1.0 версия только в имени файла, не в пути |

## 7. Открытые вопросы

1. Версионирование API: версия в пути (`/api/v1/...`) или только в артефакте — решить до #49.
2. Публиковать ли спеку через Gateway (`/openapi/identity.v1.json`) или отдавать файл из репозитория — решить в #49.
3. Нужен ли `instance` в ProblemDetails — сейчас нет, `traceId` закрывает потребность поддержки (см. api-errors §8).
