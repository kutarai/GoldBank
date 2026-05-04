using GoldBank.SharedKernel.Domain;

namespace GoldBank.Core.Modules.Ekub.Domain.Entities;

/// <summary>
/// A loan taken by a single member against the group's pot. Created in
/// <see cref="EkubLoanStatus.Voting"/> when the borrower applies. Members vote
/// (excluding the borrower); when more than 50% approve, the loan moves to
/// <see cref="EkubLoanStatus.AwaitingTreasurer"/>. The treasurer's confirmation
/// disburses it (<see cref="EkubLoanStatus.Disbursed"/>). Repayments reduce
/// <see cref="OutstandingBalance"/>; once it hits zero the loan is Closed.
///
/// Interest model: simple interest, fixed for the life of the loan.
///   total_repayable     = principal × (1 + rate × term_months / 12)
///   installment_amount  = total_repayable / term_months
///   total_interest      = total_repayable − principal
/// </summary>
public sealed class EkubLoan : AggregateRoot
{
    public Guid GroupId { get; set; }
    public Guid BorrowerCustomerId { get; set; }
    public decimal Principal { get; set; }
    public decimal InterestRatePercent { get; set; }     // copied from group at apply time
    public int TermMonths { get; set; }
    public decimal TotalRepayable { get; set; }          // principal + interest
    public decimal InstallmentAmount { get; set; }
    public decimal OutstandingBalance { get; set; }      // monotonically decreases as repayments come in
    public decimal TotalInterestEarned { get; set; }     // interest portion that has been received and added to the pot
    public string Currency { get; set; } = default!;
    public EkubLoanStatus Status { get; set; } = EkubLoanStatus.Voting;
    public string? Purpose { get; set; }
    public Guid? TreasurerCustomerId { get; set; }       // treasurer who confirmed/rejected
    public DateTime? DisbursedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public string? Notes { get; set; }
    public string TenantId { get; set; } = default!;

    public ICollection<EkubLoanVote> Votes { get; set; } = [];
    public ICollection<EkubLoanRepayment> Repayments { get; set; } = [];
}

/// <summary>
/// One member's vote on a single loan. The borrower never has a vote row.
/// Treasurer votes are recorded as a normal member vote but the disbursement
/// gate is the separate <see cref="EkubLoanStatus.AwaitingTreasurer"/> step.
/// </summary>
public sealed class EkubLoanVote : AggregateRoot
{
    public Guid LoanId { get; set; }
    public Guid VoterCustomerId { get; set; }
    public bool Approve { get; set; }
    public string TenantId { get; set; } = default!;

    public EkubLoan? Loan { get; set; }
}

/// <summary>
/// A repayment installment recorded by the treasurer. Splits into principal
/// and interest for the pot ledger; interest is distributed pro-rata to
/// members based on their confirmed contributions at the time of repayment
/// (computed lazily by <c>GetMyShare</c>).
/// </summary>
public sealed class EkubLoanRepayment : AggregateRoot
{
    public Guid LoanId { get; set; }
    public Guid GroupId { get; set; }
    public Guid TreasurerCustomerId { get; set; }
    public decimal AmountPaid { get; set; }
    public decimal PrincipalPortion { get; set; }
    public decimal InterestPortion { get; set; }
    public string Currency { get; set; } = default!;
    public string TenantId { get; set; } = default!;

    public EkubLoan? Loan { get; set; }
}
