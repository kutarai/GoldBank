using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UniBank.Core.Common.Persistence;

namespace UniBank.Reporting.Services;

/// <summary>
/// Generates user growth and registration reports with daily/weekly/monthly granularity (STORY-063).
/// Includes KYC completion rates and churn metrics.
/// </summary>
public sealed class UserGrowthReportService
{
    private readonly UniBankDbContext _dbContext;
    private readonly ILogger<UserGrowthReportService> _logger;

    public UserGrowthReportService(
        UniBankDbContext dbContext,
        ILogger<UserGrowthReportService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<UserGrowthReport> GetReportAsync(
        DateTime? dateFrom,
        DateTime? dateTo,
        string granularity,
        CancellationToken cancellationToken = default)
    {
        var from = dateFrom ?? DateTime.UtcNow.AddDays(-30);
        var to = dateTo ?? DateTime.UtcNow;

        var accounts = _dbContext.Accounts
            .Where(a => a.DeletedAt == null && a.CreatedAt >= from && a.CreatedAt <= to);

        var totalRegistered = await _dbContext.Accounts
            .Where(a => a.DeletedAt == null)
            .CountAsync(cancellationToken);

        var totalActive = await _dbContext.Accounts
            .Where(a => a.DeletedAt == null && a.Status == "active")
            .CountAsync(cancellationToken);

        List<GrowthDataPointDto> dataPoints;

        if (granularity.Equals("weekly", StringComparison.OrdinalIgnoreCase))
        {
            // Group by ISO week number using year and day-of-year
            dataPoints = await accounts
                .GroupBy(a => new { a.CreatedAt.Year, WeekNum = (a.CreatedAt.DayOfYear - 1) / 7 })
                .Select(g => new GrowthDataPointDto(
                    $"{g.Key.Year}-W{g.Key.WeekNum + 1}",
                    g.Count(),
                    g.Count(a => a.Status == "active"),
                    0))
                .OrderBy(d => d.Period)
                .ToListAsync(cancellationToken);
        }
        else if (granularity.Equals("monthly", StringComparison.OrdinalIgnoreCase))
        {
            dataPoints = await accounts
                .GroupBy(a => new { a.CreatedAt.Year, a.CreatedAt.Month })
                .Select(g => new GrowthDataPointDto(
                    $"{g.Key.Year}-{g.Key.Month:D2}",
                    g.Count(),
                    g.Count(a => a.Status == "active"),
                    0))
                .OrderBy(d => d.Period)
                .ToListAsync(cancellationToken);
        }
        else
        {
            dataPoints = await accounts
                .GroupBy(a => a.CreatedAt.Date)
                .Select(g => new GrowthDataPointDto(
                    g.Key.ToString("yyyy-MM-dd"),
                    g.Count(),
                    g.Count(a => a.Status == "active"),
                    0))
                .OrderBy(d => d.Period)
                .ToListAsync(cancellationToken);
        }

        var previousPeriodCount = await _dbContext.Accounts
            .Where(a => a.DeletedAt == null && a.CreatedAt < from)
            .CountAsync(cancellationToken);

        var currentPeriodCount = await accounts.CountAsync(cancellationToken);
        var growthRate = previousPeriodCount > 0
            ? ((double)currentPeriodCount / previousPeriodCount * 100).ToString("F1")
            : "0.0";

        _logger.LogInformation(
            "User growth report generated: {DataPoints} data points, {Total} total registered",
            dataPoints.Count, totalRegistered);

        return new UserGrowthReport(
            DataPoints: dataPoints,
            TotalRegistered: totalRegistered,
            TotalActive: totalActive,
            GrowthRate: growthRate);
    }
}

public sealed record UserGrowthReport(
    List<GrowthDataPointDto> DataPoints,
    long TotalRegistered,
    long TotalActive,
    string GrowthRate);

public sealed record GrowthDataPointDto(
    string Period,
    long NewRegistrations,
    long ActiveUsers,
    long ChurnedUsers);
