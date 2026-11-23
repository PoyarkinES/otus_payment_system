# ADR 0007. Hold/confirm/release: получатель в холде, идемпотентность и границы транзакции

- **Статус:** Accepted
- **Дата:** 2026-11-21
- **Контекст:** #52 (M2W7), ветка `feature/52-wallet-holds`
- **Связанные документы:** [architecture.ru.md](../architecture.ru.md) · [wallets-entity-interactions.ru.md](../wallets-entity-interactions.ru.md) · [db-schema.ru.md](../db-schema.ru.md) · [ADR 0006](./0006-wallet-operations-idempotency.md) · [testing-strategy.ru.md](../testing-strategy.ru.md)

## Контекст

В домене Wallets уже есть `PlaceHold`, `ReleaseHold`, `ConfirmHold` и `ReceiveTransfer`,
но Application-команд, endpoint'ов и оркестрации confirm нет. Задача #52 добавляет
hold / confirm / release, историю операций и BFF-сводку.

Ключевой вопрос — откуда `confirm` узнаёт получателя. `Hold` хранит только `UserId`
плательщика. Payments в M3 знает `toUserId`, но сейчас Payments — каркас (только
доменные события), и синхронного контракта Wallets ↔ Payments ещё нет.

`architecture.ru.md` фиксирует два требования, которые определяют решение:

1. Контракт вызова — `confirm(holdId)`, без тела (§3.2, шаг 8).
2. Сценарий потерянного ответа: повтор идёт по тому же `holdId`, и Wallets не должен
   зачислить дважды (§3.3, статус `Confirming`).

Дополнительные ограничения: один `SaveChanges` на команду, `Version` как concurrency
token, ownership по claim `sub`, только RUB, суммы `numeric(18,4)`, `confirm` после
`release` и `release` после `confirm` — 409.

## Решение

### 1. Получатель хранится в холде

`Hold` получает обязательное поле `RecipientUserId` (uuid, NOT NULL). Оно заполняется
в момент `hold` и дальше не меняется. `confirm` читает получателя из холда и не
принимает его в запросе.

Следствия:

- миграция `AddHoldRecipient`: колонка NOT NULL, default `Guid.Empty` для непустых
  dev-баз; старые холды с пустым получателем подтвердить нельзя — только release
  или пересоздание;
- `Wallet.PlaceHold` принимает `recipientUserId` и проверяет его на непустоту и на
  отличие от плательщика;
- `HoldResult` отдаёт `RecipientUserId` наружу;
- в M3 Payments передаёт `toUserId` в `hold`, а `confirm` остаётся `confirm(holdId)`.

### 2. API

| Метод и путь | Тело | Успех | Ошибки |
|---|---|---|---|
| `POST /wallets/{walletId}/holds` | `transferId`, `amount`, `recipientUserId` | 200 `HoldResult` | 400, 401, 403, 404, 409 |
| `POST /wallets/holds/{holdId}/confirm` | нет | 204 | 401, 403, 404, 409 |
| `POST /wallets/holds/{holdId}/release` | нет | 204 | 401, 403, 404, 409 |
| `GET /wallets` | нет | 200 `WalletResult[]` | 401 |
| `GET /wallets/{walletId}/operations` | query `page`, `pageSize` | 200 страница | 400, 401, 403, 404 |

`Idempotency-Key` для hold не используется: ключ идемпотентности — `transferId`,
он уже уникален в `Holds`.

### 3. Идемпотентность

| Ситуация | Ответ Wallets |
|---|---|
| Повторный hold с тем же `transferId`, та же сумма и получатель | 200, существующий холд, балансы не меняются |
| Тот же `transferId`, другая сумма или получатель | 409 |
| `transferId` принадлежит другому кошельку | 409 |
| Повторный hold после Completed/Released | 200 с текущим статусом холда |
| Повторный confirm для Completed | 204, второго зачисления нет |
| Повторный release для Released | 204, повторного возврата нет |
| confirm для Released/Expired | 409 |
| release для Completed/Expired | 409 |

`Wallet.ConfirmHold` и `Wallet.ReleaseHold` возвращают `bool`: `true` — переход
выполнен сейчас, `false` — холд уже в терминальном состоянии. Handler вызывает
`ReceiveTransfer` только при `true`; это единственная защита от двойного зачисления.

### 4. Машина состояний

| Из | В | Условие |
|---|---|---|
| — | Active | `PlaceHold`, достаточно `Available` |
| Active | Completed | `ConfirmHold`: `Held -= amount`, ledger `TransferOut`, получателю `TransferIn` |
| Active | Released | `ReleaseHold`: `Held -= amount`, `Available += amount`, ledger не пишется |
| Active | Expired | watchdog, в #52 не реализован |
| Completed / Released / Expired | — | терминальные, переходов нет |

`hold` и `release` не пишут `WalletTransaction`: полный баланс (`Available + Held`)
не меняется. `confirm` пишет `TransferOut` с `BalanceBefore`/`BalanceAfter` по полному
балансу, как требует `db-schema.ru.md` §2.3.

### 5. Транзакционная граница и retry

`confirm` — один `SaveChangesAsync` на два агрегата: кошелёк отправителя и кошелёк
получателя. EF выполняет его в одной транзакции, поэтому «списали, но не зачислили»
невозможно.

Алгоритм:

1. загрузить кошелёк по `holdId`, проверить ownership и статус холда;
2. найти кошелёк получателя по `UserId + Currency`; если нет — `Wallet.Create` и `AddAsync`;
3. `ConfirmHold` у отправителя, `ReceiveTransfer` у получателя;
4. `SaveChangesAsync`.

Retry — не более двух попыток, и только на технических конфликтах:

| Исключение | Причина | Действие |
|---|---|---|
| `DuplicateWalletException` | гонка автосоздания получателя, `IX_Wallets_UserId_Currency` | очистить трекер, повторить попытку |
| `WalletConcurrencyException` | `DbUpdateConcurrencyException` по `Version` | очистить трекер, повторить попытку |
| `DomainException` (Released/Expired, неактивный получатель) | доменный конфликт | не повторять, 409 сразу |

Повтор после конкурентного изменения безопасен: если гонку выиграл другой confirm,
повтор увидит `Completed` и вернёт 204 без зачисления; если выиграл release — 409.
Задержка между попытками не добавляется, `Thread.Sleep` не используется.

`release` использует ту же политику: повтор после concurrency увидит `Released`
и вернёт 204.

### 6. Изменения домена

- `Hold`: новое поле `RecipientUserId`, проверка непустоты в конструкторе.
- `Wallet.PlaceHold(transferId, amount, userId, recipientUserId)`: при существующем
  холде с тем же `transferId` сверяет сумму, плательщика и получателя; при
  расхождении — 409.
- `Wallet.ConfirmHold` / `Wallet.ReleaseHold` возвращают `bool` (см. §3).
- Перевод самому себе запрещён на hold: 400.
- `Money.Create` для отрицательной суммы бросает `DomainException(Validation)`,
  а не `ArgumentException` (иначе 500 вместо 400).
- `DuplicateWalletOperationException`, `DuplicateHoldException`,
  `DuplicateWalletException`, `WalletConcurrencyException` наследуются от
  `DomainException` с кодом `Conflict`, чтобы непойманный случай давал 409, а не 500.

### 7. Ошибки

| Ситуация | Код |
|---|---|
| Нет или битый токен | 401 |
| Кошелёк/холд не найден | 404 |
| Чужой кошелёк или холд | 403 |
| Сумма ≤ 0, больше 4 знаков, пустые id, self-transfer | 400 |
| Недостаточно `Available` | 409 |
| `transferId` использован с другими параметрами | 409 |
| Несовместимый переход статуса | 409 |
| Concurrency после retry | 409 |
| `page`/`pageSize` вне диапазона | 400 |

Машиночитаемого кода ошибки в ProblemDetails нет: Shared заморожен на 1.0.0. Для M3
это техдолг — Payments будет различать `InsufficientFunds` и прочие 409 по статусу
и тексту, пока в Shared не появится поле `code`.

### 8. История и список

- `GET /wallets/{id}/operations` показывает только ledger; hold/release в истории
  не видны, их состояние отражают `Available` и `Held` в `GET /wallets/{id}`.
- Сортировка: `CreatedAt` по убыванию, затем `Id` по убыванию — детерминированно
  при одинаковых timestamp.
- Пагинация: `page` с 1, `pageSize` 1..100, по умолчанию 20. Выход за пределы —
  200 с пустой страницей и реальным `TotalCount`.
- Ownership: чужая история — 403, отсутствующий кошелёк — 404. Admin-доступ
  откладывается (см. «Вне scope»).
- `GET /wallets` возвращает кошельки текущего пользователя — без него BFF не может
  построить сводку.

### 9. BFF

`GET /bff/wallets` в Gateway:

- берёт `sub` из проверенного токена;
- зовёт Wallets именованным HttpClient `wallets` (адрес — из кластера `wallets`),
  пробрасывая `Authorization`, `X-User-Id` (внутренний заголовок) и correlation id;
- возвращает кошельки, итоги по валютам и последние операции
  (`recentOperations` 5 по умолчанию, 0..20);
- не-2xx от Wallets → 502, недоступность/таймаут → 503; неполную сводку не отдаёт.

Wallets `X-User-Id` не доверяет: личность определяется JWT и claim `sub`.

## Рассмотренные альтернативы

| Вариант | Почему нет |
|---|---|
| Передавать `recipientUserId` в теле `confirm` | Повтор с другим получателем неотличим от первого; контракт расходится с `confirm(holdId)` из architecture; при lost response Payments обязан каждый раз повторять тело |
| Синхронно спрашивать получателя у Identity/Payments в confirm | Новая сетевая зависимость на критическом пути перевода, SPOF; в #52 Payments ещё не реализован |
| Отдельная таблица `Transfers` в Wallets | Дублирует агрегат Payments, размывает границу bounded context |
| `Idempotency-Key` как ключ холда | `transferId` уже уникален и является естественным ключом; второй ключ создаёт два источника правды |
| Бесконечный auto-retry при concurrency | Скрывает конкуренцию, непредсказуемая latency; 409 с повторным вызовом честнее |
| Admin-доступ к чужой истории в #52 | Нет role-политики и `RoleClaimType` в Wallets; отдельный объём и тесты |

## Последствия

**Плюсы**

- `confirm` идемпотентен и переживает потерянный ответ без тела запроса.
- Один `SaveChanges` — нет частичного зачисления и «висящих» холдов после сбоя.
- Автосоздание кошелька получателя работает, гонка обрабатывается явно.
- Контракт Wallets → Payments зафиксирован до реализации Payments.

**Минусы и риски**

- Появляется миграция и обязательное поле в `Holds`.
- Wallets не проверяет существование получателя: в MVP холд может указывать на
  несуществующего пользователя. Валидация — ответственность Payments (M3).
- `transferId` уникален глобально: повтор в другом кошельке даёт 409.
- Две попытки retry добавляют код и требуют Postgres-тестов на гонки.
- Нет машиночитаемого кода ошибки — ограничение Shared 1.0.0.
- Endpoint'ы confirm/release пока пользовательские; в M3 нужна сервисная
  аутентификация Payments → Wallets.

## План реализации

| Шаг | Что |
|---|---|
| 1 | ADR 0007, решение по получателю |
| 2 | Domain: `RecipientUserId`, `PlaceHold`, bool-возвраты, `Money`; Domain.Tests |
| 3 | Application: контракты, `IWalletRepository`, исключения, handler'ы; Application.Tests |
| 4 | Infrastructure: EF-конфигурация, репозиторий, маппинг ошибок, миграция; Postgres-тесты |
| 5 | Host: hold/confirm/release, GET /wallets, история; HTTP-тесты |
| 6 | Gateway: `/bff/wallets`, E2E, OpenAPI, клиент |
| 7 | Docs: `wallets-entity-interactions`, `db-schema`, `testing-strategy`, README; issue #52 и #8 |

## Вне scope #52

- `getHold` для reconciliation статуса `Confirming`;
- watchdog для `Expired`;
- Admin-доступ к чужой истории;
- сервисная аутентификация Payments → Wallets;
- outbox-события холдов (`HoldPlaced`, `HoldCompleted`);
- машиночитаемые коды ошибок в ProblemDetails;
- freeze/unfreeze/close кошелька.

## Ссылки

- [issue #52](https://github.com/PoyarkinES/otus_payment_system/issues/52)
- [architecture.ru.md](../architecture.ru.md)
- [wallets-entity-interactions.ru.md](../wallets-entity-interactions.ru.md)
- [db-schema.ru.md](../db-schema.ru.md)
- [ADR 0006](./0006-wallet-operations-idempotency.md)
- [testing-strategy.ru.md](../testing-strategy.ru.md)
