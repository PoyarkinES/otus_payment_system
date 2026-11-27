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
| 4. Что сработало | 3 мин | PostgreSQL-тесты, bootstrap БД, smoke через Gateway, `latest`-образы, release-first Shared. |
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
| #143/#157 Образы | Identity/Gateway/Wallets публикуются как `latest`; Compose использует `pull_policy: always`; SHA-пиннинг убран |
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

- **Интеграционные тесты на реальном PostgreSQL.** Домен проверяет правила, но только тесты на живой БД ловят ledger-инварианты, транзакции, блокировки и конкурентные `hold`/`confirm`. Поэтому расхождение «в ответе красиво, в БД не сошлось» всплывало до демо.

- **Bootstrap вместо второй БД/volume.** Одноразовый контейнер с `psql` подключается к уже работающему PostgreSQL и создаёт `otus_wallets`, только если её нет. Не нужен второй сервер и volume, данные Identity не трогаются; способ работает и на чистой БД, и на существующем volume, где init SQL уже не сработал бы.

- **Smoke через публичный Gateway.** Проверяли тем же входом, что и UI: только Gateway на `8080`, без доступа к внутреннему Wallets. Регистрация/логин → JWT → `POST /api/wallets` → `GET /api/wallets` → `200`. Это подтвердило JWT/JWKS, YARP-маршрут, сеть Compose, живость Wallets и миграции БД — то, чего unit-тесты не доказывают.

- **Единый `latest`-образ + `pull_policy: always`.** После публикации достаточно `docker compose pull && up -d`; не нужен PR на обновление SHA и ручная правка compose. Минус — меньше воспроизводимости, но для демо это приемлемый обмен; SHA-теги можно оставить только для отката.

- **Release-first для Shared.** Сначала версия → тег → публикация в GitHub Packages, затем `bump-shared.ps1` и PR потребителей. CI проверяет согласованность версий, а не пытается собрать сервис с ещё не опубликованным пакетом — это убрало `NU1102` и гонку «код есть, пакета нет».

- **Общий пакет как место исправления.** Конфликт IdentityModel (`Tokens 8.14.0` против `8.0.1`) исправляли в Shared, а не в каждом Host. Один пакет `1.0.2` обновил всех потребителей — внешний пакет оказался не бюрократией, а единой точкой согласования версий.

## Что кусало

| Проблема | Цена | Вывод |
|---|---|---|
| Смешались версии IdentityModel: `Tokens` `8.14.0`, остальные `8.0.1`; Gateway не декодировал JWT | Авторизованные запросы — `401`, хотя login проходил | Выравнивать семейство пакетов в Shared и проверять итоговый `.deps.json` |
| CI не видел Shared `1.0.1` до публикации (`NU1102`) | PR-проверки падали на restore | Не держать мёртвый artifact-путь; release-first и проверка версий (#147/#148) |
| Нужна отдельная БД, но volume Identity нельзя терять | Init SQL не подходил для существующего volume | Идемпотентный bootstrap; проверять и чистый, и существующий volume |
| Strict BFF потребовал обновить фикстуры Client | Typecheck падал на старых mock-ответах | Менять OpenAPI и фикстуры одним PR (#145) |
| Stacked PR #148/#149 вошли в main, но #138/#139 остались открытыми | Лишняя ручная гигиена задач | После merge проверять, что issues закрылись; не полагаться на `Closes` в stacked PR |
| #126 закрыли до публикации Shared `1.0.2` | Wallets ещё был на `1.0.1` | Закрывать после rollout: publish → bump → smoke → close |
| SHA-теги в Compose обновлялись вручную | Лишний PR и риск отставания | Заменено на `latest` + `pull_policy: always` (#157) |

## Правила, которые забираем в M3

1. **Release-first для Shared:** версия → тег → GitHub Packages → `bump-shared.ps1` → PR потребителей.
2. **Один источник пакетов:** GitHub Packages; локальный artifact не возвращаем.
3. **Compose smoke — в изолированном project name**; `down` без `-v`, если данные нужны.
4. **Проверять весь путь через Gateway**, а не только unit-тесты.
5. **Закрывать issue после merge/rollout и smoke**, с конкретным результатом проверки.
6. **Compose использует `latest` + `pull_policy: always`; CI проверяет наличие manifest**, а версии Shared — `bump-shared.ps1 -Check`.

## Долги и следующий шаг

- **#126** — закрыт: Shared `1.0.2`, потребители обновлены (#151), образы переведены на `latest` + `pull_policy: always` (#157).
- **#137/#138/#139** — закрыты: artifact убран, проверки версий и Compose-образов в CI.
- **#85 и #86** — закрыты (#155); следующая UI-задача — **#87**, переводы, после появления transfers API в M3.
- **M3** — переводы, идемпотентность, Redis, hold/Fraud sync: [#54](https://github.com/PoyarkinES/otus_payment_system/issues/54) и далее по [dashboard #70](https://github.com/PoyarkinES/otus_payment_system/issues/70).

**Итог:** M2 закрыт досрочно; блокеров для M3 нет. Главный урок — не смешивать «код готов» и «релиз доехал»: версии, образы, issues и smoke должны сходиться в одной точке.
