# ADR 0003. gRPC для внутреннего KYC API

- **Статус:** Proposed
- **Дата:** 2026-10-30
- **Контекст:** post-1.0, M7 «KYC/Onboarding»
- **Связанные задачи:** #73 (эпик M7), #76 (W27)

## Контекст

KYC-статус нужен внутри системы:

- Payments — проверить порог перевода/лимиты;
- Identity/Onboarding — показать состояние верификации;
- Notifications — отправить уведомление о смене статуса.

Фронтенду нужен REST для загрузки файла и получения статуса. Внутренним сервисам — typed-контракт без «JSON-договорённостей».

## Решение

1. **Публичный/edge-контракт — REST через Gateway:**
   - `POST /api/kyc/documents` (multipart upload);
   - `GET /api/kyc/status`;
   - `GET /api/kyc/documents/{id}` (метаданные, не файл).
2. **Внутренний контракт — gRPC:**
   - `GetKycStatus(userId)` → `status`, `level`, `updatedAt`;
   - опционально `WatchKycStatus(userId)` — server streaming для UI/сервисов;
   - `.proto` — источник истины, версионируется.
3. **gRPC не публикуется наружу.** Браузер не ходит в gRPC напрямую; при необходимости позже — gRPC-Web, но не в MVP.
4. **Вызовы из Payments:**
   - перед переводом выше порога;
   - с deadline (например, 500 мс), retry только на идемпотентный `Get`, circuit breaker;
   - feature flag: при выключенном KYC поведение 1.0 не меняется.
5. **Ошибки:** `NotFound` → KYC не начат; `FailedPrecondition` → `RequiresAction`; `Unavailable` → fallback-политика (например, не блокировать мелкие переводы, блокировать крупные — решается в Payments).
6. **Безопасность:** внутренняя сеть, позже mTLS/внутренний JWT. Сервис-сервис аутентификация — известное ограничение MVP (см. architecture.ru.md).

## Почему gRPC, а не REST внутри

| Критерий | gRPC | REST |
|---|---|---|
| Typed-контракт | `.proto`, кодогенерация | OpenAPI + ручные DTO |
| Размер/скорость | Бинарный protobuf, HTTP/2 | JSON, HTTP/1.1 |
| Streaming | Есть | Нет из коробки |
| Браузер | Нужен gRPC-Web | Работает напрямую |
| Отладка | Сложнее | Проще (curl) |
| Экосистема .NET | `Grpc.AspNetCore`, `Grpc.Net.Client` | ASP.NET Core minimal API |

Для внутреннего `GetKycStatus` gRPC даёт строгий контракт и низкую задержку. Для загрузки файла с фронтенда REST удобнее.

## Альтернативы

| Вариант | Почему нет |
|---|---|
| **REST внутри** | Проще, но слабее контракт; для streaming статуса придётся SSE/WebSocket |
| **События RabbitMQ** | Хорошо для асинхронных уведомлений, но Payments нужен синхронный ответ перед переводом |
| **GraphQL** | Лишний слой для service-to-service; нет выгоды |
| **gRPC для фронтенда** | Нужен gRPC-Web/прокси, усложняет Gateway и отладку |

## Последствия

**Плюсы**

- Строгий контракт между KYC и потребителями.
- Кодогенерация клиентов на C#.
- Streaming для `WatchKycStatus` при необходимости.
- Единый стиль внутренних вызовов (позже можно перевести Fraud/Identity ACL).

**Минусы**

- Protobuf toolchain и версионирование.
- HTTP/2, TLS, health checks.
- Сложнее отлаживать, чем JSON.
- Дублирование контрактов: REST для edge, gRPC для internal.

**Меры**

- `.proto` в отдельном проекте `Kyc.Contracts` (или `Kyc.Grpc`), версия в пакете.
- Contract-тесты: совместимость полей, `optional`, reserved.
- Deadline/retry/circuit breaker на клиентах.
- Feature flag в Payments.

## План внедрения

| Шаг | Когда | Результат |
|---|---|---|
| `.proto` + каркас gRPC-сервера | M7W25 | контракт зафиксирован |
| `GetKycStatus` + health/reflection | M7W27 | KYC отвечает по gRPC |
| Клиент в Payments + feature flag | M7W27 | порог работает |
| `WatchKycStatus` (опционально) | M7W27–W29 | streaming, если успеваем |
| Contract/integration tests | M7W28 | совместимость и ошибки покрыты |

## Что этот ADR не решает

- Публичный gRPC для внешних клиентов.
- mTLS/внутренний JWT — отдельное решение.
- Замену REST в других сервисах.
- API gateway для gRPC (Envoy/Kong) — не в MVP.

## Ссылки

- [Roadmap](../roadmap.ru.md)
- [Architecture](../architecture.ru.md)
- [ADR 0002 — KYC storage](./0002-kyc-storage.md)
