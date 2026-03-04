using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UniBank.Core.Common.Persistence;
using UniBank.Core.Modules.Accounts.Domain.Entities;
using UniBank.Core.Modules.Admin.Domain.Entities;
using UniBank.SharedKernel.Results;

namespace UniBank.Core.Modules.Admin.Application.Handlers;

/// <summary>
/// Creates a dispute/chargeback record for a transaction (STORY-061).
/// Validates that the transaction exists before creating the dispute.
/// </summary>
public sealed class CreateDisputeHandler
{
    private readonly UniBankDbContext _dbContext;
    private readonly CreateAuditLogHandler _auditLogHandler;
    private readonly ILogger<CreateDisputeHandler> _logger;

    public CreateDisputeHandler(
        UniBankDbContext dbContext,
        CreateAuditLogHandler auditLogHandler,
        ILogger<CreateDisputeHandler> logger)
    {
        _dbContext = dbContext;
        _auditLogHandler = auditLogHandler;
        _logger = logger;
    }

    public async Task<Result<DisputeDto>> HandleAsync(
        string transactionId,
        string accountId,
        string type,
        string description,
        string adminId,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(transactionId, out var txGuid))
            return Result.Failure<DisputeDto>(new Error("Admin.InvalidTransactionId", "Invalid transaction ID format."));

        if (!Guid.TryParse(accountId, out var accountGuid))
            return Result.Failure<DisputeDto>(new Error("Admin.InvalidAccountId", "Invalid account ID format."));

        var transaction = await _dbContext.Set<Transaction>()
            .FirstOrDefaultAsync(t => t.Id == txGuid, cancellationToken);

        if (transaction is null)
            return Result.Failure<DisputeDto>(new Error("Admin.TransactionNotFound", "Transaction not found."));

        if (!System.Enum.TryParse<DisputeType>(type, true, out var disputeType))
        {
            disputeType = DisputeType.Other;
        }

        var dispute = new Dispute
        {
            TransactionId = txGuid,
            AccountId = accountGuid,
            Type = disputeType,
            Description = description,
            Status = DisputeStatus.Open,
            RefundCurrency = transaction.Currency,
            AdminUserId = Guid.TryParse(adminId, out var adminGuid) ? adminGuid : null
        };

        _dbContext.Set<Dispute>().Add(dispute);
        await _dbContext.SaveChangesAsync(cancellationToken);

        if (Guid.TryParse(adminId, out var auditAdminGuid))
        {
            await _auditLogHandler.HandleAsync(
                auditAdminGuid,
                "CreateDispute",
                "Dispute",
                dispute.Id.ToString(),
                $"Dispute created for transaction {transactionId}. Type: {type}. Description: {description}",
                cancellationToken: cancellationToken);
        }

        _logger.LogInformation(
            "Dispute {DisputeId} created for transaction {TransactionId} by admin {AdminId}",
            dispute.Id, transactionId, adminId);

        return Result.Success(MapToDto(dispute));
    }

    internal static DisputeDto MapToDto(Dispute dispute)
    {
        return new DisputeDto(
            DisputeId: dispute.Id.ToString(),
            TransactionId: dispute.TransactionId.ToString(),
            AccountId: dispute.AccountId.ToString(),
            Type: dispute.Type.ToString(),
            Description: dispute.Description,
            Status: dispute.Status.ToString(),
            Resolution: dispute.Resolution,
            RefundAmount: dispute.RefundAmount,
            RefundCurrency: dispute.RefundCurrency,
            AdminUserId: dispute.AdminUserId?.ToString(),
            CreatedAt: dispute.CreatedAt,
            ResolvedAt: dispute.ResolvedAt);
    }
}

public sealed record DisputeDto(
    string DisputeId,
    string TransactionId,
    string AccountId,
    string Type,
    string Description,
    string Status,
    string? Resolution,
    decimal? RefundAmount,
    string RefundCurrency,
    string? AdminUserId,
    DateTime CreatedAt,
    DateTime? ResolvedAt);
