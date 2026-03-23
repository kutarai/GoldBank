using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UniBank.Core.Common.Persistence;
using UniBank.Core.Modules.CardTransactions.Application.Commands;
using UniBank.Core.Modules.CardTransactions.Application.Validators;
using UniBank.Core.Modules.CardTransactions.Domain.Entities;
using UniBank.SharedKernel.Results;

namespace UniBank.Core.Modules.CardTransactions.Application.Handlers;

/// <summary>
/// Handles card mini-statement enquiry transactions (STORY-083).
/// Returns the cardholder's most recent transactions.
/// </summary>
public sealed class StatementEnquiryHandler
{
    private const int DefaultMaxRecords = 10;
    private const int AbsoluteMaxRecords = 20;

    private readonly UniBankDbContext _dbContext;
    private readonly CardTransactionValidator _validator;
    private readonly ILogger<StatementEnquiryHandler> _logger;

    public StatementEnquiryHandler(
        UniBankDbContext dbContext,
        CardTransactionValidator validator,
        ILogger<StatementEnquiryHandler> logger)
    {
        _dbContext = dbContext;
        _validator = validator;
        _logger = logger;
    }

    public async Task<Result<StatementEnquiryResult>> HandleAsync(
        StatementEnquiryCommand command, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "StatementEnquiry received: TxnId={TransactionId}, Account={Account}, IsOnUs={IsOnUs}, MaxRecords={MaxRecords}",
            command.TransactionId, command.CardHolderAccount, command.IsOnUs, command.MaxRecords);

        // 1. Idempotency check
        var duplicate = await _validator.FindDuplicateAsync(
            command.Stan, command.SourceInstitution, command.TenantId, cancellationToken);
        if (duplicate is not null)
        {
            _logger.LogInformation("Duplicate statement enquiry detected: STAN={Stan}", command.Stan);
            return Result.Success(new StatementEnquiryResult(
                duplicate.Id.ToString(), true, "00", "Duplicate — original response returned",
                [], duplicate.BalanceAfter, duplicate.Currency));
        }

        // 2. Validate cardholder account (exists, active)
        var account = await _dbContext.Accounts
            .FirstOrDefaultAsync(
                a => a.PhoneNumber == command.CardHolderAccount
                     && a.TenantId == command.TenantId
                     && a.DeletedAt == null, cancellationToken);

        if (account is null)
            return Result.Failure<StatementEnquiryResult>(
                new Error("CardTransaction.AccountNotFound", "Invalid card/account number. Response code: 14"));

        if (account.Status is not "active")
            return Result.Failure<StatementEnquiryResult>(
                new Error("CardTransaction.AccountBlocked", "Account is blocked or inactive. Response code: 78"));

        // 3. Apply statement enquiry fee if configured (waive if insufficient)
        var enquiryFee = 0m; // TODO: Use tenant fee config for STATEMENT_ENQUIRY
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

        // 4. Retrieve recent completed transactions
        var maxRecords = command.MaxRecords > 0
            ? Math.Min(command.MaxRecords, AbsoluteMaxRecords)
            : DefaultMaxRecords;

        var transactions = await _dbContext.Transactions
            .Where(t => t.AccountId == account.Id
                        && t.TenantId == command.TenantId
                        && t.Status == "completed")
            .OrderByDescending(t => t.CreatedAt)
            .Take(maxRecords)
            .ToListAsync(cancellationToken);

        // 5. Map to statement entries
        // Off-us: sanitize descriptions — exclude counterparty names and internal references
        var entries = transactions.Select(t => new StatementEntryResult(
            Date: t.CompletedAt ?? t.CreatedAt,
            Description: command.IsOnUs
                ? (t.Description ?? t.Type)
                : SanitizeDescription(t.Type),
            Amount: t.Amount,
            Type: t.Type,
            Reference: command.IsOnUs ? (t.Reference ?? "") : "",
            BalanceAfter: t.BalanceAfter,
            Currency: t.Currency
        )).ToList();

        var transactionType = command.IsOnUs ? "OnUsStatementEnquiry" : "OffUsStatementEnquiry";

        // 6. Record the enquiry for audit
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
            "{TransactionType} completed for account {Account}, {EntryCount} entries returned",
            transactionType, command.CardHolderAccount, entries.Count);

        return Result.Success(new StatementEnquiryResult(
            cardTxn.Id.ToString(), true, "00",
            entries.Count > 0 ? "Statement retrieved" : "No transactions found",
            entries, account.AvailableBalance, account.Currency));
    }

    /// <summary>
    /// For off-us enquiries, return only the transaction type as the description.
    /// Counterparty names, phone numbers, and internal details are not exposed to external terminals.
    /// </summary>
    private static string SanitizeDescription(string transactionType)
    {
        return transactionType switch
        {
            "CARD_PURCHASE" => "Card Purchase",
            "CARD_SALE" => "Card Sale",
            "CARD_DEPOSIT" => "Card Deposit",
            "CARD_DEPOSIT_DISBURSEMENT" => "Deposit Disbursement",
            "cash_in" => "Cash In",
            "cash_out" => "Cash Out",
            "p2p_send" => "Transfer Out",
            "p2p_receive" => "Transfer In",
            "payment_nfc" => "NFC Payment",
            "payment_qr" => "QR Payment",
            "bill_payment" => "Bill Payment",
            "transfer_domestic" => "Transfer",
            "transfer_cross_border" => "International Transfer",
            "fee" => "Fee",
            "reversal" => "Reversal",
            "settlement" => "Settlement",
            _ => "Transaction"
        };
    }
}
