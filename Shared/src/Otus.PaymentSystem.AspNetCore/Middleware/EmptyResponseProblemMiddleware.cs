using Microsoft.AspNetCore.Http;
using Otus.PaymentSystem.AspNetCore.Problems;

namespace Otus.PaymentSystem.AspNetCore.Middleware;

/// <summary>
/// Заполняет ProblemDetails'ом ошибочные ответы с пустым телом:
/// challenge от JwtBearer (401), запрет (403), 404 маршрутизации, 502/504 YARP.
/// Ответы, у которых уже есть тело или content-type, не трогает.
/// </summary>
public sealed class EmptyResponseProblemMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        await next(context);

        if (context.Response.HasStarted)
            return;

        if (context.Response.StatusCode < 400)
            return;

        if (context.Response.ContentLength is > 0)
            return;

        if (!string.IsNullOrEmpty(context.Response.ContentType))
            return;

        await ApiProblems.WriteAsync(context, context.Response.StatusCode);
    }
}
