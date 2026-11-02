using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace Otus.PaymentSystem.AspNetCore.Problems;

/// <summary>
/// Единый контракт ошибок API (RFC 9457 / ProblemDetails):
/// <c>type</c>, <c>title</c>, <c>status</c>, <c>detail</c>, <c>traceId</c>, <c>errors</c>.
/// Используется и Gateway, и Identity, поэтому формат ответа совпадает.
/// </summary>
public static class ApiProblems
{
    /// <summary>Content-Type всех ошибочных ответов.</summary>
    public const string ContentType = "application/problem+json; charset=utf-8";

    /// <summary>Ключ расширения с correlation ID запроса.</summary>
    public const string TraceIdExtension = "traceId";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        // errors: { "email": ["..."] }, а не { "Email": [...] } — фронтенду так удобнее.
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
        // Кириллица в detail/errors остаётся читаемой, а не \uXXXX.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>Проблема без detail: status/title/type подставляются автоматически.</summary>
    public static ProblemDetails Create(int status, string? detail = null) => new()
    {
        Status = status,
        Title = ReasonPhrases.GetReasonPhrase(status),
        Detail = detail,
        Type = TypeFor(status)
    };

    /// <summary>Ошибка валидации: 400 + errors по полям.</summary>
    public static HttpValidationProblemDetails Validation(
        IDictionary<string, string[]> errors,
        string? detail = null)
    {
        const int status = StatusCodes.Status400BadRequest;
        return new HttpValidationProblemDetails(errors)
        {
            Status = status,
            Title = ReasonPhrases.GetReasonPhrase(status),
            Detail = detail,
            Type = TypeFor(status)
        };
    }

    /// <summary>Проблема как <see cref="IResult"/> для minimal API.</summary>
    public static IResult Result(int status, string? detail = null) =>
        new ApiProblemResult(Create(status, detail));

    /// <summary>Ошибка валидации как <see cref="IResult"/> для minimal API.</summary>
    public static IResult ValidationResult(IDictionary<string, string[]> errors, string? detail = null) =>
        new ApiProblemResult(Validation(errors, detail));

    /// <summary>
    /// Пишет ProblemDetails в ответ: статус, content-type, traceId.
    /// Если ответ уже начат — ничего не делает.
    /// </summary>
    public static async Task WriteAsync(HttpContext context, ProblemDetails problem)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(problem);

        if (context.Response.HasStarted)
            return;

        problem.Status ??= StatusCodes.Status500InternalServerError;
        problem.Title ??= ReasonPhrases.GetReasonPhrase(problem.Status.Value);
        problem.Type ??= TypeFor(problem.Status.Value);
        problem.Extensions[TraceIdExtension] = CorrelationIdHeader.Get(context);

        context.Response.StatusCode = problem.Status.Value;
        context.Response.ContentType = ContentType;

        // Сериализуем по runtime-типу, иначе у ValidationProblemDetails потеряется errors.
        await JsonSerializer.SerializeAsync(
            context.Response.Body,
            problem,
            problem.GetType(),
            SerializerOptions,
            context.RequestAborted);
    }

    /// <summary>Короткая форма для middleware: статус + необязательный detail.</summary>
    public static Task WriteAsync(HttpContext context, int status, string? detail = null) =>
        WriteAsync(context, Create(status, detail));

    private static string TypeFor(int status) => $"https://httpstatuses.io/{status}";
}
