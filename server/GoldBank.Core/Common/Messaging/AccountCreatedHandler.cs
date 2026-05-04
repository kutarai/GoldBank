using Microsoft.Extensions.Logging;
using GoldBank.SharedKernel.Events;
using GoldBank.SharedKernel.Messaging;

namespace GoldBank.Core.Common.Messaging;

/// <summary>
/// Sample handler for the AccountCreated event.
/// Demonstrates the IMessageHandler pattern that will map directly to Wolverine handlers.
///
/// In production, this handler would trigger:
/// - Welcome notification dispatch
/// - KYC workflow initiation
/// - Default account limits setup
/// - Audit trail recording
/// </summary>
public sealed class AccountCreatedHandler : IMessageHandler<AccountCreated>
{
    private readonly ILogger<AccountCreatedHandler> _logger;

    public AccountCreatedHandler(ILogger<AccountCreatedHandler> logger)
    {
        _logger = logger;
    }

    public Task HandleAsync(AccountCreated message, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Handling AccountCreated event: AccountId={AccountId}, UserId={UserId}, " +
            "PhoneNumber={PhoneNumber}, AccountType={AccountType}, Currency={Currency}, " +
            "TenantId={TenantId}, CorrelationId={CorrelationId}",
            message.AccountId,
            message.UserId,
            message.PhoneNumber,
            message.AccountType,
            message.Currency,
            message.TenantId,
            message.CorrelationId);

        // TODO: Implement actual business logic:
        // 1. Send welcome SMS/notification via Notification service
        // 2. Initiate KYC workflow if not already completed
        // 3. Set up default transaction limits for the account type
        // 4. Record audit entry

        return Task.CompletedTask;
    }
}
