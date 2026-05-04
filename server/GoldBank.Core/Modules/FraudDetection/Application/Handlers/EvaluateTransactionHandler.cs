using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using GoldBank.Core.Common.Persistence;
using GoldBank.Core.Modules.FraudDetection.Application.Services;
using GoldBank.SharedKernel.Events;
using GoldBank.SharedKernel.Messaging;
using GoldBank.SharedKernel.Results;

namespace GoldBank.Core.Modules.FraudDetection.Application.Handlers;

/// <summary>
/// Automatically evaluates fraud rules on every completed transaction (STORY-072).
/// Wired as IMessageHandler&lt;TransactionCompleted&gt; so it fires whenever any handler
/// publishes a TransactionCompleted event (cash-in, cash-out, NFC, QR, P2P, etc.).
/// Persists FraudAlert entities and publishes FraudAlertRaised for each alert so that
/// the notification service can send SMS/push to the customer and compliance team.
/// </summary>
public sealed class EvaluateTransactionHandler : IMessageHandler<TransactionCompleted>
{
    private readonly FraudDetectionEngine _engine;
    private readonly GoldBankDbContext _dbContext;
    private readonly IMessageBus _messageBus;
    private readonly ILogger<EvaluateTransactionHandler> _logger;

    public EvaluateTransactionHandler(
        FraudDetectionEngine engine,
        GoldBankDbContext dbContext,
        IMessageBus messageBus,
        ILogger<EvaluateTransactionHandler> logger)
    {
        _engine = engine;
        _dbContext = dbContext;
        _messageBus = messageBus;
        _logger = logger;
    }

    /// <summary>
    /// IMessageHandler entry point — called automatically on TransactionCompleted events.
    /// </summary>
    public async Task HandleAsync(TransactionCompleted message, CancellationToken cancellationToken = default)
    {
        // Resolve counterparty phone from the transaction record for pattern analysis
        var counterpartyPhone = await _dbContext.Transactions
            .Where(t => t.Id == message.TransactionId)
            .Select(t => t.CounterpartyPhone)
            .FirstOrDefaultAsync(cancellationToken);

        var result = await _engine.EvaluateTransactionAsync(
            message.SourceAccountId,
            message.TransactionId,
            message.Amount,
            message.Currency,
            message.TransactionType,
            counterpartyPhone,
            message.TenantId ?? "default",
            cancellationToken);

        if (result.IsFailure)
        {
            _logger.LogError("Fraud evaluation failed for txn {TransactionId}: {Error}",
                message.TransactionId, result.Error.Message);
            return;
        }

        var alerts = result.Value;
        if (alerts.Count == 0) return;

        // Persist alerts
        _dbContext.FraudAlerts.AddRange(alerts);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Publish FraudAlertRaised for each alert so notifications fire
        foreach (var alert in alerts)
        {
            await _messageBus.PublishAsync(new FraudAlertRaised(
                TransactionId: alert.TransactionId,
                AccountId: alert.AccountId,
                AlertType: alert.AlertType,
                Description: alert.Description,
                Severity: alert.Severity), cancellationToken);
        }

        _logger.LogWarning(
            "Fraud detection triggered {AlertCount} alert(s) for transaction {TransactionId} on account {AccountId}: [{AlertTypes}]",
            alerts.Count, message.TransactionId, message.SourceAccountId,
            string.Join(", ", alerts.Select(a => a.AlertType)));
    }

    /// <summary>
    /// Manual invocation entry point (for admin-triggered re-evaluation or testing).
    /// </summary>
    public async Task<Result<int>> HandleAsync(
        EvaluateTransactionCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = await _engine.EvaluateTransactionAsync(
            command.AccountId,
            command.TransactionId,
            command.Amount,
            command.Currency,
            command.TransactionType,
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

        foreach (var alert in alerts)
        {
            await _messageBus.PublishAsync(new FraudAlertRaised(
                TransactionId: alert.TransactionId,
                AccountId: alert.AccountId,
                AlertType: alert.AlertType,
                Description: alert.Description,
                Severity: alert.Severity), cancellationToken);
        }

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
    string TransactionType,
    string? CounterpartyPhone,
    string TenantId);
