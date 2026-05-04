using GoldBank.SharedKernel.Domain;

namespace GoldBank.Core.Modules.Merchants.Domain.Entities;

/// <summary>
/// Merchant aggregate root (STORY-050).
/// Represents a registered business that can accept payments and optionally act as an agent.
/// </summary>
public class Merchant : AggregateRoot
{
    public string MerchantCode { get; set; } = default!;
    public Guid OwnerAccountId { get; set; }
    public string BusinessName { get; set; } = default!;
    public string BusinessType { get; set; } = default!;
    public string? RegistrationNumber { get; set; }
    public string? TaxId { get; set; }
    public string? CategoryCode { get; set; }
    public string BusinessAddress { get; set; } = default!;
    public decimal? GpsLatitude { get; set; }
    public decimal? GpsLongitude { get; set; }
    public decimal? GpsAccuracyMeters { get; set; }
    public bool IsAgent { get; set; }
    public bool AgentTermsAccepted { get; set; }
    public DateTime? AgentTermsAcceptedAt { get; set; }
    public string Status { get; set; } = "pending_kyc";
    public string KycStatus { get; set; } = "pending";
    public string TenantId { get; set; } = default!;
    public DateTime? ActivatedAt { get; set; }
}
