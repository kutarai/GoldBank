using UniBank.SharedKernel.Domain;

namespace UniBank.Core.Modules.KYC.Domain.Entities;

/// <summary>
/// Represents a KYC document uploaded by a user (STORY-011).
/// Documents are encrypted at rest using AES-256-GCM.
/// </summary>
public class KycDocument : AggregateRoot
{
    public Guid AccountId { get; set; }
    public string DocumentType { get; set; } = default!;
    public string FileName { get; set; } = default!;
    public string ContentType { get; set; } = default!;
    public long FileSizeBytes { get; set; }
    public string FilePath { get; set; } = default!;
    public string EncryptionKeyRef { get; set; } = default!;
    public string ChecksumSha256 { get; set; } = default!;
    public string Status { get; set; } = "uploaded";
    public string TenantId { get; set; } = default!;
    public DateTime? VerifiedAt { get; set; }

    /// <summary>
    /// Raw uploaded image bytes (unencrypted), stored for direct admin display.
    /// </summary>
    public byte[]? FileData { get; set; }
}
