namespace Otus.PaymentSystem.Wallets.Domain.Aggregates;

/// <summary>Состояние холда. Переходы только из Active.</summary>
public enum HoldStatus
{
    /// <summary>Деньги заморожены, решение ещё не принято.</summary>
    Active = 0,

    /// <summary>Исполнен: деньги ушли получателю.</summary>
    Completed = 1,

    /// <summary>Снят: деньги вернулись в Available.</summary>
    Released = 2,

    /// <summary>Истёк по времени (в MVP не используется).</summary>
    Expired = 3
}
