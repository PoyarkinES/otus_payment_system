namespace Otus.PaymentSystem.Wallets.Domain.Aggregates;

/// <summary>Состояние кошелька.</summary>
public enum WalletStatus
{
    /// <summary>Операции разрешены.</summary>
    Active = 0,

    /// <summary>Заморожен: операции запрещены до разморозки.</summary>
    Frozen = 1,

    /// <summary>Закрыт: операции запрещены навсегда.</summary>
    Closed = 2
}
