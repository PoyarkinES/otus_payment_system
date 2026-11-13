# ADR 0005. JWT-валидация в сервисах через JWKS Identity

- **Статус:** Accepted
- **Дата:** 2026-11-13
- **Контекст:** Wallets и будущие сервисы должны валидировать JWT так же, как Identity/Gateway
- **Связанные документы:** [shared-nuget.ru.md](../shared-nuget.ru.md), [wallets-entity-interactions.ru.md](../wallets-entity-interactions.ru.md), [auth-requirements.ru.md](../auth-requirements.ru.md)

## Контекст

По [auth-requirements.ru.md](../auth-requirements.ru.md) `7.6` и `13 C` каждый защищённый endpoint должен валидировать issuer/audience/signature/lifetime. Identity выдаёт access-токены RS256 и публикует публичный ключ в `/.well-known/jwks.json`. Gateway уже валидирует токены по JWKS локально.

Для Wallets и следующих сервисов нужно то же самое, но:

- Identity **не** публикует `/.well-known/openid-configuration`, поэтому OIDC-discovery по `Authority` не работает;
- копировать `JwksKeyStore` в каждый сервис — дублирование;
- нельзя просто доверять `X-User-Id`: это нарушает auth-requirements и позволяет подмену при прямом доступе к сервису.

## Решение

1. Каждый сервис валидирует JWT **сам**: issuer, audience, signature, lifetime, алгоритм RS256.
2. Используется `Microsoft.AspNetCore.Authentication.JwtBearer` + кэш JWKS, без OIDC-discovery.
3. JWKS-кэш:
   - загружает `/.well-known/jwks.json` Identity;
   - TTL — 10 минут;
   - при неизвестном `kid` немедленно обновляет кэш;
   - при старте делает попытку загрузки и логирует warning, если Identity недоступен.
4. `IssuerSigningKeyResolver` возвращает ключи из кэша; `MapInboundClaims=false`, `ValidAlgorithms=["RS256"]`, `ClockSkew=30s`.
5. Конфигурация — секция `Jwt`: `Issuer`, `Audience`, `JwksUri`. Локально `JwksUri` указывает на `http://localhost:5100/.well-known/jwks.json`, в compose — на `http://identity:8080/.well-known/jwks.json`.
6. `userId` берётся из claim `sub`. `X-User-Id` — только для внутренних вызовов, публичные endpoint'ы ему не доверяют.
7. Общий код (`JwksKeyStore`, `AddJwtBearerFromJwks`) выносится в `Shared.AspNetCore`; до выноса Wallets может использовать копию из Gateway.Host.

## Рассмотренные альтернативы

| Вариант | Почему нет |
|---|---|
| `ConfigurationManager<OpenIdConnectConfiguration>` + `Authority` | Нужен `/.well-known/openid-configuration`, которого у Identity нет |
| Статический публичный ключ в конфиге | Не переживает ротацию ключа и рестарт Identity в Development |
| Доверять `X-User-Id` от Gateway | Нарушает auth-requirements; при прямом доступе к сервису header можно подделать |
| Свой JWT-парсер в каждом сервисе | Дублирование и риск ошибок; используем проверенный JwtBearer |

## Последствия

**Плюсы**

- Единый механизм для всех сервисов.
- Ротация ключей через JWKS без редеплоя.
- Нет OIDC-discovery, которого нет в Identity.
- Можно вынести в Shared.AspNetCore и переиспользовать.

**Минусы / риски**

- Нужен сетевой доступ к Identity/JWKS при холодном старте; кэш смягчает.
- В Development Identity генерирует эфемерный ключ: после рестарта старые токены невалидны, JWKS нужно обновить (unknown `kid` делает это автоматически).
- `X-User-Id` всё ещё доверяется внутри сети; service-to-service auth — отдельный долг.

**Меры**

- Кэш 10 минут + refresh по unknown `kid`.
- Health/логи при недоступном JWKS.
- Тесты с фейковым JWKS/ключами.
- Не логировать токены.

## План внедрения

| Шаг | Что |
|---|---|
| 1 | Вынести `JwksKeyStore`/`AddJwtBearerFromJwks` в `Shared.AspNetCore` (или скопировать в Wallets.Host) |
| 2 | Wallets.Host: `JwtOptions` + `JwksUri` + JwtBearer |
| 3 | Wallets: `sub` в handler'ах, ownership |
| 4 | Будущие Hosts (Payments/Fraud/Notifications/Audit) используют тот же helper |
| 5 | Gateway при желании переводится на общий helper |

## Ссылки

- [auth-requirements.ru.md](../auth-requirements.ru.md)
- [wallets-entity-interactions.ru.md](../wallets-entity-interactions.ru.md)
- [project-guide.ru.md](../project-guide.ru.md)
- [Gateway JwksKeyStore](../../Gateway/src/Otus.PaymentSystem.Gateway.Host/Security/JwksKeyStore.cs)
