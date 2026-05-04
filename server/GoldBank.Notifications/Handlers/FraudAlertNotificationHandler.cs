using Microsoft.Extensions.Logging;
using GoldBank.Notifications.Models;
using GoldBank.Notifications.Services;
using GoldBank.SharedKernel.Events;
using GoldBank.SharedKernel.Messaging;

namespace GoldBank.Notifications.Handlers;

/// <summary>
/// Sends urgent fraud alert notifications via both SMS and push.
/// Fraud alerts use Critical priority to bypass rate limiting, ensuring
/// the user is always notified of suspicious activity.
/// </summary>
public sealed class FraudAlertNotificationHandler : IMessageHandler<FraudAlertRaised>
{
    private readonly NotificationOrchestrator _orchestrator;
    private readonly ILogger<FraudAlertNotificationHandler> _logger;

    public FraudAlertNotificationHandler(
        NotificationOrchestrator orchestrator,
        ILogger<FraudAlertNotificationHandler> logger)
    {
        _orchestrator = orchestrator;
        _logger = logger;
    }

    public async Task HandleAsync(FraudAlertRaised message, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "Processing FraudAlertRaised notification for account {AccountId}, severity {Severity}",
            message.AccountId,
            message.Severity);

        var variables = new Dictionary<string, string>
        {
            ["alert_type"] = message.AlertType,
            ["description"] = message.Description,
            ["severity"] = message.Severity
        };

        await _orchestrator.SendNotificationAsync(new NotificationRequest
        {
            UserId = message.AccountId,
            TenantId = message.TenantId,
            EventType = "FraudAlertRaised",
            Variables = variables,
            Priority = NotificationPriority.Critical,
            Channels = [NotificationChannel.Sms, NotificationChannel.Push]
        }, cancellationToken);
    }
}
