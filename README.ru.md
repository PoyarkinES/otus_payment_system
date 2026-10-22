# Otus Payment System

[![CI](https://github.com/PoyarkinES/otus_payment_system/actions/workflows/ci.yml/badge.svg)](https://github.com/PoyarkinES/otus_payment_system/actions/workflows/ci.yml)

Учебный fintech-проект для портфолио .NET-разработчика.

## Цель проекта
Создать реалистичную платежную платформу, которая демонстрирует навыки:
- ASP.NET Core
- Entity Framework Core
- PostgreSQL
- JWT-аутентификация
- RabbitMQ
- CQRS / MediatR
- Clean Architecture
- DDD
- Микросервисы
- Docker
- GitHub Actions
- мониторинг и observability

## Формат проекта
Это учебный и портфолио-проект.
Его цель — развиваться поэтапно в течение нескольких месяцев без попытки реализовать всё сразу.

## Структура репозитория
Репозиторий организован как набор независимых микросервисных решений.
Каждый bounded context живёт в своей папке и имеет собственный `.sln`-файл
с единой структурой `src` / `test`.

```
.
├── Directory.Build.props        # общие настройки MSBuild (TFM, nullable, анализаторы)
├── global.json                  # зафиксированная версия .NET SDK
├── Shared/
│   ├── Otus.PaymentSystem.Shared.sln
│   ├── src/Otus.PaymentSystem.SharedKernel/
│   └── test/
├── Identity/
│   ├── Otus.PaymentSystem.Identity.sln
│   ├── src/Otus.PaymentSystem.Identity.{Domain,Application,Infrastructure}/
│   └── test/
├── Wallets/                    # такая же структура
├── Payments/                   # такая же структура
├── Fraud/                      # такая же структура
├── Notifications/              # такая же структура
└── Audit/                      # такая же структура (с заготовками тестовых проектов)
```

Каждый контекст следует Clean Architecture со следующими слоями:

- **Domain** — сущности, value objects, доменные события, без внешних зависимостей
  (ссылается только на `SharedKernel`)
- **Application** — сценарии использования, команды/запросы, DTO (ссылается на `Domain`)
- **Infrastructure** — персистентность, внешние сервисы (ссылается на `Domain`)

`SharedKernel` — самостоятельное решение, на него ссылается каждый `*.Domain`-проект.

## Основные bounded contexts
- Identity
- Wallets
- Payments
- Audit
- Fraud
- Notifications

## План развития
1. Базовая платформа
   - регистрация
   - логин
   - JWT
   - роли
   - PostgreSQL
   - миграции EF Core

2. Кошельки
   - создание кошелька
   - пополнение
   - списание
   - история операций
   - транзакции БД
   - optimistic locking

3. Переводы между пользователями
   - перевод wallet to wallet
   - комиссии
   - откат операций
   - журнал аудита

4. Асинхронная обработка
   - RabbitMQ
   - доменные события
   - Audit Service
   - Notification Service
   - Fraud Service

5. Антифрод
   - лимиты операций
   - подозрительные суммы
   - большое количество переводов
   - черный список
   - риск-скоринг

6. Платежный шлюз
   - API для магазинов
   - endpoint создания платежа
   - API Keys
   - Webhooks
   - возвраты
   - платежные ссылки

## Стек технологий
- .NET
- ASP.NET Core
- Entity Framework Core
- PostgreSQL
- RabbitMQ
- MediatR
- Docker
- GitHub Actions

## Статус
Сейчас это только стартовый каркас. Решения разделены по bounded contexts; бизнес-логика ещё не реализована.

## Лицензия
Проект распространяется под лицензией MIT. Подробнее см. файл [LICENSE](LICENSE).

## Roadmap
Подробный недельный план находится в [docs/roadmap.ru.md](docs/roadmap.ru.md).
