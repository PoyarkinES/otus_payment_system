namespace Otus.PaymentSystem.Identity.Host.Middleware;

/// <summary>
/// Читает X-Correlation-Id от Gateway или генерирует новый,
/// кладёт его в лог-скоуп и возвращает в ответе.
/// </summary>
public sealed class CorrelationIdMiddleware(
    RequestDelegate next,
    ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-Id";

    private const int MaxLength = 128;

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = GetOrCreate(context.Request.Headers[HeaderName].ToString());

        context.Request.Headers[HeaderName] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        using (logger.BeginScope(new Dictionary<string, object?>
               {
                   ["CorrelationId"] = correlationId
               }))
        {
            await next(context);
        }
    }

    private static string GetOrCreate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > MaxLength)
        {
            return Guid.NewGuid().ToString("N");
        }

        var isValid = value.All(static c =>
            char.IsLetterOrDigit(c) || c is '-' or '_' or '.');

        return isValid ? value : Guid.NewGuid().ToString("N");
    }
}
