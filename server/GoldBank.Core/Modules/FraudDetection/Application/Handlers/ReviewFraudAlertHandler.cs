using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using GoldBank.Core.Common.Persistence;
using GoldBank.SharedKernel.Results;

namespace GoldBank.Core.Modules.FraudDetection.Application.Handlers;

/// <summary>
/// Allows an admin to review and resolve a fraud alert (STORY-072).
/// Supports confirming or dismissing alerts, with optional account suspension for Critical severity.
/// </summary>
public sealed class ReviewFraudAlertHandler
{
    private readonly GoldBankDbContext _dbContext;
    private readonly ILogger<ReviewFraudAlertHandler> _logger;

    public ReviewFraudAlertHandler(
        GoldBankDbContext dbContext,
        ILogger<ReviewFraudAlertHandler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<Result> HandleAsync(
        ReviewFraudAlertCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(command.AlertId, out var alertId))
            return Result.Failure(new Error("FraudAlert.InvalidId", "Invalid alert ID format."));

        var alert = await _dbContext.FraudAlerts
            .FirstOrDefaultAsync(a => a.Id == alertId, cancellationToken);

        if (alert is null)
            return Result.Failure(new Error("FraudAlert.NotFound", "Fraud alert not found."));

        if (alert.Status is "Confirmed" or "Dismissed")
            return Result.Failure(new Error("FraudAlert.AlreadyReviewed", "This alert has already been reviewed."));

        var now = DateTime.UtcNow;

        var validDecisions = new[] { "confirm", "dismiss" };
        if (!validDecisions.Contains(command.Decision, StringComparer.OrdinalIgnoreCase))
            return Result.Failure(new Error("FraudAlert.InvalidDecision", "Decision must be 'confirm' or 'dismiss'."));

        var isConfirmed = string.Equals(command.Decision, "confirm", StringComparison.OrdinalIgnoreCase);
        alert.Status = isConfirmed ? "Confirmed" : "Dismissed";
        alert.ReviewedAt = now;
        alert.ReviewedBy = command.AdminId;
        alert.AdminNotes = command.Notes;
        alert.UpdatedAt = now;

        // Auto-suspend account for confirmed Critical severity alerts
        if (isConfirmed && command.SuspendAccount && alert.Severity == "Critical")
        {
            var account = await _dbContext.Accounts
                .FirstOrDefaultAsync(a => a.Id == alert.AccountId, cancellationToken);

            if (account is not null && account.Status != "suspended")
            {
                account.Status = "suspended";
                account.UpdatedAt = now;

                _logger.LogWarning(
                    "Account {AccountId} auto-suspended due to confirmed Critical fraud alert {AlertId} by admin {AdminId}",
                    alert.AccountId, alertId, command.AdminId);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Fraud alert {AlertId} reviewed by admin {AdminId}: decision={Decision}",
            alertId, command.AdminId, command.Decision);

        return Result.Success();
    }
}

public sealed record ReviewFraudAlertCommand(
    string AlertId,
    string AdminId,
    string Decision,
    string? Notes,
    bool SuspendAccount);
