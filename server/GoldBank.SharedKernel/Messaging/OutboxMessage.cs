namespace GoldBank.SharedKernel.Messaging;

/// <summary>
/// Represents a persisted outbox message for reliable at-least-once delivery.
/// Messages are written to PostgreSQL as part of the same transaction that modifies business state,
/// then dispatched asynchronously by the OutboxProcessor background service.
/// </summary>
public class OutboxMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// The assembly-qualified type name of the message, used for deserialization.
    /// </summary>
    public required string MessageType { get; set; }

    /// <summary>
    /// The JSON-serialized message payload.
    /// </summary>
    public required string Payload { get; set; }

    /// <summary>
    /// Tenant context for multi-tenant message routing.
    /// </summary>
    public string? TenantId { get; set; }

    /// <summary>
    /// Correlation identifier for distributed tracing across services.
    /// </summary>
    public string? CorrelationId { get; set; }

    /// <summary>
    /// When the outbox message was created (written to the database).
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the message was successfully dispatched to handlers.
    /// Null indicates the message has not yet been processed.
    /// </summary>
    public DateTime? ProcessedAt { get; set; }

    /// <summary>
    /// The number of dispatch attempts. Used for retry logic and dead-letter decisions.
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    /// The error message from the most recent failed dispatch attempt, if any.
    /// </summary>
    public string? Error { get; set; }
}
