using UniBank.SharedKernel.Domain;

namespace UniBank.Core.Modules.AI.Domain.Entities;

public sealed class AiInteraction : BaseEntity
{
    public Guid? AccountId { get; set; }
    public string InteractionType { get; set; } = default!;
    public string RequestSummary { get; set; } = default!;
    public string ResponseSummary { get; set; } = default!;
    public string ModelUsed { get; set; } = default!;
    public int InferenceTimeMs { get; set; }
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string TenantId { get; set; } = default!;
}
