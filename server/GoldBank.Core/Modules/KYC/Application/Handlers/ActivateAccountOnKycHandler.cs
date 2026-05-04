using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using GoldBank.Core.Common.Persistence;
using GoldBank.Core.Modules.KYC.Domain.Entities;
using GoldBank.SharedKernel.Messaging;
using GoldBank.SharedKernel.Results;

namespace GoldBank.Core.Modules.KYC.Application.Handlers;

/// <summary>
/// Activates an account when all KYC documents are approved (STORY-013).
/// Can be triggered by KYC admin approval or auto-approval from selfie match.
/// </summary>
public sealed class ActivateAccountOnKycHandler
{
    private readonly GoldBankDbContext _dbContext;
    private readonly IMessageBus _messageBus;
    private readonly ILogger<ActivateAccountOnKycHandler> _logger;

    public ActivateAccountOnKycHandler(
        GoldBankDbContext dbContext,
        IMessageBus messageBus,
        ILogger<ActivateAccountOnKycHandler> logger)
    {
        _dbContext = dbContext;
        _messageBus = messageBus;
        _logger = logger;
    }

    /// <summary>
    /// Check if all KYC documents for an account are approved and activate the account.
    /// </summary>
    public async Task<Result> HandleAsync(Guid accountId, CancellationToken cancellationToken = default)
    {
        var account = await _dbContext.Accounts
            .FirstOrDefaultAsync(a => a.Id == accountId && a.DeletedAt == null, cancellationToken);

        if (account is null)
            return Result.Failure(new Error("Account.NotFound", "Account not found."));

        if (account.Status == "active")
            return Result.Success(); // Already active

        // Check all KYC documents
        var documents = await _dbContext.Set<KycDocument>()
            .Where(d => d.AccountId == accountId)
            .ToListAsync(cancellationToken);

        var hasApprovedId = documents.Any(d => d.DocumentType == "national_id" && d.Status == "approved");
        var hasApprovedSelfie = documents.Any(d => d.DocumentType == "selfie" && d.Status == "approved");

        if (!hasApprovedId || !hasApprovedSelfie)
        {
            _logger.LogInformation(
                "Account {AccountId} KYC not complete. ID: {HasId}, Selfie: {HasSelfie}",
                accountId, hasApprovedId, hasApprovedSelfie);
            return Result.Failure(
                new Error("KYC.Incomplete", "KYC verification is not complete."));
        }

        // Activate ALL accounts for this phone number (ZWG + USD)
        var allAccounts = await _dbContext.Accounts
            .Where(a => a.PhoneNumber == account.PhoneNumber && a.DeletedAt == null)
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        foreach (var acct in allAccounts)
        {
            acct.Status = "active";
            acct.KycLevel = 2;
            acct.UpdatedAt = now;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "All accounts for phone {Phone} activated after KYC approval ({Count} accounts)",
            account.PhoneNumber, allAccounts.Count);

        return Result.Success();
    }
}
