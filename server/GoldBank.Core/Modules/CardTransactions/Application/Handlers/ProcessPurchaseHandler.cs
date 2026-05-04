using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using GoldBank.Core.Common.Persistence;
using GoldBank.Core.Modules.Accounts.Domain.Entities;
using GoldBank.Core.Modules.CardTransactions.Application.Commands;
using GoldBank.Core.Modules.CardTransactions.Application.Validators;
using GoldBank.Core.Modules.CardTransactions.Domain.Entities;
using GoldBank.SharedKernel.Events;
using GoldBank.SharedKernel.Messaging;
using GoldBank.SharedKernel.Results;

namespace GoldBank.Core.Modules.CardTransactions.Application.Handlers;

/// <summary>
/// Handles card purchase transactions — both on-us and off-us (STORY-078, STORY-079).
/// On-us: debits client, credits merchant account.
/// Off-us: debits client, credits acquiring bank's suspense account.
/// </summary>
public sealed class ProcessPurchaseHandler
{
    private readonly GoldBankDbContext _dbContext;
    private readonly CardTransactionValidator _validator;
    private readonly IMessageBus _messageBus;
    private readonly ILogger<ProcessPurchaseHandler> _logger;

    public ProcessPurchaseHandler(
        GoldBankDbContext dbContext,
        CardTransactionValidator validator,
        IMessageBus messageBus,
        ILogger<ProcessPurchaseHandler> logger)
    {
        _dbContext = dbContext;
        _validator = validator;
        _messageBus = messageBus;
        _logger = logger;
    }

    public async Task<Result<CardTransactionResult>> HandleAsync(
        ProcessPurchaseCommand command, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "ProcessPurchase received: TxnId={TransactionId}, Account={Account}, IsOnUs={IsOnUs}, Amount={Amount} {Currency}",
            command.TransactionId, command.CardHolderAccount, command.IsOnUs, command.Amount, command.Currency);

        // 1. Idempotency check
        var duplicate = await _validator.FindDuplicateAsync(
            command.Stan, command.SourceInstitution, command.TenantId, cancellationToken);
        if (duplicate is not null)
        {
            _logger.LogInformation("Duplicate purchase detected: STAN={Stan}, returning original response", command.Stan);
            return Result.Success(new CardTransactionResult(
                duplicate.Id.ToString(), true, duplicate.ResponseCode ?? "00",
                duplicate.AuthorizationCode, "Duplicate transaction — original response returned",
                duplicate.BalanceAfter, duplicate.Currency, duplicate.CompletedAt));
        }

        // 2. Validate amount
        var amountResult = CardTransactionValidator.ValidateAmount(command.Amount);
        if (amountResult.IsFailure)
            return Result.Failure<CardTransactionResult>(amountResult.Error);

        // 3. Validate cardholder account
        var accountResult = await _validator.ValidateCardHolderAccountAsync(
            command.CardHolderAccount, command.Currency, command.TenantId, cancellationToken);
        if (accountResult.IsFailure)
            return Result.Failure<CardTransactionResult>(accountResult.Error);

        var account = accountResult.Value;

        // 4. Route to on-us or off-us
        if (command.IsOnUs)
            return await ProcessOnUsPurchaseAsync(command, account, cancellationToken);

        return await ProcessOffUsPurchaseAsync(command, account, cancellationToken);
    }

    /// <summary>
    /// STORY-078: On-us purchase — both cardholder and merchant are bank clients.
    /// Debit client account, credit merchant owner account.
    /// </summary>
    private async Task<Result<CardTransactionResult>> ProcessOnUsPurchaseAsync(
        ProcessPurchaseCommand command, Account account, CancellationToken ct)
    {
        // Validate merchant
        var merchant = await _dbContext.Merchants
            .FirstOrDefaultAsync(
                m => m.MerchantCode == command.MerchantId
                     && m.TenantId == command.TenantId
                     && m.Status == "active", ct);

        if (merchant is null)
            return Result.Failure<CardTransactionResult>(
                new Error("CardTransaction.InvalidMerchant", "Merchant not found or inactive. Response code: 03"));

        var merchantAccount = await _dbContext.Accounts
            .FirstOrDefaultAsync(a => a.Id == merchant.OwnerAccountId && a.DeletedAt == null, ct);

        if (merchantAccount is null)
            return Result.Failure<CardTransactionResult>(
                new Error("CardTransaction.InvalidMerchant", "Merchant account not found. Response code: 03"));

        // Check balance (amount + fee)
        // TODO: Use tenant fee config when implementing fee lookup
        var cardholderFee = 0m;
        var totalDebit = command.Amount + cardholderFee;

        if (account.AvailableBalance < totalDebit)
            return Result.Failure<CardTransactionResult>(
                new Error("CardTransaction.InsufficientFunds",
                    $"Insufficient funds. Required: {totalDebit:F2}, Available: {account.AvailableBalance:F2}. Response code: 51"));

        var now = DateTime.UtcNow;
        var reference = CardTransactionValidator.GenerateReference();
        var authCode = CardTransactionValidator.GenerateAuthorizationCode();

        // Debit client
        account.Balance -= totalDebit;
        account.AvailableBalance -= totalDebit;
        account.UpdatedAt = now;

        // Credit merchant (full amount — merchant fee is separate concern)
        merchantAccount.Balance += command.Amount;
        merchantAccount.AvailableBalance += command.Amount;
        merchantAccount.UpdatedAt = now;

        // Create card transaction record
        var cardTxn = new CardTransaction
        {
            AccountId = account.Id,
            MerchantAccountId = merchantAccount.Id,
            MerchantId = command.MerchantId,
            MerchantName = command.MerchantName,
            TransactionType = "OnUsPurchase",
            Amount = command.Amount,
            Fee = cardholderFee,
            Currency = command.Currency,
            Status = "completed",
            ResponseCode = "00",
            AuthorizationCode = authCode,
            Reference = reference,
            RetrievalReference = command.RetrievalReference,
            Stan = command.Stan,
            TerminalId = command.TerminalId,
            ProcessingCode = command.ProcessingCode,
            SourceInstitution = command.SourceInstitution,
            AcquiringInstitution = command.AcquiringInstitution,
            BalanceAfter = account.Balance,
            TenantId = command.TenantId,
            CompletedAt = now
        };
        _dbContext.CardTransactions.Add(cardTxn);

        // Create transaction records on both accounts
        _dbContext.Transactions.Add(new Transaction
        {
            AccountId = account.Id,
            Type = "CARD_PURCHASE",
            Amount = -totalDebit,
            Fee = cardholderFee,
            Status = "completed",
            Reference = reference,
            Description = $"Card purchase at {command.MerchantName}",
            CounterpartyName = command.MerchantName,
            BalanceAfter = account.Balance,
            Currency = command.Currency,
            TenantId = command.TenantId,
            CompletedAt = now
        });

        _dbContext.Transactions.Add(new Transaction
        {
            AccountId = merchantAccount.Id,
            Type = "CARD_SALE",
            Amount = command.Amount,
            Fee = 0,
            Status = "completed",
            Reference = reference,
            Description = "Card sale received",
            CounterpartyName = account.FirstName is not null
                ? $"{account.FirstName} {account.LastName}".Trim()
                : account.PhoneNumber,
            BalanceAfter = merchantAccount.Balance,
            Currency = command.Currency,
            TenantId = command.TenantId,
            CompletedAt = now
        });

        await _dbContext.SaveChangesAsync(ct);

        await _messageBus.PublishAsync(new TransactionCompleted(
            TransactionId: cardTxn.Id,
            SourceAccountId: account.Id,
            DestinationAccountId: merchantAccount.Id,
            Amount: command.Amount,
            Currency: command.Currency,
            TransactionType: "CARD_PURCHASE",
            ReferenceNumber: reference), ct);

        _logger.LogInformation(
            "On-us purchase {Reference} completed: {Amount} {Currency}, client {ClientId} → merchant {MerchantId}",
            reference, command.Amount, command.Currency, account.Id, merchantAccount.Id);

        return Result.Success(new CardTransactionResult(
            cardTxn.Id.ToString(), true, "00", authCode,
            "Purchase approved", account.AvailableBalance, command.Currency, now));
    }

    /// <summary>
    /// STORY-079: Off-us purchase — cardholder is bank client, merchant is at another bank.
    /// Debit client account, credit acquiring bank's suspense account.
    /// </summary>
    private async Task<Result<CardTransactionResult>> ProcessOffUsPurchaseAsync(
        ProcessPurchaseCommand command, Account account, CancellationToken ct)
    {
        // Resolve acquiring bank's suspense account
        var suspenseAccountNumber = $"SUSPENSE-{command.AcquiringInstitution}";
        var suspenseAccount = await _dbContext.Accounts
            .FirstOrDefaultAsync(
                a => a.PhoneNumber == suspenseAccountNumber
                     && a.TenantId == command.TenantId
                     && a.DeletedAt == null, ct);

        if (suspenseAccount is null)
        {
            _logger.LogError(
                "Suspense account not found for acquiring institution {Institution}. STAN={Stan}",
                command.AcquiringInstitution, command.Stan);
            return Result.Failure<CardTransactionResult>(
                new Error("CardTransaction.SystemError",
                    $"Suspense account not configured for institution {command.AcquiringInstitution}. Response code: 96"));
        }

        // Check balance (amount + fee)
        var cardholderFee = 0m;
        var totalDebit = command.Amount + cardholderFee;

        if (account.AvailableBalance < totalDebit)
            return Result.Failure<CardTransactionResult>(
                new Error("CardTransaction.InsufficientFunds",
                    $"Insufficient funds. Required: {totalDebit:F2}, Available: {account.AvailableBalance:F2}. Response code: 51"));

        var now = DateTime.UtcNow;
        var reference = CardTransactionValidator.GenerateReference();
        var authCode = CardTransactionValidator.GenerateAuthorizationCode();

        // Debit client
        account.Balance -= totalDebit;
        account.AvailableBalance -= totalDebit;
        account.UpdatedAt = now;

        // Credit suspense account (full amount — no merchant fee deduction)
        suspenseAccount.Balance += command.Amount;
        suspenseAccount.AvailableBalance += command.Amount;
        suspenseAccount.UpdatedAt = now;

        // Create card transaction record
        var cardTxn = new CardTransaction
        {
            AccountId = account.Id,
            MerchantId = command.MerchantId,
            MerchantName = command.MerchantName,
            TransactionType = "OffUsPurchase",
            Amount = command.Amount,
            Fee = cardholderFee,
            Currency = command.Currency,
            Status = "completed",
            ResponseCode = "00",
            AuthorizationCode = authCode,
            Reference = reference,
            RetrievalReference = command.RetrievalReference,
            Stan = command.Stan,
            TerminalId = command.TerminalId,
            ProcessingCode = command.ProcessingCode,
            SourceInstitution = command.SourceInstitution,
            AcquiringInstitution = command.AcquiringInstitution,
            BalanceAfter = account.Balance,
            TenantId = command.TenantId,
            CompletedAt = now
        };
        _dbContext.CardTransactions.Add(cardTxn);

        // Create transaction record on cardholder's account
        _dbContext.Transactions.Add(new Transaction
        {
            AccountId = account.Id,
            Type = "CARD_PURCHASE",
            Amount = -totalDebit,
            Fee = cardholderFee,
            Status = "completed",
            Reference = reference,
            Description = $"Card purchase at {command.MerchantName}",
            CounterpartyName = command.MerchantName,
            BalanceAfter = account.Balance,
            Currency = command.Currency,
            TenantId = command.TenantId,
            CompletedAt = now
        });

        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Off-us purchase {Reference} completed: {Amount} {Currency}, client {ClientId} → suspense {AcquiringInstitution}",
            reference, command.Amount, command.Currency, account.Id, command.AcquiringInstitution);

        return Result.Success(new CardTransactionResult(
            cardTxn.Id.ToString(), true, "00", authCode,
            "Purchase approved", account.AvailableBalance, command.Currency, now));
    }
}
