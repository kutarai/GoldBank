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
/// Handles NFC contactless payment processing at a POS terminal (STORY-023).
/// Verifies account status, checks balance, determines if high-value PIN is required,
/// debits payer, credits merchant, creates transaction records, and publishes events.
/// </summary>
public sealed class NfcPaymentHandler
{
    private const decimal NfcFeePercentage = 0.005m; // 0.5% fee for NFC payments
    private readonly UniBankDbContext _dbContext;
    private readonly TransactionAuthorizationService _authService;
    private readonly IMessageBus _messageBus;
    private readonly ILogger<NfcPaymentHandler> _logger;

    public NfcPaymentHandler(
        UniBankDbContext dbContext,
        TransactionAuthorizationService authService,
        IMessageBus messageBus,
        ILogger<NfcPaymentHandler> logger)
    {
        _dbContext = dbContext;
        _authService = authService;
        _messageBus = messageBus;
        _logger = logger;
    }

    public async Task<Result<PaymentResult>> HandleAsync(
        Commands.NfcPaymentCommand command, CancellationToken cancellationToken = default)
    {
        if (command.Amount <= 0)
            return Result.Failure<PaymentResult>(
                new Error("Payment.InvalidAmount", "Payment amount must be greater than zero."));

        // Verify payer account exists and is active
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

        // Look up merchant by MerchantCode
        var merchant = await _dbContext.Merchants
            .FirstOrDefaultAsync(
                m => m.MerchantCode == command.MerchantId && m.Status == "active",
                cancellationToken);

        if (merchant is null)
            return Result.Failure<PaymentResult>(
                new Error("Merchant.NotFound", "Merchant not found or not active."));

        // Get merchant's owner account for crediting
        var merchantAccount = await _dbContext.Accounts
            .FirstOrDefaultAsync(
                a => a.Id == merchant.OwnerAccountId && a.DeletedAt == null,
                cancellationToken);

        if (merchantAccount is null)
            return Result.Failure<PaymentResult>(
                new Error("Merchant.AccountNotFound", "Merchant account not found."));

        // Calculate fee
        var fee = Math.Round(command.Amount * NfcFeePercentage, 2);
        var totalDebit = command.Amount + fee;

        // Check balance
        if (payerAccount.AvailableBalance < totalDebit)
            return Result.Failure<PaymentResult>(
                new Error("Payment.InsufficientFunds",
                    $"Insufficient balance. Required: {totalDebit:F2}, Available: {payerAccount.AvailableBalance:F2}"));

        // Check if high-value transaction requires PIN authorization
        var requiresAuth = await _authService.RequiresAuthorizationAsync(
            command.TenantId, "nfc_payment", command.Amount);

        if (requiresAuth && string.IsNullOrEmpty(command.Pin))
        {
            // Create pending payment record awaiting PIN confirmation
            var reference = GenerateReference();
            var pendingPayment = new Payment
            {
                PayerAccountId = command.AccountId,
                MerchantAccountId = merchant.OwnerAccountId,
                Amount = command.Amount,
                Fee = fee,
                Currency = command.Currency,
                Type = "nfc",
                Status = "pending_pin",
                Reference = reference,
                NfcData = command.NfcData,
                TerminalId = command.TerminalId,
                TenantId = command.TenantId
            };

            _dbContext.Set<Payment>().Add(pendingPayment);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "NFC payment {Reference} requires PIN authorization, amount: {Amount}, account: {AccountId}",
                reference, command.Amount, command.AccountId);

            return Result.Success(new PaymentResult(
                TransactionId: pendingPayment.Id.ToString(),
                Reference: reference,
                Amount: command.Amount,
                Fee: fee,
                NewBalance: payerAccount.AvailableBalance,
                Currency: command.Currency,
                Status: "pending_pin",
                CompletedAt: null,
                RequiresPin: true));
        }

        // If PIN was provided for high-value, verify it
        if (requiresAuth && !string.IsNullOrEmpty(command.Pin))
        {
            if (string.IsNullOrEmpty(payerAccount.PinHash))
                return Result.Failure<PaymentResult>(
                    new Error("Account.NoPinSet", "Account does not have a PIN configured."));

            var authResult = await _authService.AuthorizeAsync(
                command.AccountId, command.Pin, payerAccount.PinHash, "nfc_payment", command.Amount);

            if (authResult.IsFailure)
                return Result.Failure<PaymentResult>(authResult.Error);
        }

        // Process payment: debit payer, credit merchant
        return await ExecutePaymentAsync(
            payerAccount, merchantAccount, command.Amount, fee,
            command.Currency, "nfc", command.NfcData, null,
            command.TerminalId, command.TenantId, merchant.BusinessName,
            cancellationToken);
    }

    internal async Task<Result<PaymentResult>> ExecutePaymentAsync(
        Account payerAccount,
        Account merchantAccount,
        decimal amount,
        decimal fee,
        string currency,
        string type,
        string? nfcData,
        string? qrCodeData,
        string? terminalId,
        string tenantId,
        string merchantName,
        CancellationToken cancellationToken)
    {
        var reference = GenerateReference();
        var now = DateTime.UtcNow;
        var totalDebit = amount + fee;

        // Debit payer
        payerAccount.Balance -= totalDebit;
        payerAccount.AvailableBalance -= totalDebit;
        payerAccount.UpdatedAt = now;

        // Credit merchant (merchant receives amount minus fee)
        merchantAccount.Balance += amount;
        merchantAccount.AvailableBalance += amount;
        merchantAccount.UpdatedAt = now;

        // Create payment record
        var payment = new Payment
        {
            PayerAccountId = payerAccount.Id,
            MerchantAccountId = merchantAccount.Id,
            Amount = amount,
            Fee = fee,
            Currency = currency,
            Type = type,
            Status = "completed",
            Reference = reference,
            NfcData = nfcData,
            QrCodeData = qrCodeData,
            TerminalId = terminalId,
            CompletedAt = now,
            TenantId = tenantId
        };

        _dbContext.Set<Payment>().Add(payment);

        // Create transaction records for both parties
        var payerTransaction = new Transaction
        {
            AccountId = payerAccount.Id,
            Type = $"{type}_payment",
            Amount = -totalDebit,
            Fee = fee,
            Status = "completed",
            Reference = reference,
            Description = $"{type.ToUpperInvariant()} payment to {merchantName}",
            CounterpartyName = merchantName,
            BalanceAfter = payerAccount.Balance,
            Currency = currency,
            TenantId = tenantId,
            CompletedAt = now
        };

        var merchantTransaction = new Transaction
        {
            AccountId = merchantAccount.Id,
            Type = $"{type}_receipt",
            Amount = amount,
            Fee = 0,
            Status = "completed",
            Reference = reference,
            Description = $"{type.ToUpperInvariant()} payment received",
            CounterpartyName = payerAccount.FirstName is not null
                ? $"{payerAccount.FirstName} {payerAccount.LastName}".Trim()
                : payerAccount.PhoneNumber,
            BalanceAfter = merchantAccount.Balance,
            Currency = currency,
            TenantId = tenantId,
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
            Amount: amount,
            Currency: currency,
            TransactionType: $"{type}_payment",
            ReferenceNumber: reference), cancellationToken);

        _logger.LogInformation(
            "Payment {Reference} completed: {Type}, amount {Amount} {Currency}, payer {Payer}, merchant {Merchant}",
            reference, type, amount, currency, payerAccount.Id, merchantAccount.Id);

        return Result.Success(new PaymentResult(
            TransactionId: payment.Id.ToString(),
            Reference: reference,
            Amount: amount,
            Fee: fee,
            NewBalance: payerAccount.AvailableBalance,
            Currency: currency,
            Status: "completed",
            CompletedAt: now,
            RequiresPin: false));
    }

    private static string GenerateReference()
    {
        return $"PAY-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..28].ToUpperInvariant();
    }
}
