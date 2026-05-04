using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using GoldBank.SharedKernel.Messaging;

namespace GoldBank.Core.Common.Messaging;

/// <summary>
/// Background service that polls the PostgreSQL outbox table for pending messages
/// and dispatches them through the in-process message bus.
///
/// Processing strategy:
/// - Polls every 5 seconds for unprocessed messages
/// - Processes messages in batches of 100
/// - Retries failed messages up to 5 times (enforced by the outbox query)
/// - Uses a dedicated DI scope per polling cycle
///
/// When WolverineFx is available, this will be replaced by Wolverine's built-in
/// durable outbox with PostgreSQL persistence.
/// </summary>
public sealed class OutboxProcessor : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OutboxProcessor> _logger;

    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(5);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public OutboxProcessor(IServiceProvider serviceProvider, ILogger<OutboxProcessor> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Outbox processor started. Polling every {Interval}s", PollingInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingMessagesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in outbox processor polling cycle");
            }

            await Task.Delay(PollingInterval, stoppingToken);
        }

        _logger.LogInformation("Outbox processor stopped");
    }

    private async Task ProcessPendingMessagesAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var outbox = scope.ServiceProvider.GetRequiredService<IOutbox>();
        var messageBus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        var pendingMessages = await outbox.GetPendingMessagesAsync(100, cancellationToken);

        if (pendingMessages.Count == 0)
            return;

        _logger.LogDebug("Processing {Count} pending outbox messages", pendingMessages.Count);

        foreach (var message in pendingMessages)
        {
            try
            {
                await DispatchOutboxMessageAsync(message, messageBus, cancellationToken);
                await outbox.MarkAsProcessedAsync(message.Id, cancellationToken);

                _logger.LogDebug(
                    "Successfully dispatched outbox message {MessageId} of type {MessageType}",
                    message.Id, message.MessageType);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to dispatch outbox message {MessageId} of type {MessageType} (attempt {RetryCount})",
                    message.Id, message.MessageType, message.RetryCount + 1);

                await outbox.MarkAsFailedAsync(message.Id, ex.Message, cancellationToken);
            }
        }
    }

    private static async Task DispatchOutboxMessageAsync(
        OutboxMessage outboxMessage, IMessageBus messageBus, CancellationToken cancellationToken)
    {
        var messageType = Type.GetType(outboxMessage.MessageType)
            ?? throw new InvalidOperationException(
                $"Cannot resolve message type: {outboxMessage.MessageType}");

        var message = JsonSerializer.Deserialize(outboxMessage.Payload, messageType, JsonOptions)
            ?? throw new InvalidOperationException(
                $"Failed to deserialize outbox message {outboxMessage.Id} as {messageType.Name}");

        // Use reflection to call PublishAsync<T> with the correct generic type argument
        var publishMethod = typeof(IMessageBus)
            .GetMethod(nameof(IMessageBus.PublishAsync))!
            .MakeGenericMethod(messageType);

        await (Task)publishMethod.Invoke(messageBus, [message, cancellationToken])!;
    }
}
