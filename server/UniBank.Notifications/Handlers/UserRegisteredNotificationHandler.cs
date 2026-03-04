using Microsoft.Extensions.Logging;
using UniBank.Notifications.Models;
using UniBank.Notifications.Services;
using UniBank.SharedKernel.Events;
using UniBank.SharedKernel.Messaging;

namespace UniBank.Notifications.Handlers;

/// <summary>
/// Sends a welcome SMS when a new user registers.
/// Push is not sent because the user has not yet registered an FCM token.
/// </summary>
public sealed class UserRegisteredNotificationHandler : IMessageHandler<UserRegistered>
{
    private readonly NotificationOrchestrator _orchestrator;
    private readonly ILogger<UserRegisteredNotificationHandler> _logger;

    public UserRegisteredNotificationHandler(
        NotificationOrchestrator orchestrator,
        ILogger<UserRegisteredNotificationHandler> logger)
    {
        _orchestrator = orchestrator;
        _logger = logger;
    }

    public async Task HandleAsync(UserRegistered message, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Processing UserRegistered notification for user {UserId}",
            message.UserId);

        var variables = new Dictionary<string, string>
        {
            ["name"] = message.FirstName
        };

        await _orchestrator.SendNotificationAsync(new NotificationRequest
        {
            UserId = message.UserId,
            TenantId = message.TenantId,
            EventType = "UserRegistered",
            Variables = variables,
            Priority = NotificationPriority.Normal,
            Channels = [NotificationChannel.Sms],
            PhoneNumber = message.PhoneNumber
        }, cancellationToken);
    }
}
