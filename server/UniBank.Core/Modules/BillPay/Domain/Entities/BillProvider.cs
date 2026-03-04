using UniBank.SharedKernel.Domain;

namespace UniBank.Core.Modules.BillPay.Domain.Entities;

/// <summary>
/// Represents a bill payment provider (e.g., electricity, water, airtime).
/// Providers define the category, validation requirements, and amount constraints
/// for bill payments (STORY-037).
/// </summary>
public sealed class BillProvider : AggregateRoot
{
    public string Name { get; set; } = default!;
    public string Code { get; set; } = default!;
    public string Category { get; set; } = default!; // electricity, water, airtime, internet, insurance
    public bool RequiresMeterNumber { get; set; }
    public bool RequiresAccountNumber { get; set; }
    public decimal MinAmount { get; set; }
    public decimal MaxAmount { get; set; }
    public string Currency { get; set; } = "ZWG";
    public string Status { get; set; } = "active"; // active, inactive
    public string CountryCode { get; set; } = default!;
    public string TenantId { get; set; } = default!;
    public DateTime? DeletedAt { get; set; }
}
