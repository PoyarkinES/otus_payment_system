using Otus.PaymentSystem.SharedKernel.Events;

namespace Otus.PaymentSystem.Payments.Domain.Events;

public record TransferCompleted(
    Guid TransferId,
    Guid SourceWalletId,
    Guid DestinationWalletId,
    decimal Amount,
    string Currency,
    DateTimeOffset Timestamp
) : IApplicationEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTimeOffset Timestamp => Timestamp;
    public Guid CorrelationId { get; init; } = Guid.NewGuid();
    public Guid? CausationId { get; init; }
    public string ContextName => "Payments";
    public string RoutingKey => "payment.transfer.completed";
}
