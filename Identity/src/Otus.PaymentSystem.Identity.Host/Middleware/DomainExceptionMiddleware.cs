using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Otus.PaymentSystem.AspNetCore.Problems;
using Otus.PaymentSystem.Identity.Domain.Errors;
using Otus.PaymentSystem.Identity.Domain.Exceptions;

namespace Otus.PaymentSystem.Identity.Host.Middleware;

/// <summary>
/// Превращает <see cref="DomainException"/> в ProblemDetails по коду <see cref="DomainException.Error"/>.
/// Остальные исключения не ловит — ими занимается UnhandledExceptionMiddleware.
/// </summary>
public class DomainExceptionMiddleware(RequestDelegate next, ILogger<DomainExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (DomainException exception)
        {
            if (context.Response.HasStarted)
                throw;

            logger.LogWarning(
                "Domain rule violated: {ErrorCode} {Message}",
                exception.Error,
                exception.Message);

            await ApiProblems.WriteAsync(context, MapStatus(exception.Error), exception.Message);
        }
    }

    private static int MapStatus(DomainError error) => error switch
    {
        DomainError.Unauthorized => StatusCodes.Status401Unauthorized,
        DomainError.Forbidden => StatusCodes.Status403Forbidden,
        DomainError.NotFound => StatusCodes.Status404NotFound,
        DomainError.Conflict => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status400BadRequest
    };
}
