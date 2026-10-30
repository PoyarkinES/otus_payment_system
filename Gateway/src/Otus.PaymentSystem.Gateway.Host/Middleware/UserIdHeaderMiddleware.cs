using System.Security.Claims;

namespace Otus.PaymentSystem.Gateway.Host.Middleware;

/// <summary>
/// Убирает клиентский X-User-Id и добавляет его из проверенного JWT.
/// Внутренние сервисы доверяют этому заголовку только внутри сети.
/// </summary>
public sealed class UserIdHeaderMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-User-Id";

    public async Task InvokeAsync(HttpContext context)
    {
        // Клиент не может подменить пользователя: заголовок всегда удаляется.
        context.Request.Headers.Remove(HeaderName);

        if (context.User.Identity?.IsAuthenticated == true)
        {
            var subject = context.User.FindFirst("sub")?.Value
                          ?? context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (Guid.TryParse(subject, out var userId))
            {
                context.Request.Headers[HeaderName] = userId.ToString();
            }
        }

        await next(context);
    }
}
