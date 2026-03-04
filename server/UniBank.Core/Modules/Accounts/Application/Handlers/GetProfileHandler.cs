using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UniBank.Core.Common.Persistence;
using UniBank.SharedKernel.Results;

namespace UniBank.Core.Modules.Accounts.Application.Handlers;

/// <summary>
/// Retrieves account profile details (STORY-015).
/// </summary>
public sealed class GetProfileHandler
{
    private readonly UniBankDbContext _dbContext;
    private readonly ILogger<GetProfileHandler> _logger;

    public GetProfileHandler(UniBankDbContext dbContext, ILogger<GetProfileHandler> logger)
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
    DateTime? LastLoginAt);
