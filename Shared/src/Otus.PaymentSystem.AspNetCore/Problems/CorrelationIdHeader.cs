using System.Diagnostics;
using Microsoft.AspNetCore.Http;

namespace Otus.PaymentSystem.AspNetCore.Problems;

/// <summary>
/// Имя заголовка correlation ID и безопасное чтение его значения.
/// Один заголовок на все сервисы; он же попадает в <c>traceId</c> ProblemDetails.
/// </summary>
public static class CorrelationIdHeader
{
    public const string Name = "X-Correlation-Id";

    /// <summary>
    /// Correlation ID запроса: заголовок, иначе trace текущей Activity,
    /// иначе <see cref="HttpContext.TraceIdentifier"/>. Пустой строки не возвращает.
    /// </summary>
    public static string Get(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var value = context.Request.Headers[Name].ToString();
        if (!string.IsNullOrWhiteSpace(value))
            return value;

        return Activity.Current?.Id ?? context.TraceIdentifier;
    }
}
