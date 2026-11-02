using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Otus.PaymentSystem.AspNetCore.Problems;

namespace Otus.PaymentSystem.AspNetCore.Middleware;

/// <summary>
/// Ловит необработанные исключения и отдаёт 500 в формате ProblemDetails.
/// В production detail не содержит текст исключения.
/// </summary>
public sealed class UnhandledExceptionMiddleware(
    RequestDelegate next,
    IOptions<ApiProblemOptions> options,
    ILogger<UnhandledExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            if (context.Response.HasStarted)
                throw;

            logger.LogError(
                exception,
                "Unhandled exception for {Method} {Path}",
                context.Request.Method,
                context.Request.Path);

            var detail = options.Value.IncludeExceptionDetails
                ? exception.ToString()
                : "Внутренняя ошибка сервера.";

            await ApiProblems.WriteAsync(context, StatusCodes.Status500InternalServerError, detail);
        }
    }
}
