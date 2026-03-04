using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UniBank.Core.Common.Persistence;
using UniBank.Core.Modules.WhiteLabel.Domain.Entities;
using UniBank.SharedKernel.Results;

namespace UniBank.Core.Modules.WhiteLabel.Application.Handlers;

/// <summary>
/// Updates per-tenant fee rules and transaction limits (STORY-070).
/// Replaces existing configuration with the provided rules and limits.
/// </summary>
public sealed class UpdateFeeConfigHandler
{
    private readonly UniBankDbContext _dbContext;
    private readonly ILogger<UpdateFeeConfigHandler> _logger;

    public UpdateFeeConfigHandler(
        UniBankDbContext dbContext,
        ILogger<UpdateFeeConfigHandler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<Result<FeeConfigResult>> HandleAsync(
        string tenantId,
        List<TenantFeeConfig> feeConfigs,
        List<TenantTransactionLimit> transactionLimits,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return Result.Failure<FeeConfigResult>(
                new Error("FeeConfig.InvalidTenant", "Tenant ID is required."));
        }

        // Remove existing fee configs for this tenant
        var existingFees = await _dbContext.Set<TenantFeeConfig>()
            .Where(f => f.TenantId == tenantId)
            .ToListAsync(cancellationToken);
        _dbContext.Set<TenantFeeConfig>().RemoveRange(existingFees);

        // Remove existing limits for this tenant
        var existingLimits = await _dbContext.Set<TenantTransactionLimit>()
            .Where(l => l.TenantId == tenantId)
            .ToListAsync(cancellationToken);
        _dbContext.Set<TenantTransactionLimit>().RemoveRange(existingLimits);

        // Add new fee configs
        foreach (var fee in feeConfigs)
        {
            fee.TenantId = tenantId;
            fee.CreatedAt = DateTime.UtcNow;
            fee.UpdatedAt = DateTime.UtcNow;
        }
        _dbContext.Set<TenantFeeConfig>().AddRange(feeConfigs);

        // Add new limits
        foreach (var limit in transactionLimits)
        {
            limit.TenantId = tenantId;
            limit.CreatedAt = DateTime.UtcNow;
            limit.UpdatedAt = DateTime.UtcNow;
        }
        _dbContext.Set<TenantTransactionLimit>().AddRange(transactionLimits);

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Fee configuration updated for tenant {TenantId}: {FeeCount} fee rules, {LimitCount} limits",
            tenantId, feeConfigs.Count, transactionLimits.Count);

        return Result.Success(new FeeConfigResult(tenantId, feeConfigs, transactionLimits));
    }
}
