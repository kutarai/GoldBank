using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UniBank.Core.Common.Persistence;
using UniBank.Core.Modules.BillPay.Application.Commands;
using UniBank.Core.Modules.BillPay.Domain.Entities;
using UniBank.SharedKernel.Results;

namespace UniBank.Core.Modules.BillPay.Application.Handlers;

/// <summary>
/// Handles saving a biller as a favourite for an account (STORY-039).
/// Validates the account and provider exist, and prevents duplicate saved billers.
/// </summary>
public sealed class SaveBillerHandler
{
    private readonly UniBankDbContext _dbContext;
    private readonly ILogger<SaveBillerHandler> _logger;

    public SaveBillerHandler(UniBankDbContext dbContext, ILogger<SaveBillerHandler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<Result> HandleAsync(
        SaveBillerCommand command, CancellationToken cancellationToken = default)
    {
        // Validate inputs
        if (string.IsNullOrWhiteSpace(command.BillingReference))
            return Result.Failure(
                new Error("BillPay.InvalidReference", "Billing reference is required."));

        if (string.IsNullOrWhiteSpace(command.Nickname))
            return Result.Failure(
                new Error("BillPay.InvalidNickname", "Nickname is required."));

        // Verify account exists
        var accountExists = await _dbContext.Accounts
            .AnyAsync(
                a => a.Id == command.AccountId && a.DeletedAt == null,
                cancellationToken);

        if (!accountExists)
            return Result.Failure(
                new Error("Account.NotFound", "Account not found."));

        // Verify provider exists
        var providerExists = await _dbContext.BillProviders
            .AnyAsync(
                p => p.Id == command.ProviderId && p.DeletedAt == null,
                cancellationToken);

        if (!providerExists)
            return Result.Failure(
                new Error("BillPay.ProviderNotFound", "Bill provider not found."));

        // Check for duplicate (same account + provider + reference)
        var duplicate = await _dbContext.SavedBillers
            .AnyAsync(
                b => b.AccountId == command.AccountId
                    && b.ProviderId == command.ProviderId
                    && b.BillingReference == command.BillingReference
                    && b.DeletedAt == null,
                cancellationToken);

        if (duplicate)
            return Result.Failure(
                new Error("BillPay.DuplicateBiller",
                    "This biller with the same reference is already saved."));

        // Create SavedBiller entity
        var savedBiller = new SavedBiller
        {
            AccountId = command.AccountId,
            ProviderId = command.ProviderId,
            BillingReference = command.BillingReference,
            Nickname = command.Nickname,
            TenantId = command.TenantId
        };

        _dbContext.SavedBillers.Add(savedBiller);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Saved biller '{Nickname}' for account {AccountId}, provider {ProviderId}, ref {Reference}",
            command.Nickname, command.AccountId, command.ProviderId, command.BillingReference);

        return Result.Success();
    }
}
