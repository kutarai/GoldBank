using GoldBank.SharedKernel.Domain;

namespace GoldBank.Core.Modules.WhiteLabel.Domain.Entities;

/// <summary>
/// Per-tenant fee configuration aggregate root (STORY-070).
/// Defines fee rules for specific transaction types with support for
/// fixed, percentage, and tiered fee models.
/// </summary>
public sealed class TenantFeeConfig : AggregateRoot
{
    public string TenantId { get; set; } = default!;
    public string TransactionType { get; set; } = default!;
    public string FeeType { get; set; } = "fixed";
    public decimal Amount { get; set; }
    public decimal Percentage { get; set; }
    public decimal MinFee { get; set; }
    public decimal MaxFee { get; set; }
    public string Currency { get; set; } = "ZWG";
}
