using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Otus.PaymentSystem.Identity.Domain.Exceptions;

namespace Otus.PaymentSystem.Identity.Host.Middleware;

/// <summary>
/// Превращает <see cref="DomainException"/> в ProblemDetails с корректным HTTP-статусом.
/// Остальные исключения пробрасываются дальше (их ловит стандартный обработчик).
/// </summary>
public class DomainExceptionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (DomainException ex)
        {
            await WriteProblemAsync(context, ex);
        }
    }

    private static Task WriteProblemAsync(HttpContext context, DomainException ex)
    {
        var status = MapStatusCode(ex.Message);
        var problem = new ProblemDetails
        {
            Status = status,
            Title = ReasonPhrases.GetReasonPhrase(status),
            Detail = ex.Message,
            Type = $"https://httpstatuses.io/{status}"
        };

        context.Response.Clear();
        context.Response.StatusCode = status;
        // Передаём contentType явно: WriteAsJsonAsync сам ставит "application/json",
        // игнорируя уже установленный Response.ContentType.
        return context.Response.WriteAsJsonAsync(
            problem,
            options: (JsonSerializerOptions?)null,
            contentType: "application/problem+json; charset=utf-8");
    }

    // Маппинг бизнес-сообщений на HTTP-статусы. Строковое сравнение — компромисс:
    // когда сообщений станет много, введём ErrorCode в DomainException.
    private static int MapStatusCode(string message) => message switch
    {
        var m when m.Contains("учётные данные", StringComparison.OrdinalIgnoreCase) => StatusCodes.Status401Unauthorized,
        var m when m.Contains("заблокирован", StringComparison.OrdinalIgnoreCase) => StatusCodes.Status403Forbidden,
        var m when m.Contains("занят", StringComparison.OrdinalIgnoreCase) => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status400BadRequest
    };

    private static class ReasonPhrases
    {
        public static string GetReasonPhrase(int status) => status switch
        {
            400 => "Bad Request",
            401 => "Unauthorized",
            403 => "Forbidden",
            409 => "Conflict",
            _ => "Error"
        };
    }
}
