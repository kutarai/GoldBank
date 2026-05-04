using GoldBank.SharedKernel.Domain;

namespace GoldBank.Core.Modules.Payments.Domain.Entities;

/// <summary>
/// Represents a tokenized card for NFC contactless payments (STORY-022).
/// Stores the mapping between a payment token and the original card PAN (last 4 digits only).
/// The full PAN is never stored; only a format-preserving token is retained.
/// </summary>
public sealed class PaymentToken : AggregateRoot
{
    public Guid AccountId { get; set; }
    public string Token { get; set; } = default!;
    public string TokenReference { get; set; } = default!;
    public string CardPanLast4 { get; set; } = default!;
    public string DeviceId { get; set; } = default!;
    public string Status { get; set; } = "active";
    public DateTime ExpiresAt { get; set; }
    public string TenantId { get; set; } = default!;
    public DateTime? DeletedAt { get; set; }
}
