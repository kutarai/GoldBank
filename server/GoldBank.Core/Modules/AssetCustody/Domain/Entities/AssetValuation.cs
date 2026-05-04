using GoldBank.SharedKernel.Domain;

namespace GoldBank.Core.Modules.AssetCustody.Domain.Entities;

/// <summary>
/// Records a single point-in-time valuation of an asset by a qualified valuer.
/// Immutable audit record — no soft delete. CreatedAt is the valuation timestamp.
/// </summary>
public sealed class AssetValuation : AggregateRoot
{
    public Guid AssetId { get; set; }
    public decimal ValuationAmount { get; set; }
    public string Currency { get; set; } = default!;
    public string ValuerName { get; set; } = default!;
    public string ValuerLicense { get; set; } = default!;
    public string? ReportImagePath { get; set; }
    public string Notes { get; set; } = default!;
    public Guid TenantId { get; set; }

    public Asset? Asset { get; set; }
}
