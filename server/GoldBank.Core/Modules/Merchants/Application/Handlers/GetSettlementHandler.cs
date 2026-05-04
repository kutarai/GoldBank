using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using GoldBank.Core.Common.Persistence;
using GoldBank.Core.Modules.Merchants.Domain.Entities;
using GoldBank.Core.Modules.Payments.Domain.Entities;
using GoldBank.SharedKernel.Results;

namespace GoldBank.Core.Modules.Merchants.Application.Handlers;

/// <summary>
/// Calculates or retrieves a merchant settlement for a given period (STORY-052).
/// Sums all completed payments where the merchant is the recipient, groups by currency,
/// and creates or returns an existing MerchantSettlement record.
/// </summary>
public sealed class GetSettlementHandler
{
    private readonly GoldBankDbContext _dbContext;
    private readonly ILogger<GetSettlementHandler> _logger;

    public GetSettlementHandler(GoldBankDbContext dbContext, ILogger<GetSettlementHandler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<Result<SettlementResult>> HandleAsync(
        GetSettlementQuery query, CancellationToken cancellationToken = default)
    {
        var merchant = await _dbContext.Set<Merchant>()
            .FirstOrDefaultAsync(m => m.Id == query.MerchantId, cancellationToken);

        if (merchant is null)
            return Result.Failure<SettlementResult>(
                new Error("Merchant.NotFound", "Merchant not found."));

        var currency = string.IsNullOrEmpty(query.Currency) ? "ZWG" : query.Currency;

        // Check if a settlement already exists for this period and currency
        var existingSettlement = await _dbContext.Set<MerchantSettlement>()
            .FirstOrDefaultAsync(s =>
                s.MerchantId == query.MerchantId &&
                s.PeriodStart == query.PeriodStart &&
                s.PeriodEnd == query.PeriodEnd &&
                s.Currency == currency,
                cancellationToken);

        if (existingSettlement is not null)
        {
            _logger.LogInformation(
                "Returning existing settlement {SettlementId} for merchant {MerchantId}",
                existingSettlement.Id, query.MerchantId);

            return Result.Success(MapToResult(existingSettlement));
        }

        // Calculate settlement from completed payments in the period
        var payments = await _dbContext.Set<Payment>()
            .Where(p =>
                p.MerchantAccountId == query.MerchantId &&
                p.Status == "completed" &&
                p.Currency == currency &&
                p.CompletedAt >= query.PeriodStart &&
                p.CompletedAt < query.PeriodEnd)
            .ToListAsync(cancellationToken);

        var totalTransactions = payments.Count;
        var grossAmount = payments.Sum(p => p.Amount);
        var totalFees = payments.Sum(p => p.Fee);
        var netAmount = grossAmount - totalFees;

        var settlement = new MerchantSettlement
        {
            MerchantId = query.MerchantId,
            PeriodStart = query.PeriodStart,
            PeriodEnd = query.PeriodEnd,
            TotalTransactions = totalTransactions,
            GrossAmount = grossAmount,
            TotalFees = totalFees,
            NetAmount = netAmount,
            Currency = currency,
            Status = "pending",
            Reference = $"STL-{merchant.MerchantCode}-{query.PeriodStart:yyyyMMdd}-{query.PeriodEnd:yyyyMMdd}",
            TenantId = merchant.TenantId
        };

        _dbContext.Set<MerchantSettlement>().Add(settlement);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Created settlement {SettlementId} for merchant {MerchantId}: {TransactionCount} transactions, gross={Gross}, fees={Fees}, net={Net} {Currency}",
            settlement.Id, query.MerchantId, totalTransactions,
            grossAmount, totalFees, netAmount, currency);

        return Result.Success(MapToResult(settlement));
    }

    private static SettlementResult MapToResult(MerchantSettlement settlement) =>
        new(
            SettlementId: settlement.Id,
            MerchantId: settlement.MerchantId,
            PeriodStart: settlement.PeriodStart,
            PeriodEnd: settlement.PeriodEnd,
            TotalTransactions: settlement.TotalTransactions,
            GrossAmount: settlement.GrossAmount,
            TotalFees: settlement.TotalFees,
            NetAmount: settlement.NetAmount,
            Currency: settlement.Currency,
            Status: settlement.Status,
            PaidAt: settlement.PaidAt,
            Reference: settlement.Reference,
            CreatedAt: settlement.CreatedAt);
}

public sealed record GetSettlementQuery(
    Guid MerchantId,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    string? Currency);

public sealed record SettlementResult(
    Guid SettlementId,
    Guid MerchantId,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    int TotalTransactions,
    decimal GrossAmount,
    decimal TotalFees,
    decimal NetAmount,
    string Currency,
    string Status,
    DateTime? PaidAt,
    string Reference,
    DateTime CreatedAt);
