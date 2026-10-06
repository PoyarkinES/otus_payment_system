using Otus.PaymentSystem.SharedKernel.Events;

namespace Otus.PaymentSystem.Payments.Domain.Events;

public record PaymentRefunded(
    Guid PaymentId,
    decimal RefundAmount,
    DateTimeOffset Timestamp
) : IApplicationEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTimeOffset Timestamp => Timestamp;
    public Guid CorrelationId { get; init; } = Guid.NewGuid();
    public Guid? CausationId { get; init; }
    public string ContextName => "Payments";
    public string RoutingKey => "payment.payment.refunded";
}
