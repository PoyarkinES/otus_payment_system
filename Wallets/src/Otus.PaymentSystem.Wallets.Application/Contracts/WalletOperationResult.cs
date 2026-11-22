using Otus.PaymentSystem.Wallets.Domain.Aggregates;

namespace Otus.PaymentSystem.Wallets.Application.Contracts
{
    public sealed record WalletOperationResult(
        Guid Id,
        Guid? ReferenceId,
        string Type,
        decimal Amount,
        string Currency,
        decimal BalanceAfter,
        decimal BalanceBefore,
        string? Description,
        DateTimeOffset CreatedAt)
    {
        public static WalletOperationResult From(WalletTransaction transaction)
        {
            return new WalletOperationResult(
                transaction.Id,
                transaction.ReferenceId,
                transaction.Type.ToString(),
                transaction.Amount,
                transaction.Currency.Value,
                transaction.BalanceAfter,
                transaction.BalanceBefore,
                transaction.Description,
                transaction.CreatedAt);
        }
    }
}
