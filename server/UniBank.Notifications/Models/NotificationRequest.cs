namespace UniBank.Notifications.Models;

/// <summary>
/// Represents a request to send a notification through the orchestrator.
/// Built by event handlers and processed by the <see cref="Services.NotificationOrchestrator"/>.
/// </summary>
public sealed record NotificationRequest
{
    /// <summary>Target user ID for the notification.</summary>
    public required Guid UserId { get; init; }

    /// <summary>Tenant ID for multi-tenant template resolution.</summary>
    public string? TenantId { get; init; }

    /// <summary>Domain event type name (e.g., "TransactionCompleted").</summary>
    public required string EventType { get; init; }

    /// <summary>Template variable values for substitution.</summary>
    public required IReadOnlyDictionary<string, string> Variables { get; init; }

    /// <summary>Notification priority. Critical notifications bypass rate limiting.</summary>
    public NotificationPriority Priority { get; init; } = NotificationPriority.Normal;

    /// <summary>Channels to deliver on.</summary>
    public required IReadOnlyList<NotificationChannel> Channels { get; init; }

    /// <summary>Phone number for SMS delivery (if SMS channel is requested).</summary>
    public string? PhoneNumber { get; init; }

    /// <summary>FCM token for push delivery (if push channel is requested).</summary>
    public string? FcmToken { get; init; }
}
