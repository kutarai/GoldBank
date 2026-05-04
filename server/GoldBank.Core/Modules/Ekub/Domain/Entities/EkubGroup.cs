using GoldBank.SharedKernel.Domain;

namespace GoldBank.Core.Modules.Ekub.Domain.Entities;

/// <summary>
/// Ekub group aggregate root. An Ekub is a group savings + lending product where
/// members contribute a fixed monthly amount, the pot grows via loan interest, and
/// any member may borrow against the pot subject to a vote.
///
/// Lifecycle:
///   Forming → (3+ accepted members & roles assigned) → Active → ... → Closed
/// </summary>
public sealed class EkubGroup : AggregateRoot
{
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public string Currency { get; set; } = "ZWG";        // ZWG or USD; fixed at creation
    public decimal MonthlyContribution { get; set; }     // amount each member commits per month
    public decimal LoanInterestRatePercent { get; set; } // annual %; set by chairman, used in v2

    /// <summary>
    /// When true (default), loan interest is computed on the full principal.
    /// When false, members aren't charged interest on the portion of any loan
    /// that is &lt;= their own confirmed contributions; only the principal
    /// borrowed *above* their contributions accrues interest. The borrower is
    /// effectively borrowing their own money interest-free up to that limit.
    /// </summary>
    public bool ApplyInterestOnContributions { get; set; } = true;

    public EkubGroupStatus Status { get; set; } = EkubGroupStatus.Forming;
    public Guid ChairmanCustomerId { get; set; }         // creator → chairman by default
    public string TenantId { get; set; } = default!;
    public DateTime? ActivatedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public DateTime? LastFeeAppliedAt { get; set; }      // when the last monthly bank fee debit ran

    public ICollection<EkubMembership> Memberships { get; set; } = [];
}
