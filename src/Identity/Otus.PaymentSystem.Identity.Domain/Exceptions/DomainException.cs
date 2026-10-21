namespace Otus.PaymentSystem.Identity.Domain.Exceptions;

/// <summary>
/// Базовое доменное исключение Identity-контекста.
/// Означает нарушение бизнес-правила (инварианта), а не техническую ошибку.
/// </summary>
public class DomainException : Exception
{
    public DomainException(string message) : base(message)
    {
    }

    public DomainException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
