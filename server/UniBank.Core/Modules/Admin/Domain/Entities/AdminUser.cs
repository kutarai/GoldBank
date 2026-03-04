using UniBank.SharedKernel.Domain;

namespace UniBank.Core.Modules.Admin.Domain.Entities;

/// <summary>
/// Admin portal user with role-based access control (STORY-055).
/// Supports multi-tenant isolation: TenantId is null for super admins.
/// </summary>
public sealed class AdminUser : AggregateRoot
{
    public string Username { get; set; } = default!;
    public string PasswordHash { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string FullName { get; set; } = default!;
    public AdminRole Role { get; set; }
    public string? TenantId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastLoginAt { get; set; }
}

public enum AdminRole
{
    SuperAdmin = 0,
    Operations = 1,
    Support = 2,
    Finance = 3,
    Compliance = 4,
    TenantAdmin = 5
}
