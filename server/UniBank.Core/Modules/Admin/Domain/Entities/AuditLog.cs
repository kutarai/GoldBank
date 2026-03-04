using UniBank.SharedKernel.Domain;

namespace UniBank.Core.Modules.Admin.Domain.Entities;

/// <summary>
/// Immutable audit trail entry for admin actions (STORY-055).
/// Records who did what, to which entity, and from where.
/// </summary>
public sealed class AuditLog : AggregateRoot
{
    public Guid AdminUserId { get; set; }
    public string Action { get; set; } = default!;
    public string EntityType { get; set; } = default!;
    public string EntityId { get; set; } = default!;
    public string? Details { get; set; }
    public string? IpAddress { get; set; }
}
