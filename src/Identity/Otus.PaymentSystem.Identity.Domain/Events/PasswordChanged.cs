using Otus.PaymentSystem.SharedKernel.Events;

namespace Otus.PaymentSystem.Identity.Domain.Events;

public record PasswordChanged(
    Guid UserId,
    DateTimeOffset Timestamp
) : IApplicationEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTimeOffset Timestamp => Timestamp;
    public Guid CorrelationId { get; init; } = Guid.NewGuid();
    public Guid? CausationId { get; init; }
    public string ContextName => "Identity";
    public string RoutingKey => "identity.password.changed";
}
