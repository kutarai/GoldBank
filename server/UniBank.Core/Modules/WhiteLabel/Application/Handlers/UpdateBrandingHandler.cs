using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UniBank.Core.Common.Persistence;
using UniBank.Core.Modules.WhiteLabel.Application.Commands;
using UniBank.Core.Modules.WhiteLabel.Domain.Entities;
using UniBank.SharedKernel.Caching;
using UniBank.SharedKernel.Results;

namespace UniBank.Core.Modules.WhiteLabel.Application.Handlers;

/// <summary>
/// Updates tenant branding and invalidates cache (STORY-068).
/// </summary>
public sealed class UpdateBrandingHandler
{
    private readonly UniBankDbContext _dbContext;
    private readonly ICacheStore _cache;
    private readonly ILogger<UpdateBrandingHandler> _logger;

    public UpdateBrandingHandler(
        UniBankDbContext dbContext,
        ICacheStore cache,
        ILogger<UpdateBrandingHandler> logger)
    {
        _dbContext = dbContext;
        _cache = cache;
        _logger = logger;
    }

    public async Task<Result<TenantBranding>> HandleAsync(
        UpdateBrandingCommand command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.TenantId))
        {
            return Result.Failure<TenantBranding>(
                new Error("Branding.InvalidTenant", "Tenant ID is required."));
        }

        var branding = await _dbContext.Set<TenantBranding>()
            .FirstOrDefaultAsync(b => b.TenantId == command.TenantId, cancellationToken);

        if (branding is null)
        {
            branding = new TenantBranding
            {
                TenantId = command.TenantId,
                AppName = command.AppName,
                LogoUrl = command.LogoUrl,
                PrimaryColor = command.PrimaryColor,
                SecondaryColor = command.SecondaryColor,
                AccentColor = command.AccentColor,
                FaviconUrl = command.FaviconUrl,
                SupportEmail = command.SupportEmail,
                SupportPhone = command.SupportPhone,
                CustomCss = command.CustomCss,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _dbContext.Set<TenantBranding>().Add(branding);
        }
        else
        {
            branding.AppName = command.AppName;
            branding.LogoUrl = command.LogoUrl;
            branding.PrimaryColor = command.PrimaryColor;
            branding.SecondaryColor = command.SecondaryColor;
            branding.AccentColor = command.AccentColor;
            branding.FaviconUrl = command.FaviconUrl;
            branding.SupportEmail = command.SupportEmail;
            branding.SupportPhone = command.SupportPhone;
            branding.CustomCss = command.CustomCss;
            branding.UpdatedAt = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            await _cache.DeleteAsync($"branding:{command.TenantId}", cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cache invalidation failed for branding:{TenantId}", command.TenantId);
        }

        _logger.LogInformation("Branding updated for tenant {TenantId}", command.TenantId);

        return Result.Success(branding);
    }
}
