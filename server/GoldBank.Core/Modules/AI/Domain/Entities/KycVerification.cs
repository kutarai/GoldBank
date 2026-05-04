using GoldBank.SharedKernel.Domain;

namespace GoldBank.Core.Modules.AI.Domain.Entities;

public sealed class KycVerification : BaseEntity
{
    public Guid AccountId { get; set; }
    public string SelfieImagePath { get; set; } = default!;
    public string IdDocumentImagePath { get; set; } = default!;
    public byte[]? SelfieImageData { get; set; }
    public byte[]? IdDocumentImageData { get; set; }
    public double FaceMatchScore { get; set; }
    public string FaceMatchDecision { get; set; } = default!;
    public string? ExtractedFullName { get; set; }
    public string? ExtractedIdNumber { get; set; }
    public DateTime? ExtractedDateOfBirth { get; set; }
    public string? ExtractedNationality { get; set; }
    public string? ExtractedGender { get; set; }
    public bool? NameMatch { get; set; }
    public bool? IdNumberMatch { get; set; }
    public bool? DobMatch { get; set; }
    public string OverallDecision { get; set; } = default!;
    public string? RejectionReason { get; set; }
    public string? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string TenantId { get; set; } = default!;
    public DateTime? DeletedAt { get; set; }
}
