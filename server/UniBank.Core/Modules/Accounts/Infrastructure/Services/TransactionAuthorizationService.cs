using Microsoft.Extensions.Logging;
using UniBank.SharedKernel.Caching;
using UniBank.SharedKernel.Results;

namespace UniBank.Core.Modules.Accounts.Infrastructure.Services;

/// <summary>
/// Handles transaction authorization for high-value transactions (STORY-020).
/// Enforces PIN confirmation for amounts above configurable thresholds.
/// </summary>
public sealed class TransactionAuthorizationService
{
    private const string ThresholdKeyPrefix = "tx_auth_threshold:";
    private const decimal DefaultThreshold = 50000m;
    private readonly ICacheStore _cache;
    private readonly PinHashingService _pinHasher;
    private readonly LockoutService _lockoutService;
    private readonly ILogger<TransactionAuthorizationService> _logger;

    public TransactionAuthorizationService(
        ICacheStore cache,
        PinHashingService pinHasher,
        LockoutService lockoutService,
        ILogger<TransactionAuthorizationService> logger)
    {
        _cache = cache;
        _pinHasher = pinHasher;
        _lockoutService = lockoutService;
        _logger = logger;
    }

    public async Task<bool> RequiresAuthorizationAsync(
        string tenantId, string transactionType, decimal amount)
    {
        var threshold = await GetThresholdAsync(tenantId, transactionType);
        return amount >= threshold;
    }

    public async Task<Result> AuthorizeAsync(
        Guid accountId, string pin, string pinHash, string transactionType, decimal amount)
    {
        var (isLocked, _, lockoutSeconds) = await _lockoutService.CheckLockoutAsync(accountId);
        if (isLocked)
            return Result.Failure(
                new Error("Auth.Locked", $"Account is locked. Try again in {lockoutSeconds} seconds."));

        if (!_pinHasher.VerifyPin(pin, pinHash))
        {
            await _lockoutService.RecordFailedAttemptAsync(accountId);
            _logger.LogWarning(
                "Failed transaction authorization for account {AccountId}, type: {Type}, amount: {Amount}",
                accountId, transactionType, amount);

            return Result.Failure(
                new Error("Auth.InvalidPIN", "Invalid PIN for transaction authorization."));
        }

        await _lockoutService.ResetAttemptsAsync(accountId);

        _logger.LogInformation(
            "Transaction authorized for account {AccountId}, type: {Type}, amount: {Amount}",
            accountId, transactionType, amount);

        return Result.Success();
    }

    public async Task<decimal> GetThresholdAsync(string tenantId, string transactionType)
    {
        var key = $"{ThresholdKeyPrefix}{tenantId}:{transactionType}";

        var value = await _cache.GetAsync(key);
        if (value is not null && decimal.TryParse(value, out var threshold))
            return threshold;

        var tenantKey = $"{ThresholdKeyPrefix}{tenantId}:default";
        var tenantValue = await _cache.GetAsync(tenantKey);
        if (tenantValue is not null && decimal.TryParse(tenantValue, out var tenantThreshold))
            return tenantThreshold;

        return DefaultThreshold;
    }

    public async Task SetThresholdAsync(string tenantId, string transactionType, decimal threshold)
    {
        var key = $"{ThresholdKeyPrefix}{tenantId}:{transactionType}";
        await _cache.SetAsync(key, threshold.ToString());

        _logger.LogInformation(
            "Transaction auth threshold set: tenant={TenantId}, type={Type}, threshold={Threshold}",
            tenantId, transactionType, threshold);
    }
}
