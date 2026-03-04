namespace UniBank.Notifications.Models;

/// <summary>
/// Tracks the delivery status of a sent notification for auditing and retry purposes.
/// </summary>
public sealed class NotificationLog
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required Guid UserId { get; init; }
    public required string EventType { get; init; }
    public required NotificationChannel Channel { get; init; }
    public string? Title { get; set; }
    public required string Body { get; set; }
    public NotificationStatus Status { get; set; } = NotificationStatus.Pending;
    public string? FailureReason { get; set; }
    public int RetryCount { get; set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? SentAt { get; set; }
    public DateTimeOffset? DeliveredAt { get; set; }
}

/// <summary>
/// Delivery status for a notification log entry.
/// </summary>
public enum NotificationStatus
{
    Pending,
    Sent,
    Delivered,
    Failed,
    Skipped
}
