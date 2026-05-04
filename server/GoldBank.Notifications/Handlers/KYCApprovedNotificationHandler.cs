using Microsoft.Extensions.Logging;
using GoldBank.Notifications.Models;
using GoldBank.Notifications.Services;
using GoldBank.SharedKernel.Events;
using GoldBank.SharedKernel.Messaging;

namespace GoldBank.Notifications.Handlers;

/// <summary>
/// Sends a KYC approval notification via SMS and push when a user's identity is verified.
/// </summary>
public sealed class KYCApprovedNotificationHandler : IMessageHandler<KYCApproved>
{
    private readonly NotificationOrchestrator _orchestrator;
    private readonly ILogger<KYCApprovedNotificationHandler> _logger;

    public KYCApprovedNotificationHandler(
        NotificationOrchestrator orchestrator,
        ILogger<KYCApprovedNotificationHandler> logger)
    {
        _orchestrator = orchestrator;
        _logger = logger;
    }

    public async Task HandleAsync(KYCApproved message, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Processing KYCApproved notification for user {UserId}",
            message.UserId);

        var variables = new Dictionary<string, string>
        {
            ["kyc_level"] = message.VerificationLevel
        };

        await _orchestrator.SendNotificationAsync(new NotificationRequest
        {
            UserId = message.UserId,
            TenantId = message.TenantId,
            EventType = "KYCApproved",
            Variables = variables,
            Priority = NotificationPriority.Normal,
            Channels = [NotificationChannel.Sms, NotificationChannel.Push]
        }, cancellationToken);
    }
}
