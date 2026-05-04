using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using GoldBank.Core.Common.Persistence;
using GoldBank.Core.Modules.Accounts.Domain.Entities;
using GoldBank.SharedKernel.Results;

namespace GoldBank.Core.Modules.Admin.Application.Handlers;

/// <summary>
/// Searches transactions by multiple criteria with support for streaming large result sets (STORY-058).
/// Used by admin portal for transaction monitoring and investigation.
/// </summary>
public sealed class SearchTransactionsHandler
{
    private readonly GoldBankDbContext _dbContext;
    private readonly ILogger<SearchTransactionsHandler> _logger;

    public SearchTransactionsHandler(
        GoldBankDbContext dbContext,
        ILogger<SearchTransactionsHandler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<Result<List<AdminTransactionDto>>> HandleAsync(
        string? accountId,
        string? merchantId,
        string? reference,
        string? typeFilter,
        string? statusFilter,
        DateTime? dateFrom,
        DateTime? dateTo,
        decimal? minAmount,
        decimal? maxAmount,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var queryable = _dbContext.Set<Transaction>().AsQueryable();

        if (!string.IsNullOrWhiteSpace(accountId) && Guid.TryParse(accountId, out var accountGuid))
        {
            queryable = queryable.Where(t => t.AccountId == accountGuid);
        }

        if (!string.IsNullOrWhiteSpace(reference))
        {
            queryable = queryable.Where(t => t.Reference != null && t.Reference.Contains(reference));
        }

        if (!string.IsNullOrWhiteSpace(typeFilter))
        {
            queryable = queryable.Where(t => t.Type == typeFilter);
        }

        if (!string.IsNullOrWhiteSpace(statusFilter))
        {
            queryable = queryable.Where(t => t.Status == statusFilter);
        }

        if (dateFrom.HasValue)
        {
            queryable = queryable.Where(t => t.CreatedAt >= dateFrom.Value);
        }

        if (dateTo.HasValue)
        {
            queryable = queryable.Where(t => t.CreatedAt <= dateTo.Value);
        }

        if (minAmount.HasValue)
        {
            queryable = queryable.Where(t => t.Amount >= minAmount.Value);
        }

        if (maxAmount.HasValue)
        {
            queryable = queryable.Where(t => t.Amount <= maxAmount.Value);
        }

        var effectivePage = Math.Max(1, page);
        var effectivePageSize = Math.Clamp(pageSize, 1, 100);

        var transactions = await queryable
            .OrderByDescending(t => t.CreatedAt)
            .Skip((effectivePage - 1) * effectivePageSize)
            .Take(effectivePageSize)
            .Join(
                _dbContext.Set<Account>(),
                t => t.AccountId,
                a => a.Id,
                (t, a) => new AdminTransactionDto(
                    t.Id.ToString(),
                    t.AccountId.ToString(),
                    a.PhoneNumber,
                    t.Type,
                    t.Amount,
                    t.Fee,
                    t.Currency,
                    t.Status,
                    t.Reference ?? string.Empty,
                    t.CounterpartyName ?? string.Empty,
                    t.CreatedAt))
            .ToListAsync(cancellationToken);

        _logger.LogInformation(
            "Transaction search completed: {Count} results",
            transactions.Count);

        return Result.Success(transactions);
    }
}

public sealed record AdminTransactionDto(
    string TransactionId,
    string AccountId,
    string AccountPhone,
    string Type,
    decimal Amount,
    decimal Fee,
    string Currency,
    string Status,
    string Reference,
    string CounterpartyInfo,
    DateTime CreatedAt);
