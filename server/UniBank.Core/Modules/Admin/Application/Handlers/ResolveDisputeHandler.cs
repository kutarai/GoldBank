using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UniBank.Core.Common.Persistence;
using UniBank.Core.Modules.Accounts.Domain.Entities;
using UniBank.Core.Modules.Admin.Domain.Entities;
using UniBank.SharedKernel.Results;

namespace UniBank.Core.Modules.Admin.Application.Handlers;

/// <summary>
/// Resolves a dispute: processes refund/reversal if approved, or rejects (STORY-061).
/// Creates a refund transaction when the dispute is resolved with a refund.
/// </summary>
public sealed class ResolveDisputeHandler
{
    private readonly UniBankDbContext _dbContext;
    private readonly CreateAuditLogHandler _auditLogHandler;
    private readonly ILogger<ResolveDisputeHandler> _logger;

    public ResolveDisputeHandler(
        UniBankDbContext dbContext,
        CreateAuditLogHandler auditLogHandler,
        ILogger<ResolveDisputeHandler> logger)
    {
        _dbContext = dbContext;
        _auditLogHandler = auditLogHandler;
        _logger = logger;
    }

    public async Task<Result<DisputeDto>> HandleAsync(
        string disputeId,
        string resolution,
        string status,
        decimal? refundAmount,
        string? refundCurrency,
        string adminId,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(disputeId, out var disputeGuid))
            return Result.Failure<DisputeDto>(new Error("Admin.InvalidDisputeId", "Invalid dispute ID format."));

        var dispute = await _dbContext.Set<Dispute>()
            .FirstOrDefaultAsync(d => d.Id == disputeGuid, cancellationToken);

        if (dispute is null)
            return Result.Failure<DisputeDto>(Error.NotFound);

        if (dispute.Status is DisputeStatus.Resolved or DisputeStatus.Rejected)
            return Result.Failure<DisputeDto>(new Error("Admin.DisputeAlreadyClosed", "Dispute has already been resolved or rejected."));

        if (!System.Enum.TryParse<DisputeStatus>(status, true, out var newStatus))
        {
            return Result.Failure<DisputeDto>(new Error("Admin.InvalidDisputeStatus", "Invalid dispute status."));
        }

        dispute.Resolution = resolution;
        dispute.Status = newStatus;
        dispute.ResolvedAt = DateTime.UtcNow;
        dispute.AdminUserId = Guid.TryParse(adminId, out var adminGuid) ? adminGuid : dispute.AdminUserId;
        dispute.UpdatedAt = DateTime.UtcNow;

        if (newStatus == DisputeStatus.Resolved && refundAmount.HasValue && refundAmount.Value > 0)
        {
            dispute.RefundAmount = refundAmount.Value;
            dispute.RefundCurrency = refundCurrency ?? dispute.RefundCurrency;

            // Create refund transaction
            var account = await _dbContext.Set<Account>()
                .FirstOrDefaultAsync(a => a.Id == dispute.AccountId, cancellationToken);

            if (account is not null)
            {
                account.Balance += refundAmount.Value;
                account.AvailableBalance += refundAmount.Value;
                account.UpdatedAt = DateTime.UtcNow;

                var refundTx = new Transaction
                {
                    AccountId = dispute.AccountId,
                    Type = "refund",
                    Amount = refundAmount.Value,
                    Fee = 0,
                    Status = "completed",
                    Reference = $"REFUND-{dispute.Id}",
                    Description = $"Dispute refund: {resolution}",
                    BalanceAfter = account.Balance,
                    Currency = dispute.RefundCurrency,
                    TenantId = account.TenantId,
                    CompletedAt = DateTime.UtcNow
                };
                _dbContext.Set<Transaction>().Add(refundTx);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        if (Guid.TryParse(adminId, out var auditAdminGuid))
        {
            await _auditLogHandler.HandleAsync(
                auditAdminGuid,
                $"ResolveDispute.{status}",
                "Dispute",
                disputeId,
                $"Resolution: {resolution}. Refund: {refundAmount?.ToString("F2") ?? "N/A"} {refundCurrency ?? dispute.RefundCurrency}",
                cancellationToken: cancellationToken);
        }

        _logger.LogInformation(
            "Dispute {DisputeId} resolved with status {Status} by admin {AdminId}",
            disputeId, status, adminId);

        return Result.Success(CreateDisputeHandler.MapToDto(dispute));
    }
}
