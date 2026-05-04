using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using GoldBank.Core.Common.Persistence;
using GoldBank.Core.Modules.WhiteLabel.Domain.Entities;
using GoldBank.SharedKernel.Caching;
using GoldBank.SharedKernel.Results;

namespace GoldBank.Core.Modules.WhiteLabel.Application.Handlers;

/// <summary>
/// Retrieves tenant branding with caching (10 min TTL) (STORY-068).
/// </summary>
public sealed class GetBrandingHandler
{
    private readonly GoldBankDbContext _dbContext;
    private readonly ICacheStore _cache;
    private readonly ILogger<GetBrandingHandler> _logger;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);

    public GetBrandingHandler(
        GoldBankDbContext dbContext,
        ICacheStore cache,
        ILogger<GetBrandingHandler> logger)
    {
        _dbContext = dbContext;
        _cache = cache;
        _logger = logger;
    }

    public async Task<Result<TenantBranding>> HandleAsync(
        string tenantId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return Result.Failure<TenantBranding>(
                new Error("Branding.InvalidTenant", "Tenant ID is required."));
        }

        var cacheKey = $"branding:{tenantId}";
        try
        {
            var cached = await _cache.GetAsync(cacheKey, cancellationToken);
            if (cached is not null)
            {
                var cachedBranding = JsonSerializer.Deserialize<TenantBranding>(cached);
                if (cachedBranding is not null)
                    return Result.Success(cachedBranding);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache read failed for branding:{TenantId}", tenantId);
        }

        var branding = await _dbContext.Set<TenantBranding>()
            .FirstOrDefaultAsync(b => b.TenantId == tenantId, cancellationToken);

        if (branding is null)
        {
            return Result.Failure<TenantBranding>(
                new Error("Branding.NotFound", $"No branding configuration found for tenant '{tenantId}'."));
        }

        try
        {
            var json = JsonSerializer.Serialize(branding);
            await _cache.SetAsync(cacheKey, json, CacheTtl, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache write failed for branding:{TenantId}", tenantId);
        }

        return Result.Success(branding);
    }
}
