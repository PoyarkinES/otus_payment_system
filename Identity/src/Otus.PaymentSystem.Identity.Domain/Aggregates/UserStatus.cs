namespace Otus.PaymentSystem.Identity.Domain.Aggregates;

/// <summary>
/// Статус пользователя.
/// Жизненный цикл: Active -> Blocked -> Active (через Unblock).
/// </summary>
public enum UserStatus
{
    Active = 0,
    Blocked = 1
}
