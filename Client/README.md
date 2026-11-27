# Client

Веб-клиент Otus Payment System: одностраничное приложение (SPA) на React. Ходит **только через
Gateway** — прямых адресов Identity, Wallets и других сервисов в коде нет и быть не должно.
Эпик [#80](https://github.com/PoyarkinES/otus_payment_system/issues/80), каркас —
[#82](https://github.com/PoyarkinES/otus_payment_system/issues/82).

В клиенте реализована авторизация: вход, регистрация, автоматическое обновление токенов
(refresh), защищённые маршруты и выход. Токены хранятся только в памяти (memory),
пароль не попадает в localStorage. [#83](https://github.com/PoyarkinES/otus_payment_system/issues/83)

## Профиль и кошельки

- `/wallets` показывает доступный остаток, резерв и пять последних операций из `GET /bff/wallets`. Если кошелька ещё нет, его можно создать кнопкой «Создать кошелёк» здесь или в профиле. Кошелёк открывается в рублях с нулевым остатком. Переключатель видимости скрывает суммы на экране.
- Кнопка «Посмотреть всю историю» под последней операцией открывает `/operations`. Полная история загружается через `GET /api/wallets/{id}/operations` по 10, 20 или 50 записей. Номер страницы и размер сохраняются в адресе.
- `/profile` берёт email и роль из `GET /bff/me`, а статус аккаунта — из `GET /api/identity/users/me`. Активному администратору доступны поиск пользователя по UUID (`/admin/users`) и статус системы (`/admin/status`).
- На телефоне кошельки, операции и профиль переключаются нижней панелью с иконками. Выход находится в профиле. На широком экране разделы находятся в боковом меню.
- В режиме `npm run dev:mocks` введите `demo@otus.local` и пароль `demo-wallet`, чтобы посмотреть кошелёк с 68 операциями шести типов. Другой email начинает с пустого списка. Для проверки роли администратора используйте `admin@otus.local`. Моки принимают любой непустой пароль и хранят данные в памяти до перезагрузки страницы.

## Карта папок

```
Client/
├── src/
│   ├── app/
│   │   ├── router.tsx                  адрес → страница
│   │   └── ProtectedRoute.tsx          guard: перенаправление на /login
│   ├── pages/
│   │   ├── <имя>/                      одна страница = одна папка
│   │   │   ├── <Имя>Page.tsx           страница
│   │   │   ├── <Имя>Page.test.tsx      её тесты
│   │   │   └── …                       части, нужные только этой странице
│   │   ├── login/                      форма входа
│   │   └── register/                   форма регистрации
│   └── shared/                         общее для нескольких страниц
│       ├── api/
│       │   ├── auth/                   авторизация: токены и сессия
│       │   │   ├── storage.ts          единое хранилище токенов (memory, с подпиской)
│       │   │   └── session.ts          управление сессией (freshness, ensureFreshSession)
│       │   ├── http.ts                 apiFetch: интерцептор авторизации + single-flight refresh
│       │   └── generated/
│       │       ├── endpoints/          хуки Gateway из OpenAPI — руками не править
│       │       └── identity/           хуки Identity из OpenAPI — руками не править
│       ├── auth/
│       │   └── AuthContext.tsx         React Context токенов (useSyncExternalStore)
│       ├── ui/                         общие компоненты: загрузка, ошибка, шапка
│       └── testing/handlers.ts         ответы бэка в режиме без бэка
├── .env.example                        настройки
└── package.json                        зависимости и команды
```

- Страницы не импортируют друг друга. Код понадобился второй странице — переносим в `shared/`.
- `@/…` в импортах означает `src/…`.
- Остальные файлы в `app/` и `shared/` — настройка приложения. Для новой страницы их трогать
  не нужно.

## Запуск за минуту

Нужен Node.js 22 LTS (точная версия — в `.nvmrc`). Все команды — из папки `Client/`.

```bash
npm ci              # поставить зависимости строго по package-lock.json
npm run dev         # открыть http://localhost:5173
```

Есть два режима:

- **С настоящим бэком.** В корне репо `docker compose pull && docker compose up -d`, затем `npm run dev`.
  Образы Identity/Gateway публичные, токены GitHub/NuGet не нужны. Фронт пойдёт в Gateway на `http://localhost:8080`.
- **Без бэка.** `npm run dev:mocks` — на запросы отвечают моки, Docker не нужен. Удобно, когда
  бэк ещё не готов или не хочется его поднимать.
  Моки работают через Service Worker MSW, поэтому нужен Chrome/Edge (или другой браузер
  с полноценной поддержкой Service Worker). В Яндекс.Браузере MSW может не перехватывать
  POST-запросы — для `dev:mocks` используйте Chrome/Edge.

Все команды:

```bash
npm ci                  # ≈ dotnet restore (строго по lock-файлу)
npm run dev             # ≈ dotnet watch run — dev-сервер с горячей перезагрузкой
npm run dev:mocks       # то же, но без бэка, на моках
npm test                # ≈ dotnet test
npm run test:watch      # тесты перезапускаются при сохранении файла
npm run lint            # ≈ анализаторы Roslyn (ESLint)
npm run format          # поправить форматирование (Prettier); format:check — только проверить
npm run typecheck       # проверка типов TypeScript без сборки
npm run build           # ≈ dotnet publish — готовая статика в dist/
npm run api:generate    # перегенерировать клиент API из OpenAPI-спек
```

## Как фронт связан с бэком

```
браузер (localhost:5173)
   │  fetch + X-Correlation-Id
   ▼
Gateway (localhost:8080)        единственный адрес, который знает фронт
   │  /bff/*            — агрегаты для фронта (BFF)
   │  /api/identity/**  — прокси в Identity
   ▼
Identity, Wallets, …            из браузера напрямую недоступны
```

- **Адрес Gateway** задаётся в `VITE_API_BASE_URL`, по умолчанию `http://localhost:8080`.
  Поменять у себя — скопировать `.env.example` в `.env.local` (он не попадает в git).
- **CORS.** Gateway пускает только origin из `Cors:AllowedOrigins` в
  `Gateway/src/Otus.PaymentSystem.Gateway.Host/appsettings.json` — сейчас это
  `http://localhost:5173`. Поэтому dev-сервер закреплён на порту 5173 и не перескакивает
  на соседний, если тот занят: ошибка порта понятнее, чем CORS-ошибка в браузере.
- **Correlation ID.** Каждый запрос фронта получает новый `X-Correlation-Id`. Gateway пишет его
  в логи Serilog, передаёт в Identity и возвращает в ответе; в ProblemDetails он же лежит
  в `traceId`. Пользователь видит его в ошибке как «Код для поддержки» — по этому коду запрос
  находится в логах всех сервисов.

## Я поменял API — что делать

Фронт не пишет DTO руками: типы, функции запросов и React-хуки генерирует
[orval](https://orval.dev) из двух OpenAPI-спек:

- `openapi/gateway.v1.json` — BFF-агрегаты `/bff/*` и health Gateway;
- `openapi/identity.v1.json` — Auth-эндпоинты `/api/identity/auth/*` (login, register,
  refresh, logout, me).

После изменения контракта:

1. Поменяли эндпоинт или DTO в Gateway или Identity.
2. Обновили соответствующую `openapi/*.v1.json` — бэковый CI (`openapi-*-drift`) всё равно
   этого потребует.
3. `cd Client && npm run api:generate` — перегенерировали `src/shared/api/generated/`.
4. `npm run typecheck` — TypeScript покажет каждое место во фронте, которое сломалось
   от изменения контракта. Если ничего не красное — фронт совместим.
5. Закоммитили `openapi/*.v1.json` вместе с `Client/src/shared/api/generated/`.

Если шаг 3 забыть, в CI упадёт job **Client (lint, test, build)** на шаге
«Generated API client is up to date» с подсказкой, какие файлы устарели. Починка — те же
шаги 3–5.

Auth-эндпоинты генерируются из `openapi/identity.v1.json` с `baseUrl: '/api/identity'`.
Identity Swagger отдаёт `required` для non-nullable полей, поэтому сгенерированный
`AuthTokens` строгий (`accessToken: string`, а не `accessToken?: string | null`) —
адаптер на клиенте не нужен.

## Стек

Собирает приложение Vite, код — React 19 на TypeScript, маршруты — React Router. Данные
с сервера загружает и кэширует TanStack Query: он же отвечает за состояния «загрузка» и
«ошибка» и за повтор запроса. Компоненты интерфейса — MUI; Tailwind CSS используется только
для раскладки и отступов, а цвета, шрифты и брейкпоинты берёт из темы MUI, чтобы у
приложения был один источник оформления.

Клиент API генерирует orval из OpenAPI-спеки — он же создаёт моки MSW для тестов и режима
без бэка. Тесты — Vitest и Testing Library: страница рендерится в jsdom, а сеть подменяет
MSW, так что проверяется настоящий путь запроса через `apiFetch`. Код проверяют ESLint и
Prettier. TypeScript сейчас 6.0: версия 7 уже вышла, но typescript-eslint пока поддерживает
TypeScript только ниже 6.1.

## Ошибки

Все сервисы отвечают на ошибки в формате ProblemDetails
([docs/api-errors.ru.md](../docs/api-errors.ru.md)). `apiFetch` превращает такой ответ в
`ApiProblem` (статус, `detail`, `traceId`, ошибки полей у 400), а недоступность сервера — в
`ApiNetworkError`. Компонент `ErrorState` показывает пользователю:

```
сервер недоступен     «Сервер недоступен» — проверьте, что Gateway запущен
400                   «Проверьте введённые данные»
401                   «Нужно войти заново»
403                   «Недостаточно прав»
404                   «Не найдено»
409                   «Конфликт данных»
429                   «Слишком много запросов» — подождите и повторите
502, 503, 504         «Сервис временно недоступен»
другие 5xx            «Ошибка на сервере»
```

Под заголовком — `detail` от сервера, если он есть, и «Код для поддержки» (correlation ID).
Ошибки 4xx не повторяются автоматически, сбой сервера или сети — повторяется один раз.

Любой ответ вне 2xx — это ошибка в `error` хука, а не данные в `data`. Сгенерированные типы
ответа перечисляют и ошибочные статусы (например, `bffMeResponse401`), но до `data` они не
доходят: проверять `if (data.status === 401)` бесполезно, смотреть нужно на `error`.

Исключение — статусы, которые вызов явно разрешил через `acceptStatus`. Так сделано у
`/bff/health`: его 503 не ошибка, Gateway отвечает тем же телом, что и при 200, и сообщает,
какой сервис недоступен. Поэтому страница вызывает
`useBffHealth({ request: { acceptStatus: [503] } })` и показывает «Система работает с
ограничениями» со списком сервисов.

## Как добавить страницу

1. Создать папку `src/pages/<имя>/` и в ней компонент `<Имя>Page.tsx`.
2. Добавить маршрут в `src/app/router.tsx`, в `children`: `{ path: 'wallets', element: <WalletsPage /> }`.
3. Если нужного эндпоинта нет в `src/shared/api/generated/` — сначала раздел
   «Я поменял API».
4. Данные брать сгенерированным хуком (`useBffHealth()` и т.п.) и показывать состояния через
   `LoadingState` и `ErrorState` из `src/shared/ui/` — как в `pages/status/StatusPage.tsx`.
5. Рядом положить `<Имя>Page.test.tsx`: `renderRoute('/<путь>')` открывает приложение на
   нужном адресе, `server.use(http.get(...))` задаёт ответ сервера
   (пример — `pages/profile/ProfilePage.test.tsx`).
6. Проверить: `npm run lint && npm run typecheck && npm test`.
