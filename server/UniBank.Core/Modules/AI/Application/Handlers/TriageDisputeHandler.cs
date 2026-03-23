using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UniBank.Core.Common.Persistence;
using UniBank.Core.Modules.AI.Application.Commands;
using UniBank.Core.Modules.AI.Domain.Entities;
using UniBank.Core.Modules.AI.Infrastructure.Services;
using UniBank.SharedKernel.Results;

namespace UniBank.Core.Modules.AI.Application.Handlers;

public sealed record DisputeTriageResult(
    string Reference, string Classification, string Priority,
    string AssignedTeam, string Summary, double Confidence, string ExpectedResolution);

public sealed class TriageDisputeHandler
{
    private readonly UniBankDbContext _dbContext;
    private readonly OllamaClient _ollamaClient;
    private readonly ILogger<TriageDisputeHandler> _logger;

    private static readonly Dictionary<string, (string Priority, string Team, string Sla)> DisputeRouting = new()
    {
        ["unauthorized_transaction"] = ("high", "fraud", "24 hours"),
        ["card_fraud"] = ("high", "fraud", "24 hours"),
        ["duplicate_charge"] = ("medium", "operations", "3 business days"),
        ["wrong_amount"] = ("medium", "operations", "3 business days"),
        ["atm_failed_dispensing"] = ("medium", "operations", "2 business days"),
        ["service_not_received"] = ("low", "customer_service", "5 business days"),
    };

    public TriageDisputeHandler(
        UniBankDbContext dbContext, OllamaClient ollamaClient,
        ILogger<TriageDisputeHandler> logger)
    {
        _dbContext = dbContext;
        _ollamaClient = ollamaClient;
        _logger = logger;
    }

    public async Task<Result<DisputeTriageResult>> HandleAsync(
        TriageDisputeCommand command, CancellationToken cancellationToken = default)
    {
        var transaction = await _dbContext.Transactions
            .FirstOrDefaultAsync(t => t.Id == command.TransactionId, cancellationToken);

        var txnContext = transaction is not null
            ? $"Transaction: {transaction.Description}, Amount: {transaction.Amount:N2} {transaction.Currency}, " +
              $"Date: {transaction.CreatedAt:dd/MM/yyyy}, Status: {transaction.Status}"
            : "Transaction details not available";

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = await _ollamaClient.ChatAsync(
            "You are a transaction dispute classifier. Classify the dispute into exactly one type: " +
            "unauthorized_transaction, duplicate_charge, wrong_amount, service_not_received, " +
            "atm_failed_dispensing, card_fraud. " +
            "Return ONLY valid JSON: {\"type\": \"...\", \"summary\": \"1-2 sentence summary\", \"confidence\": 0.0-1.0}",
            $"Customer complaint: {command.Description}\n{txnContext}",
            temperature: 0.1,
            cancellationToken: cancellationToken);
        sw.Stop();

        var classification = "service_not_received";
        var summary = command.Description;
        var confidence = 0.5;

        try
        {
            var parsed = JsonDocument.Parse(result.Message.Content);
            classification = parsed.RootElement.GetProperty("type").GetString() ?? classification;
            summary = parsed.RootElement.GetProperty("summary").GetString() ?? summary;
            confidence = parsed.RootElement.GetProperty("confidence").GetDouble();
        }
        catch { /* use defaults */ }

        if (!DisputeRouting.TryGetValue(classification, out var routing))
            routing = ("low", "customer_service", "5 business days");

        // If confidence too low, route to manual triage
        if (confidence < 0.7)
            routing = ("medium", "operations", "3 business days");

        var reference = $"DSP-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString()[..5].ToUpper()}";

        var dispute = new TransactionDispute
        {
            AccountId = command.AccountId,
            TransactionId = command.TransactionId,
            UserDescription = command.Description,
            DisputeType = classification,
            Priority = routing.Priority,
            AiSummary = summary,
            AiRecommendedAction = $"Route to {routing.Team} team",
            ClassificationConfidence = confidence,
            Status = "open",
            AssignedTeam = routing.Team,
            Reference = reference,
            TenantId = command.TenantId,
        };

        _dbContext.TransactionDisputes.Add(dispute);

        _dbContext.AiInteractions.Add(new AiInteraction
        {
            AccountId = command.AccountId,
            InteractionType = "dispute_triage",
            RequestSummary = $"Dispute for txn {command.TransactionId}: {command.Description[..Math.Min(100, command.Description.Length)]}",
            ResponseSummary = $"Type: {classification}, Priority: {routing.Priority}, Team: {routing.Team}",
            ModelUsed = "qwen3-vl",
            InferenceTimeMs = (int)sw.ElapsedMilliseconds,
            Success = true,
            TenantId = command.TenantId,
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new DisputeTriageResult(
            reference, classification, routing.Priority,
            routing.Team, summary, confidence, routing.Sla));
    }
}
