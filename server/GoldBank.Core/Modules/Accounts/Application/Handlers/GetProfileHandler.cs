using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using GoldBank.Core.Common.Persistence;
using GoldBank.SharedKernel.Results;

namespace GoldBank.Core.Modules.Accounts.Application.Handlers;

/// <summary>
/// Retrieves account profile details (STORY-015).
/// </summary>
public sealed class GetProfileHandler
{
    private readonly GoldBankDbContext _dbContext;
    private readonly ILogger<GetProfileHandler> _logger;

    public GetProfileHandler(GoldBankDbContext dbContext, ILogger<GetProfileHandler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<Result<ProfileResult>> HandleAsync(
        Guid accountId, CancellationToken cancellationToken = default)
    {
        var account = await _dbContext.Accounts
            .FirstOrDefaultAsync(a => a.Id == accountId && a.DeletedAt == null, cancellationToken);

        if (account is null)
            return Result.Failure<ProfileResult>(
                new Error("Account.NotFound", "Account not found."));

        // Load all accounts for this phone number (ZWG + USD)
        var allAccounts = await _dbContext.Accounts
            .Where(a => a.PhoneNumber == account.PhoneNumber && a.DeletedAt == null)
            .OrderBy(a => a.Currency)
            .ToListAsync(cancellationToken);

        var accountSummaries = allAccounts.Select(a => new AccountSummaryResult(
            AccountId: a.Id.ToString(),
            Currency: a.Currency,
            Balance: a.Balance,
            AvailableBalance: a.AvailableBalance,
            CardPanLast4: a.CardPan is not null && a.CardPan.Length >= 4
                ? a.CardPan[^4..] : null
        )).ToList();

        return Result.Success(new ProfileResult(
            AccountId: account.Id.ToString(),
            PhoneNumber: account.PhoneNumber,
            FirstName: account.FirstName,
            LastName: account.LastName,
            Email: account.Email,
            DateOfBirth: account.DateOfBirth,
            NationalId: account.NationalId,
            Status: account.Status,
            KycLevel: account.KycLevel,
            CreatedAt: account.CreatedAt,
            LastLoginAt: account.LastLoginAt,
            Accounts: accountSummaries));
    }
}

public sealed record ProfileResult(
    string AccountId,
    string PhoneNumber,
    string? FirstName,
    string? LastName,
    string? Email,
    string? DateOfBirth,
    string? NationalId,
    string Status,
    int KycLevel,
    DateTime CreatedAt,
    DateTime? LastLoginAt,
    List<AccountSummaryResult> Accounts);

public sealed record AccountSummaryResult(
    string AccountId,
    string Currency,
    decimal Balance,
    decimal AvailableBalance,
    string? CardPanLast4);
