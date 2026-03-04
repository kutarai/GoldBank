using Microsoft.EntityFrameworkCore;
using UniBank.Core.Common.Persistence;
using UniBank.Core.Modules.Loans.Domain.Entities;
using UniBank.SharedKernel.Results;

namespace UniBank.Core.Modules.Loans.Application.Handlers;

/// <summary>
/// Retrieves detailed information about a specific loan.
/// </summary>
public sealed class GetLoanHandler
{
    private readonly UniBankDbContext _dbContext;

    public GetLoanHandler(UniBankDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<Loan>> HandleAsync(
        Guid loanId, Guid accountId, CancellationToken cancellationToken = default)
    {
        var loan = await _dbContext.Loans
            .Include(l => l.Payments.OrderBy(p => p.PaymentNumber))
            .FirstOrDefaultAsync(
                l => l.Id == loanId && l.AccountId == accountId && l.DeletedAt == null,
                cancellationToken);

        if (loan is null)
            return Result.Failure<Loan>(new Error("Loan.NotFound", "Loan not found."));

        return Result.Success(loan);
    }
}
