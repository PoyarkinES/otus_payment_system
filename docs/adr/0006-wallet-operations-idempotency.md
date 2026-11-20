# ADR 0006. Deposit/withdraw: идемпотентность и конкурентность

- **Статус:** Accepted
- **Дата:** 2026-11-17
- **Контекст:** #51 (M2W6), денежные операции Wallets
- **Связанные документы:** [wallets-entity-interactions.ru.md](../wallets-entity-interactions.ru.md), [db-schema.ru.md](../db-schema.ru.md), [auth-requirements.ru.md](../auth-requirements.ru.md), [testing-strategy.ru.md](../testing-strategy.ru.md)

## Контекст

В агрегате Wallet уже есть методы Deposit и Withdraw: они меняют Available, пишут WalletTransaction и увеличивают Version. Не хватает Application-команд, HTTP-эндпоинтов, идемпотентности и обработки конкурентных изменений.

Денежная операция отличается от create/get:

- клиент может повторить HTTP-запрос после таймаута;
- два запроса могут одновременно менять один кошелёк;
- повтор не должен создать вторую операцию в ledger;
- при конфликте Version нельзя терять обновление;
- баланс и ledger должны сохраняться атомарно.

## Решение

### 1. API

~~~http
POST /wallets/{walletId}/deposit
Idempotency-Key: <uuid>
Content-Type: application/json

{ "amount": 1000.0000 }
~~~

~~~http
POST /wallets/{walletId}/withdraw
Idempotency-Key: <uuid>
Content-Type: application/json

{ "amount": 100.0000 }
~~~

Оба endpoint:

- требуют JWT;
- берут userId из claim sub;
- проверяют ownership: чужой кошелёк → 403, отсутствующий → 404;
- возвращают 200 и WalletResult с актуальными Available, Held, Total, Version;
- проходят через существующий Gateway-маршрут /api/wallets/**; новый маршрут не нужен.

### 2. Idempotency-Key

- Заголовок обязателен. Формат MVP — UUID.
- Ключ хранится в WalletTransaction.ReferenceId.
- Уникальность: filtered unique index по (WalletId, ReferenceId), где ReferenceId IS NOT NULL.
- Ключ scoped per wallet: один и тот же UUID в разных кошельках — разные операции.
- Handler перед изменением проверяет, есть ли уже транзакция с этим ключом:
  - есть и совпадает тип/сумма — возвращает текущий WalletResult, второй ledger не создаётся;
  - есть, но тип/сумма другие — 409;
  - нет — выполняет операцию.
- Гонка двух одинаковых запросов: один SaveChanges побеждает, второй получает unique violation; handler очищает ChangeTracker, перечитывает кошелёк и возвращает существующую операцию, а не 500/409.
- Idempotency-Key не является transferId. Для hold/confirm/release в #52 остаётся отдельный TransferId.

Если позже понадобятся произвольные строковые ключи, добавляется отдельная колонка IdempotencyKey string; текущее решение использует ReferenceId как UUID.

### 3. Транзакционная граница

- Один SaveChangesAsync на команду.
- Wallet и WalletTransaction сохраняются в одной транзакции EF.
- Отдельный IUnitOfWork и отдельный репозиторий транзакций не вводятся: один агрегат — один репозиторий.
- Ledger пишется только методами Wallet.Deposit/Withdraw.

### 4. Конкурентность

- Version уже является concurrency token.
- Concurrent update ловится как DbUpdateConcurrencyException.
- Поведение MVP: вернуть 409 Conflict с ProblemDetails; клиент может повторить запрос с тем же Idempotency-Key.
- Потерянных обновлений быть не должно: второй писатель не перезаписывает Version и баланс.
- Автоматический retry внутри handler не делается в #51; это усложняет семантику insufficient funds и idempotency.

### 5. Деньги и валидация

- Сумма > 0.
- Не более 4 знаков после запятой.
- Валюта операции должна совпадать с валютой кошелька.
- В MVP поддерживается только RUB.
- Знак операции задаётся типом WalletTransaction, а не отрицательной суммой.
- Ошибки домена маппятся в ProblemDetails: Validation → 400, NotFound → 404, Conflict → 409, Forbidden → 403.

### 6. Ошибки идемпотентности

| Ситуация | Ответ |
|---|---|
| Ключ отсутствует или не UUID | 400 |
| Кошелёк не найден | 404 |
| Чужой кошелёк | 403 |
| Недостаточно средств | 409 |
| Тот же ключ, те же тип/сумма | 200, повторного ledger нет |
| Тот же ключ, другие тип/сумма | 409 |
| Конкурентное изменение Version | 409, без потери обновления |

## Рассмотренные альтернативы

| Вариант | Почему нет |
|---|---|
| Без idempotency | Retry после таймаута создаёт вторую операцию |
| Отдельная таблица IdempotencyKeys | Больше инфраструктуры для MVP; можно добавить позже |
| Redis для ключей | Внешняя зависимость и отдельная согласованность с БД |
| Строковый IdempotencyKey в новой колонке | Гибче, но требует дополнительной миграции; отложено |
| Автоматический retry при concurrency | Усложняет поведение при недостатке средств; 409 проще и честнее |

## Последствия

**Плюсы**

- Retry безопасен.
- Ledger не дублируется.
- Нет потерянных обновлений.
- Одно понятное правило хранения ключа.

**Минусы / риски**

- Ключ ограничен UUID.
- При конфликте клиент должен повторить запрос.
- ReferenceId теперь несёт два смысла: ссылка на операцию и idempotency key. Это допустимо для deposit/withdraw, но нужно явно документировать.
- Уникальный индекс нужно добавить миграцией.

## План реализации

| Шаг | Что |
|---|---|
| 1 | DTO WalletOperationRequest, команды DepositWalletCommand/WithdrawWalletCommand |
| 2 | Handlers: ownership, idempotency check, вызов Wallet.Deposit/Withdraw, SaveChanges |
| 3 | Endpoint'ы deposit/withdraw и Idempotency-Key |
| 4 | Unique index (WalletId, ReferenceId), обработка unique/concurrency |
| 5 | Тесты: handler, Host HTTP, Postgres, Gateway E2E |
| 6 | Обновить wallets-entity-interactions, db-schema, testing-strategy |

## Вне scope #51

- hold/confirm/release — #52;
- произвольные строковые idempotency keys;
- автоматический retry/backoff;
- распределённая идемпотентность между сервисами;
- CHECK-ограничения, если они не добавлены отдельной миграцией #17.

## Ссылки

- [wallets-entity-interactions.ru.md](../wallets-entity-interactions.ru.md)
- [db-schema.ru.md](../db-schema.ru.md)
- [auth-requirements.ru.md](../auth-requirements.ru.md)
- [testing-strategy.ru.md](../testing-strategy.ru.md)
- [issue #51](https://github.com/PoyarkinES/otus_payment_system/issues/51)
