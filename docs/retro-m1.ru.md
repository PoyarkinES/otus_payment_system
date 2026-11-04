# Ретро M1 — Edge & Foundation

**Факт:** эпик [#40](https://github.com/PoyarkinES/otus_payment_system/issues/40) закрыт **4 ноября 2026**, при плане до 29 ноября. Фора — три с половиной недели.
**PR:** [#104](https://github.com/PoyarkinES/otus_payment_system/pull/104), squash `e1f9030`, 20 файлов, `+1009 / −91`.

## Что вошло в M1

| Задача | Результат |
|---|---|
| #46 Gateway | YARP, маршруты Identity, `X-User-Id`, CORS |
| #47, #10 PostgreSQL | EF Core, миграции, интеграционные тесты на реальной БД |
| #48 Refresh/logout | Ротация, reuse-детект с отзывом цепочки, отзыв сессий при смене пароля, RS256 + JWKS |
| #20 Логирование | Serilog, correlation ID на Gateway и Identity |
| #22, #21 | Единый ProblemDetails, валидация DTO |
| #19 CI | Тесты Shared/Gateway/Identity, Postgres отдельным job, coverage |
| #49 BFF и стек | `/bff/me`, `/bff/health`, админский `GET /users/{id}`, Docker Compose полного стека |
| #16 Документы | `testing-strategy.ru.md`, `demo-m1.ru.md` |

**Тесты на момент закрытия:** Identity 101, Gateway 17, Shared 14; плюс job `identity-integration-tests` (PostgreSQL) и `openapi-drift`.

## Что сработало

- **Один шаг — одна проверка.** Сборка/тесты после каждого файла ловили ошибки раньше, чем они накапливались.
- **Точечные коммиты по смыслу.** История читается, ревью по файлам возможно, squash в `main` даёт один внятный коммит.
- **Спека как артефакт.** `openapi-drift` — единственная проверка контракта, которой не нужна человеческая память.
- **Смоук на реальном стеке.** Поймал то, чего не видит юнит-тест: `502` при лежащем Identity, `503` в `/bff/health`, `401/403` на маршрутах.
- **Compose с `healthchecks` + `depends_on: service_healthy`.** Порядок запуска виден в `docker compose ps`, а не угадывается по логам.
- **ADR и чек-лист как источник решений.** Спор о «contract-first для BFF» закрылся ссылкой на документ, а не по памяти.

## Что кусало

| Проблема | Цена | Вывод |
|---|---|---|
| Локальные порты: 5100, 8080, 5434 и `docker-compose.override.yml` | ~40 минут | Перед запуском — проверка `netstat`, и один экземпляр сервиса на порт |
| `Get-NetTCPConnection -ErrorAction SilentlyContinue` врёт про «порт свободен» | один неверный диагноз | Авторитет — `netstat -ano` + реальный запрос |
| GitHub Actions partial outage (4 ноября, ~40 минут) | ожидание и лишний пустой коммит | Сначала статус-страница, потом гипотезы о своём коде |
| Issues закрыли до мержа PR | откат двух issues | Порядок: merge → close, не наоборот |
| Неверные пути `dotnet run` в инструкции | два круга | Команды писать от корня репозитория и проверять каталог |
| Тестовая обвязка `IResult` без `RequestServices` | два круга | `Ok<T>`/`JsonHttpResult` требуют DI; `UnauthorizedHttpResult` — весь auth-конвейер |

## Правила, которые забираем в M2

1. Перед запуском сервисов — `netstat -ano | Select-String '<порт>'`, а не cmdlet с `SilentlyContinue`.
2. Issues закрываются **после** мержа PR; при блокировке CI issues остаются открытыми.
3. Если CI «не запускается» — сначала `githubstatus.com`, потом настройки репозитория, и только потом код.
4. Команды в инструкциях пишутся от корня репозитория; если команда зависит от каталога — это указывается явно.
5. Тест, который требует DI-контейнер, либо получает его явно, либо не исполняет результат, а проверяет тип.

## Долги (заведены как issues)

| # | Долг | Оценка | Приоритет |
|---|---|---|---|
| [#105](https://github.com/PoyarkinES/otus_payment_system/issues/105) | D1: `docker build` в CI | 0.5 дня | P1 |
| [#106](https://github.com/PoyarkinES/otus_payment_system/issues/106) | D2: HTTP-тесты хоста (`WebApplicationFactory`) | 1 день | P1 |
| [#107](https://github.com/PoyarkinES/otus_payment_system/issues/107) | D3: спека Gateway/BFF + клиент | 2 дня | P1 |
| [#108](https://github.com/PoyarkinES/otus_payment_system/issues/108) | D4: `UserStatus` строкой | 0.5 дня | P2 |
| [#109](https://github.com/PoyarkinES/otus_payment_system/issues/109) | D5: rate limiting на `/bff/*` | 0.5 дня | P2 |

Итого ~4.5 дня. Остаток форы — на подготовку схемы Wallets к M2.

## Решение по календарю

M2 стартует по плану **30 ноября 2026**. Фора не сдвигает M2 влево: сначала закрываем долги P1 (#105–#107), потому что Wallets начинается с денег и транзакций, а не с прототипа. Долги P2 (#108–#109) можно вести параллельно.
