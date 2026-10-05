# Otus Payment System

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
- Docker
- GitHub Actions
- мониторинг и observability

## Формат проекта
Это учебный и портфолио-проект.
Его цель — развиваться поэтапно в течение нескольких месяцев без попытки реализовать всё сразу.

## Основные bounded contexts
- Identity
- Wallet
- Payments
- Merchants
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
Сейчас это только стартовый каркас. Бизнес-логика еще не реализована.

## Лицензия
Проект распространяется под лицензией MIT. Подробнее см. файл [LICENSE](LICENSE).

## Roadmap
Подробный недельный план находится в [docs/roadmap.ru.md](docs/roadmap.ru.md).
