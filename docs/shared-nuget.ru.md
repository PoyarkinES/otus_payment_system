# Shared как NuGet-пакеты

> **Статус:** решение принято (ADR 0004), миграция не начата. Сейчас сервисы используют `ProjectReference`; целевое состояние — `PackageReference` на внутренние NuGet-пакеты.
>
> Связанные: [project-guide.ru.md](./project-guide.ru.md) · [ADR 0004](./adr/0004-shared-nuget.md)

## 1. Решение

`SharedKernel` и `AspNetCore` упаковываются в NuGet-пакеты. Сервисы подключают их через `PackageReference` с фиксированной версией. Монорепо остаётся: `Shared.sln` — источник пакетов и тестов, а не набор проектов внутри каждого сервисного `.sln`.

Это убирает физическую зависимость сборки сервиса от соседней папки `Shared`: сервису нужен пакет из feed, а не исходники рядом.

## 2. Пакеты

| Пакет | Проект | Кто использует | Зависимости |
|---|---|---|---|
| `Otus.PaymentSystem.SharedKernel` | `Shared/src/Otus.PaymentSystem.SharedKernel` | все `*.Domain` | нет |
| `Otus.PaymentSystem.AspNetCore` | `Shared/src/Otus.PaymentSystem.AspNetCore` | все `*.Host` | `Microsoft.AspNetCore.App`, `FluentValidation` |

Тестовые проекты не пакуются: `IsPackable=false`.

Текущее состояние:

- `AspNetCore.csproj`: `IsPackable=false` — при миграции поставить `true`.
- `SharedKernel.csproj`: метаданных пакета пока нет.
- `NuGet.config`, локального feed и pack-скрипта нет.
- Identity, Gateway и Wallets пока на `ProjectReference`.

## 3. Метаданные

В `csproj` или `Shared/Directory.Build.props`:

```xml
<PropertyGroup>
  <IsPackable>true</IsPackable>
  <PackageId>Otus.PaymentSystem.SharedKernel</PackageId>
  <Version>1.0.0</Version>
  <Authors>Otus Payment System</Authors>
  <Description>Общие примитивы домена.</Description>
  <RepositoryUrl>https://github.com/PoyarkinES/otus_payment_system</RepositoryUrl>
</PropertyGroup>
```

Для `AspNetCore` — свой `PackageId` и описание. Общую версию удобно держать в `Shared/Directory.Build.props`.

## 4. Версионирование

SemVer:

- `MAJOR` — breaking change в публичном API;
- `MINOR` — новая обратная совместимая функциональность;
- `PATCH` — исправления;
- prerelease — `1.1.0-preview.1`.

Правила:

1. Одна версия на оба пакета (проще для старта).
2. Сервисы фиксируют точную версию, без `*` в production.
3. Изменил Shared — подними версию, сделай `pack`, обнови `PackageReference`.
4. В CI можно переопределить: `-p:PackageVersion=1.2.3-ci.42`.
5. Опционально — Central Package Management (`Directory.Packages.props`).

## 5. Локальная разработка

Собрать пакеты:

```powershell
dotnet pack Shared/Otus.PaymentSystem.Shared.sln -c Release -o artifacts/packages
```

Локальный feed — `NuGet.config` в корне:

```xml
<configuration>
  <packageSources>
    <add key="local-shared" value="artifacts/packages" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
```

`artifacts/packages` не коммитим.

Цикл изменения Shared:

1. Изменить код.
2. Поднять версию.
3. `dotnet pack`.
4. Обновить `PackageReference` в сервисах.
5. `dotnet restore/build/test`.

NuGet кэширует пакеты по версии: при той же версии сервис может взять старый пакет из `~/.nuget/packages`. Правильно — поднять версию; очистка кэша — крайняя мера.

## 6. Подключение в сервисах

Было:

```xml
<ProjectReference Include="..\..\..\Shared\src\Otus.PaymentSystem.SharedKernel\Otus.PaymentSystem.SharedKernel.csproj" />
```

Стало:

```xml
<ItemGroup>
  <PackageReference Include="Otus.PaymentSystem.SharedKernel" Version="1.0.0" />
</ItemGroup>
```

Где менять:

| Проект | Пакет |
|---|---|
| `Identity.Domain` | `SharedKernel` |
| `Identity.Host` | `AspNetCore` |
| `Gateway.Host` | `AspNetCore` |
| `Wallets.Domain` | `SharedKernel` |
| `Wallets.Host` | `AspNetCore` |
| `Payments/Fraud/Notifications/Audit.Domain` | `SharedKernel` |
| их `Host` (когда появятся) | `AspNetCore` |

Правила:

- Не ставить `PrivateAssets=all` на runtime-библиотеки.
- `Shared` не добавлять в `.sln` сервиса.
- `Shared.sln` остаётся для сборки, тестов и `pack`.

## 7. CI

Целевая схема:

1. Job `pack-shared`: тесты Shared, `dotnet pack`, публикация пакетов.
2. Job'ы сервисов: restore из feed/артефакта, build/test.

Пакеты можно передавать через `actions/upload-artifact` / `download-artifact` или публиковать в GitHub Packages / Azure Artifacts.

Важно:

- версия пакета в CI должна совпадать с `PackageReference`;
- для PR — версия с суффиксом и обновление `PackageReference` в том же PR;
- для GitHub Packages — `packages: write` и `GITHUB_TOKEN`;
- кэш `~/.nuget/packages` использовать осторожно: при той же версии можно получить старый пакет.

## 8. План миграции

| Фаза | Что сделать | Проверка |
|---|---|---|
| 0 | ADR + этот документ | решение зафиксировано |
| 1 | Метаданные, `IsPackable`, `NuGet.config` | `dotnet pack` создаёт `.nupkg` |
| 2 | Identity → `PackageReference` | собирается без `ProjectReference` |
| 3 | Gateway → `PackageReference` | собирается из пакета |
| 4 | Wallets → `PackageReference` | собирается из пакета |
| 5 | Payments/Fraud/Notifications/Audit | нет `ProjectReference` на Shared |
| 6 | CI: `pack-shared` + feed/artifact | матрица зелёная |

Правила:

- один сервис — один PR; не смешивать с фичами (M2W5);
- сначала Identity, потом Gateway, потом Wallets;
- `Shared.sln` не удалять;
- в `.sln` сервисов Shared-проектов быть не должно.

## 9. Риски и меры

| Риск | Мера |
|---|---|
| Забыли поднять версию | версия в `Shared/Directory.Build.props`, checklist в PR |
| Версии разъехались | Central Package Management, обновление в одном PR |
| Локальный feed недоступен | `artifacts/packages` + `NuGet.config`; в CI — artifact |
| Медленный inner loop | pack-скрипт; при активной разработке Shared — временно ProjectReference |
| Секреты в feed | токены только в secrets; не коммитить `apiKey` |
| Breaking change без major | SemVer + ревью публичного API |

## 10. FAQ

**Монорепо и NuGet не противоречат?**
Нет. Это internal packages: пакеты собираются из того же репозитория.

**`.sln` можно упаковать?**
Нет, упаковываются проекты. `Shared.sln` — удобная точка входа для `dotnet pack`.

**Можно одновременно `ProjectReference` и `PackageReference`?**
На один проект — нет. Разные сервисы могут временно использовать разные способы.

**`dotnet pack` упакует тесты?**
Поставь `IsPackable=false` у тестовых проектов.

**Как обновить пакет с той же версией?**
Лучше поднять версию; очистка `~/.nuget/packages/otus.paymentsystem.*` — крайняя мера.

## 11. Ссылки

- [ADR 0004](./adr/0004-shared-nuget.md)
- [project-guide.ru.md](./project-guide.ru.md)
- [architecture.ru.md](./architecture.ru.md)
- [testing-strategy.ru.md](./testing-strategy.ru.md)
