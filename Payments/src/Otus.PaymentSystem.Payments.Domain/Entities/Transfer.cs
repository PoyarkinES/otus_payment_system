namespace Otus.PaymentSystem.Payments.Domain.Entities;

public enum TransferStatus
{
    Created,
    Held,
    Completed,
    Failed,
    Confirming
}

public enum TransferFailureReason
{
    None,
    RecipientNotFound,
    InsufficientFunds,
    FraudBlocked,
    HoldFailed,
    ConfirmFailed,
    ReleaseFailed
}

public class Transfer
{
    public Guid Id { get; private set; }
    public Guid FromUserId { get; private set; }
    public Guid ToUserId { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; }
    public TransferStatus Status { get; private set; }
    public Guid? HoldId { get; private set; }
    public TransferFailureReason FailureReason { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    // Пустой конструктор нужен для Entity Framework Core
    private Transfer()
    {
        Currency = null!;
    }

    // Основной конструктор для создания нового перевода
    public Transfer(Guid fromUserId, Guid toUserId, decimal amount, string currency)
    {
        if (amount <= 0)
            throw new ArgumentException("Сумма перевода должна быть больше нуля.", nameof(amount));

        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
            throw new ArgumentException("Валюта должна быть трехбуквенным кодом (например, RUB).", nameof(currency));

        Id = Guid.NewGuid();
        FromUserId = fromUserId;
        ToUserId = toUserId;
        Amount = amount;
        Currency = currency.ToUpperInvariant();
        Status = TransferStatus.Created;
        FailureReason = TransferFailureReason.None;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    // --- Строгий переход по статусам (State Machine) ---

    public void MarkHeld(Guid holdId)
    {
        if (Status != TransferStatus.Created)
            throw new InvalidOperationException($"Невозможно заморозить средства из статуса {Status}");

        HoldId = holdId;
        Status = TransferStatus.Held;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void MarkCompleted()
    {
        if (Status != TransferStatus.Held)
            throw new InvalidOperationException($"Невозможно завершить перевод из статуса {Status}");

        Status = TransferStatus.Completed;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void MarkFailed(TransferFailureReason reason)
    {
        if (Status == TransferStatus.Completed || Status == TransferStatus.Failed)
            throw new InvalidOperationException($"Невозможно отменить перевод из статуса {Status}");

        FailureReason = reason;
        Status = TransferStatus.Failed;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void MarkConfirming()
    {
        if (Status != TransferStatus.Held)
            throw new InvalidOperationException($"Невозможно перевести в Confirming из статуса {Status}");

        Status = TransferStatus.Confirming;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
