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
    public Guid? BranchId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastLoginAt { get; set; }
}

public enum AdminRole
{
    Admin = 0,              // Full access (replaces SuperAdmin)
    KycOfficer = 1,         // KYC module
    FraudAnalyst = 2,       // Fraud module
    CustomerService = 3,    // Disputes + accounts (replaces Support)
    LoanOfficer = 4,        // Loans module (new)
    ComplianceOfficer = 5,  // Read-only all + reports (replaces Compliance)
    BranchManager = 6,      // Branch-scoped all (replaces TenantAdmin)
}
