using GoldBank.SharedKernel.Domain;

namespace GoldBank.Core.Modules.Ekub.Domain.Entities;

/// <summary>
/// Joins one Customer to one EkubGroup with a role. Roles are unique per group:
/// at most one Chairman, one Treasurer, one Secretary; the rest are Members.
/// Soft-delete via LeftAt — historical contributions remain visible after exit.
/// </summary>
public sealed class EkubMembership : AggregateRoot
{
    public Guid GroupId { get; set; }
    public Guid CustomerId { get; set; }
    public EkubMemberRole Role { get; set; } = EkubMemberRole.Member;
    public DateTime JoinedAt { get; set; }
    public DateTime? LeftAt { get; set; }
    public string? ExitReason { get; set; }              // free-text when LeftAt is set
    public string TenantId { get; set; } = default!;

    public EkubGroup? Group { get; set; }
}
