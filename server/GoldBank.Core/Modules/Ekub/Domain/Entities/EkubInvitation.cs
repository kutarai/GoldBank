using GoldBank.SharedKernel.Domain;

namespace GoldBank.Core.Modules.Ekub.Domain.Entities;

/// <summary>
/// An outstanding (or resolved) invitation to join a group. Sent by the chairman
/// or secretary to a phone number — the invitee may or may not yet be a GoldBank
/// customer at the time of invitation. When they log in we match by phone.
/// </summary>
public sealed class EkubInvitation : AggregateRoot
{
    public Guid GroupId { get; set; }
    public string InviteePhone { get; set; } = default!;
    public Guid? InviteeCustomerId { get; set; }         // resolved on accept (or eagerly if found)
    public Guid InviterCustomerId { get; set; }          // chairman or secretary
    public EkubInvitationStatus Status { get; set; } = EkubInvitationStatus.Pending;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RespondedAt { get; set; }
    public string TenantId { get; set; } = default!;
}
