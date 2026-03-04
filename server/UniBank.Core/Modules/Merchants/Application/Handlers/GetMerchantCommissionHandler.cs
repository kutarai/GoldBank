using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UniBank.Core.Common.Persistence;
using UniBank.Core.Modules.Agents.Domain.Entities;
using UniBank.Core.Modules.Merchants.Domain.Entities;
using UniBank.SharedKernel.Results;

namespace UniBank.Core.Modules.Merchants.Application.Handlers;

/// <summary>
/// Generates a commission report for agent-type merchants (STORY-054).
/// Queries AgentCommission records, groups by transaction type (cash_in, cash_out),
/// and calculates totals by type and overall.
/// </summary>
public sealed class GetMerchantCommissionHandler
{
    private readonly UniBankDbContext _dbContext;
    private readonly ILogger<GetMerchantCommissionHandler> _logger;

    public GetMerchantCommissionHandler(
        UniBankDbContext dbContext,
        ILogger<GetMerchantCommissionHandler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<Result<CommissionReportResult>> HandleAsync(
        GetMerchantCommissionQuery query, CancellationToken cancellationToken = default)
    {
        var merchant = await _dbContext.Set<Merchant>()
            .FirstOrDefaultAsync(m => m.Id == query.MerchantId, cancellationToken);

        if (merchant is null)
            return Result.Failure<CommissionReportResult>(
                new Error("Merchant.NotFound", "Merchant not found."));

        if (!merchant.IsAgent)
            return Result.Failure<CommissionReportResult>(
                new Error("Merchant.NotAgent", "Commission reports are only available for agent-type merchants."));

        var commissionsQuery = _dbContext.Set<AgentCommission>()
            .Where(c => c.MerchantId == query.MerchantId);

        // Apply date range filter
        if (query.DateFrom.HasValue)
            commissionsQuery = commissionsQuery.Where(c => c.CreatedAt >= query.DateFrom.Value);

        if (query.DateTo.HasValue)
            commissionsQuery = commissionsQuery.Where(c => c.CreatedAt < query.DateTo.Value);

        // Group by transaction type and calculate totals
        var groupedCommissions = await commissionsQuery
            .GroupBy(c => c.TransactionType)
            .Select(g => new
            {
                TransactionType = g.Key,
                TransactionCount = g.Count(),
                TotalTransactionAmount = g.Sum(c => c.TransactionAmount),
                AverageCommissionRate = g.Average(c => c.CommissionRate),
                TotalCommissionAmount = g.Sum(c => c.CommissionAmount),
                Currency = g.Min(c => c.Currency)
            })
            .ToListAsync(cancellationToken);

        var lineItems = groupedCommissions.Select(g => new CommissionLineItemResult(
            TransactionType: g.TransactionType,
            TransactionCount: g.TransactionCount,
            TotalTransactionAmount: g.TotalTransactionAmount,
            AverageCommissionRate: g.AverageCommissionRate,
            TotalCommissionAmount: g.TotalCommissionAmount,
            Currency: g.Currency ?? "ZWG"))
            .ToList();

        var totalCommission = lineItems.Sum(li => li.TotalCommissionAmount);
        var totalTransactions = lineItems.Sum(li => li.TransactionCount);
        var currency = lineItems.Count > 0 ? lineItems[0].Currency : "ZWG";

        _logger.LogInformation(
            "Generated commission report for merchant {MerchantId}: {TypeCount} types, {TotalTransactions} transactions, total commission={TotalCommission} {Currency}",
            query.MerchantId, lineItems.Count, totalTransactions, totalCommission, currency);

        return Result.Success(new CommissionReportResult(
            MerchantId: query.MerchantId,
            LineItems: lineItems,
            TotalCommission: totalCommission,
            TotalTransactions: totalTransactions,
            Currency: currency,
            DateFrom: query.DateFrom,
            DateTo: query.DateTo));
    }
}

public sealed record GetMerchantCommissionQuery(
    Guid MerchantId,
    DateTime? DateFrom,
    DateTime? DateTo);

public sealed record CommissionReportResult(
    Guid MerchantId,
    IReadOnlyList<CommissionLineItemResult> LineItems,
    decimal TotalCommission,
    int TotalTransactions,
    string Currency,
    DateTime? DateFrom,
    DateTime? DateTo);

public sealed record CommissionLineItemResult(
    string TransactionType,
    int TransactionCount,
    decimal TotalTransactionAmount,
    decimal AverageCommissionRate,
    decimal TotalCommissionAmount,
    string Currency);
