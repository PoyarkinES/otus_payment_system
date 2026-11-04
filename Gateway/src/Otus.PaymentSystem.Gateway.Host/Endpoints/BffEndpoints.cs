using System.Security.Claims;
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
/// BFF-эндпоинты для фронтенда: то, что удобно вызывать из UI, но не хочется
/// размазывать по сервисам. Публичный вход — Gateway, поэтому контракт живёт здесь.
/// </summary>
public static class BffEndpoints
{
    /// <summary>Имя HttpClient для внутренних вызовов Identity (регистрируется в Program.cs).</summary>
    public const string IdentityClientName = "identity";

    private const string OkStatus = "ok";
    private const string UnavailableStatus = "unavailable";

    public static IEndpointRouteBuilder MapBffEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/bff").WithTags("BFF");

        // /bff/me намеренно не ходит в Identity: sub, email и role уже есть в токене,
        // а лишний сетевой вызов на каждом запросе страницы — это latency и точка отказа.
        group.MapGet("/me", GetMe)
            .WithName("BffMe")
            .RequireAuthorization()
            .Produces<BffMeResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        // /bff/health — единственное место, где Gateway синхронно зовёт Identity.
        // Rate limiting здесь появится вместе с общей политикой на /bff/* (M1W4+).
        group.MapGet("/health", GetHealthAsync)
            .WithName("BffHealth")
            .AllowAnonymous()
            .Produces<BffHealthResult>(StatusCodes.Status200OK)
            .Produces<BffHealthResult>(StatusCodes.Status503ServiceUnavailable);

        return app;
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
}
