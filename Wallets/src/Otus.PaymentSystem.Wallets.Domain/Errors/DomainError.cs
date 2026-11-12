namespace Otus.PaymentSystem.Wallets.Domain.Errors;

/// <summary>
/// Код доменной ошибки. HTTP-статус выбирается по коду, а не по тексту сообщения:
/// сообщение можно менять и переводить, код — часть контракта.
/// </summary>
public enum DomainError
{
    /// <summary>Некорректные входные данные доменного правила — 400.</summary>
    Validation = 0,

    /// <summary>Сущность не найдена — 404.</summary>
    NotFound = 1,

    /// <summary>Конфликт состояния (нехватка средств, повторный переход) — 409.</summary>
    Conflict = 2,

    /// <summary>Неверные учётные данные — 401.</summary>
    Unauthorized = 3,

    /// <summary>Действие запрещено — 403.</summary>
    Forbidden = 4
}
