using Microsoft.EntityFrameworkCore;
using GoldBank.Core.Common.Persistence;
using GoldBank.Core.Modules.Loans.Domain.Entities;
using GoldBank.SharedKernel.Results;

namespace GoldBank.Core.Modules.Loans.Application.Handlers;

/// <summary>
/// Retrieves detailed information about a specific loan.
/// </summary>
public sealed class GetLoanHandler
{
    private readonly GoldBankDbContext _dbContext;

    public GetLoanHandler(GoldBankDbContext dbContext)
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
