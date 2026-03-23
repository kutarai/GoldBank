using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UniBank.Core.Common.Persistence;
using UniBank.Core.Modules.Accounts.Application.Commands;
using UniBank.SharedKernel.Results;

namespace UniBank.Core.Modules.Accounts.Application.Handlers;

/// <summary>
/// Updates account profile fields (STORY-015).
/// </summary>
public sealed class UpdateProfileHandler
{
    private readonly UniBankDbContext _dbContext;
    private readonly ILogger<UpdateProfileHandler> _logger;

    public UpdateProfileHandler(UniBankDbContext dbContext, ILogger<UpdateProfileHandler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<Result<ProfileResult>> HandleAsync(
        UpdateProfileCommand command, CancellationToken cancellationToken = default)
    {
        var account = await _dbContext.Accounts
            .FirstOrDefaultAsync(a => a.Id == command.AccountId && a.DeletedAt == null, cancellationToken);

        if (account is null)
            return Result.Failure<ProfileResult>(
                new Error("Account.NotFound", "Account not found."));

        // Update personal info on ALL accounts for this phone (person-centric)
        var allAccounts = await _dbContext.Accounts
            .Where(a => a.PhoneNumber == account.PhoneNumber && a.DeletedAt == null)
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        foreach (var acct in allAccounts)
        {
            if (!string.IsNullOrEmpty(command.FirstName))
                acct.FirstName = command.FirstName;
            if (!string.IsNullOrEmpty(command.LastName))
                acct.LastName = command.LastName;
            if (!string.IsNullOrEmpty(command.Email))
                acct.Email = command.Email;
            if (!string.IsNullOrEmpty(command.DateOfBirth))
                acct.DateOfBirth = command.DateOfBirth;
            if (!string.IsNullOrEmpty(command.NationalId))
                acct.NationalId = command.NationalId;
            acct.UpdatedAt = now;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Profile updated for phone {Phone} ({Count} accounts)", account.PhoneNumber, allAccounts.Count);

        // Build account summaries
        var accountSummaries = allAccounts.Select(a => new AccountSummaryResult(
            AccountId: a.Id.ToString(),
            Currency: a.Currency,
            Balance: a.Balance,
            AvailableBalance: a.AvailableBalance,
            CardPanLast4: a.CardPan is not null && a.CardPan.Length >= 4 ? a.CardPan[^4..] : null
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
