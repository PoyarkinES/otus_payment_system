using Otus.PaymentSystem.Wallets.Domain.Aggregates;

namespace Otus.PaymentSystem.Wallets.Application.Contracts
{
    public sealed record PagedWalletTransactions(
        IReadOnlyCollection<WalletTransaction> Items,
        int TotalCount);
}
