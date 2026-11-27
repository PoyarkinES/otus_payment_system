# Ретро M2 — Wallets

**План:** 30 ноября — 27 декабря 2026. **Факт:** M2 закрыт **27 ноября 2026**, досрочно.
**Эпик:** [#41](https://github.com/PoyarkinES/otus_payment_system/issues/41) — закрыт. **Milestone M2** — закрыт.
**Недели:** [#50](https://github.com/PoyarkinES/otus_payment_system/issues/50)–[#53](https://github.com/PoyarkinES/otus_payment_system/issues/53).

## Как вести рассказ (15–20 минут)

| Блок | Время | Что сказать |
|---|---:|---|
| 1. Итог | 1–2 мин | M2 закрыт досрочно: Wallets, holds, Compose, Shared, UI-кошелёк. Эпик и milestone закрыты. |
| 2. Что вошло | 3 мин | Пройти по таблице: #50–#53, #128, Shared 1.0.1 → 1.0.2, образы, UI #84/#85. |
| 3. Демо | 3–5 мин | Показать стек и сценарий: register → wallet → deposit → hold → confirm → history; `GET /api/wallets` → 200. |
| 4. Что сработало | 3 мин | PostgreSQL-тесты, bootstrap БД, smoke через Gateway, SHA-образы, release-first Shared. |
| 5. Что кусало | 4 мин | IdentityModel, NU1102 в CI, bootstrap БД, strict BFF, stacked PR, преждевременное закрытие #126. |
| 6. Правила и долги | 2–3 мин | Что забираем в M3, что осталось: #86, transfers. |

## Что вошло в M2

| Задача | Результат |
|---|---|
| #50 Wallets | Создание и чтение кошелька, `available`/`held` |
| #51 Операции | Пополнение, списание и ledger операций |
| #52 Holds | Hold/confirm/release, история и проверка владельца |
| #53 Тесты и демо | PostgreSQL-тесты, конкурентные операции, negative paths, Gateway-сценарий |
| #128 Compose | Wallets в общем стеке, отдельная `otus_wallets`, идемпотентный bootstrap, автомиграции |
| #142 Shared | Выравнивание IdentityModel; `shared-v1.0.1` |
| #151 Shared 1.0.2 | Потребители обновлены на `1.0.2`; #126 закрыт после rollout |
| #143/#152/#153 Образы | Identity/Gateway/Wallets пересобраны, SHA-образы закреплены; обновление тегов автоматизировано |
| #146 Ретро | Этот документ; #53 закрыта |
| #84/#85 Web UI | Профиль/сессия и экран кошельков закрыты |

**Проверка стека:** `postgres`, `identity`, `wallets` и `gateway` — `healthy`; `wallets-db-bootstrap` — `Exited (0)`; авторизованный `GET /api/wallets` через Gateway — `200`.

**Проверки:** интеграционные тесты Wallets на PostgreSQL, happy/negative paths и конкурентные hold/confirm зелёные в CI. API smoke: регистрация, login, создание кошелька и список кошельков прошли.

## Демо

Полный сценарий — [demo-m2.ru.md](./demo-m2.ru.md). Для рассказа достаточно:

```powershell
docker compose -f docker-compose.yml ps -a
# postgres, identity, wallets, gateway — healthy
# wallets-db-bootstrap — Exited (0)

Invoke-RestMethod http://localhost:8080/bff/health | ConvertTo-Json
# gateway + identity: ok; Wallets проверяется отдельно

# register → login → create wallet → deposit → hold → confirm → history
# финал: GET /api/wallets через Gateway → 200
```

Если демо не запускается — показать `ps -a`, `GET /api/wallets` и сказать, что smoke уже пройден на этом стеке; не тратить ретро на отладку.

## Что сработало

- **Интеграционные тесты на реальном PostgreSQL.** Проверяют не только домен, но и ledger, транзакции и конкуренцию.
- **Bootstrap вместо второй БД/volume.** Идемпотентное создание `otus_wallets` не трогает данные Identity.
- **Smoke через публичный Gateway.** Подтвердил авторизацию и маршрут до Wallets в собранном стеке.
- **Закреплённые SHA-образы + проверка в CI.** Compose не уезжает на случайный `latest`; теги проверяются на существование.
- **Release-first для Shared.** Версия → тег → GitHub Packages → `bump-shared.ps1` → PR потребителей. CI проверяет согласованность версий.
- **Общий пакет как место исправления.** IdentityModel выровняли в Shared, а не в каждом хосте отдельно.

## Что кусало

| Проблема | Цена | Вывод |
|---|---|---|
| Смешались версии IdentityModel: `Tokens` `8.14.0`, остальные `8.0.1`; Gateway не декодировал JWT | Авторизованные запросы — `401`, хотя login проходил | Выравнивать семейство пакетов в Shared и проверять итоговый `.deps.json` |
| CI не видел Shared `1.0.1` до публикации (`NU1102`) | PR-проверки падали на restore | Не держать мёртвый artifact-путь; release-first и проверка версий (#147/#148) |
| Нужна отдельная БД, но volume Identity нельзя терять | Init SQL не подходил для существующего volume | Идемпотентный bootstrap; проверять и чистый, и существующий volume |
| Strict BFF потребовал обновить фикстуры Client | Typecheck падал на старых mock-ответах | Менять OpenAPI и фикстуры одним PR (#145) |
| Stacked PR #148/#149 вошли в main, но #138/#139 остались открытыми | Лишняя ручная гигиена задач | После merge проверять, что issues закрылись; не полагаться на `Closes` в stacked PR |
| #126 закрыли до публикации Shared `1.0.2` | Wallets ещё был на `1.0.1` | Закрывать после rollout: publish → bump → smoke → close |
| SHA-теги в Compose обновлялись вручную | Риск отставания образов | Автоматизация #152/#153 + CI-проверка манифестов |

## Правила, которые забираем в M3

1. **Release-first для Shared:** версия → тег → GitHub Packages → `bump-shared.ps1` → PR потребителей.
2. **Один источник пакетов:** GitHub Packages; локальный artifact не возвращаем.
3. **Compose smoke — в изолированном project name**; `down` без `-v`, если данные нужны.
4. **Проверять весь путь через Gateway**, а не только unit-тесты.
5. **Закрывать issue после merge/rollout и smoke**, с конкретным результатом проверки.
6. **SHA-образы и версии Shared проверяются в CI** (#148/#149), а не глазами.

## Долги и следующий шаг

- **#126** — закрыт: Shared `1.0.2`, потребители обновлены (#151), образы закреплены (#153).
- **#137/#138/#139** — закрыты: artifact убран, проверки версий и Compose-образов в CI.
- **#85** — закрыт; **#86** — следующая UI-задача: формы top-up/withdraw, `Idempotency-Key`, фильтры истории. История уже готова частично.
- **M3** — переводы, идемпотентность, Redis, hold/Fraud sync: [#54](https://github.com/PoyarkinES/otus_payment_system/issues/54) и далее по [dashboard #70](https://github.com/PoyarkinES/otus_payment_system/issues/70).

**Итог:** M2 закрыт досрочно; блокеров для M3 нет. Главный урок — не смешивать «код готов» и «релиз доехал»: версии, образы, issues и smoke должны сходиться в одной точке.
