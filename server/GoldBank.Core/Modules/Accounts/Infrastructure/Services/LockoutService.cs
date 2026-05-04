using Microsoft.Extensions.Logging;
using GoldBank.SharedKernel.Caching;

namespace GoldBank.Core.Modules.Accounts.Infrastructure.Services;

/// <summary>
/// Manages account lockout after failed PIN attempts (STORY-018).
/// 5 failed attempts = 30-minute lockout.
/// </summary>
public sealed class LockoutService
{
    private readonly ICacheStore _cache;
    private readonly ILogger<LockoutService> _logger;

    private const int MaxAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan AttemptWindow = TimeSpan.FromMinutes(15);

    public LockoutService(ICacheStore cache, ILogger<LockoutService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<(bool IsLocked, int RemainingAttempts, long LockoutRemainingSeconds)> CheckLockoutAsync(
        Guid accountId)
    {
        var lockoutKey = $"lockout:{accountId}";
        var attemptsKey = $"auth_attempts:{accountId}";

        var lockoutTtl = await _cache.GetTimeToLiveAsync(lockoutKey);
        if (lockoutTtl.HasValue && lockoutTtl.Value > TimeSpan.Zero)
        {
            return (true, 0, (long)lockoutTtl.Value.TotalSeconds);
        }

        var attemptsStr = await _cache.GetAsync(attemptsKey);
        var attempts = int.TryParse(attemptsStr, out var a) ? a : 0;
        return (false, MaxAttempts - attempts, 0);
    }

    public async Task RecordFailedAttemptAsync(Guid accountId)
    {
        var attemptsKey = $"auth_attempts:{accountId}";

        var attempts = await _cache.IncrementAsync(attemptsKey);
        await _cache.SetExpiryAsync(attemptsKey, AttemptWindow);

        if (attempts >= MaxAttempts)
        {
            var lockoutKey = $"lockout:{accountId}";
            await _cache.SetAsync(lockoutKey, "locked", LockoutDuration);
            await _cache.DeleteAsync(attemptsKey);

            _logger.LogWarning("Account {AccountId} locked out after {Attempts} failed attempts", accountId, attempts);
        }
    }

    public async Task ResetAttemptsAsync(Guid accountId)
    {
        await _cache.DeleteAsync($"auth_attempts:{accountId}");
    }
}
