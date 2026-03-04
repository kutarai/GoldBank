using Microsoft.EntityFrameworkCore;
using UniBank.Core.Common.Persistence;
using UniBank.Core.Modules.Loans.Domain.Entities;
using UniBank.SharedKernel.Results;

namespace UniBank.Core.Modules.Loans.Application.Handlers;

/// <summary>
/// Lists loans for a given account with optional status filtering.
/// </summary>
public sealed class ListLoansHandler
{
    private static readonly string[] ActiveStatuses = ["approved", "disbursed", "repaying"];
    private static readonly string[] CompletedStatuses = ["paid_off", "rejected", "defaulted"];

    private readonly UniBankDbContext _dbContext;

    public ListLoansHandler(UniBankDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<List<Loan>>> HandleAsync(
        Guid accountId, string statusFilter, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Loans
            .Where(l => l.AccountId == accountId && l.DeletedAt == null);

        query = statusFilter switch
        {
            "active" => query.Where(l => ActiveStatuses.Contains(l.Status)),
            "completed" => query.Where(l => CompletedStatuses.Contains(l.Status)),
            _ => query,
        };

        var loans = await query
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync(cancellationToken);

        return Result.Success(loans);
    }
}
