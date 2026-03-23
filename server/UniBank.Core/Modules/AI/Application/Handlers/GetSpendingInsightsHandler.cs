using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UniBank.Core.Common.Persistence;
using UniBank.Core.Modules.AI.Application.Commands;
using UniBank.Core.Modules.AI.Domain.Entities;
using UniBank.Core.Modules.AI.Infrastructure.Services;
using UniBank.SharedKernel.Results;

namespace UniBank.Core.Modules.AI.Application.Handlers;

public sealed class GetSpendingInsightsHandler
{
    private readonly UniBankDbContext _dbContext;
    private readonly OllamaClient _ollamaClient;
    private readonly ILogger<GetSpendingInsightsHandler> _logger;

    public GetSpendingInsightsHandler(
        UniBankDbContext dbContext, OllamaClient ollamaClient,
        ILogger<GetSpendingInsightsHandler> logger)
    {
        _dbContext = dbContext;
        _ollamaClient = ollamaClient;
        _logger = logger;
    }

    public async Task<Result<List<string>>> HandleAsync(
        GetSpendingInsightsCommand command, CancellationToken cancellationToken = default)
    {
        var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);

        var transactions = await _dbContext.Transactions
            .Where(t => t.AccountId == command.AccountId
                        && t.CreatedAt >= thirtyDaysAgo
                        && t.Status == "completed")
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new { t.Description, t.Amount, t.Currency, t.CreatedAt })
            .ToListAsync(cancellationToken);

        if (transactions.Count == 0)
            return Result.Success(new List<string> { "No transactions in the last 30 days to analyze." });

        var summary = string.Join("\n", transactions.Select(t =>
            $"{t.CreatedAt:dd/MM}: {t.Description} {t.Amount:N2} {t.Currency}"));

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = await _ollamaClient.ChatAsync(
            "Analyze this banking customer's spending for the last 30 days. " +
            "Generate exactly 3 concise, actionable insights. Each insight should be one sentence. " +
            "Focus on spending patterns, unusual changes, or savings opportunities. " +
            "Return ONLY a JSON array of strings, e.g. [\"insight1\", \"insight2\", \"insight3\"]",
            summary,
            temperature: 0.3,
            cancellationToken: cancellationToken);
        sw.Stop();

        var responseText = result.Message.Content.Trim();
        List<string> insights;
        try
        {
            insights = System.Text.Json.JsonSerializer.Deserialize<List<string>>(responseText) ?? [];
        }
        catch
        {
            insights = [responseText];
        }

        _dbContext.AiInteractions.Add(new AiInteraction
        {
            AccountId = command.AccountId,
            InteractionType = "spending_insights",
            RequestSummary = $"Spending insights for {transactions.Count} transactions",
            ResponseSummary = string.Join("; ", insights.Take(3)),
            ModelUsed = "qwen3-vl",
            InferenceTimeMs = (int)sw.ElapsedMilliseconds,
            Success = true,
            TenantId = command.TenantId,
        });
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(insights);
    }
}
