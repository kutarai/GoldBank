using UniBank.Notifications.Models;

namespace UniBank.Notifications.Services;

/// <summary>
/// Retrieves notification templates by event type and channel.
/// In production this would be backed by a database; for development, an in-memory
/// seed store provides the default templates from STORY-073.
/// </summary>
public interface INotificationTemplateStore
{
    /// <summary>
    /// Gets the active template for the given event type and channel.
    /// Returns a tenant-specific override if one exists; otherwise returns the global default.
    /// </summary>
    Task<NotificationTemplate?> GetTemplateAsync(
        string eventType,
        NotificationChannel channel,
        string? tenantId = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// In-memory template store seeded with the default notification templates from STORY-073.
/// Replace with a database-backed implementation in production.
/// </summary>
public sealed class InMemoryNotificationTemplateStore : INotificationTemplateStore
{
    private static readonly List<NotificationTemplate> Templates =
    [
        // TransactionCompleted
        new NotificationTemplate
        {
            EventType = "TransactionCompleted",
            Channel = NotificationChannel.Push,
            TitleTemplate = "{transaction_type} {currency} {amount}",
            BodyTemplate = "You {transaction_type} {amount} {counterparty_name}. Balance: {balance}. Ref: {reference}",
            Variables = ["transaction_type", "currency", "amount", "counterparty_name", "balance", "reference"]
        },
        new NotificationTemplate
        {
            EventType = "TransactionCompleted",
            Channel = NotificationChannel.Sms,
            BodyTemplate = "UniBank: {transaction_type} {amount} {counterparty_name}. Bal: {balance}. Ref: {reference}",
            Variables = ["transaction_type", "amount", "counterparty_name", "balance", "reference"]
        },

        // TransactionFailed
        new NotificationTemplate
        {
            EventType = "TransactionFailed",
            Channel = NotificationChannel.Push,
            TitleTemplate = "Transaction Failed",
            BodyTemplate = "Your transaction of {amount} failed. Reason: {failure_reason}. Ref: {reference}",
            Variables = ["amount", "failure_reason", "reference"]
        },
        new NotificationTemplate
        {
            EventType = "TransactionFailed",
            Channel = NotificationChannel.Sms,
            BodyTemplate = "UniBank: Transaction {amount} failed. {failure_reason}. Ref: {reference}",
            Variables = ["amount", "failure_reason", "reference"]
        },

        // UserRegistered
        new NotificationTemplate
        {
            EventType = "UserRegistered",
            Channel = NotificationChannel.Sms,
            BodyTemplate = "Welcome to UniBank, {name}! Your account is ready. Complete KYC to unlock all features.",
            Variables = ["name"]
        },

        // AccountCreated
        new NotificationTemplate
        {
            EventType = "AccountCreated",
            Channel = NotificationChannel.Push,
            TitleTemplate = "Welcome to UniBank",
            BodyTemplate = "Your {account_type} account has been created. Complete KYC to unlock all features.",
            Variables = ["account_type"]
        },
        new NotificationTemplate
        {
            EventType = "AccountCreated",
            Channel = NotificationChannel.Sms,
            BodyTemplate = "Welcome to UniBank! Your {account_type} account is ready. Complete KYC to unlock all features.",
            Variables = ["account_type"]
        },

        // KYCApproved
        new NotificationTemplate
        {
            EventType = "KYCApproved",
            Channel = NotificationChannel.Push,
            TitleTemplate = "KYC Approved",
            BodyTemplate = "Your identity has been verified. You now have {kyc_level} access.",
            Variables = ["kyc_level"]
        },
        new NotificationTemplate
        {
            EventType = "KYCApproved",
            Channel = NotificationChannel.Sms,
            BodyTemplate = "UniBank: KYC approved. {kyc_level} access granted.",
            Variables = ["kyc_level"]
        },

        // KYCRejected
        new NotificationTemplate
        {
            EventType = "KYCRejected",
            Channel = NotificationChannel.Push,
            TitleTemplate = "KYC Review Update",
            BodyTemplate = "Your verification was not accepted. Reason: {rejection_reason}. Please resubmit.",
            Variables = ["rejection_reason"]
        },
        new NotificationTemplate
        {
            EventType = "KYCRejected",
            Channel = NotificationChannel.Sms,
            BodyTemplate = "UniBank: Your verification was not accepted. Please resubmit in the app.",
            Variables = ["rejection_reason"]
        },

        // FraudAlertRaised
        new NotificationTemplate
        {
            EventType = "FraudAlertRaised",
            Channel = NotificationChannel.Push,
            TitleTemplate = "Security Alert",
            BodyTemplate = "Unusual activity detected on your account. {description}. Contact support if unauthorized.",
            Variables = ["description"],
            Priority = NotificationPriority.Critical
        },
        new NotificationTemplate
        {
            EventType = "FraudAlertRaised",
            Channel = NotificationChannel.Sms,
            BodyTemplate = "ALERT: Unusual activity on your UniBank account. {description}. Call support immediately if not you.",
            Variables = ["description"],
            Priority = NotificationPriority.Critical
        },

        // LowFloatAlert
        new NotificationTemplate
        {
            EventType = "LowFloatAlert",
            Channel = NotificationChannel.Push,
            TitleTemplate = "Low Float Warning",
            BodyTemplate = "Your float balance is {current_float}. Limit: {float_limit}. Please top up.",
            Variables = ["current_float", "float_limit"]
        },
        new NotificationTemplate
        {
            EventType = "LowFloatAlert",
            Channel = NotificationChannel.Sms,
            BodyTemplate = "UniBank Agent: Low float ({current_float}/{float_limit}). Please top up.",
            Variables = ["current_float", "float_limit"]
        }
    ];

    public Task<NotificationTemplate?> GetTemplateAsync(
        string eventType,
        NotificationChannel channel,
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        // First try tenant-specific override, then fall back to global default
        var template = Templates.FirstOrDefault(t =>
            t.EventType == eventType &&
            t.Channel == channel &&
            t.TenantId == tenantId &&
            t.IsActive);

        template ??= Templates.FirstOrDefault(t =>
            t.EventType == eventType &&
            t.Channel == channel &&
            t.TenantId == null &&
            t.IsActive);

        return Task.FromResult(template);
    }
}
