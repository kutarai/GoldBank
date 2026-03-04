using UniBank.SharedKernel.Domain;

namespace UniBank.Core.Modules.FraudDetection.Domain.Entities;

/// <summary>
/// Represents a fraud detection alert triggered by rules-based analysis of transaction patterns (STORY-072).
/// Tracks the lifecycle of an alert from creation through admin review to resolution.
/// </summary>
public sealed class FraudAlert : AggregateRoot
{
    public Guid AccountId { get; set; }
    public Guid TransactionId { get; set; }
    public string AlertType { get; set; } = default!;
    public string Severity { get; set; } = "Medium";
    public string Description { get; set; } = default!;
    public string Status { get; set; } = "Open";
    public string? AdminNotes { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewedBy { get; set; }
    public string TenantId { get; set; } = default!;
}
