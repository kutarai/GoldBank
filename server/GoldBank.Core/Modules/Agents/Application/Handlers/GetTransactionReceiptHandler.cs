using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using GoldBank.Core.Common.Persistence;
using GoldBank.SharedKernel.Results;

namespace GoldBank.Core.Modules.Agents.Application.Handlers;

/// <summary>
/// Result for a transaction receipt query.
/// </summary>
public sealed record TransactionReceiptResult(
    string ReceiptNumber,
    string TransactionType,
    string CustomerPhone,
    decimal Amount,
    decimal Commission,
    decimal NetAmount,
    string AgentName,
    string Reference,
    DateTime Timestamp,
    string Status,
    string Currency);

/// <summary>
/// Handles transaction receipt retrieval for agent transactions (STORY-036).
/// Loads the transaction and related commission record, then formats receipt data.
/// </summary>
public sealed class GetTransactionReceiptHandler
{
    private readonly GoldBankDbContext _dbContext;
    private readonly ILogger<GetTransactionReceiptHandler> _logger;

    public GetTransactionReceiptHandler(
        GoldBankDbContext dbContext,
        ILogger<GetTransactionReceiptHandler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<Result<TransactionReceiptResult>> HandleAsync(
        Guid transactionId, Guid agentMerchantId, CancellationToken cancellationToken = default)
    {
        // Verify agent merchant exists
        var merchant = await _dbContext.Merchants
            .FirstOrDefaultAsync(
                m => m.Id == agentMerchantId && m.BusinessType == "agent",
                cancellationToken);

        if (merchant is null)
            return Result.Failure<TransactionReceiptResult>(
                new Error("Agent.NotFound", "Agent merchant not found."));

        // Load the transaction - look for transactions tied to the agent's account
        var transaction = await _dbContext.Transactions
            .FirstOrDefaultAsync(
                t => t.Id == transactionId,
                cancellationToken);

        if (transaction is null)
            return Result.Failure<TransactionReceiptResult>(
                new Error("Transaction.NotFound", "Transaction not found."));

        // Load related commission record
        var commission = await _dbContext.AgentCommissions
            .FirstOrDefaultAsync(
                c => c.TransactionId == transactionId && c.MerchantId == agentMerchantId,
                cancellationToken);

        if (commission is null)
            return Result.Failure<TransactionReceiptResult>(
                new Error("Commission.NotFound", "Commission record not found for this transaction."));

        var amount = Math.Abs(transaction.Amount);
        var commissionAmount = commission.CommissionAmount;
        var netAmount = amount - commissionAmount;

        var receiptNumber = $"RCP-{transaction.CreatedAt:yyyyMMdd}-{transactionId.ToString("N")[..10].ToUpperInvariant()}";

        _logger.LogDebug(
            "Receipt generated for transaction {TransactionId}, agent {AgentId}",
            transactionId, agentMerchantId);

        return Result.Success(new TransactionReceiptResult(
            ReceiptNumber: receiptNumber,
            TransactionType: commission.TransactionType,
            CustomerPhone: transaction.CounterpartyPhone ?? string.Empty,
            Amount: amount,
            Commission: commissionAmount,
            NetAmount: netAmount,
            AgentName: merchant.BusinessName,
            Reference: transaction.Reference ?? string.Empty,
            Timestamp: transaction.CompletedAt ?? transaction.CreatedAt,
            Status: transaction.Status,
            Currency: transaction.Currency));
    }
}
