using Microsoft.Extensions.Logging;
using UniBank.Core.Common.Persistence;
using UniBank.Core.Modules.FraudDetection.Application.Services;
using UniBank.SharedKernel.Results;

namespace UniBank.Core.Modules.FraudDetection.Application.Handlers;

/// <summary>
/// Called after each transaction to evaluate fraud rules and create alerts if triggered (STORY-072).
/// Persists any generated FraudAlert entities to the database.
/// </summary>
public sealed class EvaluateTransactionHandler
{
    private readonly FraudDetectionEngine _engine;
    private readonly UniBankDbContext _dbContext;
    private readonly ILogger<EvaluateTransactionHandler> _logger;

    public EvaluateTransactionHandler(
        FraudDetectionEngine engine,
        UniBankDbContext dbContext,
        ILogger<EvaluateTransactionHandler> logger)
    {
        _engine = engine;
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<Result<int>> HandleAsync(
        EvaluateTransactionCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = await _engine.EvaluateTransactionAsync(
            command.AccountId,
            command.TransactionId,
            command.Amount,
            command.Currency,
            command.CounterpartyPhone,
            command.TenantId,
            cancellationToken);

        if (result.IsFailure)
            return Result.Failure<int>(result.Error);

        var alerts = result.Value;

        if (alerts.Count == 0)
            return Result.Success(0);

        _dbContext.FraudAlerts.AddRange(alerts);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogWarning(
            "Fraud detection triggered {AlertCount} alert(s) for transaction {TransactionId} on account {AccountId}",
            alerts.Count, command.TransactionId, command.AccountId);

        return Result.Success(alerts.Count);
    }
}

public sealed record EvaluateTransactionCommand(
    Guid AccountId,
    Guid TransactionId,
    decimal Amount,
    string Currency,
    string? CounterpartyPhone,
    string TenantId);
