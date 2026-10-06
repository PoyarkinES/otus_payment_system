using Otus.PaymentSystem.SharedKernel.Events;

namespace Otus.PaymentSystem.Fraud.Domain.Events;

public record TransferBlockedByFraud(
    Guid TransferId,
    Guid RuleId,
    string Reason,
    DateTimeOffset Timestamp
) : IApplicationEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTimeOffset Timestamp => Timestamp;
    public Guid CorrelationId { get; init; } = Guid.NewGuid();
    public Guid? CausationId { get; init; }
    public string ContextName => "Fraud";
    public string RoutingKey => "fraud.transfer.blocked";
}
