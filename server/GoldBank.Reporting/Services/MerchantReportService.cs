using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using GoldBank.Core.Common.Persistence;

namespace GoldBank.Reporting.Services;

/// <summary>
/// Generates merchant and agent performance reports (STORY-064).
/// Includes transaction volumes, commission earnings, and merchant rankings.
/// </summary>
public sealed class MerchantReportService
{
    private readonly GoldBankDbContext _dbContext;
    private readonly ILogger<MerchantReportService> _logger;

    public MerchantReportService(
        GoldBankDbContext dbContext,
        ILogger<MerchantReportService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<MerchantReport> GetReportAsync(
        DateTime? dateFrom,
        DateTime? dateTo,
        string? merchantId,
        CancellationToken cancellationToken = default)
    {
        var from = dateFrom ?? DateTime.UtcNow.AddDays(-30);
        var to = dateTo ?? DateTime.UtcNow;

        var merchantsQuery = _dbContext.Merchants
            .Where(m => m.Status == "active");

        if (!string.IsNullOrWhiteSpace(merchantId) && Guid.TryParse(merchantId, out var mGuid))
        {
            merchantsQuery = merchantsQuery.Where(m => m.Id == mGuid);
        }

        var commissions = _dbContext.AgentCommissions
            .Where(c => c.CreatedAt >= from && c.CreatedAt <= to);

        var merchantMetrics = await merchantsQuery
            .GroupJoin(
                commissions,
                m => m.Id,
                c => c.MerchantId,
                (m, comms) => new { m.Id, m.BusinessName, Commissions = comms })
            .Select(g => new MerchantMetricDto(
                g.Id.ToString(),
                g.BusinessName,
                g.Commissions.Count(),
                g.Commissions.Sum(c => c.TransactionAmount),
                g.Commissions.Sum(c => c.CommissionAmount),
                "ZWG"))
            .OrderByDescending(m => m.Volume)
            .ToListAsync(cancellationToken);

        var totalVolume = merchantMetrics.Sum(m => m.Volume);
        var totalTransactions = merchantMetrics.Sum(m => m.TransactionCount);

        _logger.LogInformation(
            "Merchant report generated: {Count} merchants, {Volume} total volume",
            merchantMetrics.Count, totalVolume);

        return new MerchantReport(
            Merchants: merchantMetrics,
            TotalVolume: totalVolume,
            TotalTransactions: totalTransactions,
            Currency: "ZWG");
    }
}

public sealed record MerchantReport(
    List<MerchantMetricDto> Merchants,
    decimal TotalVolume,
    int TotalTransactions,
    string Currency);

public sealed record MerchantMetricDto(
    string MerchantId,
    string BusinessName,
    int TransactionCount,
    decimal Volume,
    decimal Commission,
    string Currency);
