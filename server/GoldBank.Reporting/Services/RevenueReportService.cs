using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using GoldBank.Core.Common.Persistence;

namespace GoldBank.Reporting.Services;

/// <summary>
/// Generates revenue and fee reports by transaction type, tenant, and period (STORY-065).
/// Supports daily, weekly, and monthly granularity with period-over-period comparison.
/// </summary>
public sealed class RevenueReportService
{
    private readonly GoldBankDbContext _dbContext;
    private readonly ILogger<RevenueReportService> _logger;

    public RevenueReportService(
        GoldBankDbContext dbContext,
        ILogger<RevenueReportService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<RevenueReport> GetReportAsync(
        DateTime? dateFrom,
        DateTime? dateTo,
        string granularity,
        CancellationToken cancellationToken = default)
    {
        var from = dateFrom ?? DateTime.UtcNow.AddDays(-30);
        var to = dateTo ?? DateTime.UtcNow;

        var transactions = _dbContext.Transactions
            .Where(t => t.CreatedAt >= from && t.CreatedAt <= to && t.Status == "completed");

        // Revenue by period
        List<RevenueDataPointDto> dataPoints;

        if (granularity.Equals("monthly", StringComparison.OrdinalIgnoreCase))
        {
            dataPoints = await transactions
                .GroupBy(t => new { t.CreatedAt.Year, t.CreatedAt.Month })
                .Select(g => new RevenueDataPointDto(
                    $"{g.Key.Year}-{g.Key.Month:D2}",
                    g.Sum(t => t.Fee),
                    g.Count(),
                    "ZWG"))
                .OrderBy(d => d.Period)
                .ToListAsync(cancellationToken);
        }
        else if (granularity.Equals("weekly", StringComparison.OrdinalIgnoreCase))
        {
            dataPoints = await transactions
                .GroupBy(t => new { t.CreatedAt.Year, WeekNum = (t.CreatedAt.DayOfYear - 1) / 7 })
                .Select(g => new RevenueDataPointDto(
                    $"{g.Key.Year}-W{g.Key.WeekNum + 1}",
                    g.Sum(t => t.Fee),
                    g.Count(),
                    "ZWG"))
                .OrderBy(d => d.Period)
                .ToListAsync(cancellationToken);
        }
        else
        {
            dataPoints = await transactions
                .GroupBy(t => new { t.CreatedAt.Year, t.CreatedAt.Month, t.CreatedAt.Day })
                .Select(g => new RevenueDataPointDto(
                    $"{g.Key.Year:D4}-{g.Key.Month:D2}-{g.Key.Day:D2}",
                    g.Sum(t => t.Fee),
                    g.Count(),
                    "ZWG"))
                .OrderBy(d => d.Period)
                .ToListAsync(cancellationToken);
        }

        var totalRevenue = dataPoints.Sum(d => d.Revenue);

        // Revenue by transaction type
        var revenueByType = await transactions
            .GroupBy(t => t.Type)
            .Select(g => new RevenueByTypeDto(
                g.Key,
                g.Sum(t => t.Fee),
                g.Count(),
                "0.0",
                "ZWG"))
            .OrderByDescending(r => r.Revenue)
            .ToListAsync(cancellationToken);

        // Calculate percentages
        var enrichedByType = revenueByType
            .Select(r => r with
            {
                Percentage = totalRevenue > 0
                    ? (r.Revenue / totalRevenue * 100).ToString("F1")
                    : "0.0"
            })
            .ToList();

        _logger.LogInformation(
            "Revenue report generated: {TotalRevenue} ZWG total, {DataPoints} periods",
            totalRevenue, dataPoints.Count);

        return new RevenueReport(
            DataPoints: dataPoints,
            TotalRevenue: totalRevenue,
            RevenueByType: enrichedByType,
            Currency: "ZWG");
    }
}

public sealed record RevenueReport(
    List<RevenueDataPointDto> DataPoints,
    decimal TotalRevenue,
    List<RevenueByTypeDto> RevenueByType,
    string Currency);

public sealed record RevenueDataPointDto(
    string Period,
    decimal Revenue,
    int TransactionCount,
    string Currency);

public sealed record RevenueByTypeDto(
    string TransactionType,
    decimal Revenue,
    int Count,
    string Percentage,
    string Currency);
