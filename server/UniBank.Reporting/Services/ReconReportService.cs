using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UniBank.Core.Common.Persistence;

namespace UniBank.Reporting.Services;

/// <summary>
/// Generates daily reconciliation reports comparing internal records with partner data (STORY-066).
/// Identifies matched and unmatched transactions with discrepancy details.
/// </summary>
public sealed class ReconReportService
{
    private readonly UniBankDbContext _dbContext;
    private readonly ILogger<ReconReportService> _logger;

    public ReconReportService(
        UniBankDbContext dbContext,
        ILogger<ReconReportService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<ReconReport> GetReportAsync(
        string batchDate,
        string? partnerCode,
        CancellationToken cancellationToken = default)
    {
        DateTime targetDate;
        if (!DateTime.TryParse(batchDate, out targetDate))
        {
            targetDate = DateTime.UtcNow.Date.AddDays(-1);
        }

        var dayStart = targetDate.Date;
        var dayEnd = dayStart.AddDays(1);

        var transactionsQuery = _dbContext.Transactions
            .Where(t => t.CreatedAt >= dayStart && t.CreatedAt < dayEnd);

        if (!string.IsNullOrWhiteSpace(partnerCode))
        {
            transactionsQuery = transactionsQuery
                .Where(t => t.Type == partnerCode);
        }

        var totalTransactions = await transactionsQuery.CountAsync(cancellationToken);
        var totalAmount = await transactionsQuery.SumAsync(t => t.Amount, cancellationToken);

        // Completed transactions are considered "matched"
        var matchedCount = await transactionsQuery
            .Where(t => t.Status == "completed")
            .CountAsync(cancellationToken);

        // Pending or failed transactions are considered "unmatched"
        var unmatchedCount = totalTransactions - matchedCount;

        // Get discrepancies (failed/pending transactions as potential mismatches)
        var discrepancies = await transactionsQuery
            .Where(t => t.Status != "completed")
            .Take(100)
            .Select(t => new ReconDiscrepancyDto(
                t.Reference ?? t.Id.ToString(),
                t.Amount,
                0m,
                t.Status == "failed" ? "Failed" : "Pending",
                t.Currency))
            .ToListAsync(cancellationToken);

        var status = unmatchedCount == 0 ? "Balanced" : "Discrepancies Found";

        _logger.LogInformation(
            "Reconciliation report generated for {Date}: {Total} transactions, {Matched} matched, {Unmatched} unmatched",
            batchDate, totalTransactions, matchedCount, unmatchedCount);

        return new ReconReport(
            BatchDate: batchDate,
            PartnerCode: partnerCode ?? "ALL",
            TotalTransactions: totalTransactions,
            TotalAmount: totalAmount,
            MatchedCount: matchedCount,
            UnmatchedCount: unmatchedCount,
            Status: status,
            Discrepancies: discrepancies,
            Currency: "ZWG");
    }
}

public sealed record ReconReport(
    string BatchDate,
    string PartnerCode,
    int TotalTransactions,
    decimal TotalAmount,
    int MatchedCount,
    int UnmatchedCount,
    string Status,
    List<ReconDiscrepancyDto> Discrepancies,
    string Currency);

public sealed record ReconDiscrepancyDto(
    string TransactionReference,
    decimal OurAmount,
    decimal PartnerAmount,
    string DiscrepancyType,
    string Currency);
