namespace GoldBank.Notifications.Models;

/// <summary>
/// Represents a user's notification channel preferences.
/// Determines which channels (SMS, Push, both) a user wants to receive notifications on.
/// </summary>
public sealed record NotificationPreference
{
    /// <summary>The user identifier this preference applies to.</summary>
    public required Guid UserId { get; init; }

    /// <summary>Whether the user wants to receive SMS notifications.</summary>
    public bool SmsEnabled { get; init; } = true;

    /// <summary>Whether the user wants to receive push notifications.</summary>
    public bool PushEnabled { get; init; } = true;

    /// <summary>
    /// The user's phone number for SMS delivery.
    /// Masked when included in logs. Never include full account numbers.
    /// </summary>
    public string? PhoneNumber { get; init; }

    /// <summary>
    /// The user's FCM token for push notification delivery.
    /// Null if the user has never registered a device.
    /// </summary>
    public string? FcmToken { get; init; }
}
