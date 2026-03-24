using UniBank.SharedKernel.Domain;

namespace UniBank.Core.Modules.AssetCustody.Domain.Entities;

/// <summary>
/// Stores the daily spot price for a precious metal (gold, silver, platinum).
/// Global — no TenantId. Populated by the price feed background service or entered manually by an admin.
/// </summary>
public sealed class DailyPrice : AggregateRoot
{
    /// <summary>"gold", "silver", or "platinum"</summary>
    public string AssetType { get; set; } = default!;
    public decimal PricePerGramUsd { get; set; }
    public decimal PricePerOzUsd { get; set; }
    /// <summary>"api" or "manual"</summary>
    public string Source { get; set; } = default!;
    public DateOnly Date { get; set; }
}
