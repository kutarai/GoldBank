using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using GoldBank.SharedKernel.Events;
using GoldBank.SharedKernel.Messaging;

namespace GoldBank.Core.Modules.Payments.Application.Handlers;

/// <summary>
/// Handles payment events and routes notifications to subscribed gRPC streams (STORY-025, STORY-028).
/// Maintains per-account channels that gRPC streaming RPCs can read from.
/// Implements IMessageHandler to receive TransactionCompleted events from the message bus.
/// </summary>
public sealed class PaymentNotificationHandler : IMessageHandler<TransactionCompleted>, IDisposable
{
    private readonly ConcurrentDictionary<Guid, Channel<PaymentNotificationData>> _subscriptions = new();
    private readonly ILogger<PaymentNotificationHandler> _logger;
    private bool _disposed;

    public PaymentNotificationHandler(ILogger<PaymentNotificationHandler> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Handles incoming TransactionCompleted events and routes notifications
    /// to all relevant account subscribers (source and destination).
    /// </summary>
    public async Task HandleAsync(TransactionCompleted message, CancellationToken cancellationToken = default)
    {
        var notification = new PaymentNotificationData(
            NotificationId: Guid.NewGuid().ToString(),
            TransactionId: message.TransactionId.ToString(),
            Type: message.TransactionType,
            Title: FormatTitle(message.TransactionType, message.Amount),
            Body: FormatBody(message.TransactionType, message.Amount, message.Currency, message.ReferenceNumber),
            Amount: message.Amount,
            Currency: message.Currency,
            Status: "completed",
            Reference: message.ReferenceNumber ?? string.Empty,
            CreatedAt: message.OccurredOn);

        // Notify source account
        await TryWriteToSubscriberAsync(message.SourceAccountId, notification, cancellationToken);

        // Notify destination account if applicable
        if (message.DestinationAccountId.HasValue)
        {
            await TryWriteToSubscriberAsync(message.DestinationAccountId.Value, notification, cancellationToken);
        }
    }

    /// <summary>
    /// Subscribes an account to receive payment notifications via a channel reader.
    /// Returns a ChannelReader that the gRPC streaming RPC reads from.
    /// </summary>
    public ChannelReader<PaymentNotificationData> Subscribe(Guid accountId)
    {
        var channel = Channel.CreateBounded<PaymentNotificationData>(
            new BoundedChannelOptions(100)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false
            });

        _subscriptions.AddOrUpdate(accountId, channel, (_, existing) =>
        {
            // Close old channel if re-subscribing
            existing.Writer.TryComplete();
            return channel;
        });

        _logger.LogInformation("Account {AccountId} subscribed to payment notifications", accountId);
        return channel.Reader;
    }

    /// <summary>
    /// Unsubscribes an account from payment notifications and completes the channel.
    /// </summary>
    public void Unsubscribe(Guid accountId)
    {
        if (_subscriptions.TryRemove(accountId, out var channel))
        {
            channel.Writer.TryComplete();
            _logger.LogInformation("Account {AccountId} unsubscribed from payment notifications", accountId);
        }
    }

    private async Task TryWriteToSubscriberAsync(
        Guid accountId, PaymentNotificationData notification, CancellationToken cancellationToken)
    {
        if (_subscriptions.TryGetValue(accountId, out var channel))
        {
            try
            {
                await channel.Writer.WriteAsync(notification, cancellationToken);
            }
            catch (ChannelClosedException)
            {
                _subscriptions.TryRemove(accountId, out _);
            }
            catch (OperationCanceledException)
            {
                // Stream was cancelled, clean up
                _subscriptions.TryRemove(accountId, out _);
            }
        }
    }

    private static string FormatTitle(string transactionType, decimal amount)
    {
        return transactionType switch
        {
            "nfc_payment" => $"NFC Payment Sent - {amount:F2}",
            "nfc_receipt" => $"NFC Payment Received - {amount:F2}",
            "qr_payment" => $"QR Payment Sent - {amount:F2}",
            "qr_receipt" => $"QR Payment Received - {amount:F2}",
            _ => $"Payment - {amount:F2}",
        };
    }

    private static string FormatBody(string transactionType, decimal amount, string currency, string? reference)
    {
        var direction = transactionType.Contains("receipt", StringComparison.OrdinalIgnoreCase)
            ? "received"
            : "sent";

        var refText = !string.IsNullOrEmpty(reference)
            ? $" Ref: {reference}"
            : string.Empty;

        return $"Payment of {amount:F2} {currency} {direction}.{refText}";
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        foreach (var kvp in _subscriptions)
        {
            kvp.Value.Writer.TryComplete();
        }
        _subscriptions.Clear();
    }
}

/// <summary>
/// Data structure for payment notifications sent via gRPC streaming.
/// </summary>
public sealed record PaymentNotificationData(
    string NotificationId,
    string TransactionId,
    string Type,
    string Title,
    string Body,
    decimal Amount,
    string Currency,
    string Status,
    string Reference,
    DateTime CreatedAt);
