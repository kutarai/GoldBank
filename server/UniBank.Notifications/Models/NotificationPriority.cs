namespace UniBank.Notifications.Models;

/// <summary>
/// Priority level for a notification. Critical notifications bypass rate limiting.
/// </summary>
public enum NotificationPriority
{
    Low,
    Normal,
    High,
    Critical
}
