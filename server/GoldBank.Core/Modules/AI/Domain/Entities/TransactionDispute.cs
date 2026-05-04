using GoldBank.SharedKernel.Domain;

namespace GoldBank.Core.Modules.AI.Domain.Entities;

public sealed class TransactionDispute : BaseEntity
{
    public Guid AccountId { get; set; }
    public Guid TransactionId { get; set; }
    public string UserDescription { get; set; } = default!;
    public string? EvidenceImagePath { get; set; }
    public string DisputeType { get; set; } = default!;
    public string Priority { get; set; } = default!;
    public string AiSummary { get; set; } = default!;
    public string AiRecommendedAction { get; set; } = default!;
    public double ClassificationConfidence { get; set; }
    public string Status { get; set; } = "open";
    public string AssignedTeam { get; set; } = default!;
    public string Reference { get; set; } = default!;
    public string? ResolutionNotes { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string TenantId { get; set; } = default!;
    public DateTime? DeletedAt { get; set; }
}
