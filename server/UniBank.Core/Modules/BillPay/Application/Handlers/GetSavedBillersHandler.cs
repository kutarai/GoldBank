using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UniBank.Core.Common.Persistence;

namespace UniBank.Core.Modules.BillPay.Application.Handlers;

/// <summary>
/// Handles retrieving saved/favourite billers for an account (STORY-039).
/// Joins with the BillProvider table to include provider names in the result.
/// </summary>
public sealed class GetSavedBillersHandler
{
    private readonly UniBankDbContext _dbContext;
    private readonly ILogger<GetSavedBillersHandler> _logger;

    public GetSavedBillersHandler(UniBankDbContext dbContext, ILogger<GetSavedBillersHandler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<List<SavedBillerDto>> HandleAsync(
        Guid accountId, CancellationToken cancellationToken = default)
    {
        var billers = await (
            from sb in _dbContext.SavedBillers
            join bp in _dbContext.BillProviders on sb.ProviderId equals bp.Id
            where sb.AccountId == accountId && sb.DeletedAt == null && bp.DeletedAt == null
            orderby sb.LastPaidAt descending, sb.Nickname
            select new SavedBillerDto(
                sb.Id.ToString(),
                sb.ProviderId.ToString(),
                bp.Name,
                sb.BillingReference,
                sb.Nickname,
                sb.LastPaidAt)
        ).AsNoTracking().ToListAsync(cancellationToken);

        _logger.LogInformation(
            "Retrieved {Count} saved billers for account {AccountId}",
            billers.Count, accountId);

        return billers;
    }
}

/// <summary>
/// Data transfer object for saved biller query results, including the provider name.
/// </summary>
public sealed record SavedBillerDto(
    string Id,
    string ProviderId,
    string ProviderName,
    string BillingReference,
    string Nickname,
    DateTime? LastPaidAt);
