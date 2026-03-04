using Microsoft.EntityFrameworkCore;
using UniBank.Core.Common.Persistence;
using UniBank.Core.Modules.Loans.Domain.Entities;
using UniBank.SharedKernel.Results;

namespace UniBank.Core.Modules.Loans.Application.Handlers;

/// <summary>
/// Retrieves the amortization schedule for a specific loan.
/// </summary>
public sealed class GetLoanScheduleHandler
{
    private readonly UniBankDbContext _dbContext;

    public GetLoanScheduleHandler(UniBankDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Result<List<LoanPayment>>> HandleAsync(
        Guid loanId, Guid accountId, CancellationToken cancellationToken = default)
    {
        var loanExists = await _dbContext.Loans
            .AnyAsync(
                l => l.Id == loanId && l.AccountId == accountId && l.DeletedAt == null,
                cancellationToken);

        if (!loanExists)
            return Result.Failure<List<LoanPayment>>(new Error("Loan.NotFound", "Loan not found."));

        var payments = await _dbContext.LoanPayments
            .Where(p => p.LoanId == loanId)
            .OrderBy(p => p.PaymentNumber)
            .ToListAsync(cancellationToken);

        return Result.Success(payments);
    }
}
