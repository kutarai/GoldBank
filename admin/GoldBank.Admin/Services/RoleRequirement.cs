namespace GoldBank.Admin.Services;

/// <summary>
/// Role name constants and composite access-group strings for use with
/// <c>[Authorize(Roles = AdminRoles.KycAccess)]</c> on Blazor pages and
/// with <c>&lt;AuthorizeView Roles="..."&gt;</c> in markup.
/// Composite strings are comma-separated, matching the way ASP.NET Core
/// evaluates role authorization (any of the listed roles grants access).
/// </summary>
public static class AdminRoles
{
    // Individual role names — must match the AdminRole enum values.
    public const string Admin             = "Admin";
    public const string KycOfficer        = "KycOfficer";
    public const string FraudAnalyst      = "FraudAnalyst";
    public const string CustomerService   = "CustomerService";
    public const string LoanOfficer       = "LoanOfficer";
    public const string ComplianceOfficer = "ComplianceOfficer";
    public const string BranchManager     = "BranchManager";

    // Composite access groups — used as [Authorize(Roles = "...")] values.
    public const string KycAccess      = $"{Admin},{KycOfficer},{BranchManager}";
    public const string FraudAccess    = $"{Admin},{FraudAnalyst},{BranchManager}";
    public const string DisputeAccess  = $"{Admin},{CustomerService},{BranchManager}";
    public const string LoanAccess     = $"{Admin},{LoanOfficer},{BranchManager}";
    public const string CustomerAccess = $"{Admin},{CustomerService},{BranchManager}";
    public const string ReportAccess   = $"{Admin},{ComplianceOfficer},{BranchManager}";
    public const string ConfigAccess   = Admin;
    public const string UserManagement = $"{Admin},{BranchManager}";
}
