using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using GoldBank.Core.Common.Persistence;
using GoldBank.Core.Modules.WhiteLabel.Domain.Entities;
using GoldBank.SharedKernel.Results;

namespace GoldBank.Core.Modules.WhiteLabel.Application.Handlers;

/// <summary>
/// Retrieves per-tenant fee and limit configuration (STORY-070).
/// </summary>
public sealed class GetFeeConfigHandler
{
    private readonly GoldBankDbContext _dbContext;
    private readonly ILogger<GetFeeConfigHandler> _logger;

    public GetFeeConfigHandler(
        GoldBankDbContext dbContext,
        ILogger<GetFeeConfigHandler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<Result<FeeConfigResult>> HandleAsync(
        string tenantId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return Result.Failure<FeeConfigResult>(
                new Error("FeeConfig.InvalidTenant", "Tenant ID is required."));
        }

        _logger.LogDebug("Retrieving fee config for tenant {TenantId}", tenantId);

        var feeConfigs = await _dbContext.Set<TenantFeeConfig>()
            .Where(f => f.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        var limits = await _dbContext.Set<TenantTransactionLimit>()
            .Where(l => l.TenantId == tenantId)
            .ToListAsync(cancellationToken);

        return Result.Success(new FeeConfigResult(tenantId, feeConfigs, limits));
    }
}

public sealed record FeeConfigResult(
    string TenantId,
    List<TenantFeeConfig> FeeConfigs,
    List<TenantTransactionLimit> TransactionLimits);
