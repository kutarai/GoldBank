using UniBank.SharedKernel.Domain;

namespace UniBank.Core.Modules.BranchCash.Domain.Entities;

/// <summary>
/// Authoritative registry of legal-tender denominations per currency (STORY-163).
/// Replaces the hardcoded map used by DenominationValidationService in Sprint 25.
/// </summary>
public sealed class CurrencyDenomination : AggregateRoot
{
    public string TenantId { get; set; } = "unibank";
    public string Currency { get; set; } = "USD";
    public decimal FaceValue { get; set; }
    public string DenominationType { get; set; } = "Note"; // "Note" or "Coin"
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
}
