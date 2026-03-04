using Microsoft.Extensions.Logging;
using UniBank.Core.Common.Persistence;
using UniBank.Core.Modules.Admin.Domain.Entities;
using UniBank.SharedKernel.Results;

namespace UniBank.Core.Modules.Admin.Application.Handlers;

/// <summary>
/// Creates audit log entries for admin actions (STORY-055).
/// Every admin operation should be logged for compliance and traceability.
/// </summary>
public sealed class CreateAuditLogHandler
{
    private readonly UniBankDbContext _dbContext;
    private readonly ILogger<CreateAuditLogHandler> _logger;

    public CreateAuditLogHandler(
        UniBankDbContext dbContext,
        ILogger<CreateAuditLogHandler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<Result> HandleAsync(
        Guid adminUserId,
        string action,
        string entityType,
        string entityId,
        string? details = null,
        string? ipAddress = null,
        CancellationToken cancellationToken = default)
    {
        var auditLog = new AuditLog
        {
            AdminUserId = adminUserId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            Details = details,
            IpAddress = ipAddress
        };

        _dbContext.Set<AuditLog>().Add(auditLog);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Audit log created: Admin {AdminUserId} performed {Action} on {EntityType} {EntityId}",
            adminUserId, action, entityType, entityId);

        return Result.Success();
    }
}
