using Otus.PaymentSystem.Identity.Domain.Errors;

namespace Otus.PaymentSystem.Identity.Domain.Exceptions;

/// <summary>
/// Базовое доменное исключение Identity-контекста.
/// Означает нарушение бизнес-правила (инварианта), а не техническую ошибку.
/// <see cref="Error"/> задаёт HTTP-статус в ProblemDetails.
/// </summary>
public class DomainException : Exception
{
    public DomainException(string message, DomainError error = DomainError.Validation) : base(message)
    {
        Error = error;
    }

    public DomainException(string message, Exception innerException, DomainError error = DomainError.Validation)
        : base(message, innerException)
    {
        Error = error;
    }

    /// <summary>Код ошибки для маппинга на HTTP-статус.</summary>
    public DomainError Error { get; }
}
