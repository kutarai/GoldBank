using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using GoldBank.Core.Common.Persistence;
using GoldBank.Core.Modules.FraudDetection.Domain.Entities;
using GoldBank.SharedKernel.Caching;
using GoldBank.SharedKernel.Results;

namespace GoldBank.Core.Modules.FraudDetection.Application.Services;

/// <summary>
/// Rules engine that evaluates transactions against fraud detection rules (STORY-072).
/// Supports six rule types: UnusualAmount, VelocityBreach, GeographicAnomaly,
/// PatternAnomaly, NewAccountRisk, and FailedAttempts.
/// </summary>
public sealed class FraudDetectionEngine
{
    private const string VelocityKeyPrefix = "fraud:velocity:";
    private const string PatternKeyPrefix = "fraud:pattern:";
    private const string FailedAttemptsKeyPrefix = "fraud:failed:";
    private const int VelocityWindowSeconds = 3600; // 1 hour
    private const int PatternWindowSeconds = 86400; // 24 hours
    private const int FailedAttemptsWindowSeconds = 1800; // 30 minutes
    private const int MaxTransactionsPerHour = 10;
    private const decimal UnusualAmountMultiplier = 5.0m;
    private const int PatternRepeatThreshold = 3;
    private const int MaxFailedAttemptsPerWindow = 5;
    private const int NewAccountWindowHours = 24;
    private const decimal NewAccountDailyLimitPercentage = 0.50m; // 50% of daily limit

    private readonly GoldBankDbContext _dbContext;
    private readonly ICacheStore _cache;
    private readonly ILogger<FraudDetectionEngine> _logger;

    public FraudDetectionEngine(
        GoldBankDbContext dbContext,
        ICacheStore cache,
        ILogger<FraudDetectionEngine> logger)
    {
        _dbContext = dbContext;
        _cache = cache;
        _logger = logger;
    }

    public async Task<Result<List<FraudAlert>>> EvaluateTransactionAsync(
        Guid accountId, Guid transactionId, decimal amount, string currency,
        string transactionType, string? counterpartyPhone, string tenantId,
        CancellationToken cancellationToken = default)
    {
        var alerts = new List<FraudAlert>();

        var unusualAmountAlert = await CheckUnusualAmountAsync(
            accountId, transactionId, amount, currency, tenantId, cancellationToken);
        if (unusualAmountAlert is not null) alerts.Add(unusualAmountAlert);

        var velocityAlert = await CheckVelocityBreachAsync(
            accountId, transactionId, tenantId, cancellationToken);
        if (velocityAlert is not null) alerts.Add(velocityAlert);

        var geographicAlert = await CheckGeographicAnomalyAsync(
            accountId, transactionId, tenantId, cancellationToken);
        if (geographicAlert is not null) alerts.Add(geographicAlert);

        if (!string.IsNullOrEmpty(counterpartyPhone))
        {
            var patternAlert = await CheckPatternAnomalyAsync(
                accountId, transactionId, amount, counterpartyPhone, tenantId, cancellationToken);
            if (patternAlert is not null) alerts.Add(patternAlert);
        }

        var newAccountAlert = await CheckNewAccountRiskAsync(
            accountId, transactionId, amount, currency, tenantId, cancellationToken);
        if (newAccountAlert is not null) alerts.Add(newAccountAlert);

        return Result.Success(alerts);
    }

    /// <summary>
    /// Records a failed payment attempt for the account. Call this from payment handlers
    /// when a transaction fails due to wrong PIN, insufficient funds, etc.
    /// Returns a fraud alert if the threshold is breached.
    /// </summary>
    public async Task<FraudAlert?> RecordFailedAttemptAsync(
        Guid accountId, Guid transactionId, string tenantId,
        CancellationToken cancellationToken = default)
    {
        var failedKey = $"{FailedAttemptsKeyPrefix}{accountId}";
        var currentCount = await _cache.IncrementAsync(failedKey, cancellationToken);

        if (currentCount == 1)
            await _cache.SetExpiryAsync(failedKey, TimeSpan.FromSeconds(FailedAttemptsWindowSeconds), cancellationToken);

        if (currentCount <= MaxFailedAttemptsPerWindow) return null;

        var severity = currentCount > 10 ? "Critical" : "High";
        _logger.LogWarning(
            "FailedAttempts fraud alert: account {AccountId}, {Count} failed attempts in 30 minutes",
            accountId, currentCount);

        return new FraudAlert
        {
            AccountId = accountId,
            TransactionId = transactionId,
            AlertType = "FailedAttempts",
            Severity = severity,
            Description = $"Account has {currentCount} failed payment attempts in the last 30 minutes, exceeding the threshold of {MaxFailedAttemptsPerWindow}.",
            Status = "Open",
            TenantId = tenantId
        };
    }

    private async Task<FraudAlert?> CheckUnusualAmountAsync(
        Guid accountId, Guid transactionId, decimal amount, string currency,
        string tenantId, CancellationToken cancellationToken)
    {
        var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
        var averageAmount = await _dbContext.Transactions
            .Where(t => t.AccountId == accountId && t.CreatedAt >= thirtyDaysAgo && t.Amount > 0)
            .Select(t => (decimal?)Math.Abs(t.Amount))
            .AverageAsync(cancellationToken);

        if (averageAmount is null or 0) return null;
        if (amount <= averageAmount.Value * UnusualAmountMultiplier) return null;

        var severity = amount > averageAmount.Value * 10 ? "Critical" : "High";
        _logger.LogWarning(
            "UnusualAmount fraud alert: account {AccountId}, amount {Amount} {Currency} is {Multiplier:F1}x avg {Average:F2}",
            accountId, amount, currency, amount / averageAmount.Value, averageAmount.Value);

        return new FraudAlert
        {
            AccountId = accountId, TransactionId = transactionId,
            AlertType = "UnusualAmount", Severity = severity,
            Description = $"Transaction amount {amount:F2} {currency} exceeds {amount / averageAmount.Value:F1}x the 30-day average of {averageAmount.Value:F2} {currency}.",
            Status = "Open", TenantId = tenantId
        };
    }

    private async Task<FraudAlert?> CheckVelocityBreachAsync(
        Guid accountId, Guid transactionId, string tenantId, CancellationToken cancellationToken)
    {
        var velocityKey = $"{VelocityKeyPrefix}{accountId}";
        var currentCount = await _cache.IncrementAsync(velocityKey, cancellationToken);

        if (currentCount == 1)
            await _cache.SetExpiryAsync(velocityKey, TimeSpan.FromSeconds(VelocityWindowSeconds), cancellationToken);

        if (currentCount <= MaxTransactionsPerHour) return null;

        var severity = currentCount > 20 ? "Critical" : "High";
        _logger.LogWarning("VelocityBreach: account {AccountId}, {Count} transactions/hour", accountId, currentCount);

        return new FraudAlert
        {
            AccountId = accountId, TransactionId = transactionId,
            AlertType = "VelocityBreach", Severity = severity,
            Description = $"Account has {currentCount} transactions in the last hour, exceeding the threshold of {MaxTransactionsPerHour}.",
            Status = "Open", TenantId = tenantId
        };
    }

    private async Task<FraudAlert?> CheckGeographicAnomalyAsync(
        Guid accountId, Guid transactionId, string tenantId, CancellationToken cancellationToken)
    {
        var account = await _dbContext.Accounts
            .Where(a => a.Id == accountId)
            .Select(a => new { a.TenantId, a.PhoneCountryCode })
            .FirstOrDefaultAsync(cancellationToken);

        if (account is null) return null;
        if (string.Equals(account.TenantId, tenantId, StringComparison.OrdinalIgnoreCase)) return null;

        _logger.LogWarning("GeographicAnomaly: account {AccountId}, tx tenant {TxTenant} vs registered {AccTenant}",
            accountId, tenantId, account.TenantId);

        return new FraudAlert
        {
            AccountId = accountId, TransactionId = transactionId,
            AlertType = "GeographicAnomaly", Severity = "Medium",
            Description = $"Transaction originated from tenant '{tenantId}' but account is registered under tenant '{account.TenantId}'.",
            Status = "Open", TenantId = tenantId
        };
    }

    private async Task<FraudAlert?> CheckPatternAnomalyAsync(
        Guid accountId, Guid transactionId, decimal amount, string counterpartyPhone,
        string tenantId, CancellationToken cancellationToken)
    {
        var patternKey = $"{PatternKeyPrefix}{accountId}:{counterpartyPhone}:{amount:F2}";
        var currentCount = await _cache.IncrementAsync(patternKey, cancellationToken);

        if (currentCount == 1)
            await _cache.SetExpiryAsync(patternKey, TimeSpan.FromSeconds(PatternWindowSeconds), cancellationToken);

        if (currentCount < PatternRepeatThreshold) return null;

        var severity = currentCount >= 5 ? "High" : "Medium";
        _logger.LogWarning("PatternAnomaly: account {AccountId}, amount {Amount} to {Recipient} {Count} times/24h",
            accountId, amount, counterpartyPhone, currentCount);

        return new FraudAlert
        {
            AccountId = accountId, TransactionId = transactionId,
            AlertType = "PatternAnomaly", Severity = severity,
            Description = $"Same amount {amount:F2} sent to {counterpartyPhone} {currentCount} times in the last 24 hours (threshold: {PatternRepeatThreshold}).",
            Status = "Open", TenantId = tenantId
        };
    }

    /// <summary>
    /// NewAccountRisk: Detects transactions exceeding 50% of daily limit within 24 hours of account activation.
    /// New accounts are high risk for fraud — legitimate users rarely max out limits immediately.
    /// </summary>
    private async Task<FraudAlert?> CheckNewAccountRiskAsync(
        Guid accountId, Guid transactionId, decimal amount, string currency,
        string tenantId, CancellationToken cancellationToken)
    {
        var account = await _dbContext.Accounts
            .Where(a => a.Id == accountId)
            .Select(a => new { a.CreatedAt, a.DailyLimit, a.Status })
            .FirstOrDefaultAsync(cancellationToken);

        if (account is null) return null;

        // Only applies within 24 hours of account creation
        var hoursSinceCreation = (DateTime.UtcNow - account.CreatedAt).TotalHours;
        if (hoursSinceCreation > NewAccountWindowHours) return null;

        var threshold = account.DailyLimit * NewAccountDailyLimitPercentage;
        if (amount <= threshold) return null;

        _logger.LogWarning(
            "NewAccountRisk: account {AccountId} created {Hours:F1}h ago, tx {Amount} {Currency} exceeds {Threshold:F2} (50% of daily limit {DailyLimit:F2})",
            accountId, hoursSinceCreation, amount, currency, threshold, account.DailyLimit);

        return new FraudAlert
        {
            AccountId = accountId,
            TransactionId = transactionId,
            AlertType = "NewAccountRisk",
            Severity = "High",
            Description = $"New account (created {hoursSinceCreation:F0}h ago) transacting {amount:F2} {currency}, which exceeds 50% of daily limit ({threshold:F2} {currency}).",
            Status = "Open",
            TenantId = tenantId
        };
    }
}
