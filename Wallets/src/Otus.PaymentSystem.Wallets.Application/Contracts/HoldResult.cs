using Otus.PaymentSystem.Wallets.Domain.Aggregates;
using Otus.PaymentSystem.Wallets.Domain.ValueObjects;

namespace Otus.PaymentSystem.Wallets.Application.Contracts
{
    public sealed record HoldResult(Guid Id,
        Guid WalletId,
        Guid TransferId,
        Guid UserId,
        Guid RecipientUserId,
        decimal Amount,
        string Currency,
        string Status,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt)
    {
        public static HoldResult From(Hold hold, Currency currency)
        {
            return new HoldResult(
                hold.Id,
                hold.WalletId,
                hold.TransferId,
                hold.UserId,
                hold.RecipientUserId,
                hold.Amount,
                currency.Value,
                hold.Status.ToString(),
                hold.CreatedAt,
                hold.UpdatedAt
            );
        }
    }
}
