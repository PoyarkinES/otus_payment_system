namespace Otus.PaymentSystem.Wallets.Host.Contracts
{
    public sealed record PlaceHoldRequest(Guid TransferId, decimal Amount, Guid RecipientUserId);
}
