using Microsoft.Extensions.Logging;
using GoldBank.Notifications.Models;
using GoldBank.Notifications.Services;
using GoldBank.SharedKernel.Events;
using GoldBank.SharedKernel.Messaging;

namespace GoldBank.Notifications.Handlers;

/// <summary>
/// Sends a notification when a user's KYC verification is rejected,
/// prompting them to resubmit their documents.
/// </summary>
public sealed class KYCRejectedNotificationHandler : IMessageHandler<KYCRejected>
{
    private readonly NotificationOrchestrator _orchestrator;
    private readonly ILogger<KYCRejectedNotificationHandler> _logger;

    public KYCRejectedNotificationHandler(
        NotificationOrchestrator orchestrator,
        ILogger<KYCRejectedNotificationHandler> logger)
    {
        _orchestrator = orchestrator;
        _logger = logger;
    }

    public async Task HandleAsync(KYCRejected message, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Processing KYCRejected notification for user {UserId}",
            message.UserId);

        var variables = new Dictionary<string, string>
        {
            ["rejection_reason"] = message.Reason,
            ["rejected_field"] = message.RejectedField ?? "document"
        };

        await _orchestrator.SendNotificationAsync(new NotificationRequest
        {
            UserId = message.UserId,
            TenantId = message.TenantId,
            EventType = "KYCRejected",
            Variables = variables,
            Priority = NotificationPriority.Normal,
            Channels = [NotificationChannel.Sms, NotificationChannel.Push]
        }, cancellationToken);
    }
}
