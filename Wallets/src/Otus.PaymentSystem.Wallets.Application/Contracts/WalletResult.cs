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
    DateTimeOffset UpdatedAt);
