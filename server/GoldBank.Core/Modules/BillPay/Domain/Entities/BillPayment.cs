using GoldBank.SharedKernel.Domain;

namespace GoldBank.Core.Modules.BillPay.Domain.Entities;

/// <summary>
/// Represents a bill payment transaction (STORY-038).
/// Tracks the full lifecycle from pending through processing to completed/failed.
/// For prepaid utilities (electricity, airtime), a token is generated upon completion.
/// </summary>
public sealed class BillPayment : AggregateRoot
{
    public Guid AccountId { get; set; }
    public Guid ProviderId { get; set; }
    public string BillingReference { get; set; } = default!;
    public decimal Amount { get; set; }
    public decimal Fee { get; set; }
    public string Currency { get; set; } = "ZWG";
    public string Status { get; set; } = "pending"; // pending, processing, completed, failed
    public string Reference { get; set; } = default!;
    public string? Token { get; set; } // For prepaid utilities like electricity and airtime
    public DateTime? CompletedAt { get; set; }
    public string TenantId { get; set; } = default!;
    public DateTime? DeletedAt { get; set; }
}
