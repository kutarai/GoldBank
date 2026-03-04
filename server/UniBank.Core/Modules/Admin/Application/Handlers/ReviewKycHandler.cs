using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UniBank.Core.Common.Persistence;
using UniBank.Core.Modules.Accounts.Domain.Entities;
using UniBank.Core.Modules.KYC.Domain.Entities;
using UniBank.SharedKernel.Results;

namespace UniBank.Core.Modules.Admin.Application.Handlers;

/// <summary>
/// Reviews KYC documents: approve, reject, or request resubmission (STORY-059).
/// Logs reviewer ID and timestamp for compliance audit trail.
/// </summary>
public sealed class ReviewKycHandler
{
    private readonly UniBankDbContext _dbContext;
    private readonly CreateAuditLogHandler _auditLogHandler;
    private readonly ILogger<ReviewKycHandler> _logger;

    public ReviewKycHandler(
        UniBankDbContext dbContext,
        CreateAuditLogHandler auditLogHandler,
        ILogger<ReviewKycHandler> logger)
    {
        _dbContext = dbContext;
        _auditLogHandler = auditLogHandler;
        _logger = logger;
    }

    public async Task<Result> HandleAsync(
        string documentId,
        string decision,
        string notes,
        string adminId,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(documentId, out var docGuid))
            return Result.Failure(new Error("Admin.InvalidDocumentId", "Invalid document ID format."));

        var document = await _dbContext.Set<KycDocument>()
            .FirstOrDefaultAsync(d => d.Id == docGuid, cancellationToken);

        if (document is null)
            return Result.Failure(Error.NotFound);

        var previousStatus = document.Status;

        document.Status = decision.ToUpperInvariant() switch
        {
            "APPROVE" => "approved",
            "REJECT" => "rejected",
            "REQUEST_RESUBMIT" => "resubmit_requested",
            _ => document.Status
        };

        if (decision.Equals("APPROVE", StringComparison.OrdinalIgnoreCase))
        {
            document.VerifiedAt = DateTime.UtcNow;

            // Upgrade account KYC level when document is approved
            var account = await _dbContext.Set<Account>()
                .FirstOrDefaultAsync(a => a.Id == document.AccountId, cancellationToken);

            if (account is not null)
            {
                account.KycLevel = Math.Min(account.KycLevel + 1, 3);

                if (account.Status == "pending_kyc" && account.KycLevel >= 1)
                {
                    account.Status = "active";
                }

                account.UpdatedAt = DateTime.UtcNow;
            }
        }

        document.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        if (Guid.TryParse(adminId, out var adminGuid))
        {
            await _auditLogHandler.HandleAsync(
                adminGuid,
                $"ReviewKYC.{decision}",
                "KycDocument",
                documentId,
                $"Status changed from {previousStatus} to {document.Status}. Notes: {notes}",
                cancellationToken: cancellationToken);
        }

        _logger.LogInformation(
            "KYC document {DocumentId} reviewed: {Decision} by admin {AdminId}",
            documentId, decision, adminId);

        return Result.Success();
    }
}
