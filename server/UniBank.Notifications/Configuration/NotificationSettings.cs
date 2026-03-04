namespace UniBank.Notifications.Configuration;

/// <summary>
/// Configuration settings for the Notification Service.
/// Bound from the "Notifications" section of appsettings.json.
/// </summary>
public sealed class NotificationSettings
{
    public const string SectionName = "Notifications";

    /// <summary>Maximum notifications per user per hour. Default: 10. Critical events bypass this limit.</summary>
    public int MaxNotificationsPerUserPerHour { get; set; } = 10;

    /// <summary>Maximum number of retry attempts for failed notification deliveries.</summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>Retry delay intervals in seconds, indexed by retry count (0-based).</summary>
    public int[] RetryDelaySeconds { get; set; } = [30, 120, 600];
}
