using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UniBank.Core.Common.Persistence;
using UniBank.SharedKernel.Caching;
using UniBank.SharedKernel.Results;

namespace UniBank.Core.Modules.Accounts.Application.Handlers;

/// <summary>
/// Retrieves account balance with caching (STORY-016).
/// Caches balance for 30 seconds to reduce database load.
/// </summary>
public sealed class GetBalanceHandler
{
    private const string CacheKeyPrefix = "balance:";
    private const int CacheTtlSeconds = 30;
    private readonly UniBankDbContext _dbContext;
    private readonly ICacheStore _cache;
    private readonly ILogger<GetBalanceHandler> _logger;

    public GetBalanceHandler(
        UniBankDbContext dbContext,
        ICacheStore cache,
        ILogger<GetBalanceHandler> logger)
    {
        _dbContext = dbContext;
        _cache = cache;
        _logger = logger;
    }

    public async Task<Result<BalanceResult>> HandleAsync(
        Guid accountId, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"{CacheKeyPrefix}{accountId}";

        // Check cache first
        var cached = await _cache.GetAsync(cacheKey, cancellationToken);
        if (cached is not null)
        {
            var parts = cached.Split('|');
            if (parts.Length == 4)
            {
                return Result.Success(new BalanceResult(
                    AccountId: accountId.ToString(),
                    Balance: decimal.Parse(parts[0]),
                    AvailableBalance: decimal.Parse(parts[1]),
                    DailyLimit: decimal.Parse(parts[2]),
                    Currency: parts[3]));
            }
        }

        // Fetch from database
        var account = await _dbContext.Accounts
            .FirstOrDefaultAsync(a => a.Id == accountId && a.DeletedAt == null, cancellationToken);

        if (account is null)
            return Result.Failure<BalanceResult>(
                new Error("Account.NotFound", "Account not found."));

        var result = new BalanceResult(
            AccountId: account.Id.ToString(),
            Balance: account.Balance,
            AvailableBalance: account.AvailableBalance,
            DailyLimit: account.DailyLimit,
            Currency: account.Currency);

        // Cache the result
        var cacheValue = $"{result.Balance}|{result.AvailableBalance}|{result.DailyLimit}|{result.Currency}";
        await _cache.SetAsync(cacheKey, cacheValue, TimeSpan.FromSeconds(CacheTtlSeconds), cancellationToken);

        return Result.Success(result);
    }
}

public sealed record BalanceResult(
    string AccountId,
    decimal Balance,
    decimal AvailableBalance,
    decimal DailyLimit,
    string Currency);
