using UniBank.SharedKernel.Domain;

namespace UniBank.Core.Modules.BillPay.Domain.Entities;

/// <summary>
/// Represents a user's saved/favourite biller for quick repeat payments (STORY-039).
/// Links an account to a provider with a specific billing reference and optional nickname.
/// </summary>
public sealed class SavedBiller : AggregateRoot
{
    public Guid AccountId { get; set; }
    public Guid ProviderId { get; set; }
    public string BillingReference { get; set; } = default!;
    public string Nickname { get; set; } = default!;
    public DateTime? LastPaidAt { get; set; }
    public string TenantId { get; set; } = default!;
    public DateTime? DeletedAt { get; set; }
}
