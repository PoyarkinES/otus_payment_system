using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Otus.PaymentSystem.Gateway.Host.Middleware;

namespace Otus.PaymentSystem.Gateway.Host.Endpoints;

/// <summary>Ответ GET /bff/me: данные пользователя, собранные из claims access-токена.</summary>
/// <param name="UserId">Claim "sub" — идентификатор пользователя.</param>
/// <param name="Email">Claim "email".</param>
/// <param name="Role">Claim "role".</param>
public sealed record BffMeResult(Guid UserId, string Email, string Role);

/// <summary>Ответ GET /bff/health: агрегат по Gateway и его внутренним зависимостям.</summary>
/// <param name="Status">"ok", если все сервисы живы, иначе "degraded".</param>
/// <param name="Services">Статус каждого сервиса: "ok" или "unavailable".</param>
public sealed record BffHealthResult(string Status, BffServiceStatus Services);

/// <summary>Статусы сервисов в агрегированном health-ответе.</summary>
public sealed record BffServiceStatus(string Gateway, string Identity);

/// <summary>
/// Ответ GET /bff/wallets: информация о кошельках пользователя.
/// </summary>
/// <param name="Wallets">Список кошельков пользователя.</param>
/// <param name="Totals">Итоговые суммы по валютам.</param>
public sealed record BffWalletsResult(
    IReadOnlyCollection<BffWalletResult> Wallets,
    IReadOnlyCollection<BffWalletCurrencyTotalResult> Totals);

/// <summary>
/// Информация о кошельке пользователя.
/// </summary>
/// <param name="Id">Идентификатор кошелька.</param>
/// <param name="Currency">Валюта кошелька.</param>
/// <param name="Available">Доступная сумма.</param>
/// <param name="Held">Зарезервированная сумма.</param>
/// <param name="Total">Общая сумма.</param>
/// <param name="Status">Статус кошелька.</param>
/// <param name="RecentOperations">Недавние операции по кошельку.</param>
public sealed record BffWalletResult(
    Guid Id,
    string Currency,
    decimal Available,
    decimal Held,
    decimal Total,
    string Status,
    IReadOnlyCollection<BffWalletOperationResult> RecentOperations);

/// <summary>
/// Итоговые суммы по валютам.
/// </summary>
/// <param name="Currency">Валюта.</param>
/// <param name="Available">Доступная сумма.</param>
/// <param name="Held">Зарезервированная сумма.</param>
/// <param name="Total">Общая сумма.</param>
public sealed record BffWalletCurrencyTotalResult(
    string Currency,
    decimal Available,
    decimal Held,
    decimal Total);

/// <summary>
/// Информация о недавней операции по кошельку.
/// </summary>
/// <param name="Id">Идентификатор операции.</param>
/// <param name="Type">Тип операции.</param>
/// <param name="Amount">Сумма операции.</param>
/// <param name="Currency">Валюта операции.</param>
/// <param name="CreatedAt">Дата и время создания операции.</param>
/// <param name="BalanceAfter">Баланс кошелька после операции.</param>
/// <param name="BalanceBefore">Баланс кошелька до операции.</param>
/// <param name="Description">Описание операции (если есть).</param>
/// <param name="ReferenceId">Идентификатор связанной сущности (например, заказа) в системе.</param>
public sealed record BffWalletOperationResult(
    Guid Id,
    Guid? ReferenceId,
    string Type,
    decimal Amount,
    string Currency,
    decimal BalanceBefore,
    decimal BalanceAfter,
    string? Description,
    DateTimeOffset CreatedAt);

/// <summary>
/// BFF-эндпоинты для фронтенда: то, что удобно вызывать из UI, но не хочется
/// размазывать по сервисам. Публичный вход — Gateway, поэтому контракт живёт здесь.
/// </summary>
public static class BffEndpoints
{
    /// <summary>Имя HttpClient для внутренних вызовов Identity (регистрируется в Program.cs).</summary>
    public const string IdentityClientName = "identity";
    public const string WalletsClientName = "wallets";

    private const string OkStatus = "ok";
    private const string UnavailableStatus = "unavailable";
    private const int DefaultRecentOperations = 5;
    private const int MaximumRecentOperations = 20;

    public static IEndpointRouteBuilder MapBffEndpoints(this IEndpointRouteBuilder app)
    {
        // Лимит на всю группу: анонимный /bff/health дёргает Identity и без лимита
        // работает как усилитель нагрузки; отдельные лимиты по эндпоинтам обходились бы
        // через соседний маршрут.
        RouteGroupBuilder group = app.MapGroup("/bff")
            .WithTags("BFF")
            .RequireRateLimiting("bff");

        // /bff/me намеренно не ходит в Identity: sub, email и role уже есть в токене,
        // а лишний сетевой вызов на каждом запросе страницы — это latency и точка отказа.
        group.MapGet("/me", GetMe)
            .WithName("BffMe")
            .RequireAuthorization()
            .Produces<BffMeResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        // /bff/health — единственное место, где Gateway синхронно зовёт Identity.
        group.MapGet("/health", GetHealthAsync)
            .WithName("BffHealth")
            .AllowAnonymous()
            .Produces<BffHealthResult>(StatusCodes.Status200OK)
            .Produces<BffHealthResult>(StatusCodes.Status503ServiceUnavailable)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapGet("/wallets", GetWalletsAsync)
            .WithName("BffWallets")
            .WithSummary("Сводка по кошелькам пользователя")
            .WithDescription("Возвращает кошельки, суммы по валютам и последние операции. " +
                             "recentOperations по умолчанию равен 5; допустимый диапазон — 0..20.")
            .RequireAuthorization()
            .Produces<BffWalletsResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        return app;
    }

    /// <summary>
    /// Возвращает кошельки пользователя, последние операции каждого кошелька и суммы,
    /// сгруппированные по валюте. Gateway только агрегирует данные Wallets и не содержит
    /// бизнес-логики балансов.
    /// </summary>
    internal static async Task<IResult> GetWalletsAsync(
        [FromQuery, DefaultValue(DefaultRecentOperations), Range(0, MaximumRecentOperations)] int? recentOperations,
        HttpContext context,
        IHttpClientFactory httpClientFactory,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        if (!Guid.TryParse(context.User.FindFirstValue("sub"), out Guid userId))
            return Results.Unauthorized();

        int operationsCount = recentOperations ?? DefaultRecentOperations;
        if (operationsCount is < 0 or > MaximumRecentOperations)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Недопустимое количество операций.",
                detail: $"Параметр recentOperations должен быть от 0 до {MaximumRecentOperations}.");
        }

        ILogger logger = loggerFactory.CreateLogger("Bff.Wallets");
        HttpClient client = httpClientFactory.CreateClient(WalletsClientName);

        try
        {
            WalletServiceWalletResult[] wallets = await SendWalletsGetAsync<WalletServiceWalletResult[]>(
                client,
                context,
                userId,
                "wallets",
                ct);

            WalletServiceOperationResult[][] operations = operationsCount == 0
                ? wallets.Select(_ => Array.Empty<WalletServiceOperationResult>()).ToArray()
                : await Task.WhenAll(wallets.Select(wallet => GetRecentOperationsAsync(
                    client,
                    context,
                    userId,
                    wallet.Id,
                    operationsCount,
                    ct)));

            var walletResults = wallets.Select((wallet, index) => new BffWalletResult(
                wallet.Id,
                wallet.Currency,
                wallet.Available,
                wallet.Held,
                wallet.Total,
                wallet.Status,
                operations[index].Select(operation => new BffWalletOperationResult(
                    operation.Id,
                    operation.ReferenceId,
                    operation.Type,
                    operation.Amount,
                    operation.Currency,
                    operation.BalanceBefore,
                    operation.BalanceAfter,
                    operation.Description,
                    operation.CreatedAt)).ToArray())).ToArray();

            BffWalletCurrencyTotalResult[] totals = wallets
                .GroupBy(wallet => wallet.Currency, StringComparer.Ordinal)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => new BffWalletCurrencyTotalResult(
                    group.Key,
                    group.Sum(wallet => wallet.Available),
                    group.Sum(wallet => wallet.Held),
                    group.Sum(wallet => wallet.Total)))
                .ToArray();

            return Results.Ok(new BffWalletsResult(walletResults, totals));
        }
        catch (WalletsUpstreamException ex)
        {
            logger.LogWarning(ex, "Wallets вернул неуспешный статус {StatusCode} для BFF-сводки", (int)ex.StatusCode);
            return Results.Problem(
                statusCode: StatusCodes.Status502BadGateway,
                title: "Ошибка ответа сервиса кошельков.",
                detail: "Сервис кошельков не смог обработать запрос.");
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Wallets вернул некорректный ответ для BFF-сводки");
            return Results.Problem(
                statusCode: StatusCodes.Status502BadGateway,
                title: "Некорректный ответ сервиса кошельков.",
                detail: "Не удалось прочитать ответ сервиса кошельков.");
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Сервис кошельков недоступен для BFF-сводки");
            return Results.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Сервис кошельков недоступен.",
                detail: "Повторите запрос позже.");
        }
        catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Истекло время ожидания ответа сервиса кошельков для BFF-сводки");
            return Results.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Сервис кошельков не ответил вовремя.",
                detail: "Повторите запрос позже.");
        }
    }

    private static async Task<WalletServiceOperationResult[]> GetRecentOperationsAsync(
        HttpClient client,
        HttpContext context,
        Guid userId,
        Guid walletId,
        int pageSize,
        CancellationToken ct)
    {
        WalletServiceOperationsPageResult page = await SendWalletsGetAsync<WalletServiceOperationsPageResult>(
            client,
            context,
            userId,
            $"wallets/{walletId:D}/operations?page=1&pageSize={pageSize}",
            ct);

        return page.Items ?? throw new JsonException("Wallets вернул страницу операций без Items.");
    }

    private static async Task<T> SendWalletsGetAsync<T>(
        HttpClient client,
        HttpContext context,
        Guid userId,
        string path,
        CancellationToken ct)
        where T : class
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Accept.ParseAdd("application/json");

        string authorization = context.Request.Headers.Authorization.ToString();
        if (!string.IsNullOrWhiteSpace(authorization))
            request.Headers.TryAddWithoutValidation("Authorization", authorization);

        request.Headers.TryAddWithoutValidation("X-User-Id", userId.ToString("D"));

        string correlationId = context.Request.Headers[CorrelationIdMiddleware.HeaderName].ToString();
        if (!string.IsNullOrWhiteSpace(correlationId))
            request.Headers.TryAddWithoutValidation(CorrelationIdMiddleware.HeaderName, correlationId);

        using HttpResponseMessage response = await client.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
            throw new WalletsUpstreamException(response.StatusCode);

        return await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct)
               ?? throw new JsonException("Wallets вернул пустое тело ответа.");
    }

    /// <summary>
    /// Текущий пользователь из claims; без обращения к БД Identity.
    /// Токен без sub/email/role считаем испорченным: молча отдать пустую роль хуже, чем 401.
    /// </summary>
    internal static IResult GetMe(ClaimsPrincipal user)
    {
        var rawUserId = user.FindFirstValue("sub");
        var email = user.FindFirstValue("email");
        var role = user.FindFirstValue("role");

        if (!Guid.TryParse(rawUserId, out Guid userId)
            || string.IsNullOrEmpty(email)
            || string.IsNullOrEmpty(role))
        {
            return Results.Unauthorized();
        }

        return Results.Ok(new BffMeResult(userId, email, role));
    }

    /// <summary>
    /// Агрегированный health: сам Gateway (раз ответил — он жив) и Identity /health.
    /// Отдаёт 503, если хоть один сервис недоступен, — по этому коду удобно строить
    /// healthcheck контейнера и smoke-тест docker compose.
    /// </summary>
    internal static async Task<IResult> GetHealthAsync(
        HttpContext context,
        IHttpClientFactory httpClientFactory,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var identityStatus = await ProbeIdentityAsync(context, httpClientFactory, loggerFactory, ct);
        var status = identityStatus == OkStatus ? OkStatus : "degraded";

        var result = new BffHealthResult(status, new BffServiceStatus(OkStatus, identityStatus));

        return status == OkStatus
            ? Results.Ok(result)
            : Results.Json(result, statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    /// <summary>Один GET в Identity /health; любая ошибка считается недоступностью.</summary>
    private static async Task<string> ProbeIdentityAsync(
        HttpContext context,
        IHttpClientFactory httpClientFactory,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        ILogger logger = loggerFactory.CreateLogger("Bff.Health");
        HttpClient client = httpClientFactory.CreateClient(IdentityClientName);

        using var request = new HttpRequestMessage(HttpMethod.Get, "health");

        // Тот же correlation ID, что у клиента: health-вызов виден в логах Identity.
        var correlationId = context.Request.Headers[CorrelationIdMiddleware.HeaderName].ToString();
        if (!string.IsNullOrEmpty(correlationId))
            request.Headers.TryAddWithoutValidation(CorrelationIdMiddleware.HeaderName, correlationId);

        try
        {
            using HttpResponseMessage response = await client.SendAsync(request, ct);

            if (response.IsSuccessStatusCode)
                return OkStatus;

            logger.LogWarning(
                "Identity /health вернул {StatusCode}",
                (int)response.StatusCode);

            return UnavailableStatus;
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Identity /health недоступен");
            return UnavailableStatus;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // Сработал таймаут HttpClient, а не отмена запроса клиентом.
            logger.LogWarning("Identity /health не ответил вовремя");
            return UnavailableStatus;
        }
    }

    private sealed record WalletServiceWalletResult(
        Guid Id,
        string Currency,
        decimal Available,
        decimal Held,
        decimal Total,
        string Status);

    private sealed record WalletServiceOperationResult(
        Guid Id,
        Guid? ReferenceId,
        string Type,
        decimal Amount,
        string Currency,
        decimal BalanceAfter,
        decimal BalanceBefore,
        string? Description,
        DateTimeOffset CreatedAt);

    private sealed record WalletServiceOperationsPageResult(
        WalletServiceOperationResult[]? Items,
        int Page,
        int PageSize,
        int TotalCount,
        int TotalPages);

    private sealed class WalletsUpstreamException(HttpStatusCode statusCode)
        : Exception($"Wallets вернул HTTP {(int)statusCode}.")
    {
        public HttpStatusCode StatusCode { get; } = statusCode;
    }
}
