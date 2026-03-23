using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UniBank.Core.Common.Persistence;
using UniBank.Core.Modules.AI.Application.Commands;
using UniBank.Core.Modules.AI.Domain.Entities;
using UniBank.Core.Modules.AI.Infrastructure.Services;
using UniBank.SharedKernel.Results;

namespace UniBank.Core.Modules.AI.Application.Handlers;

public sealed record LoanEligibilityResult(
    string Likelihood, string EstimatedRateMin, string EstimatedRateMax,
    string Assessment, string Disclaimer);

public sealed class CheckLoanEligibilityHandler
{
    private readonly UniBankDbContext _dbContext;
    private readonly OllamaClient _ollamaClient;
    private readonly ILogger<CheckLoanEligibilityHandler> _logger;

    public CheckLoanEligibilityHandler(
        UniBankDbContext dbContext, OllamaClient ollamaClient,
        ILogger<CheckLoanEligibilityHandler> logger)
    {
        _dbContext = dbContext;
        _ollamaClient = ollamaClient;
        _logger = logger;
    }

    public async Task<Result<LoanEligibilityResult>> HandleAsync(
        CheckLoanEligibilityCommand command, CancellationToken cancellationToken = default)
    {
        var account = await _dbContext.Accounts
            .FirstOrDefaultAsync(a => a.Id == command.AccountId && a.DeletedAt == null, cancellationToken);

        if (account is null)
            return Result.Failure<LoanEligibilityResult>(
                new Error("Account.NotFound", "Account not found."));

        var accountAgeDays = (DateTime.UtcNow - account.CreatedAt).TotalDays;
        var avgBalance30d = account.Balance; // simplified

        var existingLoans = await _dbContext.Loans
            .Where(l => l.AccountId == command.AccountId && l.DeletedAt == null
                        && (l.Status == "repaying" || l.Status == "disbursed"))
            .SumAsync(l => l.MonthlyPayment, cancellationToken);

        var context = $"Customer profile:\n" +
                      $"- Account age: {accountAgeDays:F0} days\n" +
                      $"- KYC level: {account.KycLevel}\n" +
                      $"- Current balance: {account.Balance:N2} {account.Currency}\n" +
                      $"- Existing monthly loan obligations: {existingLoans:N2} {account.Currency}\n" +
                      $"- Requested loan: {command.DesiredAmount:N2} {command.Currency} over {command.TenureMonths} months\n" +
                      $"- Purpose: {command.Purpose}\n\n" +
                      $"Bank lending criteria:\n" +
                      $"- Min account age: 30 days\n" +
                      $"- Min KYC level: 1\n" +
                      $"- Max debt-to-income ratio: 40%\n" +
                      $"- Rate tiers: 18% (excellent), 22% (good), 26% (fair), 30% (poor), 36% (high risk)\n";

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = await _ollamaClient.ChatAsync(
            "You are a loan eligibility advisor. Assess the customer's loan eligibility. " +
            "Return ONLY valid JSON: {\"likelihood\": \"high/medium/low\", \"rate_min\": \"0.XX\", " +
            "\"rate_max\": \"0.XX\", \"assessment\": \"2-3 sentence assessment\"}",
            context,
            temperature: 0.1,
            cancellationToken: cancellationToken);
        sw.Stop();

        string likelihood = "medium", rateMin = "0.22", rateMax = "0.30", assessment = result.Message.Content;

        try
        {
            var parsed = System.Text.Json.JsonDocument.Parse(result.Message.Content);
            likelihood = parsed.RootElement.GetProperty("likelihood").GetString() ?? "medium";
            rateMin = parsed.RootElement.GetProperty("rate_min").GetString() ?? "0.22";
            rateMax = parsed.RootElement.GetProperty("rate_max").GetString() ?? "0.30";
            assessment = parsed.RootElement.GetProperty("assessment").GetString() ?? assessment;
        }
        catch { /* use defaults */ }

        _dbContext.AiInteractions.Add(new AiInteraction
        {
            AccountId = command.AccountId,
            InteractionType = "loan_eligibility",
            RequestSummary = $"Loan eligibility: {command.DesiredAmount} {command.Currency} over {command.TenureMonths}mo",
            ResponseSummary = $"Likelihood: {likelihood}, Rate: {rateMin}-{rateMax}",
            ModelUsed = "qwen3-vl",
            InferenceTimeMs = (int)sw.ElapsedMilliseconds,
            Success = true,
            TenantId = command.TenantId,
        });
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new LoanEligibilityResult(
            likelihood, rateMin, rateMax, assessment,
            "This is an estimate only. Actual approval is subject to full assessment."));
    }
}
