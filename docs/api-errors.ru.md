# Единый формат ошибок API (ProblemDetails)

> **Статус:** контракт зафиксирован 31.10.2026, реализация — [#22](https://github.com/PoyarkinES/otus_payment_system/issues/22) в ветке `feature/m1-w3-problemdetails`.
> **Связанные:** [#22](https://github.com/PoyarkinES/otus_payment_system/issues/22) · [#21](https://github.com/PoyarkinES/otus_payment_system/issues/21) (валидация DTO) · [#48](https://github.com/PoyarkinES/otus_payment_system/issues/48) (refresh/JWKS/rate limiting) · [ADR 0001](./adr/0001-api-gateway.md)

## 1. Зачем

Ошибка должна выглядеть одинаково при прямом вызове Identity и через Gateway. Фронтенд разбирает один формат, а поддержка находит запрос по `traceId`. Контракт — часть Definition of Done недели M1W3, поэтому он фиксируется до кода и не меняется в каждом сервисе по-своему.

## 2. Контракт

Все ошибки отдаются с `Content-Type: application/problem+json; charset=utf-8` и телом по RFC 9457.

| Поле | Тип | Обязательно | Смысл |
|---|---|---|---|
| `type` | string (URI) | да | `https://httpstatuses.io/{status}` |
| `title` | string | да | Стандартная причина статуса (`Bad Request`, `Unauthorized`, …) |
| `status` | int | да | HTTP-статус, совпадает с кодом ответа |
| `detail` | string | да, кроме 404 от маршрутизации | Понятное человеку пояснение на русском, без stack trace и секретов |
| `traceId` | string | да | Correlation ID запроса (см. раздел 4) |
| `errors` | object<string, string[]> | только 400 (валидация) | Ошибки по полям, ключи — camelCase |

`detail` для 500 в production — общий текст («Внутренняя ошибка сервера.»), текст исключения допускается только в Development.

### 2.1. 400 — ошибка валидации

```json
{
  "type": "https://httpstatuses.io/400",
  "title": "Bad Request",
  "status": 400,
  "detail": "Запрос содержит некорректные поля.",
  "traceId": "4f1c2a9b8e7d4c6f9a0b1c2d3e4f5a6b",
  "errors": {
    "email": ["Некорректный формат email."],
    "password": ["Пароль должен быть не короче 8 символов."]
  }
}
```

### 2.2. 401 — не аутентифицирован

```json
{
  "type": "https://httpstatuses.io/401",
  "title": "Unauthorized",
  "status": 401,
  "detail": "Неверные учётные данные.",
  "traceId": "4f1c2a9b8e7d4c6f9a0b1c2d3e4f5a6b"
}
```

### 2.3. 404 и 409

```json
{
  "type": "https://httpstatuses.io/404",
  "title": "Not Found",
  "status": 404,
  "detail": "Пользователь не найден.",
  "traceId": "4f1c2a9b8e7d4c6f9a0b1c2d3e4f5a6b"
}
```

```json
{
  "type": "https://httpstatuses.io/409",
  "title": "Conflict",
  "status": 409,
  "detail": "Email уже занят.",
  "traceId": "4f1c2a9b8e7d4c6f9a0b1c2d3e4f5a6b"
}
```

### 2.4. 500 — необработанная ошибка

```json
{
  "type": "https://httpstatuses.io/500",
  "title": "Internal Server Error",
  "status": 500,
  "detail": "Внутренняя ошибка сервера.",
  "traceId": "4f1c2a9b8e7d4c6f9a0b1c2d3e4f5a6b"
}
```

## 3. Маппинг ситуаций на статусы

| Ситуация | Статус | Где возникает |
|---|---|---|
| FluentValidation: пустой email, слабый пароль, слишком длинное поле | 400 + `errors` | Identity, позже Gateway (#21) |
| Доменное правило ввода (`DomainError.Validation`) | 400 | Identity |
| Нет токена, просрочен, неверная подпись | 401 | Gateway, Identity (JwtBearer challenge) |
| Неверные учётные данные (`DomainError.Unauthorized`) | 401 | Identity |
| Нет роли/прав, заблокированный пользователь (`DomainError.Forbidden`) | 403 | Gateway, Identity |
| Пользователь/ресурс не найден (`DomainError.NotFound`) | 404 | Identity |
| Неизвестный маршрут | 404 | Gateway, Identity |
| Конфликт: email занят, повторная блокировка (`DomainError.Conflict`) | 409 | Identity |
| Необработанное исключение | 500 | Gateway, Identity |
| Rate limit login и refresh (#48) | 429 + `Retry-After` | Gateway (политики `login` — 5/мин, `refresh` — 30/мин, по IP) |
| Identity недоступен через YARP | 502/504 | Gateway |

Правило: `401` — «кто ты», `403` — «я тебя знаю, но нельзя», `400` — «данные неверны», `409` — «состояние не позволяет».

## 4. `traceId` и correlation ID

- Заголовок — `X-Correlation-Id`, одно имя на все сервисы.
- Gateway генерирует ID, если клиент его не прислал, и возвращает в ответе; Identity принимает ID от Gateway и не перегенерирует.
- `traceId` в ProblemDetails совпадает с `X-Correlation-Id` этого же ответа: клиент может прислать его в поддержку, а поддержка найдёт запись в логах.
- Fallback, если заголовка нет: `Activity.Current.Id`, затем `HttpContext.TraceIdentifier`. Пустым `traceId` не бывает.

## 5. Где живёт код

`Shared/src/Otus.PaymentSystem.AspNetCore/` — общая библиотека для Host-проектов:

- `Problems/ApiProblems` — создание и запись ProblemDetails, `Validation` с `errors`;
- `Problems/CorrelationIdHeader` — имя заголовка и чтение `traceId`;
- `Middleware/UnhandledExceptionMiddleware` — 500 без stack trace вне Development;
- `Middleware/EmptyResponseProblemMiddleware` — заполняет пустые 4xx/5xx (challenge от JwtBearer, 404 маршрутизации, 502/504 YARP).

**Почему не `SharedKernel`:** на `SharedKernel` ссылается каждый `*.Domain`, а библиотеке нужен `FrameworkReference Microsoft.AspNetCore.App`. Положив ProblemDetails в `SharedKernel`, мы протащили бы ASP.NET Core в Domain. Поэтому web-примитивы — отдельный проект, который ссылают только Host-проекты. Структуру `Shared/` в README обновляем, когда проект появится в коде, а не до этого.

**Проверено 02.11.2026** (ветка `feature/m1-w3-problemdetails`, коммит `5efd0a0`):

- явный `FrameworkReference Microsoft.AspNetCore.App` — только в двух тестовых проектах; Host-проекты используют `Microsoft.NET.Sdk.Web` и получают shared framework неявно;
- все `*.Domain` ссылаются только на `SharedKernel`; `SharedKernel.csproj` пустой, без `FrameworkReference`;
- `Identity.Infrastructure` получает `Microsoft.AspNetCore.App` транзитивно через пакет `Microsoft.AspNetCore.Authentication.JwtBearer` (`frameworkReferences` в `obj/project.assets.json`). Правило это не нарушает: Domain от Infrastructure не зависит. Кандидат в техдолг — вынести JWT из Infrastructure; отдельный issue, не #22;
- `Identity.Domain`, `Identity.Application`, `SharedKernel` — в `project.assets.json` упоминаний `Microsoft.AspNetCore.App` нет.

## 6. Правила

1. Формат один для Gateway и Identity: одинаковые поля, `type`, `title`, camelCase в `errors`.
2. `detail` не содержит stack trace, SQL, имён таблиц, токенов и паролей.
3. Gateway не перезаписывает ProblemDetails, пришедший от Identity: заполняются только ответы с пустым телом.
4. 401 и 403 различимы и по статусу, и по `detail`.
5. Каждый эндпоинт объявляет свои ошибки в OpenAPI: `ProducesProblem(status)` и `ProducesValidationProblem()`.
6. Новые доменные ошибки не маппятся по тексту сообщения: у `DomainException` есть код `DomainError`, маппинг — по коду.

## 7. Тесты (DoD #22)

**Статус на 02.11.2026:** автоматические тесты зелёные (Shared 14, Identity.Host 13, Gateway 8); smoke Gateway/Identity — 400/401/404 в одном формате.

- [x] 400: невалидный DTO → `errors` по полям в camelCase.
- [x] 401: нет токена / неверные креды.
- [x] 403: нет роли, заблокированный пользователь.
- [x] 404: неизвестный маршрут и «не найден» из домена.
- [x] 409: email занят, повторная блокировка.
- [x] 500: необработанное исключение, `detail` без stack trace.
- [x] Shared: `errors` не теряются (сериализация по runtime-типу), `Response.HasStarted` → no-op.
- [x] Identity: статус выбирается по `DomainError`, а не по тексту сообщения.
- [x] `traceId` присутствует и совпадает с `X-Correlation-Id`.
- [x] Ответ через Gateway и напрямую из Identity совпадает по формату.

## 8. Открытые вопросы

1. Формат 429 (`Retry-After`, тело ProblemDetails) — фиксируется в #48 вместе с rate limiting.
2. Нужна ли локализация `title`/`detail` (сейчас `title` — стандартный английский, `detail` — русский) — решается при появлении Web UI.
3. Нужен ли `instance` (URI конкретного запроса) — в контракт не входит: `traceId` закрывает потребность поддержки.
