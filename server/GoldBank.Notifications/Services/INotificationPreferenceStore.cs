using GoldBank.Notifications.Models;

namespace GoldBank.Notifications.Services;

/// <summary>
/// Retrieves notification preferences for a user. In production this would be backed
/// by a database; for development, an in-memory default is used.
/// </summary>
public interface INotificationPreferenceStore
{
    /// <summary>
    /// Gets the notification preference for the specified user.
    /// Returns a default preference (all channels enabled) if no explicit preference exists.
    /// </summary>
    Task<NotificationPreference> GetPreferenceAsync(Guid userId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Default in-memory implementation that returns all-channels-enabled preferences.
/// Replace with a database-backed implementation when the preferences UI is built.
/// </summary>
public sealed class DefaultNotificationPreferenceStore : INotificationPreferenceStore
{
    public Task<NotificationPreference> GetPreferenceAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var preference = new NotificationPreference
        {
            UserId = userId,
            SmsEnabled = true,
            PushEnabled = true
        };

        return Task.FromResult(preference);
    }
}
