using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UniBank.Core.Common.Persistence;

namespace UniBank.Reporting.Services;

/// <summary>
/// Provides real-time dashboard metrics by querying accounts, transactions, and merchants (STORY-062).
/// </summary>
public sealed class DashboardService
{
    private readonly UniBankDbContext _dbContext;
    private readonly ILogger<DashboardService> _logger;

    public DashboardService(
        UniBankDbContext dbContext,
        ILogger<DashboardService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<DashboardMetrics> GetDashboardAsync(
        DateTime? dateFrom,
        DateTime? dateTo,
        CancellationToken cancellationToken = default)
    {
        var from = dateFrom ?? DateTime.UtcNow.AddDays(-30);
        var to = dateTo ?? DateTime.UtcNow;

        var totalUsers = await _dbContext.Accounts
            .Where(a => a.DeletedAt == null)
            .CountAsync(cancellationToken);

        var activeUsers = await _dbContext.Accounts
            .Where(a => a.DeletedAt == null && a.Status == "active")
            .CountAsync(cancellationToken);

        var transactionsQuery = _dbContext.Transactions
            .Where(t => t.CreatedAt >= from && t.CreatedAt <= to);

        var totalTransactions = await transactionsQuery.CountAsync(cancellationToken);

        var totalVolume = await transactionsQuery
            .SumAsync(t => t.Amount, cancellationToken);

        var totalRevenue = await transactionsQuery
            .SumAsync(t => t.Fee, cancellationToken);

        var activeMerchants = await _dbContext.Merchants
            .Where(m => m.Status == "active")
            .CountAsync(cancellationToken);

        var activeAgents = await _dbContext.Merchants
            .Where(m => m.Status == "active" && m.IsAgent)
            .CountAsync(cancellationToken);

        // Daily metrics for the period — use DateOnly for server-side grouping
        var dailyTxRaw = await transactionsQuery
            .GroupBy(t => new { t.CreatedAt.Year, t.CreatedAt.Month, t.CreatedAt.Day })
            .Select(g => new
            {
                g.Key.Year, g.Key.Month, g.Key.Day,
                Count = g.Count(),
                Volume = g.Sum(t => t.Amount)
            })
            .OrderBy(d => d.Year).ThenBy(d => d.Month).ThenBy(d => d.Day)
            .ToListAsync(cancellationToken);

        var dailyMetrics = dailyTxRaw
            .Select(d => new DailyMetricDto(
                $"{d.Year:D4}-{d.Month:D2}-{d.Day:D2}", d.Count, d.Volume, 0))
            .ToList();

        // Enrich daily metrics with new user counts
        var dailyNewUsers = await _dbContext.Accounts
            .Where(a => a.CreatedAt >= from && a.CreatedAt <= to && a.DeletedAt == null)
            .GroupBy(a => new { a.CreatedAt.Year, a.CreatedAt.Month, a.CreatedAt.Day })
            .Select(g => new
            {
                Date = $"{g.Key.Year:D4}-{g.Key.Month:D2}-{g.Key.Day:D2}",
                Count = g.Count()
            })
            .ToListAsync(cancellationToken);

        var usersByDate = dailyNewUsers.ToDictionary(u => u.Date, u => u.Count);
        var enrichedMetrics = dailyMetrics
            .Select(d => d with { NewUsers = usersByDate.GetValueOrDefault(d.Date) })
            .ToList();

        _logger.LogInformation(
            "Dashboard metrics generated: {TotalUsers} users, {TotalTx} transactions, {Volume} volume",
            totalUsers, totalTransactions, totalVolume);

        return new DashboardMetrics(
            TotalUsers: totalUsers,
            ActiveUsers: activeUsers,
            TotalTransactions: totalTransactions,
            TotalVolume: totalVolume,
            TotalRevenue: totalRevenue,
            ActiveMerchants: activeMerchants,
            ActiveAgents: activeAgents,
            ActiveTerminals: 0,
            DailyMetrics: enrichedMetrics);
    }
}

public sealed record DashboardMetrics(
    long TotalUsers,
    long ActiveUsers,
    long TotalTransactions,
    decimal TotalVolume,
    decimal TotalRevenue,
    int ActiveMerchants,
    int ActiveAgents,
    int ActiveTerminals,
    List<DailyMetricDto> DailyMetrics);

public sealed record DailyMetricDto(
    string Date,
    long Transactions,
    decimal Volume,
    long NewUsers);
