namespace GoldBank.Notifications.Services;

/// <summary>
/// Abstraction for push notification delivery. Implementations may use Firebase Cloud
/// Messaging (FCM), Apple Push Notification Service (APNs), or a console stub for development.
/// </summary>
public interface IPushNotificationProvider
{
    /// <summary>
    /// Sends a push notification to the device identified by the given token.
    /// </summary>
    /// <param name="deviceToken">FCM or platform-specific device token.</param>
    /// <param name="title">Notification title.</param>
    /// <param name="body">Notification body text.</param>
    /// <param name="data">Optional key-value data payload for the app to process.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the notification was accepted for delivery; false otherwise.</returns>
    Task<bool> SendAsync(
        string deviceToken,
        string title,
        string body,
        IReadOnlyDictionary<string, string>? data = null,
        CancellationToken cancellationToken = default);
}
