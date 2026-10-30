# Issue #12 — [P1W2] Реализовать сервис Identity

> Эпик. Декомпозиция на 8 подзадач (вертикальные срезы).
>
> **Статус на 30.10.2026:** все подзадачи 12.1–12.8 реализованы и влиты в `main` (PR #35, #36), issue #12 закрыт. Документ оставлен как историческая декомпозиция; актуальный план — [roadmap.ru.md](./roadmap.ru.md).

## Objective
Реализовать сервис Identity для управления пользователями, аутентификацией и базовыми правами доступа.

## Expected outcome
- Подготовлен каркас Identity-сервиса.
- Определены основные точки входа и модели.
- Сервис готов к расширению бизнес-логикой.

## Acceptance criteria
- [ ] Сервис создан
- [ ] Есть базовые модели и API-эндпоинты
- [ ] Подготовлена интеграция с авторизацией

## Маппинг критериев на подзадачи

| Требование Issue | Подзадачи |
|---|---|
| Сервис создан | 12.1 |
| Базовые модели | 12.2, 12.3 |
| API-эндпоинты / точки входа | 12.5, 12.7 |
| Интеграция с авторизацией | 12.4, 12.6 |
| Управление пользователями (Objective) | 12.7 |
| Базовые права доступа (Objective) | 12.8 |

## Технические решения (дефолты; поправить при необходимости)

| Вопрос | Решение |
|---|---|
| MediatR | Да, ставим сразу (в стеке по roadmap) |
| Persistence для #12 | EF Core InMemory (PostgreSQL — неделя 3) |
| JWT-секрет | appsettings.Development.json + TODO: user-secrets |
| FluentValidation | Да, для DTO в 12.5 |
| Хэширование пароля | ASP.NET Core PasswordHasher<User> |
| Target framework | net10.0 (по Directory.Build.props) |

## Порядок работ

12.1 -> 12.2 -> 12.3 -> 12.4 -> 12.5 -> 12.6 -> 12.7 -> 12.8

Каждая подзадача — отдельный точечный коммит (git add конкретных файлов, не -A).

---

## 12.1 — Host-проект + wiring

**Что делаем**
- Создать Otus.PaymentSystem.Identity.Host (ASP.NET Core Web API, net10.0).
- Ссылки на Identity.Domain, Identity.Application, Identity.Infrastructure.
- Добавить в OtusPaymentSystem.sln.
- Настроить Program.cs, Swagger, health-check.

**DoD**
- [ ] Проект собирается (dotnet build).
- [ ] dotnet run поднимает Swagger UI.
- [ ] GET /health -> 200.

**Критерий Issue:** Сервис создан.

---

## 12.2 — Domain: модель User

**Что делаем**
- User (Aggregate Root), UserStatus (enum), VO Email.
- Инварианты: email уникален, пароль только как хэш.
- Доменные события: UserRegistered, UserBlocked, UserUnblocked, PasswordChanged.

**DoD**
- [ ] Domain собирается.
- [ ] Юнит-тесты инвариантов зелёные.

**Критерий Issue:** Базовые модели.

---

## 12.3 — Application: контракты + handlers

**Что делаем**
- Интерфейсы IUserRepository, IPasswordHasher, ITokenService.
- RegisterCommand / LoginCommand + handlers (MediatR).
- DTO запросов/ответов.

**DoD**
- [ ] Application собирается.
- [ ] Handlers покрыты юнит-тестами с моками.

**Критерий Issue:** Базовые модели; готов к расширению.

---

## 12.4 — Infrastructure: persistence + JWT

**Что делаем**
- EF Core InMemory, реализация IUserRepository.
- PasswordHasher на базе ASP.NET Core.
- JwtTokenService (claims: sub, email, role, jti, exp).
- Конфиг секрета JWT.

**DoD**
- [ ] register/login работают на InMemory.
- [ ] Выдаётся валидный JWT.

**Критерий Issue:** Интеграция с авторизацией.

---

## 12.5 — API-эндпоинты аутентификации

**Что делаем**
- POST /auth/register, POST /auth/login.
- DTO + FluentValidation.
- ProblemDetails для ошибок.

**DoD**
- [ ] Swagger: register -> 201, login -> 200 + JWT.
- [ ] Неверные креды -> 401.
- [ ] Невалидный запрос -> 400.

**Критерий Issue:** API-эндпоинты / точки входа.

---

## 12.6 — Интеграция с авторизацией (аутентификация)

**Что делаем**
- JWT bearer middleware (AddAuthentication/AddAuthorization).
- GET /auth/me с [Authorize].
- Конфиг appsettings.Development.json.

**DoD**
- [ ] /auth/me без токена -> 401.
- [ ] /auth/me с токеном -> 200 + данные.

**Критерий Issue:** Интеграция с авторизацией.

---

## 12.7 — Управление пользователями

**Что делаем**
- GET /users/me (профиль), PUT /users/me (обновление email/пароля — минимум).
- POST /users/{id}/block и /unblock под ролью Admin (нужно для 12.8).

**DoD**
- [ ] Пользователь читает/меняет свой профиль.
- [ ] Доступ к чужому профилю -> 403.
- [ ] Блокировка работает под Admin.

**Критерий Issue:** Управление пользователями (Objective).

---

## 12.8 — Базовые права доступа (roles/policies)

**Что делаем**
- Роли User / Admin; claim role в JWT.
- Policy-based авторизация; admin-only политика на block/unblock.

**DoD**
- [ ] User на admin-эндпоинт -> 403.
- [ ] Admin -> 200.

**Критерий Issue:** Базовые права доступа (Objective).

---

## Вне scope #12 (недели 2–3)

- Refresh-token + ротация + logout
- PostgreSQL + EF Core migrations
- Ownership-политики сверх минимума
- Rate-limiting / brute-force защита
- Аудит security-событий
- Интеграционные тесты (Testcontainers)
