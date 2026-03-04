using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UniBank.Core.Common.Persistence;
using UniBank.Core.Modules.Accounts.Domain.Entities;
using UniBank.SharedKernel.Results;

namespace UniBank.Core.Modules.Admin.Application.Handlers;

/// <summary>
/// Manages account lifecycle operations: suspend, freeze, close, activate (STORY-056).
/// All actions are audit-logged for compliance.
/// </summary>
public sealed class ManageAccountHandler
{
    private readonly UniBankDbContext _dbContext;
    private readonly CreateAuditLogHandler _auditLogHandler;
    private readonly ILogger<ManageAccountHandler> _logger;

    public ManageAccountHandler(
        UniBankDbContext dbContext,
        CreateAuditLogHandler auditLogHandler,
        ILogger<ManageAccountHandler> logger)
    {
        _dbContext = dbContext;
        _auditLogHandler = auditLogHandler;
        _logger = logger;
    }

    public async Task<Result> HandleAsync(
        string accountId,
        string action,
        string reason,
        string adminId,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(accountId, out var accountGuid))
            return Result.Failure(new Error("Admin.InvalidAccountId", "Invalid account ID format."));

        var account = await _dbContext.Set<Account>()
            .FirstOrDefaultAsync(a => a.Id == accountGuid, cancellationToken);

        if (account is null)
            return Result.Failure(Error.NotFound);

        var previousStatus = account.Status;

        account.Status = action.ToUpperInvariant() switch
        {
            "SUSPEND" => "suspended",
            "FREEZE" => "frozen",
            "CLOSE" => "closed",
            "ACTIVATE" => "active",
            "UNFREEZE" => "active",
            "RESET_PIN" => account.Status,
            _ => account.Status
        };

        if (action.Equals("RESET_PIN", StringComparison.OrdinalIgnoreCase))
        {
            account.PinHash = null;
        }

        account.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        if (Guid.TryParse(adminId, out var adminGuid))
        {
            await _auditLogHandler.HandleAsync(
                adminGuid,
                $"ManageAccount.{action}",
                "Account",
                accountId,
                $"Status changed from {previousStatus} to {account.Status}. Reason: {reason}",
                cancellationToken: cancellationToken);
        }

        _logger.LogInformation(
            "Account {AccountId} status changed from {Previous} to {Current} by admin {AdminId}",
            accountId, previousStatus, account.Status, adminId);

        return Result.Success();
    }
}
