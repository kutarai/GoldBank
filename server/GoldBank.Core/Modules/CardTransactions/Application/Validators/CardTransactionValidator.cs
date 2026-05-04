using Microsoft.EntityFrameworkCore;
using GoldBank.Core.Common.Persistence;
using GoldBank.Core.Modules.Accounts.Domain.Entities;
using GoldBank.SharedKernel.Results;

namespace GoldBank.Core.Modules.CardTransactions.Application.Validators;

/// <summary>
/// Common validation logic for card transactions (STORY-077).
/// Validates that the cardholder account exists, is active, and the currency matches.
/// </summary>
public sealed class CardTransactionValidator
{
    private readonly GoldBankDbContext _dbContext;

    public CardTransactionValidator(GoldBankDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Validates a cardholder account for a card transaction.
    /// Returns the account if valid, or a Result failure with the appropriate ISO 8583 response code.
    /// </summary>
    public async Task<Result<Account>> ValidateCardHolderAccountAsync(
        string accountIdentifier, string currency, string tenantId, CancellationToken cancellationToken = default)
    {
        var account = await _dbContext.Accounts
            .FirstOrDefaultAsync(
                a => a.PhoneNumber == accountIdentifier
                     && a.TenantId == tenantId
                     && a.DeletedAt == null,
                cancellationToken);

        if (account is null)
            return Result.Failure<Account>(
                new Error("CardTransaction.AccountNotFound", "Invalid card/account number. Response code: 14"));

        if (account.Status is not "active")
            return Result.Failure<Account>(
                new Error("CardTransaction.AccountBlocked", "Account is blocked or inactive. Response code: 78"));

        if (!string.Equals(account.Currency, currency, StringComparison.OrdinalIgnoreCase))
            return Result.Failure<Account>(
                new Error("CardTransaction.CurrencyMismatch", "Currency not supported by account. Response code: 12"));

        return Result.Success(account);
    }

    /// <summary>
    /// Validates that a financial transaction amount is positive.
    /// </summary>
    public static Result ValidateAmount(decimal amount)
    {
        if (amount <= 0)
            return Result.Failure(
                new Error("CardTransaction.InvalidAmount", "Transaction amount must be greater than zero. Response code: 13"));

        return Result.Success();
    }

    /// <summary>
    /// Checks for a duplicate transaction by STAN + source institution within the last 5 minutes.
    /// Returns the existing CardTransaction if found, null otherwise.
    /// </summary>
    public async Task<Domain.Entities.CardTransaction?> FindDuplicateAsync(
        string stan, string sourceInstitution, string tenantId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(stan) || string.IsNullOrWhiteSpace(sourceInstitution))
            return null;

        var cutoff = DateTime.UtcNow.AddMinutes(-5);

        return await _dbContext.CardTransactions
            .FirstOrDefaultAsync(
                ct => ct.Stan == stan
                      && ct.SourceInstitution == sourceInstitution
                      && ct.TenantId == tenantId
                      && ct.CreatedAt >= cutoff,
                cancellationToken);
    }

    /// <summary>
    /// Generates a 6-character alphanumeric authorization code.
    /// </summary>
    public static string GenerateAuthorizationCode()
    {
        return Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
    }

    /// <summary>
    /// Generates a card transaction reference.
    /// </summary>
    public static string GenerateReference()
    {
        return $"CTX-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..28].ToUpperInvariant();
    }

    /// <summary>
    /// Maps an error code to the appropriate ISO 8583 response code.
    /// </summary>
    public static string MapToResponseCode(string errorCode)
    {
        return errorCode switch
        {
            "CardTransaction.AccountNotFound" => "14",
            "CardTransaction.AccountBlocked" => "78",
            "CardTransaction.CurrencyMismatch" => "12",
            "CardTransaction.InvalidAmount" => "13",
            "CardTransaction.InsufficientFunds" => "51",
            "CardTransaction.InvalidMerchant" => "03",
            _ => "96"
        };
    }
}
