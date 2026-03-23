using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UniBank.Core.Common.Persistence;
using UniBank.Core.Modules.CardTransactions.Application.Commands;
using UniBank.Core.Modules.CardTransactions.Application.Validators;
using UniBank.Core.Modules.CardTransactions.Domain.Entities;
using UniBank.SharedKernel.Results;

namespace UniBank.Core.Modules.CardTransactions.Application.Handlers;

/// <summary>
/// Handles card balance enquiry transactions (STORY-082).
/// Returns the cardholder's available and ledger balances.
/// </summary>
public sealed class BalanceEnquiryHandler
{
    private readonly UniBankDbContext _dbContext;
    private readonly CardTransactionValidator _validator;
    private readonly ILogger<BalanceEnquiryHandler> _logger;

    public BalanceEnquiryHandler(
        UniBankDbContext dbContext,
        CardTransactionValidator validator,
        ILogger<BalanceEnquiryHandler> logger)
    {
        _dbContext = dbContext;
        _validator = validator;
        _logger = logger;
    }

    public async Task<Result<BalanceEnquiryResult>> HandleAsync(
        BalanceEnquiryCommand command, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "BalanceEnquiry received: TxnId={TransactionId}, Account={Account}, IsOnUs={IsOnUs}",
            command.TransactionId, command.CardHolderAccount, command.IsOnUs);

        // 1. Idempotency check
        var duplicate = await _validator.FindDuplicateAsync(
            command.Stan, command.SourceInstitution, command.TenantId, cancellationToken);
        if (duplicate is not null)
        {
            _logger.LogInformation("Duplicate balance enquiry detected: STAN={Stan}", command.Stan);
            // Off-us duplicates also withhold ledger balance
            var dupLedger = command.IsOnUs ? duplicate.BalanceAfter : 0m;
            return Result.Success(new BalanceEnquiryResult(
                duplicate.Id.ToString(), true, "00", "Duplicate — original response returned",
                duplicate.BalanceAfter, dupLedger, duplicate.Currency));
        }

        // 2. Validate cardholder account (exists, active)
        var account = await _dbContext.Accounts
            .FirstOrDefaultAsync(
                a => a.PhoneNumber == command.CardHolderAccount
                     && a.TenantId == command.TenantId
                     && a.DeletedAt == null, cancellationToken);

        if (account is null)
            return Result.Failure<BalanceEnquiryResult>(
                new Error("CardTransaction.AccountNotFound", "Invalid card/account number. Response code: 14"));

        if (account.Status is not "active")
            return Result.Failure<BalanceEnquiryResult>(
                new Error("CardTransaction.AccountBlocked", "Account is blocked or inactive. Response code: 78"));

        // 3. Apply balance enquiry fee if configured (waive if insufficient funds)
        var enquiryFee = 0m; // TODO: Use tenant fee config for BALANCE_ENQUIRY
        if (enquiryFee > 0 && account.AvailableBalance >= enquiryFee)
        {
            account.Balance -= enquiryFee;
            account.AvailableBalance -= enquiryFee;
            account.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            enquiryFee = 0;
        }

        var transactionType = command.IsOnUs ? "OnUsBalanceEnquiry" : "OffUsBalanceEnquiry";

        // 4. Record the enquiry for audit
        var cardTxn = new CardTransaction
        {
            AccountId = account.Id,
            TransactionType = transactionType,
            Amount = 0,
            Fee = enquiryFee,
            Currency = account.Currency,
            Status = "completed",
            ResponseCode = "00",
            Reference = CardTransactionValidator.GenerateReference(),
            RetrievalReference = command.RetrievalReference,
            Stan = command.Stan,
            TerminalId = command.TerminalId,
            SourceInstitution = command.SourceInstitution,
            BalanceAfter = account.AvailableBalance,
            TenantId = command.TenantId,
            CompletedAt = DateTime.UtcNow
        };
        _dbContext.CardTransactions.Add(cardTxn);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "{TransactionType} completed for account {Account}",
            transactionType, command.CardHolderAccount);

        // Off-us: withhold ledger balance — only available balance is returned to external terminals
        var ledgerBalance = command.IsOnUs ? account.Balance : 0m;

        return Result.Success(new BalanceEnquiryResult(
            cardTxn.Id.ToString(), true, "00", "Balance enquiry successful",
            account.AvailableBalance, ledgerBalance, account.Currency));
    }
}
