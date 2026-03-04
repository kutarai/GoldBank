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

        if (!string.IsNullOrEmpty(command.FirstName))
            account.FirstName = command.FirstName;

        if (!string.IsNullOrEmpty(command.LastName))
            account.LastName = command.LastName;

        if (!string.IsNullOrEmpty(command.Email))
            account.Email = command.Email;

        if (!string.IsNullOrEmpty(command.DateOfBirth))
            account.DateOfBirth = command.DateOfBirth;

        if (!string.IsNullOrEmpty(command.NationalId))
            account.NationalId = command.NationalId;

        account.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Profile updated for account {AccountId}", command.AccountId);

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
            LastLoginAt: account.LastLoginAt));
    }
}
