using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using GoldBank.Core.Common.Persistence;
using GoldBank.Core.Modules.BillPay.Domain.Entities;

namespace GoldBank.Core.Modules.BillPay.Application.Handlers;

/// <summary>
/// Handles listing bill payment providers filtered by category and country (STORY-037).
/// Returns only active providers that have not been soft-deleted.
/// </summary>
public sealed class ListProvidersHandler
{
    private readonly GoldBankDbContext _dbContext;
    private readonly ILogger<ListProvidersHandler> _logger;

    public ListProvidersHandler(GoldBankDbContext dbContext, ILogger<ListProvidersHandler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<List<BillProvider>> HandleAsync(
        string? category, string? countryCode, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.BillProviders
            .Where(p => p.Status == "active" && p.DeletedAt == null);

        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(p => p.Category == category);
        }

        if (!string.IsNullOrWhiteSpace(countryCode))
        {
            query = query.Where(p => p.CountryCode == countryCode);
        }

        var providers = await query
            .OrderBy(p => p.Category)
            .ThenBy(p => p.Name)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        _logger.LogInformation(
            "Listed {Count} bill providers for category '{Category}', country '{Country}'",
            providers.Count, category ?? "all", countryCode ?? "all");

        return providers;
    }
}
