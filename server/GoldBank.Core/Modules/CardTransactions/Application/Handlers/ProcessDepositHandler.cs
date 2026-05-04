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
/// Handles card deposit transactions — both on-us and off-us (STORY-080, STORY-081).
/// On-us: debits merchant account (float), credits client.
/// Off-us: debits acquiring bank's suspense account, credits client.
/// </summary>
public sealed class ProcessDepositHandler
{
    private readonly GoldBankDbContext _dbContext;
    private readonly CardTransactionValidator _validator;
    private readonly IMessageBus _messageBus;
    private readonly ILogger<ProcessDepositHandler> _logger;

    public ProcessDepositHandler(
        GoldBankDbContext dbContext,
        CardTransactionValidator validator,
        IMessageBus messageBus,
        ILogger<ProcessDepositHandler> logger)
    {
        _dbContext = dbContext;
        _validator = validator;
        _messageBus = messageBus;
        _logger = logger;
    }

    public async Task<Result<CardTransactionResult>> HandleAsync(
        ProcessDepositCommand command, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "ProcessDeposit received: TxnId={TransactionId}, Account={Account}, IsOnUs={IsOnUs}, Amount={Amount} {Currency}",
            command.TransactionId, command.CardHolderAccount, command.IsOnUs, command.Amount, command.Currency);

        // 1. Idempotency check
        var duplicate = await _validator.FindDuplicateAsync(
            command.Stan, command.SourceInstitution, command.TenantId, cancellationToken);
        if (duplicate is not null)
        {
            _logger.LogInformation("Duplicate deposit detected: STAN={Stan}, returning original response", command.Stan);
            return Result.Success(new CardTransactionResult(
                duplicate.Id.ToString(), true, duplicate.ResponseCode ?? "00",
                duplicate.AuthorizationCode, "Duplicate transaction — original response returned",
                duplicate.BalanceAfter, duplicate.Currency, duplicate.CompletedAt));
        }

        // 2. Validate amount
        var amountResult = CardTransactionValidator.ValidateAmount(command.Amount);
        if (amountResult.IsFailure)
            return Result.Failure<CardTransactionResult>(amountResult.Error);

        // 3. Validate cardholder account (no balance check for deposits — client receives money)
        var accountResult = await _validator.ValidateCardHolderAccountAsync(
            command.CardHolderAccount, command.Currency, command.TenantId, cancellationToken);
        if (accountResult.IsFailure)
            return Result.Failure<CardTransactionResult>(accountResult.Error);

        var account = accountResult.Value;

        // 4. Route to on-us or off-us
        if (command.IsOnUs)
            return await ProcessOnUsDepositAsync(command, account, cancellationToken);

        return await ProcessOffUsDepositAsync(command, account, cancellationToken);
    }

    /// <summary>
    /// STORY-080: On-us deposit — merchant is a bank client.
    /// Debit merchant owner account (float), credit client.
    /// </summary>
    private async Task<Result<CardTransactionResult>> ProcessOnUsDepositAsync(
        ProcessDepositCommand command, Account account, CancellationToken ct)
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

        // Check merchant has sufficient float
        if (merchantAccount.AvailableBalance < command.Amount)
            return Result.Failure<CardTransactionResult>(
                new Error("CardTransaction.InsufficientFunds",
                    $"Merchant insufficient float. Required: {command.Amount:F2}, Available: {merchantAccount.AvailableBalance:F2}. Response code: 51"));

        var now = DateTime.UtcNow;
        var reference = CardTransactionValidator.GenerateReference();
        var authCode = CardTransactionValidator.GenerateAuthorizationCode();
        var depositFee = 0m; // TODO: Use tenant fee config

        // Debit merchant (float)
        merchantAccount.Balance -= command.Amount;
        merchantAccount.AvailableBalance -= command.Amount;
        merchantAccount.UpdatedAt = now;

        // Credit client (amount minus deposit fee)
        var clientCredit = command.Amount - depositFee;
        account.Balance += clientCredit;
        account.AvailableBalance += clientCredit;
        account.UpdatedAt = now;

        // Create card transaction record
        var cardTxn = new CardTransaction
        {
            AccountId = account.Id,
            MerchantAccountId = merchantAccount.Id,
            MerchantId = command.MerchantId,
            MerchantName = command.MerchantName,
            TransactionType = "OnUsDeposit",
            Amount = command.Amount,
            Fee = depositFee,
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

        // Transaction record on client's account (credit)
        _dbContext.Transactions.Add(new Transaction
        {
            AccountId = account.Id,
            Type = "CARD_DEPOSIT",
            Amount = clientCredit,
            Fee = depositFee,
            Status = "completed",
            Reference = reference,
            Description = $"Card deposit at {command.MerchantName}",
            CounterpartyName = command.MerchantName,
            BalanceAfter = account.Balance,
            Currency = command.Currency,
            TenantId = command.TenantId,
            CompletedAt = now
        });

        // Transaction record on merchant's account (debit)
        _dbContext.Transactions.Add(new Transaction
        {
            AccountId = merchantAccount.Id,
            Type = "CARD_DEPOSIT_DISBURSEMENT",
            Amount = -command.Amount,
            Fee = 0,
            Status = "completed",
            Reference = reference,
            Description = "Card deposit disbursement",
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
            SourceAccountId: merchantAccount.Id,
            DestinationAccountId: account.Id,
            Amount: command.Amount,
            Currency: command.Currency,
            TransactionType: "CARD_DEPOSIT",
            ReferenceNumber: reference), ct);

        _logger.LogInformation(
            "On-us deposit {Reference} completed: {Amount} {Currency}, merchant {MerchantId} → client {ClientId}",
            reference, command.Amount, command.Currency, merchantAccount.Id, account.Id);

        return Result.Success(new CardTransactionResult(
            cardTxn.Id.ToString(), true, "00", authCode,
            "Deposit approved", account.AvailableBalance, command.Currency, now));
    }

    /// <summary>
    /// STORY-081: Off-us deposit — merchant is at another bank.
    /// Debit acquiring bank's suspense account, credit client.
    /// Suspense accounts may go negative (settlement covers this).
    /// </summary>
    private async Task<Result<CardTransactionResult>> ProcessOffUsDepositAsync(
        ProcessDepositCommand command, Account account, CancellationToken ct)
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

        var now = DateTime.UtcNow;
        var reference = CardTransactionValidator.GenerateReference();
        var authCode = CardTransactionValidator.GenerateAuthorizationCode();
        var depositFee = 0m; // TODO: Use tenant fee config

        // Debit suspense account (may go negative — settlement will cover)
        suspenseAccount.Balance -= command.Amount;
        suspenseAccount.UpdatedAt = now;

        // Credit client (amount minus deposit fee)
        var clientCredit = command.Amount - depositFee;
        account.Balance += clientCredit;
        account.AvailableBalance += clientCredit;
        account.UpdatedAt = now;

        // Create card transaction record
        var cardTxn = new CardTransaction
        {
            AccountId = account.Id,
            MerchantId = command.MerchantId,
            MerchantName = command.MerchantName,
            TransactionType = "OffUsDeposit",
            Amount = command.Amount,
            Fee = depositFee,
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

        // Transaction record on client's account (credit)
        _dbContext.Transactions.Add(new Transaction
        {
            AccountId = account.Id,
            Type = "CARD_DEPOSIT",
            Amount = clientCredit,
            Fee = depositFee,
            Status = "completed",
            Reference = reference,
            Description = $"Card deposit at {command.MerchantName}",
            CounterpartyName = command.MerchantName,
            BalanceAfter = account.Balance,
            Currency = command.Currency,
            TenantId = command.TenantId,
            CompletedAt = now
        });

        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Off-us deposit {Reference} completed: {Amount} {Currency}, suspense {AcquiringInstitution} → client {ClientId}",
            reference, command.Amount, command.Currency, command.AcquiringInstitution, account.Id);

        return Result.Success(new CardTransactionResult(
            cardTxn.Id.ToString(), true, "00", authCode,
            "Deposit approved", account.AvailableBalance, command.Currency, now));
    }
}
