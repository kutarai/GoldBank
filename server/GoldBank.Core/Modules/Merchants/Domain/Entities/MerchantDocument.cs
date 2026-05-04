using GoldBank.SharedKernel.Domain;

namespace GoldBank.Core.Modules.Merchants.Domain.Entities;

/// <summary>
/// Business registration document for merchant KYC (STORY-050).
/// </summary>
public class MerchantDocument : BaseEntity
{
    public Guid MerchantId { get; set; }
    public string DocumentType { get; set; } = default!;
    public string FilePath { get; set; } = default!;
    public string FileName { get; set; } = default!;
    public string ContentType { get; set; } = default!;
    public long FileSizeBytes { get; set; }
    public string EncryptionKeyRef { get; set; } = default!;
    public string ChecksumSha256 { get; set; } = default!;
    public string Status { get; set; } = "uploaded";
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}
