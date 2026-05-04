using GoldBank.SharedKernel.Domain;

namespace GoldBank.Core.Modules.Accounts.Domain.Entities;

/// <summary>
/// Persisted refresh token for token rotation (STORY-018).
/// Stored in database for revocation and family tracking.
/// </summary>
public class RefreshToken : BaseEntity
{
    public Guid AccountId { get; set; }
    public string Token { get; set; } = default!;
    public string DeviceId { get; set; } = default!;
    public string TenantId { get; set; } = default!;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public string? ReplacedByToken { get; set; }

    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    public bool IsRevoked => RevokedAt is not null;
    public bool IsActive => !IsRevoked && !IsExpired;
}
