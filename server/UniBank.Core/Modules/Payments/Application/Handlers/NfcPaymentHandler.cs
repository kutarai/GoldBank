using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UniBank.Core.Common.Persistence;
using UniBank.Core.Modules.Accounts.Domain.Entities;
using UniBank.Core.Modules.Accounts.Infrastructure.Services;
using UniBank.Core.Modules.Agents.Infrastructure.Services;
using UniBank.Core.Modules.Payments.Domain.Entities;
using UniBank.SharedKernel.Events;
using UniBank.SharedKernel.Messaging;
using UniBank.SharedKernel.Results;

namespace UniBank.Core.Modules.Payments.Application.Handlers;

/// <summary>
/// Handles NFC contactless payment processing at a POS terminal (STORY-023, EPIC-020).
/// Customer pays amount + fee + tax. Merchant receives amount minus discount rate commission.
/// </summary>
public sealed class NfcPaymentHandler
{
    private readonly UniBankDbContext _dbContext;
    private readonly TariffEngine _tariffEngine;
    private readonly TransactionAuthorizationService _authService;
    private readonly IMessageBus _messageBus;
    private readonly ILogger<NfcPaymentHandler> _logger;

    public NfcPaymentHandler(
        UniBankDbContext dbContext,
        TariffEngine tariffEngine,
        TransactionAuthorizationService authService,
        IMessageBus messageBus,
        ILogger<NfcPaymentHandler> logger)
    {
        _dbContext = dbContext;
        _tariffEngine = tariffEngine;
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

        // Calculate tariff: customer fee, merchant commission, and tax
        var tariff = _tariffEngine.Calculate("pos_nfc", command.Amount);
        var fee = tariff.CustomerFee;
        var totalDebit = tariff.TotalCustomerDebit;

        // Check balance (amount + customer fee + tax)
        if (payerAccount.AvailableBalance < totalDebit)
            return Result.Failure<PaymentResult>(
                new Error("Payment.InsufficientFunds",
                    $"Insufficient balance. Required: {totalDebit:F2} (amount {command.Amount:F2} + fee {fee:F2} + tax {tariff.Tax:F2}), Available: {payerAccount.AvailableBalance:F2}"));

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
                Tax: tariff.Tax,
                MerchantCommission: tariff.MerchantDiscount,
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
            payerAccount, merchantAccount, command.Amount, fee, tariff.Tax, tariff.MerchantDiscount,
            command.Currency, "nfc", command.NfcData, null,
            command.TerminalId, command.TenantId, merchant.BusinessName,
            cancellationToken);
    }

    internal async Task<Result<PaymentResult>> ExecutePaymentAsync(
        Account payerAccount,
        Account merchantAccount,
        decimal amount,
        decimal fee,
        decimal tax,
        decimal merchantCommission,
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
        var totalDebit = amount + fee + tax;
        var merchantCredit = amount - merchantCommission;

        // Debit payer (amount + fee + tax)
        payerAccount.Balance -= totalDebit;
        payerAccount.AvailableBalance -= totalDebit;
        payerAccount.UpdatedAt = now;

        // Credit merchant (amount minus merchant discount rate commission)
        merchantAccount.Balance += merchantCredit;
        merchantAccount.AvailableBalance += merchantCredit;
        merchantAccount.UpdatedAt = now;

        // Create payment record
        var payment = new Payment
        {
            PayerAccountId = payerAccount.Id,
            MerchantAccountId = merchantAccount.Id,
            Amount = amount,
            Fee = fee,
            Tax = tax,
            MerchantCommission = merchantCommission,
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

        // Create payer debit transaction (amount + fee + tax)
        var payerTransaction = new Transaction
        {
            AccountId = payerAccount.Id,
            Type = $"{type}_payment",
            Amount = -totalDebit,
            Fee = fee,
            Tax = tax,
            Status = "completed",
            Reference = reference,
            Description = $"{type.ToUpperInvariant()} payment to {merchantName}",
            CounterpartyName = merchantName,
            BalanceAfter = payerAccount.Balance,
            Currency = currency,
            TenantId = tenantId,
            CompletedAt = now
        };

        // Create merchant credit transaction (amount minus commission)
        var merchantTransaction = new Transaction
        {
            AccountId = merchantAccount.Id,
            Type = $"{type}_receipt",
            Amount = merchantCredit,
            Fee = merchantCommission,
            Tax = 0m,
            Status = "completed",
            Reference = reference,
            Description = $"{type.ToUpperInvariant()} payment received (commission {merchantCommission:F2} {currency})",
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
            "Payment {Reference} completed: {Type}, amount {Amount} {Currency}, fee {Fee}, tax {Tax}, merchantComm {MerchantComm}, payer {Payer}, merchant {Merchant}",
            reference, type, amount, currency, fee, tax, merchantCommission, payerAccount.Id, merchantAccount.Id);

        return Result.Success(new PaymentResult(
            TransactionId: payment.Id.ToString(),
            Reference: reference,
            Amount: amount,
            Fee: fee,
            Tax: tax,
            MerchantCommission: merchantCommission,
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
