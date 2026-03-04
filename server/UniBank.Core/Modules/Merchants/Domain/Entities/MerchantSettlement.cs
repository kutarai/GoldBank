using UniBank.SharedKernel.Domain;

namespace UniBank.Core.Modules.Merchants.Domain.Entities;

/// <summary>
/// Merchant settlement aggregate root (STORY-052).
/// Represents a calculated settlement for a merchant over a specific period,
/// summarizing gross payments, fees retained, and net payout amount.
/// </summary>
public sealed class MerchantSettlement : AggregateRoot
{
    public Guid MerchantId { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public int TotalTransactions { get; set; }
    public decimal GrossAmount { get; set; }
    public decimal TotalFees { get; set; }
    public decimal NetAmount { get; set; }
    public string Currency { get; set; } = "ZWG";
    public string Status { get; set; } = "pending";
    public DateTime? PaidAt { get; set; }
    public string Reference { get; set; } = default!;
    public string TenantId { get; set; } = default!;
    public DateTime? DeletedAt { get; set; }
}
