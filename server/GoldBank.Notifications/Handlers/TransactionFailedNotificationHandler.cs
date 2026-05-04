using Microsoft.Extensions.Logging;
using GoldBank.Notifications.Models;
using GoldBank.Notifications.Services;
using GoldBank.SharedKernel.Events;
using GoldBank.SharedKernel.Messaging;

namespace GoldBank.Notifications.Handlers;

/// <summary>
/// Sends an SMS and push notification when a transaction fails.
/// Includes failure reason but never includes sensitive data such as full account numbers or PINs.
/// </summary>
public sealed class TransactionFailedNotificationHandler : IMessageHandler<TransactionFailed>
{
    private readonly NotificationOrchestrator _orchestrator;
    private readonly ILogger<TransactionFailedNotificationHandler> _logger;

    public TransactionFailedNotificationHandler(
        NotificationOrchestrator orchestrator,
        ILogger<TransactionFailedNotificationHandler> logger)
    {
        _orchestrator = orchestrator;
        _logger = logger;
    }

    public async Task HandleAsync(TransactionFailed message, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Processing TransactionFailed notification for transaction {TransactionId}",
            message.TransactionId);

        var variables = new Dictionary<string, string>
        {
            ["amount"] = FormatMoney(message.Amount, message.Currency),
            ["currency"] = message.Currency,
            ["failure_reason"] = message.Reason,
            ["reference"] = message.TransactionId.ToString()[..8]
        };

        await _orchestrator.SendNotificationAsync(new NotificationRequest
        {
            UserId = message.SourceAccountId,
            TenantId = message.TenantId,
            EventType = "TransactionFailed",
            Variables = variables,
            Priority = NotificationPriority.High,
            Channels = [NotificationChannel.Sms, NotificationChannel.Push]
        }, cancellationToken);
    }

    private static string FormatMoney(decimal amount, string currency)
    {
        return currency switch
        {
            "ZWG" => $"${amount:N2}",
            "USD" => $"${amount:N2}",
            _ => $"{currency} {amount:N2}"
        };
    }
}
