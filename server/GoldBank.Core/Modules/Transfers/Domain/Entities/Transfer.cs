using GoldBank.SharedKernel.Domain;

namespace GoldBank.Core.Modules.Transfers.Domain.Entities;

/// <summary>
/// Represents a P2P or cross-border money transfer between accounts (STORY-029, STORY-030).
/// Tracks the full lifecycle from initiation through processing to completion or failure.
/// </summary>
public sealed class Transfer : AggregateRoot
{
    public Guid SenderAccountId { get; set; }
    public Guid? RecipientAccountId { get; set; }
    public string RecipientPhone { get; set; } = default!;
    public string? RecipientName { get; set; }
    public string Type { get; set; } = default!; // "domestic" or "cross_border"
    public decimal SendAmount { get; set; }
    public string SendCurrency { get; set; } = "ZWG";
    public decimal ReceiveAmount { get; set; }
    public string ReceiveCurrency { get; set; } = "ZWG";
    public decimal Fee { get; set; }
    public string? ExchangeRate { get; set; }
    public string Status { get; set; } = "pending"; // pending, processing, completed, failed
    public string Reference { get; set; } = default!;
    public string? Description { get; set; }
    public DateTime? EstimatedDelivery { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string TenantId { get; set; } = default!;
    public DateTime? DeletedAt { get; set; }
}
