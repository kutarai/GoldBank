using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UniBank.Notifications.Configuration;

namespace UniBank.Notifications.Services;

/// <summary>
/// In-memory sliding-window rate limiter that enforces a maximum number of notifications
/// per user per hour. In production, this should be backed by Redis for distributed operation.
/// Critical-priority notifications bypass rate limiting entirely.
/// </summary>
public sealed class NotificationRateLimiter
{
    private readonly ConcurrentDictionary<Guid, TimestampWindow> _windows = new();
    private readonly ILogger<NotificationRateLimiter> _logger;
    private readonly int _maxPerHour;

    public NotificationRateLimiter(
        IOptions<NotificationSettings> settings,
        ILogger<NotificationRateLimiter> logger)
    {
        _logger = logger;
        _maxPerHour = settings.Value.MaxNotificationsPerUserPerHour;
    }

    /// <summary>
    /// Checks whether the user is allowed to receive another notification.
    /// If allowed, the current timestamp is recorded.
    /// </summary>
    /// <param name="userId">The target user ID.</param>
    /// <returns>True if the notification is allowed; false if rate-limited.</returns>
    public bool IsAllowed(Guid userId)
    {
        var now = DateTimeOffset.UtcNow;
        var window = _windows.GetOrAdd(userId, _ => new TimestampWindow());

        lock (window)
        {
            // Remove timestamps older than 1 hour
            var cutoff = now.AddHours(-1);
            while (window.Timestamps.Count > 0 && window.Timestamps.Peek() < cutoff)
            {
                window.Timestamps.Dequeue();
            }

            if (window.Timestamps.Count >= _maxPerHour)
            {
                _logger.LogWarning(
                    "Rate limit exceeded for user {UserId}: {Count}/{Max} notifications in the last hour",
                    userId,
                    window.Timestamps.Count,
                    _maxPerHour);
                return false;
            }

            window.Timestamps.Enqueue(now);
            return true;
        }
    }

    /// <summary>
    /// Holds the sliding window of notification timestamps for a single user.
    /// </summary>
    private sealed class TimestampWindow
    {
        public Queue<DateTimeOffset> Timestamps { get; } = new();
    }
}
