using UniBank.SharedKernel.Domain;

namespace UniBank.Core.Modules.Accounts.Domain.Entities;

/// <summary>
/// Core Account aggregate root representing a user's bank account.
/// The account is created during registration (STORY-009) and secured with a PIN (STORY-010).
/// </summary>
public class Account : AggregateRoot
{
    public string PhoneNumber { get; set; } = default!;
    public string PhoneCountryCode { get; set; } = default!;
    public string? PinHash { get; set; }
    public string TenantId { get; set; } = default!;
    public string? DeviceId { get; set; }
    public string Status { get; set; } = "pending_kyc";
    public int KycLevel { get; set; }
    public decimal DailyLimit { get; set; } = 1000.00m;
    public decimal MonthlyLimit { get; set; } = 5000.00m;
    public decimal Balance { get; set; }
    public decimal AvailableBalance { get; set; }
    public string Currency { get; set; } = "ZWG";
    public string? CardPan { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Email { get; set; }
    public string? DateOfBirth { get; set; }
    public string? NationalId { get; set; }
    public DateTime? DeletedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }

    /// <summary>
    /// Customer's handwritten signature image (raw bytes, e.g. JPEG/PNG).
    /// Captured during onboarding for cheque/withdrawal authorisation.
    /// </summary>
    public byte[]? SignatureImage { get; set; }

    /// <summary>
    /// Username (or short admin id) of the staff member who verified the signature.
    /// </summary>
    public string? SignatureVerifiedBy { get; set; }

    /// <summary>
    /// UTC timestamp at which the signature was verified.
    /// </summary>
    public DateTime? SignatureVerifiedAt { get; set; }

    /// <summary>
    /// Returns true if a PIN has already been set for this account.
    /// </summary>
    public bool HasPinSet => !string.IsNullOrEmpty(PinHash);
}
