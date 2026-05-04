using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using GoldBank.Core.Common.Persistence;
using GoldBank.Core.Modules.AI.Application.Commands;
using GoldBank.Core.Modules.AI.Domain.Entities;
using GoldBank.Core.Modules.AI.Infrastructure.Services;
using GoldBank.SharedKernel.Results;

namespace GoldBank.Core.Modules.AI.Application.Handlers;

public sealed record LoanDocVerificationResult(
    PayslipFields? ExtractedFields, string? ExtractedIncome,
    decimal DeclaredIncome, double VariancePercentage,
    bool NameMatch, bool IncomeMatch, bool FlaggedForReview);

public sealed class VerifyLoanDocumentsHandler
{
    private readonly GoldBankDbContext _dbContext;
    private readonly DocumentOcrService _ocrService;
    private readonly ILogger<VerifyLoanDocumentsHandler> _logger;

    public VerifyLoanDocumentsHandler(
        GoldBankDbContext dbContext, DocumentOcrService ocrService,
        ILogger<VerifyLoanDocumentsHandler> logger)
    {
        _dbContext = dbContext;
        _ocrService = ocrService;
        _logger = logger;
    }

    public async Task<Result<LoanDocVerificationResult>> HandleAsync(
        VerifyLoanDocumentsCommand command, CancellationToken cancellationToken = default)
    {
        var account = await _dbContext.Accounts
            .FirstOrDefaultAsync(a => a.Id == command.AccountId && a.DeletedAt == null, cancellationToken);

        if (account is null)
            return Result.Failure<LoanDocVerificationResult>(
                new Error("Account.NotFound", "Account not found."));

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var fields = await _ocrService.ExtractPayslipFieldsAsync(command.DocumentImage, cancellationToken);
        sw.Stop();

        // Cross-validate
        var accountName = $"{account.FirstName} {account.LastName}".ToLowerInvariant();
        var extractedName = fields.EmployeeName?.ToLowerInvariant() ?? "";
        var nameMatch = accountName.Split(' ').Count(p =>
            extractedName.Contains(p)) >= 2;

        var extractedIncome = fields.NetSalary ?? fields.GrossSalary;
        decimal.TryParse(extractedIncome, out var extractedAmount);
        var variance = command.DeclaredIncome > 0
            ? Math.Abs((double)(extractedAmount - command.DeclaredIncome) / (double)command.DeclaredIncome * 100)
            : 100;

        var incomeMatch = variance <= 10;
        var flagged = !nameMatch || !incomeMatch;

        _dbContext.AiInteractions.Add(new AiInteraction
        {
            AccountId = command.AccountId,
            InteractionType = "loan_doc_verify",
            RequestSummary = $"Loan doc verification: declared income {command.DeclaredIncome}",
            ResponseSummary = $"Extracted: {extractedIncome}, Variance: {variance:F1}%, Name: {(nameMatch ? "match" : "mismatch")}",
            ModelUsed = "qwen3-vl",
            InferenceTimeMs = (int)sw.ElapsedMilliseconds,
            Success = true,
            TenantId = command.TenantId,
        });
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success(new LoanDocVerificationResult(
            fields, extractedIncome, command.DeclaredIncome,
            variance, nameMatch, incomeMatch, flagged));
    }
}
