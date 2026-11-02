namespace Otus.PaymentSystem.AspNetCore.Problems;

/// <summary>Настройки единого формата ошибок (ProblemDetails).</summary>
public sealed class ApiProblemOptions
{
    /// <summary>
    /// Добавлять ли текст исключения в <c>detail</c> ответа 500.
    /// Включать только в Development: в production detail не раскрывает внутренние детали.
    /// </summary>
    public bool IncludeExceptionDetails { get; set; }
}
