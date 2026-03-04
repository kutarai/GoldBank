using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UniBank.Core.Common.Persistence;
using UniBank.Core.Modules.Accounts.Domain.Entities;
using UniBank.Core.Modules.BillPay.Application.Commands;
using UniBank.Core.Modules.BillPay.Domain.Entities;
using UniBank.Core.Modules.BillPay.Domain.Events;
using UniBank.SharedKernel.Messaging;
using UniBank.SharedKernel.Results;

namespace UniBank.Core.Modules.BillPay.Application.Handlers;

/// <summary>
/// Handles bill payment processing (STORY-038).
/// Validates account, PIN, provider, amount constraints, and balance before
/// debiting the account, creating a BillPayment record, and recording a Transaction.
/// For prepaid utilities (electricity, airtime), a token is generated.
/// </summary>
public sealed class PayBillHandler
{
    private const decimal FlatFee = 2.00m;
    private const decimal FeePercentage = 0.005m; // 0.5%

    private readonly UniBankDbContext _dbContext;
    private readonly IMessageBus _messageBus;
    private readonly ILogger<PayBillHandler> _logger;

    public PayBillHandler(
        UniBankDbContext dbContext,
        IMessageBus messageBus,
        ILogger<PayBillHandler> logger)
    {
        _dbContext = dbContext;
        _messageBus = messageBus;
        _logger = logger;
    }

    public async Task<Result<PayBillResult>> HandleAsync(
        PayBillCommand command, CancellationToken cancellationToken = default)
    {
        // Validate inputs
        if (string.IsNullOrWhiteSpace(command.BillingReference))
            return Result.Failure<PayBillResult>(
                new Error("BillPay.InvalidReference", "Billing reference is required."));

        if (command.Amount <= 0)
            return Result.Failure<PayBillResult>(
                new Error("BillPay.InvalidAmount", "Amount must be greater than zero."));

        if (string.IsNullOrWhiteSpace(command.Pin))
            return Result.Failure<PayBillResult>(
                new Error("BillPay.PinRequired", "PIN is required for bill payments."));

        // Find and verify account
        var account = await _dbContext.Accounts
            .FirstOrDefaultAsync(
                a => a.Id == command.AccountId && a.DeletedAt == null,
                cancellationToken);

        if (account is null)
            return Result.Failure<PayBillResult>(
                new Error("Account.NotFound", "Account not found."));

        if (account.Status != "active")
            return Result.Failure<PayBillResult>(
                new Error("Account.Inactive", "Account is not active."));

        // Verify PIN
        if (string.IsNullOrEmpty(account.PinHash))
            return Result.Failure<PayBillResult>(
                new Error("Account.NoPinSet", "Account does not have a PIN configured."));

        if (!BCrypt.Net.BCrypt.Verify(command.Pin, account.PinHash))
            return Result.Failure<PayBillResult>(
                new Error("Auth.InvalidPIN", "Invalid PIN."));

        // Find and verify provider
        var provider = await _dbContext.BillProviders
            .FirstOrDefaultAsync(
                p => p.Id == command.ProviderId && p.DeletedAt == null,
                cancellationToken);

        if (provider is null)
            return Result.Failure<PayBillResult>(
                new Error("BillPay.ProviderNotFound", "Bill provider not found."));

        if (provider.Status != "active")
            return Result.Failure<PayBillResult>(
                new Error("BillPay.ProviderInactive", "Bill provider is currently inactive."));

        // Validate amount within provider min/max range
        if (command.Amount < provider.MinAmount)
            return Result.Failure<PayBillResult>(
                new Error("BillPay.BelowMinimum",
                    $"Amount {command.Amount:F2} is below the minimum of {provider.MinAmount:F2} for {provider.Name}."));

        if (command.Amount > provider.MaxAmount)
            return Result.Failure<PayBillResult>(
                new Error("BillPay.AboveMaximum",
                    $"Amount {command.Amount:F2} exceeds the maximum of {provider.MaxAmount:F2} for {provider.Name}."));

        // Calculate fee: flat 2.00 or 0.5%, whichever is greater
        var percentageFee = Math.Round(command.Amount * FeePercentage, 2);
        var fee = Math.Max(FlatFee, percentageFee);
        var totalDebit = command.Amount + fee;

        // Check balance
        if (account.Balance < totalDebit)
            return Result.Failure<PayBillResult>(
                new Error("BillPay.InsufficientFunds",
                    $"Insufficient balance. Required: {totalDebit:F2}, Available: {account.Balance:F2}"));

        // Debit account
        account.Balance -= totalDebit;
        account.AvailableBalance -= totalDebit;
        account.UpdatedAt = DateTime.UtcNow;

        // Generate reference
        var reference = $"BP-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";

        // Generate token for prepaid utilities (electricity, airtime)
        string? token = null;
        if (provider.Category is "electricity" or "airtime")
        {
            token = GeneratePrepaidToken();
        }

        var completedAt = DateTime.UtcNow;

        // Create BillPayment record
        var billPayment = new BillPayment
        {
            AccountId = command.AccountId,
            ProviderId = command.ProviderId,
            BillingReference = command.BillingReference,
            Amount = command.Amount,
            Fee = fee,
            Currency = command.Currency,
            Status = "completed",
            Reference = reference,
            Token = token,
            CompletedAt = completedAt,
            TenantId = command.TenantId
        };

        _dbContext.BillPayments.Add(billPayment);

        // Create Transaction record
        var transaction = new Transaction
        {
            AccountId = command.AccountId,
            Type = "bill_payment",
            Amount = command.Amount,
            Fee = fee,
            Status = "completed",
            Reference = reference,
            Description = $"Bill payment to {provider.Name} - {command.BillingReference}",
            CounterpartyName = provider.Name,
            BalanceAfter = account.Balance,
            Currency = command.Currency,
            TenantId = command.TenantId,
            CompletedAt = completedAt
        };

        _dbContext.Transactions.Add(transaction);

        // Update LastPaidAt on any matching saved biller
        var savedBiller = await _dbContext.SavedBillers
            .FirstOrDefaultAsync(
                b => b.AccountId == command.AccountId
                    && b.ProviderId == command.ProviderId
                    && b.BillingReference == command.BillingReference
                    && b.DeletedAt == null,
                cancellationToken);

        if (savedBiller is not null)
        {
            savedBiller.LastPaidAt = completedAt;
            savedBiller.UpdatedAt = completedAt;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Publish domain event
        var domainEvent = new BillPaymentCompleted(
            PaymentId: billPayment.Id,
            AccountId: command.AccountId,
            ProviderId: command.ProviderId,
            ProviderName: provider.Name,
            Amount: command.Amount,
            Fee: fee,
            Currency: command.Currency,
            Reference: reference,
            Token: token,
            BillingReference: command.BillingReference)
        {
            TenantId = command.TenantId
        };

        await _messageBus.PublishAsync(domainEvent, cancellationToken);

        _logger.LogInformation(
            "Bill payment completed: ref {Reference}, provider {Provider}, amount {Amount} {Currency}, account {AccountId}",
            reference, provider.Name, command.Amount, command.Currency, command.AccountId);

        return Result.Success(new PayBillResult(
            TransactionId: transaction.Id.ToString(),
            Reference: reference,
            Token: token,
            Amount: command.Amount,
            Fee: fee,
            NewBalance: account.Balance,
            Currency: command.Currency,
            CompletedAt: completedAt));
    }

    /// <summary>
    /// Generates a simulated 20-digit numeric prepaid token for electricity/airtime.
    /// In production, this would be obtained from the provider's API.
    /// </summary>
    private static string GeneratePrepaidToken()
    {
        var random = Random.Shared;
        var chars = new char[20];
        for (var i = 0; i < 20; i++)
        {
            chars[i] = (char)('0' + random.Next(0, 10));
        }
        return new string(chars);
    }
}
