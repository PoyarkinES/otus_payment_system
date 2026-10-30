# ADR 0001. API Gateway / BFF на YARP — отдельный edge-слой

- **Статус:** Proposed
- **Дата:** 2026-10-30
- **Контекст:** M1 (ноябрь 2026), до начала Wallets и frontend-интеграции
- **Связанные задачи:** #40 (эпик M1), #46 (M1W1), #49 (M1W4)

## Контекст

В проекте уже есть Identity с JWT, но нет единой точки входа. Если фронтенд начнёт ходить напрямую в каждый сервис, придётся:

- дублировать CORS, валидацию JWT и обработку ошибок в каждом сервисе;
- знать фронтенду внутренние адреса и порты;
- агрегировать данные (профиль + кошельки + переводы) на клиенте;
- менять фронтенд при каждом переименовании внутреннего endpoint.

В старом roadmap был «платёжный шлюз» (Payment Gateway) в месяце 6, но это другое: merchant acquiring для магазинов. **API Gateway** — это edge-слой между клиентом и сервисами, и он нужен уже в месяце 1.

> ADR согласован с [architecture.ru.md](../architecture.ru.md) (PR #37): Gateway валидирует JWT локально, добавляет `X-User-Id`, а Payments использует hold/confirm/release и Redis-кэш получателя.

## Решение

1. Ввести **API Gateway / BFF** как отдельный инфраструктурный сервис `Gateway/`, а **не** bounded context.
2. Реализовать его на **YARP** (Yet Another Reverse Proxy) в ASP.NET Core: маршрутизация + BFF-эндпоинты.
3. Ввести Gateway в **M1 (ноябрь 2026)**, до Wallets и до полноценного frontend UI.
4. Оставить **Payment Gateway** (мерчанты, API keys, webhooks, refunds) бизнес-возможностью Payments в **M6**. Это два разных «шлюза», их нельзя смешивать.
5. Gateway отвечает только за edge-задачи:
   - маршрутизация `/api/identity/**`, `/api/wallets/**`, `/api/transfers/**`, `/api/fraud/**`, `/api/notifications/**`, `/api/audit/**`, `/api/payments/**` (merchant, M6);
   - локальная валидация JWT по публичному ключу/JWKS Identity (issuer/audience/signature/lifetime);
   - добавление `X-User-Id` из `sub` для внутренних вызовов; `Authorization` пробрасывается как есть;
   - CORS для фронтенда;
   - rate limiting на edge;
   - correlation ID (`X-Correlation-Id`);
   - BFF-агрегация (`/bff/me`, `/bff/wallets`, `/bff/transfers`) — только read-only представления;
   - health-агрегация и ссылки на OpenAPI сервисов.
6. Бизнес-логика в Gateway **не живёт**. Если для ответа нужны данные нескольких сервисов, Gateway их только агрегирует. Hold/confirm/release, Fraud-вердикт и балансы — не в Gateway.

## Маршруты (целевой контракт)

| Публичный путь | Куда | Примечание |
|---|---|---|
| `/api/identity/**` | Identity Host | path transform: `/api/identity/auth/login` → `/auth/login` |
| `/api/wallets/**` | Wallets Host | JWT + ownership проверяется в Wallets |
| `/api/transfers/**` | Payments Host | переводы wallet-to-wallet |
| `/api/payments/**` | Payments Host | merchant payments (M6) |
| `/api/fraud/**` | Fraud Host | service-to-service и admin |
| `/api/notifications/**` | Notifications Host | admin/read-only |
| `/api/audit/**` | Audit Host | admin/read-only |
| `/bff/me` | Gateway | профиль + роли |
| `/bff/wallets` | Gateway | список кошельков + балансы |
| `/bff/transfers` | Gateway | история переводов |
| `/bff/health` | Gateway | агрегированный health |

Порт Gateway для локальной разработки — `8080`; фронтенд — `http://localhost:5173`.

## Рассмотренные альтернативы

| Вариант | Почему нет |
|---|---|
| **Ocelot** | Меньше активности, слабее поддержка .NET 10 и BFF-сценариев; YARP ближе к ASP.NET Core. |
| **nginx / Kong / Traefik** | Хороши как ingress, но требуют отдельного контейнера и не дают BFF на C#; для учебного проекта лишняя инфраструктура. |
| **Прямые вызовы сервисов из фронтенда** | Дублирование auth/CORS/ошибок, жёсткая связность, сложная агрегация. |
| **Service mesh (Istio/Linkerd)** | Избыточно для 6 сервисов и одного разработчика; не решает BFF-агрегацию. |

## Последствия

**Плюсы**

- Один URL для фронтенда и внешних клиентов.
- JWT, CORS, ошибки, correlation ID и rate limiting — в одном месте.
- BFF скрывает внутреннюю топологию сервисов.
- Проще добавлять новые сервисы: фронтенд не меняется.

**Минусы / риски**

- Gateway — единая точка отказа; нужны healthchecks и resilience.
- Появляется сетельной hop; latency нужно измерять.
- Нужно синхронизировать JWT-настройки: Identity публикует JWKS, Gateway валидирует по публичному ключу.
- Gateway — доверенный источник `X-User-Id`; в MVP внутренняя сеть, в production — mTLS/подписанный внутренний JWT.
- Нельзя тащить бизнес-логику в BFF, иначе он станет «распределённым монолитом».

**Меры**

- JWT: Identity выдаёт RSA-ключ и JWKS; Gateway кэширует публичный ключ. Симметричный секрет — только как временный dev-режим до W3.
- `X-User-Id` принимается только от Gateway; внешние запросы с этим заголовком отбрасываются.
- Healthchecks и таймауты на маршрутах.
- Контрактные тесты на маршруты, 401/403 и подмену `X-User-Id`.
- Явное правило: BFF только агрегирует, не принимает бизнес-решений.

## План внедрения

| Шаг | Когда | Результат |
|---|---|---|
| ADR + каркас `Gateway.Host` | M1W1 (#46) | сервис собирается, `/health` 200 |
| Маршруты Identity + JWT + CORS + `X-User-Id` | M1W1 (#46) | login/me через Gateway |
| RSA/JWKS Identity, переключение Gateway | M1W3 (#48) | локальная валидация по публичному ключу |
| BFF `/bff/me` + health-агрегация | M1W4 (#49) | фронтенд ходит в одну точку |
| Маршруты Wallets/Transfers | M2–M3 | новые сервисы без изменения фронтенда |
| Rate limiting и наблюдаемость | M1W3 / M4W16 | защита edge и трассировка |

## Что этот ADR не решает

- Выбор frontend-стека (React/Blazor) — отдельное решение post-1.0.
- Service discovery (Consul/K8s) — пока конфиг YARP.
- mTLS между сервисами — позже, при выходе за Docker Compose.
- Замену Identity на внешний IdP.

## Ссылки

- [Roadmap](../roadmap.ru.md)
- [Архитектура](../architecture.ru.md)
- [Bounded contexts](../bounded-contexts.md)
- YARP: <https://microsoft.github.io/reverse-proxy/>
