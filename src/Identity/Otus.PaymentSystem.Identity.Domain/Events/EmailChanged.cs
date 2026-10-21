using Otus.PaymentSystem.SharedKernel.Events;

namespace Otus.PaymentSystem.Identity.Domain.Events;

/// <summary>Email пользователя изменён.</summary>
public record EmailChanged(
    Guid UserId,
    string OldEmail,
    string NewEmail,
    DateTimeOffset Timestamp
) : IApplicationEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public Guid CorrelationId { get; init; } = Guid.NewGuid();
    public Guid? CausationId { get; init; }
    public string ContextName => "Identity";
    public string RoutingKey => "identity.email.changed";
}
