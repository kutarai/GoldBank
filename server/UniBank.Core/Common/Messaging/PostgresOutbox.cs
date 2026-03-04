using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UniBank.Core.Common.Persistence;
using UniBank.SharedKernel.Messaging;

namespace UniBank.Core.Common.Messaging;

/// <summary>
/// PostgreSQL-backed implementation of the transactional outbox pattern.
/// Messages are stored in the same database transaction as the business state change,
/// ensuring exactly-once semantics between state mutation and message publication.
/// The OutboxProcessor background service polls for pending messages and dispatches them.
/// </summary>
public sealed class PostgresOutbox : IOutbox
{
    private readonly PublicDbContext _dbContext;
    private readonly ILogger<PostgresOutbox> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public PostgresOutbox(PublicDbContext dbContext, ILogger<PostgresOutbox> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task StoreAsync<T>(T message, string? tenantId = null, string? correlationId = null,
        CancellationToken cancellationToken = default) where T : class
    {
        ArgumentNullException.ThrowIfNull(message);

        var outboxMessage = new OutboxMessage
        {
            MessageType = typeof(T).AssemblyQualifiedName!,
            Payload = JsonSerializer.Serialize(message, JsonOptions),
            TenantId = tenantId,
            CorrelationId = correlationId
        };

        _dbContext.Set<OutboxMessage>().Add(outboxMessage);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogDebug(
            "Stored outbox message {MessageId} of type {MessageType} for tenant {TenantId}",
            outboxMessage.Id, typeof(T).Name, tenantId);
    }

    public async Task<IReadOnlyList<OutboxMessage>> GetPendingMessagesAsync(int batchSize = 100,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Set<OutboxMessage>()
            .Where(m => m.ProcessedAt == null && m.RetryCount < 5)
            .OrderBy(m => m.CreatedAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
    }

    public async Task MarkAsProcessedAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        await _dbContext.Set<OutboxMessage>()
            .Where(m => m.Id == messageId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(m => m.ProcessedAt, DateTime.UtcNow),
                cancellationToken);
    }

    public async Task MarkAsFailedAsync(Guid messageId, string error,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.Set<OutboxMessage>()
            .Where(m => m.Id == messageId)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(m => m.RetryCount, m => m.RetryCount + 1)
                    .SetProperty(m => m.Error, error),
                cancellationToken);

        _logger.LogWarning("Outbox message {MessageId} failed: {Error}", messageId, error);
    }
}
