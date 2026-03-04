using UniBank.SharedKernel.Domain;

namespace UniBank.Core.Modules.Loans.Domain.Entities;

/// <summary>
/// Represents a personal unsecured loan application and its lifecycle.
/// Tracks principal, outstanding balance, repayment schedule, and credit scoring.
/// </summary>
public sealed class Loan : AggregateRoot
{
    public Guid AccountId { get; set; }
    public decimal Principal { get; set; }
    public decimal OutstandingBalance { get; set; }
    public decimal InterestRate { get; set; }
    public int TenureMonths { get; set; }
    public decimal MonthlyPayment { get; set; }
    public string Purpose { get; set; } = default!;
    public string Status { get; set; } = "pending"; // pending, approved, rejected, disbursed, repaying, paid_off, defaulted
    public int CreditScore { get; set; }
    public int PaymentsMade { get; set; }
    public string Reference { get; set; } = default!;
    public string Currency { get; set; } = "ZWG";
    public string TenantId { get; set; } = default!;
    public DateTime? DisbursedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? DeletedAt { get; set; }

    public ICollection<LoanPayment> Payments { get; set; } = [];
}
