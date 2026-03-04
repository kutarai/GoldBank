using UniBank.SharedKernel.Domain;

namespace UniBank.Core.Modules.Admin.Domain.Entities;

/// <summary>
/// Dispute/chargeback record linked to a transaction (STORY-061).
/// Tracks the lifecycle from creation through investigation to resolution.
/// </summary>
public sealed class Dispute : AggregateRoot
{
    public Guid TransactionId { get; set; }
    public Guid AccountId { get; set; }
    public DisputeType Type { get; set; }
    public string Description { get; set; } = default!;
    public DisputeStatus Status { get; set; } = DisputeStatus.Open;
    public string? Resolution { get; set; }
    public decimal? RefundAmount { get; set; }
    public string RefundCurrency { get; set; } = "ZWG";
    public Guid? AdminUserId { get; set; }
    public DateTime? ResolvedAt { get; set; }
}

public enum DisputeType
{
    Unauthorized = 0,
    Duplicate = 1,
    ServiceNotReceived = 2,
    IncorrectAmount = 3,
    Other = 4
}

public enum DisputeStatus
{
    Open = 0,
    Investigating = 1,
    Resolved = 2,
    Rejected = 3
}
