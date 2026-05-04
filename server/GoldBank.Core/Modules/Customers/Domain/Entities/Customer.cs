using GoldBank.SharedKernel.Domain;

namespace GoldBank.Core.Modules.Customers.Domain.Entities;

/// <summary>
/// Customer aggregate — represents a real person who owns one or more accounts (typically
/// one per currency) and may also own assets held in custody. Introduced so that assets
/// (and any future "person-scoped" data) can be attached to the human, not to a specific
/// currency-bound account.
/// </summary>
public class Customer : AggregateRoot
{
    public string PhoneNumber { get; set; } = default!;
    public string PhoneCountryCode { get; set; } = default!;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Email { get; set; }
    public string? DateOfBirth { get; set; }
    public string? NationalId { get; set; }
    public string TenantId { get; set; } = default!;
    public string Status { get; set; } = "active";
    public DateTime? DeletedAt { get; set; }
}
