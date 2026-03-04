using UniBank.SharedKernel.Domain;

namespace UniBank.Core.Modules.Loans.Domain.Entities;

/// <summary>
/// Represents a single scheduled payment in a loan's amortization schedule.
/// </summary>
public sealed class LoanPayment : BaseEntity
{
    public Guid LoanId { get; set; }
    public int PaymentNumber { get; set; }
    public decimal PrincipalAmount { get; set; }
    public decimal InterestAmount { get; set; }
    public decimal TotalPayment { get; set; }
    public decimal RemainingBalance { get; set; }
    public DateTime DueDate { get; set; }
    public bool IsPaid { get; set; }

    public Loan Loan { get; set; } = default!;
}
