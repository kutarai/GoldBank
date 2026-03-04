using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UniBank.Core.Common.Persistence;
using UniBank.SharedKernel.Results;

namespace UniBank.Core.Modules.Agents.Application.Handlers;

/// <summary>
/// A single line item in a commission report, grouped by transaction type.
/// </summary>
public sealed record CommissionLineItemResult(
    string TransactionType,
    int Count,
    decimal TotalAmount,
    decimal TotalCommission,
    string Currency);

/// <summary>
/// Result for a commission report query.
/// </summary>
public sealed record CommissionReportResult(
    Guid AgentId,
    decimal TotalCommission,
    int TotalTransactions,
    IReadOnlyList<CommissionLineItemResult> Items,
    string Currency);

/// <summary>
/// Handles commission report generation for agent merchants.
/// Queries AgentCommission records for a date range, groups by transaction type,
/// and returns aggregated commission data.
/// </summary>
public sealed class GetCommissionReportHandler
{
    private readonly UniBankDbContext _dbContext;
    private readonly ILogger<GetCommissionReportHandler> _logger;

    public GetCommissionReportHandler(
        UniBankDbContext dbContext,
        ILogger<GetCommissionReportHandler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<Result<CommissionReportResult>> HandleAsync(
        Guid agentMerchantId, DateTime from, DateTime to, CancellationToken cancellationToken = default)
    {
        // Verify agent merchant exists
        var merchant = await _dbContext.Merchants
            .FirstOrDefaultAsync(
                m => m.Id == agentMerchantId && m.BusinessType == "agent",
                cancellationToken);

        if (merchant is null)
            return Result.Failure<CommissionReportResult>(
                new Error("Agent.NotFound", "Agent merchant not found."));

        // Query commissions within the date range
        var commissions = await _dbContext.AgentCommissions
            .Where(c => c.MerchantId == agentMerchantId
                        && c.CreatedAt >= from
                        && c.CreatedAt <= to)
            .ToListAsync(cancellationToken);

        // Group by transaction type
        var grouped = commissions
            .GroupBy(c => c.TransactionType)
            .Select(g => new CommissionLineItemResult(
                TransactionType: g.Key,
                Count: g.Count(),
                TotalAmount: g.Sum(c => c.TransactionAmount),
                TotalCommission: g.Sum(c => c.CommissionAmount),
                Currency: g.FirstOrDefault()?.Currency ?? "ZWG"))
            .ToList();

        var totalCommission = commissions.Sum(c => c.CommissionAmount);
        var totalTransactions = commissions.Count;

        // Determine currency from commissions or fall back to default
        var currency = commissions.FirstOrDefault()?.Currency ?? "ZWG";

        _logger.LogDebug(
            "Commission report generated for agent {AgentId}: {TotalTransactions} transactions, total commission {TotalCommission}",
            agentMerchantId, totalTransactions, totalCommission);

        return Result.Success(new CommissionReportResult(
            AgentId: agentMerchantId,
            TotalCommission: totalCommission,
            TotalTransactions: totalTransactions,
            Items: grouped,
            Currency: currency));
    }
}
