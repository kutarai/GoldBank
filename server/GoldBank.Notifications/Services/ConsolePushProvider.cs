using Microsoft.Extensions.Logging;

namespace GoldBank.Notifications.Services;

/// <summary>
/// Development stub that logs push notifications to the console instead of sending them
/// through Firebase Cloud Messaging. Replace with a real FCM provider for production.
/// </summary>
public sealed class ConsolePushProvider : IPushNotificationProvider
{
    private readonly ILogger<ConsolePushProvider> _logger;

    public ConsolePushProvider(ILogger<ConsolePushProvider> logger)
    {
        _logger = logger;
    }

    public Task<bool> SendAsync(
        string deviceToken,
        string title,
        string body,
        IReadOnlyDictionary<string, string>? data = null,
        CancellationToken cancellationToken = default)
    {
        var maskedToken = MaskToken(deviceToken);

        _logger.LogInformation(
            "[CONSOLE PUSH] Token: {Token} | Title: {Title} | Body: {Body}",
            maskedToken,
            title,
            body);

        if (data is { Count: > 0 })
        {
            _logger.LogDebug("[CONSOLE PUSH] Data payload: {DataKeys}", string.Join(", ", data.Keys));
        }

        return Task.FromResult(true);
    }

    /// <summary>
    /// Masks the device token for logging, showing only the first 8 characters.
    /// </summary>
    private static string MaskToken(string token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return "(empty)";
        }

        var visibleLength = Math.Min(8, token.Length);
        return string.Concat(token.AsSpan(0, visibleLength), "...");
    }
}
