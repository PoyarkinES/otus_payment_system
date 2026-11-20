using Otus.PaymentSystem.Wallets.Domain.Aggregates;
using Otus.PaymentSystem.Wallets.Domain.Errors;
using Otus.PaymentSystem.Wallets.Domain.Exceptions;

namespace Otus.PaymentSystem.Wallets.Application.Commands;

internal static class WalletOperationGuard
{
    public static void EnsureSameOperation(
        WalletTransaction existing,
        WalletTransactionType expectedType,
        decimal amount)
    {
        if (existing.Type != expectedType || existing.Amount != amount)
        {
            throw new DomainException(
                "Idempotency-Key уже использован для другой операции.",
                DomainError.Conflict);
        }
    }
}
