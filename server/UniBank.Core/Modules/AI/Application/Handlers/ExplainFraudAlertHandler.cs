using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UniBank.Core.Common.Persistence;
using UniBank.Core.Modules.AI.Application.Commands;
using UniBank.Core.Modules.AI.Domain.Entities;
using UniBank.Core.Modules.AI.Infrastructure.Services;
using UniBank.SharedKernel.Results;

namespace UniBank.Core.Modules.AI.Application.Handlers;

public sealed record FraudExplanationResult(string Explanation, List<string> SuggestedActions);

public sealed class ExplainFraudAlertHandler
{
    private readonly UniBankDbContext _dbContext;
    private readonly OllamaClient _ollamaClient;
    private readonly ILogger<ExplainFraudAlertHandler> _logger;

    public ExplainFraudAlertHandler(
        UniBankDbContext dbContext, OllamaClient ollamaClient,
        ILogger<ExplainFraudAlertHandler> logger)
    {
        _dbContext = dbContext;
        _ollamaClient = ollamaClient;
        _logger = logger;
    }

    public async Task<Result<FraudExplanationResult>> HandleAsync(
        ExplainFraudAlertCommand command, CancellationToken cancellationToken = default)
    {
        var transaction = await _dbContext.Transactions
            .FirstOrDefaultAsync(t => t.Id == command.TransactionId, cancellationToken);

        var account = await _dbContext.Accounts
            .FirstOrDefaultAsync(a => a.Id == command.AccountId && a.DeletedAt == null, cancellationToken);

        var txnInfo = transaction is not null
            ? $"Transaction: {transaction.Description}, Amount: {transaction.Amount:N2} {transaction.Currency}, Date: {transaction.CreatedAt:dd/MM/yyyy}"
            : "Transaction details not available";

        var accountInfo = account is not null
            ? $"Customer: {account.FirstName}, usual transaction range: low-medium amounts"
            : "";

        var context = $"{txnInfo}\n{accountInfo}\n" +
                      $"Fraud rules triggered: {command.FraudRulesTriggered}\n" +
                      $"Risk score: {command.RiskScore:F2}";

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = await _ollamaClient.ChatAsync(
            "You are a fraud alert communicator for UniBank. Explain why this transaction was flagged " +
            "in simple, non-technical language. Always provide clear next steps. Be reassuring but serious. " +
            "Return ONLY valid JSON: {\"explanation\": \"...\", \"actions\": [\"action1\", \"action2\"]}",
            context,
            temperature: 0.2,
            cancellationToken: cancellationToken);
        sw.Stop();

        var explanation = result.Message.Content;
        var actions = new List<string> { "Confirm Transaction", "Report Fraud" };

        try
        {
            var parsed = System.Text.Json.JsonDocument.Parse(result.Message.Content);
            explanation = parsed.RootElement.GetProperty("explanation").GetString() ?? explanation;
            actions = parsed.RootElement.GetProperty("actions")
                .EnumerateArray().Select(a => a.GetString()!).ToList();
        }
        catch { /* use defaults */ }

        _dbContext.AiInteractions.Add(new AiInteraction
        {
            AccountId = command.AccountId,
            InteractionType = "fraud_explain",
            RequestSummary = $"Fraud alert for txn {command.TransactionId}, risk: {command.RiskScore:F2}",
            ResponseSummary = explanation.Length > 500 ? explanation[..500] : explanation,
            ModelUsed = "qwen3-vl",
            InferenceTimeMs = (int)sw.ElapsedMilliseconds,
            Success = true,
            TenantId = command.TenantId,
        });
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new FraudExplanationResult(explanation, actions));
    }
}
