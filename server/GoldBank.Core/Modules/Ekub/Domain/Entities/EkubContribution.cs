using GoldBank.SharedKernel.Domain;

namespace GoldBank.Core.Modules.Ekub.Domain.Entities;

/// <summary>
/// One cash contribution from a member into the group pot. Created by the
/// member (or a teller acting on their behalf) in Pending state and confirmed
/// by the treasurer; only Confirmed contributions count toward the pot and the
/// member's "my share" calculation.
///
/// Period (e.g. "2026-05") is the contribution's accounting month so we can
/// surface "members behind on this month's payment" later without scanning by
/// timestamp.
/// </summary>
public sealed class EkubContribution : AggregateRoot
{
    public Guid GroupId { get; set; }
    public Guid CustomerId { get; set; }                 // contributor
    public Guid MembershipId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = default!;
    public string Period { get; set; } = default!;       // "YYYY-MM" — the month this counts towards
    public EkubContributionStatus Status { get; set; } = EkubContributionStatus.Pending;
    public Guid? ConfirmedByCustomerId { get; set; }     // treasurer who confirmed/rejected
    public DateTime? ConfirmedAt { get; set; }
    public string? Notes { get; set; }
    public string TenantId { get; set; } = default!;
}
