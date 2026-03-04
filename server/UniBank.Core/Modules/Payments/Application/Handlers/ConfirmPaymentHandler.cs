using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UniBank.Core.Common.Persistence;
using UniBank.Core.Modules.Accounts.Domain.Entities;
using UniBank.Core.Modules.Accounts.Infrastructure.Services;
using UniBank.Core.Modules.Payments.Domain.Entities;
using UniBank.SharedKernel.Events;
using UniBank.SharedKernel.Messaging;
using UniBank.SharedKernel.Results;

namespace UniBank.Core.Modules.Payments.Application.Handlers;

/// <summary>
/// Handles PIN confirmation for high-value NFC payments (STORY-024).
/// Loads the pending payment, verifies the PIN against the payer's account,
/// and completes the payment processing if valid.
/// </summary>
public sealed class ConfirmPaymentHandler
{
    private readonly UniBankDbContext _dbContext;
    private readonly TransactionAuthorizationService _authService;
    private readonly IMessageBus _messageBus;
    private readonly ILogger<ConfirmPaymentHandler> _logger;

    public ConfirmPaymentHandler(
        UniBankDbContext dbContext,
        TransactionAuthorizationService authService,
        IMessageBus messageBus,
        ILogger<ConfirmPaymentHandler> logger)
    {
        _dbContext = dbContext;
        _authService = authService;
        _messageBus = messageBus;
        _logger = logger;
    }

    public async Task<Result<PaymentResult>> HandleAsync(
        Commands.ConfirmPaymentCommand command, CancellationToken cancellationToken = default)
    {
        // Load pending payment
        var payment = await _dbContext.Set<Payment>()
            .FirstOrDefaultAsync(
                p => p.Id == command.TransactionId && p.Status == "pending_pin",
                cancellationToken);

        if (payment is null)
            return Result.Failure<PaymentResult>(
                new Error("Payment.NotFound", "Pending payment not found or already processed."));

        // Verify the caller owns this payment
        if (payment.PayerAccountId != command.AccountId)
            return Result.Failure<PaymentResult>(
                new Error("Payment.Unauthorized", "You are not authorized to confirm this payment."));

        // Check payment has not expired (5 minute window for PIN entry)
        if (payment.CreatedAt.AddMinutes(5) < DateTime.UtcNow)
        {
            payment.Status = "expired";
            payment.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);

            return Result.Failure<PaymentResult>(
                new Error("Payment.Expired", "Payment confirmation window has expired. Please initiate a new payment."));
        }

        // Load payer account
        var payerAccount = await _dbContext.Accounts
            .FirstOrDefaultAsync(
                a => a.Id == command.AccountId && a.DeletedAt == null,
                cancellationToken);

        if (payerAccount is null)
            return Result.Failure<PaymentResult>(
                new Error("Account.NotFound", "Payer account not found."));

        if (payerAccount.Status != "active")
            return Result.Failure<PaymentResult>(
                new Error("Account.Inactive", "Payer account is not active."));

        if (string.IsNullOrEmpty(payerAccount.PinHash))
            return Result.Failure<PaymentResult>(
                new Error("Account.NoPinSet", "Account does not have a PIN configured."));

        // Verify PIN via TransactionAuthorizationService
        var authResult = await _authService.AuthorizeAsync(
            command.AccountId, command.Pin, payerAccount.PinHash,
            "nfc_payment", payment.Amount);

        if (authResult.IsFailure)
            return Result.Failure<PaymentResult>(authResult.Error);

        // Re-check balance (may have changed since initial request)
        var totalDebit = payment.Amount + payment.Fee;
        if (payerAccount.AvailableBalance < totalDebit)
        {
            payment.Status = "failed";
            payment.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);

            return Result.Failure<PaymentResult>(
                new Error("Payment.InsufficientFunds",
                    $"Insufficient balance. Required: {totalDebit:F2}, Available: {payerAccount.AvailableBalance:F2}"));
        }

        // Load merchant account
        var merchantAccount = await _dbContext.Accounts
            .FirstOrDefaultAsync(
                a => a.Id == payment.MerchantAccountId && a.DeletedAt == null,
                cancellationToken);

        if (merchantAccount is null)
        {
            payment.Status = "failed";
            payment.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);

            return Result.Failure<PaymentResult>(
                new Error("Merchant.AccountNotFound", "Merchant account not found."));
        }

        // Look up merchant name for transaction description
        var merchant = await _dbContext.Merchants
            .FirstOrDefaultAsync(
                m => m.OwnerAccountId == payment.MerchantAccountId,
                cancellationToken);

        var merchantName = merchant?.BusinessName ?? "Merchant";

        var now = DateTime.UtcNow;

        // Debit payer
        payerAccount.Balance -= totalDebit;
        payerAccount.AvailableBalance -= totalDebit;
        payerAccount.UpdatedAt = now;

        // Credit merchant
        merchantAccount.Balance += payment.Amount;
        merchantAccount.AvailableBalance += payment.Amount;
        merchantAccount.UpdatedAt = now;

        // Update payment status
        payment.Status = "completed";
        payment.CompletedAt = now;
        payment.UpdatedAt = now;

        // Create transaction records
        var payerTransaction = new Transaction
        {
            AccountId = payerAccount.Id,
            Type = "nfc_payment",
            Amount = -totalDebit,
            Fee = payment.Fee,
            Status = "completed",
            Reference = payment.Reference,
            Description = $"NFC payment to {merchantName}",
            CounterpartyName = merchantName,
            BalanceAfter = payerAccount.Balance,
            Currency = payment.Currency,
            TenantId = payment.TenantId,
            CompletedAt = now
        };

        var merchantTransaction = new Transaction
        {
            AccountId = merchantAccount.Id,
            Type = "nfc_receipt",
            Amount = payment.Amount,
            Fee = 0,
            Status = "completed",
            Reference = payment.Reference,
            Description = "NFC payment received",
            CounterpartyName = payerAccount.FirstName is not null
                ? $"{payerAccount.FirstName} {payerAccount.LastName}".Trim()
                : payerAccount.PhoneNumber,
            BalanceAfter = merchantAccount.Balance,
            Currency = payment.Currency,
            TenantId = payment.TenantId,
            CompletedAt = now
        };

        _dbContext.Transactions.Add(payerTransaction);
        _dbContext.Transactions.Add(merchantTransaction);

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Publish payment completed event
        await _messageBus.PublishAsync(new TransactionCompleted(
            TransactionId: payment.Id,
            SourceAccountId: payerAccount.Id,
            DestinationAccountId: merchantAccount.Id,
            Amount: payment.Amount,
            Currency: payment.Currency,
            TransactionType: "nfc_payment",
            ReferenceNumber: payment.Reference), cancellationToken);

        _logger.LogInformation(
            "NFC payment {Reference} confirmed with PIN, amount: {Amount}, account: {AccountId}",
            payment.Reference, payment.Amount, command.AccountId);

        return Result.Success(new PaymentResult(
            TransactionId: payment.Id.ToString(),
            Reference: payment.Reference,
            Amount: payment.Amount,
            Fee: payment.Fee,
            NewBalance: payerAccount.AvailableBalance,
            Currency: payment.Currency,
            Status: "completed",
            CompletedAt: now,
            RequiresPin: false));
    }
}
