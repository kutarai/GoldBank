using UniBank.SharedKernel.Domain;

namespace UniBank.Core.Modules.AssetCustody.Domain.Entities;

/// <summary>
/// Represents a trusted safe deposit facility that physically holds customer assets in custody.
/// Managed by bank admins; tracks trust status and optionally an API endpoint for automated verification.
/// </summary>
public sealed class DepositHouse : AggregateRoot
{
    public string Name { get; set; } = default!;
    public string Address { get; set; } = default!;
    public string City { get; set; } = default!;
    public string ContactPhone { get; set; } = default!;
    public string ContactEmail { get; set; } = default!;
    public string LicenseNumber { get; set; } = default!;
    public string? ApiEndpoint { get; set; }
    public TrustStatus TrustStatus { get; set; } = TrustStatus.Probationary;
    public bool IsActive { get; set; } = true;
    public Guid TenantId { get; set; }

    public ICollection<Asset> Assets { get; set; } = [];
}
