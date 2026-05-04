using GoldBank.SharedKernel.Domain;

namespace GoldBank.Core.Modules.AssetCustody.Domain.Entities;

/// <summary>
/// Represents a physical asset held in custody at a deposit house on behalf of a customer.
/// Tracks the asset's registration details, verification status, and current valuation.
/// Implements ISoftDeletable so removed assets are retained for audit purposes.
/// </summary>
public sealed class Asset : AggregateRoot, ISoftDeletable
{
    public Guid CustomerId { get; set; }
    public Guid DepositHouseId { get; set; }
    public string ReceiptNumber { get; set; } = default!;
    public AssetType AssetType { get; set; }
    public string Description { get; set; } = default!;
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = default!;
    public decimal? WeightGrams { get; set; }
    public decimal? Purity { get; set; }
    public string ReceiptImagePath { get; set; } = default!;
    public DateTime ReceiptDate { get; set; }
    public decimal LastValuationAmount { get; set; }
    public DateTime? LastValuationDate { get; set; }
    public DateTime? LastVerificationDate { get; set; }
    public VerificationStatus VerificationStatus { get; set; } = VerificationStatus.Pending;
    public AssetStatus Status { get; set; } = AssetStatus.PendingVerification;
    public Guid TenantId { get; set; }

    // ISoftDeletable
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string? DeletedBy { get; set; }

    public DepositHouse? DepositHouse { get; set; }
    public ICollection<AssetValuation> Valuations { get; set; } = [];
}
