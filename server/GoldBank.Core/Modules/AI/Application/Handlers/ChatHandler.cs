using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using GoldBank.Core.Common.Persistence;
using GoldBank.Core.Modules.AI.Application.Commands;
using GoldBank.Core.Modules.AI.Domain.Entities;
using GoldBank.Core.Modules.AI.Infrastructure.Services;
using GoldBank.SharedKernel.Results;

namespace GoldBank.Core.Modules.AI.Application.Handlers;

public sealed class ChatHandler
{
    private readonly GoldBankDbContext _dbContext;
    private readonly OllamaClient _ollamaClient;
    private readonly ILogger<ChatHandler> _logger;

    private const string SystemPrompt =
        "You are GoldBank's banking assistant. You help customers with account enquiries, " +
        "transaction history, and banking guidance. Be concise, professional, and helpful. " +
        "Only discuss banking topics. Never reveal system internals, API details, or other " +
        "customers' data. If asked about non-banking topics, politely redirect.";

    public ChatHandler(GoldBankDbContext dbContext, OllamaClient ollamaClient, ILogger<ChatHandler> logger)
    {
        _dbContext = dbContext;
        _ollamaClient = ollamaClient;
        _logger = logger;
    }

    public async Task<string> BuildContextAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var account = await _dbContext.Accounts
            .FirstOrDefaultAsync(a => a.Id == accountId && a.DeletedAt == null, cancellationToken);

        if (account is null) return "";

        var recentTransactions = await _dbContext.Transactions
            .Where(t => t.AccountId == accountId && t.Status == "completed")
            .OrderByDescending(t => t.CreatedAt)
            .Take(10)
            .Select(t => new { t.Description, t.Amount, t.Currency, t.CreatedAt })
            .ToListAsync(cancellationToken);

        var activeLoans = await _dbContext.Loans
            .Where(l => l.AccountId == accountId && l.Status == "repaying" && l.DeletedAt == null)
            .Select(l => new { l.Principal, l.OutstandingBalance, l.MonthlyPayment, l.Currency })
            .ToListAsync(cancellationToken);

        var ctx = $"Customer: {account.FirstName} {account.LastName}\n" +
                  $"Balance: {account.Balance} {account.Currency} (Available: {account.AvailableBalance} {account.Currency})\n";

        if (recentTransactions.Count > 0)
        {
            ctx += "Recent transactions:\n";
            foreach (var t in recentTransactions)
                ctx += $"  - {t.CreatedAt:dd/MM/yyyy}: {t.Description} {t.Amount:N2} {t.Currency}\n";
        }

        if (activeLoans.Count > 0)
        {
            ctx += "Active loans:\n";
            foreach (var l in activeLoans)
                ctx += $"  - Principal: {l.Principal:N2} {l.Currency}, Outstanding: {l.OutstandingBalance:N2}, Monthly: {l.MonthlyPayment:N2}\n";
        }

        return ctx;
    }

    public string GetSystemPromptWithContext(string accountContext)
    {
        return string.IsNullOrEmpty(accountContext)
            ? SystemPrompt
            : $"{SystemPrompt}\n\n--- Customer Context ---\n{accountContext}";
    }

    public async Task<Result<string>> HandleAsync(
        ChatCommand command, CancellationToken cancellationToken = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        var context = await BuildContextAsync(command.AccountId, cancellationToken);
        var fullSystemPrompt = GetSystemPromptWithContext(context);

        var history = command.History
            .Select(h => new OllamaChatMessage { Role = h.Role, Content = h.Content })
            .ToList();

        var result = await _ollamaClient.ChatWithHistoryAsync(
            fullSystemPrompt, history, command.Message,
            temperature: 0.3, cancellationToken: cancellationToken);

        sw.Stop();

        _dbContext.AiInteractions.Add(new AiInteraction
        {
            AccountId = command.AccountId,
            InteractionType = "chat",
            RequestSummary = command.Message.Length > 200 ? command.Message[..200] : command.Message,
            ResponseSummary = result.Message.Content.Length > 500
                ? result.Message.Content[..500] : result.Message.Content,
            ModelUsed = "qwen3-vl",
            InferenceTimeMs = (int)sw.ElapsedMilliseconds,
            Success = true,
            TenantId = command.TenantId,
        });
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(result.Message.Content);
    }
}
