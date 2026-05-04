using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using GoldBank.Core.Common.Persistence;
using GoldBank.Core.Modules.Accounts.Domain.Entities;
using GoldBank.Core.Modules.Loans.Application.Commands;
using GoldBank.Core.Modules.Loans.Domain.Entities;
using GoldBank.Core.Modules.Loans.Infrastructure.Services;
using GoldBank.SharedKernel.Events;
using GoldBank.SharedKernel.Messaging;
using GoldBank.SharedKernel.Results;

namespace GoldBank.Core.Modules.Loans.Application.Handlers;

/// <summary>
/// Handles loan applications. Validates account, verifies PIN, runs credit scoring,
/// auto-approves if score >= 500, calculates terms, generates amortization schedule,
/// and disburses funds if approved.
/// </summary>
public sealed class ApplyForLoanHandler
{
    private const int MinCreditScore = 500;
    private static readonly int[] AllowedTenures = [3, 6, 12, 18, 24];

    private readonly GoldBankDbContext _dbContext;
    private readonly CreditScoringEngine _creditScoring;
    private readonly IMessageBus _messageBus;
    private readonly ILogger<ApplyForLoanHandler> _logger;

    public ApplyForLoanHandler(
        GoldBankDbContext dbContext,
        CreditScoringEngine creditScoring,
        IMessageBus messageBus,
        ILogger<ApplyForLoanHandler> logger)
    {
        _dbContext = dbContext;
        _creditScoring = creditScoring;
        _messageBus = messageBus;
        _logger = logger;
    }

    public async Task<Result<LoanApplicationResult>> HandleAsync(
        ApplyForLoanCommand command, CancellationToken cancellationToken = default)
    {
        if (command.Amount <= 0)
            return Result.Failure<LoanApplicationResult>(
                new Error("Loan.InvalidAmount", "Loan amount must be greater than zero."));

        if (!AllowedTenures.Contains(command.TenureMonths))
            return Result.Failure<LoanApplicationResult>(
                new Error("Loan.InvalidTenure", "Tenure must be 3, 6, 12, 18, or 24 months."));

        // Verify account
        var account = await _dbContext.Accounts
            .FirstOrDefaultAsync(
                a => a.Id == command.AccountId && a.DeletedAt == null,
                cancellationToken);

        if (account is null)
            return Result.Failure<LoanApplicationResult>(
                new Error("Account.NotFound", "Account not found."));

        if (account.Status != "active")
            return Result.Failure<LoanApplicationResult>(
                new Error("Account.Inactive", "Account is not active."));

        // Verify PIN
        if (string.IsNullOrEmpty(account.PinHash))
            return Result.Failure<LoanApplicationResult>(
                new Error("Account.NoPinSet", "Account does not have a PIN configured."));

        if (!BCrypt.Net.BCrypt.Verify(command.Pin, account.PinHash))
            return Result.Failure<LoanApplicationResult>(
                new Error("Auth.InvalidPIN", "Invalid PIN provided."));

        // Check KYC level
        if (account.KycLevel < 1)
            return Result.Failure<LoanApplicationResult>(
                new Error("Loan.InsufficientKyc", "KYC level 1 or higher is required to apply for a loan."));

        // Check no existing active loans that are defaulted
        var hasDefaultedLoan = await _dbContext.Loans
            .AnyAsync(l => l.AccountId == command.AccountId && l.Status == "defaulted" && l.DeletedAt == null,
                cancellationToken);

        if (hasDefaultedLoan)
            return Result.Failure<LoanApplicationResult>(
                new Error("Loan.DefaultedLoan", "Cannot apply for a new loan while you have a defaulted loan."));

        // Validate collateral assets — every ID must exist and belong to this customer
        if (command.CollateralAssetIds.Count > 0)
        {
            var idsToCheck = command.CollateralAssetIds.Distinct().ToList();
            var ownedCount = await _dbContext.Assets
                .CountAsync(
                    a => idsToCheck.Contains(a.Id) && a.CustomerId == account.CustomerId,
                    cancellationToken);

            if (ownedCount != idsToCheck.Count)
                return Result.Failure<LoanApplicationResult>(
                    new Error(
                        "Loan.InvalidCollateral",
                        "One or more collateral assets do not exist or do not belong to this customer."));
        }

        // Run credit scoring
        var creditScore = await _creditScoring.CalculateScoreAsync(account, cancellationToken);
        var isApproved = creditScore >= MinCreditScore;

        var interestRate = await _creditScoring.GetInterestRateAsync(creditScore, command.TenureMonths, account.TenantId?.ToString(), cancellationToken);
        var monthlyPayment = CreditScoringEngine.CalculateMonthlyPayment(
            command.Amount, interestRate, command.TenureMonths);

        var reference = GenerateReference();
        var now = DateTime.UtcNow;
        var status = isApproved ? "approved" : "rejected";

        // Create loan record
        var loan = new Loan
        {
            AccountId = command.AccountId,
            Principal = command.Amount,
            OutstandingBalance = isApproved ? command.Amount : 0,
            InterestRate = interestRate,
            TenureMonths = command.TenureMonths,
            MonthlyPayment = monthlyPayment,
            Purpose = command.Purpose,
            Status = status,
            CreditScore = creditScore,
            PaymentsMade = 0,
            Reference = reference,
            Currency = command.Currency,
            TenantId = command.TenantId,
            CollateralAssetIds = command.CollateralAssetIds.Distinct().ToList(),
        };

        _dbContext.Loans.Add(loan);

        if (isApproved)
        {
            // Generate amortization schedule
            var remainingBalance = command.Amount;
            var monthlyRate = interestRate / 12m;

            for (var i = 1; i <= command.TenureMonths; i++)
            {
                var interestAmount = Math.Round(remainingBalance * monthlyRate, 2);
                var principalAmount = Math.Round(monthlyPayment - interestAmount, 2);

                // Adjust last payment for rounding
                if (i == command.TenureMonths)
                {
                    principalAmount = remainingBalance;
                    interestAmount = Math.Round(remainingBalance * monthlyRate, 2);
                }

                remainingBalance -= principalAmount;
                if (remainingBalance < 0) remainingBalance = 0;

                var payment = new LoanPayment
                {
                    LoanId = loan.Id,
                    PaymentNumber = i,
                    PrincipalAmount = principalAmount,
                    InterestAmount = interestAmount,
                    TotalPayment = principalAmount + interestAmount,
                    RemainingBalance = remainingBalance,
                    DueDate = now.AddMonths(i),
                    IsPaid = false,
                };

                _dbContext.LoanPayments.Add(payment);
            }

            // Disburse: credit account balance
            loan.Status = "disbursed";
            loan.DisbursedAt = now;

            account.Balance += command.Amount;
            account.AvailableBalance += command.Amount;
            account.UpdatedAt = now;

            // Create transaction record
            var transaction = new Transaction
            {
                AccountId = account.Id,
                Type = "loan_disbursement",
                Amount = command.Amount,
                Fee = 0,
                Status = "completed",
                Reference = reference,
                Description = $"Loan disbursement - {command.Purpose}",
                BalanceAfter = account.Balance,
                Currency = command.Currency,
                TenantId = command.TenantId,
                CompletedAt = now,
            };

            _dbContext.Transactions.Add(transaction);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        if (isApproved)
        {
            await _messageBus.PublishAsync(new TransactionCompleted(
                TransactionId: loan.Id,
                SourceAccountId: Guid.Empty,
                DestinationAccountId: account.Id,
                Amount: command.Amount,
                Currency: command.Currency,
                TransactionType: "loan_disbursement",
                ReferenceNumber: reference), cancellationToken);
        }

        _logger.LogInformation(
            "Loan application {Reference}: {Status}, score {Score}, amount {Amount} {Currency}, tenure {Tenure}mo",
            reference, status, creditScore, command.Amount, command.Currency, command.TenureMonths);

        return Result.Success(new LoanApplicationResult(
            LoanId: loan.Id.ToString(),
            Reference: reference,
            Status: loan.Status,
            Principal: command.Amount,
            Currency: command.Currency,
            InterestRate: interestRate,
            MonthlyPayment: monthlyPayment,
            TenureMonths: command.TenureMonths,
            CreditScore: creditScore,
            NewBalance: account.Balance));
    }

    private static string GenerateReference()
    {
        return $"LN-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..26].ToUpperInvariant();
    }
}
