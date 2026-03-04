using Microsoft.Extensions.Logging;
using UniBank.Notifications.Models;
using UniBank.Notifications.Services;
using UniBank.SharedKernel.Events;
using UniBank.SharedKernel.Messaging;

namespace UniBank.Notifications.Handlers;

/// <summary>
/// Sends a low float warning to agents when their float balance drops below the threshold.
/// </summary>
public sealed class LowFloatAlertNotificationHandler : IMessageHandler<LowFloatAlert>
{
    private readonly NotificationOrchestrator _orchestrator;
    private readonly ILogger<LowFloatAlertNotificationHandler> _logger;

    public LowFloatAlertNotificationHandler(
        NotificationOrchestrator orchestrator,
        ILogger<LowFloatAlertNotificationHandler> logger)
    {
        _orchestrator = orchestrator;
        _logger = logger;
    }

    public async Task HandleAsync(LowFloatAlert message, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "Processing LowFloatAlert notification for agent {AgentId}, balance {Balance} {Currency}",
            message.AgentId,
            message.CurrentBalance,
            message.Currency);

        var variables = new Dictionary<string, string>
        {
            ["current_float"] = FormatMoney(message.CurrentBalance, message.Currency),
            ["float_limit"] = FormatMoney(message.ThresholdAmount, message.Currency),
            ["agent_code"] = message.AgentCode
        };

        await _orchestrator.SendNotificationAsync(new NotificationRequest
        {
            UserId = message.AgentId,
            TenantId = message.TenantId,
            EventType = "LowFloatAlert",
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
