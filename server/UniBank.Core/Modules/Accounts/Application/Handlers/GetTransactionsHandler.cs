using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UniBank.Core.Common.Persistence;
using UniBank.Core.Modules.Accounts.Application.Commands;
using UniBank.Core.Modules.Accounts.Domain.Entities;
using UniBank.SharedKernel.Results;

namespace UniBank.Core.Modules.Accounts.Application.Handlers;

/// <summary>
/// Retrieves paginated transaction history for an account (STORY-017).
/// Supports filtering by type, status, and date range.
/// </summary>
public sealed class GetTransactionsHandler
{
    private readonly UniBankDbContext _dbContext;
    private readonly ILogger<GetTransactionsHandler> _logger;

    public GetTransactionsHandler(UniBankDbContext dbContext, ILogger<GetTransactionsHandler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<Result<List<TransactionResult>>> HandleAsync(
        GetTransactionsQuery query, CancellationToken cancellationToken = default)
    {
        // Verify account exists
        var accountExists = await _dbContext.Accounts
            .AnyAsync(a => a.Id == query.AccountId && a.DeletedAt == null, cancellationToken);

        if (!accountExists)
            return Result.Failure<List<TransactionResult>>(
                new Error("Account.NotFound", "Account not found."));

        var txQuery = _dbContext.Set<Transaction>()
            .Where(t => t.AccountId == query.AccountId);

        // Apply filters
        if (query.StartDate.HasValue)
            txQuery = txQuery.Where(t => t.CreatedAt >= query.StartDate.Value);

        if (query.EndDate.HasValue)
            txQuery = txQuery.Where(t => t.CreatedAt <= query.EndDate.Value);

        if (!string.IsNullOrEmpty(query.TypeFilter))
            txQuery = txQuery.Where(t => t.Type == query.TypeFilter);

        if (!string.IsNullOrEmpty(query.StatusFilter))
            txQuery = txQuery.Where(t => t.Status == query.StatusFilter);

        // Paginate
        var transactions = await txQuery
            .OrderByDescending(t => t.CreatedAt)
            .Skip(query.Offset)
            .Take(query.Limit)
            .Select(t => new TransactionResult(
                t.Id.ToString(),
                t.Type,
                t.Amount,
                t.Fee,
                t.Status,
                t.Reference,
                t.Description,
                t.CounterpartyName,
                t.CounterpartyPhone,
                t.BalanceAfter,
                t.Currency,
                t.CreatedAt,
                t.CompletedAt))
            .ToListAsync(cancellationToken);

        return Result.Success(transactions);
    }
}

public sealed record GetTransactionsQuery(
    Guid AccountId,
    DateTime? StartDate,
    DateTime? EndDate,
    string? TypeFilter,
    string? StatusFilter,
    int Offset = 0,
    int Limit = 50);

public sealed record TransactionResult(
    string TransactionId,
    string Type,
    decimal Amount,
    decimal Fee,
    string Status,
    string? Reference,
    string? Description,
    string? CounterpartyName,
    string? CounterpartyPhone,
    decimal BalanceAfter,
    string Currency,
    DateTime CreatedAt,
    DateTime? CompletedAt);
