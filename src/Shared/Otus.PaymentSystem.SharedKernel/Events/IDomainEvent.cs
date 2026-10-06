namespace Otus.PaymentSystem.SharedKernel.Events;

/// <summary>
/// Base interface for all domain events in the system.
/// </summary>
public interface IDomainEvent
{
    /// <summary>
    /// Unique identifier for this event instance.
    /// </summary>
    Guid EventId { get; }

    /// <summary>
    /// Timestamp when the event occurred (UTC).
    /// </summary>
    DateTimeOffset Timestamp { get; }

    /// <summary>
    /// Correlation ID to trace a request across multiple bounded contexts.
    /// </summary>
    Guid CorrelationId { get; }

    /// <summary>
    /// Causation ID — the domain event or command that caused this event.
    /// </summary>
    Guid? CausationId { get; }
}

/// <summary>
/// Marker interface for events that should be published to the message broker.
/// </summary>
public interface IApplicationEvent : IDomainEvent
{
    /// <summary>
    /// The bounded context that published this event.
    /// </summary>
    string ContextName { get; }

    /// <summary>
    /// Routing key for the message broker (e.g., "payment.transfer.completed").
    /// </summary>
    string RoutingKey { get; }
}
