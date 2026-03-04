using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UniBank.Core.Common.Persistence;
using UniBank.Core.Modules.Accounts.Domain.Entities;
using UniBank.SharedKernel.Results;

namespace UniBank.Core.Modules.Admin.Application.Handlers;

/// <summary>
/// Searches customers by name, phone, or national ID with pagination (STORY-056).
/// Used by admin portal for customer account management.
/// </summary>
public sealed class SearchCustomersHandler
{
    private readonly UniBankDbContext _dbContext;
    private readonly ILogger<SearchCustomersHandler> _logger;

    public SearchCustomersHandler(
        UniBankDbContext dbContext,
        ILogger<SearchCustomersHandler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<Result<SearchCustomersResult>> HandleAsync(
        string? query,
        string? statusFilter,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var queryable = _dbContext.Set<Account>()
            .Where(a => a.DeletedAt == null)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query))
        {
            var searchTerm = query.Trim().ToLower();
            queryable = queryable.Where(a =>
                (a.FirstName != null && a.FirstName.ToLower().Contains(searchTerm)) ||
                (a.LastName != null && a.LastName.ToLower().Contains(searchTerm)) ||
                a.PhoneNumber.ToLower().Contains(searchTerm) ||
                (a.NationalId != null && a.NationalId.ToLower().Contains(searchTerm)));
        }

        if (!string.IsNullOrWhiteSpace(statusFilter))
        {
            queryable = queryable.Where(a => a.Status == statusFilter);
        }

        var totalCount = await queryable.CountAsync(cancellationToken);
        var effectivePage = Math.Max(1, page);
        var effectivePageSize = Math.Clamp(pageSize, 1, 100);

        var customers = await queryable
            .OrderByDescending(a => a.CreatedAt)
            .Skip((effectivePage - 1) * effectivePageSize)
            .Take(effectivePageSize)
            .Select(a => new CustomerSummaryDto(
                a.Id.ToString(),
                a.PhoneNumber,
                $"{a.FirstName ?? ""} {a.LastName ?? ""}".Trim(),
                a.Status,
                a.KycLevel,
                a.Balance,
                a.Currency,
                a.CreatedAt,
                a.LastLoginAt))
            .ToListAsync(cancellationToken);

        _logger.LogInformation(
            "Customer search completed: query={Query}, results={Count}, total={Total}",
            query, customers.Count, totalCount);

        return Result.Success(new SearchCustomersResult(
            Customers: customers,
            TotalCount: totalCount,
            Page: effectivePage,
            PageSize: effectivePageSize));
    }
}

public sealed record CustomerSummaryDto(
    string AccountId,
    string PhoneNumber,
    string FullName,
    string Status,
    int KycLevel,
    decimal Balance,
    string Currency,
    DateTime CreatedAt,
    DateTime? LastLoginAt);

public sealed record SearchCustomersResult(
    List<CustomerSummaryDto> Customers,
    int TotalCount,
    int Page,
    int PageSize);
