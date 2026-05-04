using Microsoft.Extensions.Logging;
using GoldBank.Notifications.Models;
using GoldBank.Notifications.Services;
using GoldBank.SharedKernel.Events;
using GoldBank.SharedKernel.Messaging;

namespace GoldBank.Notifications.Handlers;

/// <summary>
/// Sends a welcome notification via SMS and push when a new account is created.
/// </summary>
public sealed class AccountCreatedNotificationHandler : IMessageHandler<AccountCreated>
{
    private readonly NotificationOrchestrator _orchestrator;
    private readonly ILogger<AccountCreatedNotificationHandler> _logger;

    public AccountCreatedNotificationHandler(
        NotificationOrchestrator orchestrator,
        ILogger<AccountCreatedNotificationHandler> logger)
    {
        _orchestrator = orchestrator;
        _logger = logger;
    }

    public async Task HandleAsync(AccountCreated message, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Processing AccountCreated notification for account {AccountId}, user {UserId}",
            message.AccountId,
            message.UserId);

        var variables = new Dictionary<string, string>
        {
            ["account_type"] = message.AccountType
        };

        await _orchestrator.SendNotificationAsync(new NotificationRequest
        {
            UserId = message.UserId,
            TenantId = message.TenantId,
            EventType = "AccountCreated",
            Variables = variables,
            Priority = NotificationPriority.Normal,
            Channels = [NotificationChannel.Sms, NotificationChannel.Push],
            PhoneNumber = message.PhoneNumber
        }, cancellationToken);
    }
}
