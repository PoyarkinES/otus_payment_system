using Otus.PaymentSystem.Wallets.Domain.Aggregates;

namespace Otus.PaymentSystem.Wallets.Application.Contracts;

public sealed record WalletResult(
    Guid Id,
    Guid UserId,
    string Currency,
    decimal Available,
    decimal Held,
    decimal Total,
    string Status,
    int Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{

    public static WalletResult From(Wallet wallet)
    {
        return new(
            wallet.Id,
            wallet.UserId,
            wallet.Currency.Value,
            wallet.Available,
            wallet.Held,
            wallet.Total,
            wallet.Status.ToString(),
            wallet.Version,
            wallet.CreatedAt,
            wallet.UpdatedAt);
    }
}

