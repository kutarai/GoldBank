using UniBank.SharedKernel.Domain;

namespace UniBank.Core.Modules.Admin.Domain.Entities;

/// <summary>
/// System configuration entry supporting multi-tenant overrides (STORY-060).
/// TenantId is null for global configuration values.
/// </summary>
public sealed class SystemConfig : AggregateRoot
{
    public string Key { get; set; } = default!;
    public string ValueJson { get; set; } = default!;
    public string? TenantId { get; set; }
    public Guid? UpdatedBy { get; set; }
}
