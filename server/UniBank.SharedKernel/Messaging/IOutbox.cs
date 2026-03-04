namespace UniBank.SharedKernel.Messaging;

/// <summary>
/// Interface for the transactional outbox pattern.
/// Messages are persisted within the same database transaction as the business state change,
/// guaranteeing that the message is only published if the transaction commits.
/// </summary>
public interface IOutbox
{
    /// <summary>
    /// Stores a message in the outbox table as part of the current unit of work.
    /// The message will be dispatched later by the OutboxProcessor.
    /// </summary>
    Task StoreAsync<T>(T message, string? tenantId = null, string? correlationId = null,
        CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Retrieves unprocessed outbox messages for dispatch, ordered by creation time.
    /// </summary>
    /// <param name="batchSize">Maximum number of messages to retrieve.</param>
    Task<IReadOnlyList<OutboxMessage>> GetPendingMessagesAsync(int batchSize = 100,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks an outbox message as successfully processed.
    /// </summary>
    Task MarkAsProcessedAsync(Guid messageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records a failed dispatch attempt, incrementing the retry count and storing the error.
    /// </summary>
    Task MarkAsFailedAsync(Guid messageId, string error, CancellationToken cancellationToken = default);
}
