namespace Otus.PaymentSystem.Wallets.Domain.Aggregates;

/// <summary>
/// Тип операции в ledger. Значения фиксированы порядком из db-schema:
/// менять нельзя — в БД хранится integer.
/// </summary>
public enum WalletTransactionType
{
    TopUp = 0,
    Withdrawal = 1,
    TransferIn = 2,
    TransferOut = 3,
    Fee = 4,
    Refund = 5
}
