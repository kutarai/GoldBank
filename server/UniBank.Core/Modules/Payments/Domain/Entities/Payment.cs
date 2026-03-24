using UniBank.SharedKernel.Domain;

namespace UniBank.Core.Modules.Payments.Domain.Entities;

/// <summary>
/// Represents a payment transaction (NFC or QR) between a payer and a merchant (STORY-023 through STORY-027).
/// Tracks the full lifecycle from initiation through completion or failure.
/// </summary>
public sealed class Payment : AggregateRoot
{
    public Guid PayerAccountId { get; set; }
    public Guid MerchantAccountId { get; set; }
    public decimal Amount { get; set; }
    public decimal Fee { get; set; }
    public decimal Tax { get; set; }
    public decimal MerchantCommission { get; set; }
    public string Currency { get; set; } = "ZWG";
    public string Type { get; set; } = default!;
    public string Status { get; set; } = "pending";
    public string Reference { get; set; } = default!;
    public string? Description { get; set; }
    public string? NfcData { get; set; }
    public string? QrCodeData { get; set; }
    public string? TerminalId { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string TenantId { get; set; } = default!;
    public DateTime? DeletedAt { get; set; }
}
