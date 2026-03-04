using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UniBank.Core.Common.Persistence;
using UniBank.Core.Modules.FraudDetection.Domain.Entities;
using UniBank.SharedKernel.Caching;
using UniBank.SharedKernel.Results;

namespace UniBank.Core.Modules.FraudDetection.Application.Services;

/// <summary>
/// Rules engine that evaluates transactions against fraud detection rules (STORY-072).
/// Supports four rule types: UnusualAmount, VelocityBreach, GeographicAnomaly, PatternAnomaly.
/// </summary>
public sealed class FraudDetectionEngine
{
    private const string VelocityKeyPrefix = "fraud:velocity:";
    private const string PatternKeyPrefix = "fraud:pattern:";
    private const int VelocityWindowSeconds = 3600; // 1 hour
    private const int PatternWindowSeconds = 86400; // 24 hours
    private const int MaxTransactionsPerHour = 10;
    private const decimal UnusualAmountMultiplier = 5.0m;
    private const int PatternRepeatThreshold = 3;

    private readonly UniBankDbContext _dbContext;
    private readonly ICacheStore _cache;
    private readonly ILogger<FraudDetectionEngine> _logger;

    public FraudDetectionEngine(
        UniBankDbContext dbContext,
        ICacheStore cache,
        ILogger<FraudDetectionEngine> logger)
    {
        _dbContext = dbContext;
        _cache = cache;
        _logger = logger;
    }

    public async Task<Result<List<FraudAlert>>> EvaluateTransactionAsync(
        Guid accountId, Guid transactionId, decimal amount, string currency,
        string? counterpartyPhone, string tenantId,
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

        return Result.Success(alerts);
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
}
