using Microsoft.Extensions.Logging;
using UniBank.SharedKernel.Caching;

namespace UniBank.Core.Modules.Accounts.Infrastructure.Services;

/// <summary>
/// Manages user sessions with configurable auto-timeout (STORY-019).
/// Uses ICacheStore for session tracking with TTL-based expiration.
/// </summary>
public sealed class SessionService
{
    private const string SessionKeyPrefix = "session:";
    private const int DefaultTimeoutSeconds = 300; // 5 minutes
    private readonly ICacheStore _cache;
    private readonly ILogger<SessionService> _logger;

    public SessionService(ICacheStore cache, ILogger<SessionService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task CreateSessionAsync(
        Guid accountId, string deviceId, int timeoutSeconds = DefaultTimeoutSeconds)
    {
        var key = $"{SessionKeyPrefix}{accountId}:{deviceId}";
        var sessionData = $"{DateTime.UtcNow:O}|{deviceId}";

        await _cache.SetAsync(key, sessionData, TimeSpan.FromSeconds(timeoutSeconds));

        _logger.LogInformation(
            "Session created for account {AccountId} on device {DeviceId}, timeout: {Timeout}s",
            accountId, deviceId, timeoutSeconds);
    }

    public async Task<bool> IsSessionActiveAsync(Guid accountId, string deviceId)
    {
        var key = $"{SessionKeyPrefix}{accountId}:{deviceId}";
        return await _cache.ExistsAsync(key);
    }

    public async Task RefreshSessionAsync(
        Guid accountId, string deviceId, int timeoutSeconds = DefaultTimeoutSeconds)
    {
        var key = $"{SessionKeyPrefix}{accountId}:{deviceId}";
        if (await _cache.ExistsAsync(key))
        {
            await _cache.SetExpiryAsync(key, TimeSpan.FromSeconds(timeoutSeconds));
        }
    }

    public async Task EndSessionAsync(Guid accountId, string deviceId)
    {
        var key = $"{SessionKeyPrefix}{accountId}:{deviceId}";
        await _cache.DeleteAsync(key);

        _logger.LogInformation("Session ended for account {AccountId} on device {DeviceId}",
            accountId, deviceId);
    }

    public async Task EndAllSessionsAsync(Guid accountId)
    {
        var pattern = $"{SessionKeyPrefix}{accountId}:*";
        var count = await _cache.DeleteByPatternAsync(pattern);

        _logger.LogInformation("All sessions ended for account {AccountId}, count: {Count}",
            accountId, count);
    }
}
