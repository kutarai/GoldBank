using Microsoft.Extensions.Logging;
using GoldBank.Notifications.Models;
using GoldBank.Notifications.Services;
using GoldBank.SharedKernel.Events;
using GoldBank.SharedKernel.Messaging;

namespace GoldBank.Notifications.Handlers;

/// <summary>
/// Sends a transaction receipt via SMS and push notification when a transaction completes.
/// Never includes full account numbers in the notification body.
/// </summary>
public sealed class TransactionCompletedNotificationHandler : IMessageHandler<TransactionCompleted>
{
    private readonly NotificationOrchestrator _orchestrator;
    private readonly ILogger<TransactionCompletedNotificationHandler> _logger;

    public TransactionCompletedNotificationHandler(
        NotificationOrchestrator orchestrator,
        ILogger<TransactionCompletedNotificationHandler> logger)
    {
        _orchestrator = orchestrator;
        _logger = logger;
    }

    public async Task HandleAsync(TransactionCompleted message, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Processing TransactionCompleted notification for transaction {TransactionId}",
            message.TransactionId);

        var variables = new Dictionary<string, string>
        {
            ["amount"] = FormatMoney(message.Amount, message.Currency),
            ["currency"] = message.Currency,
            ["transaction_type"] = FormatTransactionType(message.TransactionType),
            ["reference"] = message.ReferenceNumber ?? "N/A",
            ["counterparty_name"] = "recipient",
            ["balance"] = "***"
        };

        await _orchestrator.SendNotificationAsync(new NotificationRequest
        {
            UserId = message.SourceAccountId,
            TenantId = message.TenantId,
            EventType = "TransactionCompleted",
            Variables = variables,
            Priority = NotificationPriority.Normal,
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

    private static string FormatTransactionType(string type) => type switch
    {
        "p2p_send" => "Sent",
        "p2p_receive" => "Received",
        "cash_in" => "Cash In",
        "cash_out" => "Cash Out",
        "payment_nfc" => "NFC Payment",
        "payment_qr" => "QR Payment",
        "bill_payment" => "Bill Payment",
        _ => type
    };
}
