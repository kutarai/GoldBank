namespace UniBank.Notifications.Models;

/// <summary>
/// A notification template with placeholders for variable substitution.
/// Templates are keyed by (EventType, Channel) and can be overridden per tenant.
/// </summary>
public sealed record NotificationTemplate
{
    /// <summary>Event type name that triggers this template (e.g., "TransactionCompleted").</summary>
    public required string EventType { get; init; }

    /// <summary>Delivery channel this template targets.</summary>
    public required NotificationChannel Channel { get; init; }

    /// <summary>Title template with {placeholder} variables. Used for push notifications.</summary>
    public string? TitleTemplate { get; init; }

    /// <summary>Body template with {placeholder} variables.</summary>
    public required string BodyTemplate { get; init; }

    /// <summary>Expected variable names for documentation and validation.</summary>
    public IReadOnlyList<string> Variables { get; init; } = [];

    /// <summary>Whether this template is active and should be used for sending.</summary>
    public bool IsActive { get; init; } = true;

    /// <summary>Priority of notifications generated from this template.</summary>
    public NotificationPriority Priority { get; init; } = NotificationPriority.Normal;

    /// <summary>Tenant ID for tenant-specific overrides. Null means default/global template.</summary>
    public string? TenantId { get; init; }
}
