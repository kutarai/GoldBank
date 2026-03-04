using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UniBank.Core.Common.Persistence;
using UniBank.Core.Modules.Merchants.Domain.Entities;
using UniBank.SharedKernel.Results;

namespace UniBank.Core.Modules.Admin.Application.Handlers;

/// <summary>
/// Manages merchant lifecycle: approve, reject, suspend, activate (STORY-057).
/// All actions are audit-logged for compliance.
/// </summary>
public sealed class ManageMerchantHandler
{
    private readonly UniBankDbContext _dbContext;
    private readonly CreateAuditLogHandler _auditLogHandler;
    private readonly ILogger<ManageMerchantHandler> _logger;

    public ManageMerchantHandler(
        UniBankDbContext dbContext,
        CreateAuditLogHandler auditLogHandler,
        ILogger<ManageMerchantHandler> logger)
    {
        _dbContext = dbContext;
        _auditLogHandler = auditLogHandler;
        _logger = logger;
    }

    public async Task<Result> HandleAsync(
        string merchantId,
        string action,
        string reason,
        string adminId,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(merchantId, out var merchantGuid))
            return Result.Failure(new Error("Admin.InvalidMerchantId", "Invalid merchant ID format."));

        var merchant = await _dbContext.Set<Merchant>()
            .FirstOrDefaultAsync(m => m.Id == merchantGuid, cancellationToken);

        if (merchant is null)
            return Result.Failure(Error.NotFound);

        var previousStatus = merchant.Status;

        merchant.Status = action.ToUpperInvariant() switch
        {
            "APPROVE" => "active",
            "SUSPEND" => "suspended",
            "ACTIVATE" => "active",
            "CLOSE" => "closed",
            _ => merchant.Status
        };

        if (action.Equals("APPROVE", StringComparison.OrdinalIgnoreCase))
        {
            merchant.KycStatus = "approved";
            merchant.ActivatedAt = DateTime.UtcNow;
        }

        merchant.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        if (Guid.TryParse(adminId, out var adminGuid))
        {
            await _auditLogHandler.HandleAsync(
                adminGuid,
                $"ManageMerchant.{action}",
                "Merchant",
                merchantId,
                $"Status changed from {previousStatus} to {merchant.Status}. Reason: {reason}",
                cancellationToken: cancellationToken);
        }

        _logger.LogInformation(
            "Merchant {MerchantId} status changed from {Previous} to {Current} by admin {AdminId}",
            merchantId, previousStatus, merchant.Status, adminId);

        return Result.Success();
    }
}
