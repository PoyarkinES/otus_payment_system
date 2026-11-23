namespace Otus.PaymentSystem.Wallets.Application.Contracts
{
    public sealed record WalletOperationsPageResult(
        IReadOnlyCollection<WalletOperationResult> Items,
        int Page,
        int PageSize,
        int TotalCount)
    {
        public int TotalPages => TotalCount == 0 ? 0 : (TotalCount + PageSize - 1) / PageSize;
    }
}
