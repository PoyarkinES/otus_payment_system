using Otus.PaymentSystem.SharedKernel.Events;

namespace Otus.PaymentSystem.Fraud.Domain.Events;

public record FraudAlertRaised(
    Guid AlertId,
    Guid? TransferId,
    Guid UserId,
    Guid RuleId,
    string Severity,
    DateTimeOffset Timestamp
) : IApplicationEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTimeOffset Timestamp => Timestamp;
    public Guid CorrelationId { get; init; } = Guid.NewGuid();
    public Guid? CausationId { get; init; }
    public string ContextName => "Fraud";
    public string RoutingKey => "fraud.alert.raised";
}
