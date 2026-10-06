using Otus.PaymentSystem.SharedKernel.Events;

namespace Otus.PaymentSystem.Wallets.Domain.Events;

public record BalanceWithdrawn(
    Guid WalletId,
    decimal Amount,
    string Currency,
    Guid OperationId,
    DateTimeOffset Timestamp
) : IApplicationEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTimeOffset Timestamp => Timestamp;
    public Guid CorrelationId { get; init; } = Guid.NewGuid();
    public Guid? CausationId { get; init; }
    public string ContextName => "Wallets";
    public string RoutingKey => "wallet.balance.withdrawn";
}
