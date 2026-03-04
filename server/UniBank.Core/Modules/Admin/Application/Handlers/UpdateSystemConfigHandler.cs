using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UniBank.Core.Common.Persistence;
using UniBank.Core.Modules.Admin.Domain.Entities;
using UniBank.SharedKernel.Results;

namespace UniBank.Core.Modules.Admin.Application.Handlers;

/// <summary>
/// Updates or creates system configuration entries with audit logging (STORY-060).
/// Supports tenant-specific overrides when TenantId is provided.
/// </summary>
public sealed class UpdateSystemConfigHandler
{
    private readonly UniBankDbContext _dbContext;
    private readonly CreateAuditLogHandler _auditLogHandler;
    private readonly ILogger<UpdateSystemConfigHandler> _logger;

    public UpdateSystemConfigHandler(
        UniBankDbContext dbContext,
        CreateAuditLogHandler auditLogHandler,
        ILogger<UpdateSystemConfigHandler> logger)
    {
        _dbContext = dbContext;
        _auditLogHandler = auditLogHandler;
        _logger = logger;
    }

    public async Task<Result> HandleAsync(
        string key,
        string valueJson,
        string? tenantId,
        string adminId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            return Result.Failure(new Error("Admin.InvalidConfigKey", "Configuration key is required."));

        var existing = await _dbContext.Set<SystemConfig>()
            .FirstOrDefaultAsync(c => c.Key == key && c.TenantId == tenantId, cancellationToken);

        string previousValue;

        if (existing is not null)
        {
            previousValue = existing.ValueJson;
            existing.ValueJson = valueJson;
            existing.UpdatedBy = Guid.TryParse(adminId, out var parsedId) ? parsedId : null;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            previousValue = "(new)";
            var config = new SystemConfig
            {
                Key = key,
                ValueJson = valueJson,
                TenantId = string.IsNullOrWhiteSpace(tenantId) ? null : tenantId,
                UpdatedBy = Guid.TryParse(adminId, out var parsedId) ? parsedId : null
            };
            _dbContext.Set<SystemConfig>().Add(config);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        if (Guid.TryParse(adminId, out var adminGuid))
        {
            await _auditLogHandler.HandleAsync(
                adminGuid,
                "UpdateSystemConfig",
                "SystemConfig",
                key,
                $"Previous: {previousValue}, New: {valueJson}, Tenant: {tenantId ?? "global"}",
                cancellationToken: cancellationToken);
        }

        _logger.LogInformation(
            "System config {Key} updated by admin {AdminId} for tenant {TenantId}",
            key, adminId, tenantId ?? "global");

        return Result.Success();
    }
}
