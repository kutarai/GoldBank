using UniBank.SharedKernel.Domain;

namespace UniBank.Core.Modules.FraudDetection.Domain.Entities;

/// <summary>
/// Defines a configurable fraud detection rule with JSON parameters (STORY-072).
/// Rules can be activated/deactivated and scoped to specific tenants.
/// </summary>
public sealed class FraudRule : AggregateRoot
{
    public string Name { get; set; } = default!;
    public string RuleType { get; set; } = default!;
    public string Parameters { get; set; } = "{}";
    public bool IsActive { get; set; } = true;
    public string TenantId { get; set; } = default!;
}
