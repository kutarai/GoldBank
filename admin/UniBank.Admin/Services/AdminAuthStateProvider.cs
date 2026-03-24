using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace UniBank.Admin.Services;

/// <summary>
/// Custom Blazor Server AuthenticationStateProvider for the admin portal.
/// Uses in-memory session state (scoped per SignalR circuit).
/// </summary>
public sealed class AdminAuthStateProvider : AuthenticationStateProvider
{
    private readonly ILogger<AdminAuthStateProvider> _logger;
    private ClaimsPrincipal _currentUser = new(new ClaimsIdentity());

    public AdminAuthStateProvider(ILogger<AdminAuthStateProvider> logger)
    {
        _logger = logger;
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        return Task.FromResult(new AuthenticationState(_currentUser));
    }

    public Task<bool> LoginAsync(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return Task.FromResult(false);

        var record = ResolveCredentials(username, password);
        if (record is null)
        {
            _logger.LogWarning("Failed login attempt for username '{Username}'.", username);
            return Task.FromResult(false);
        }

        _currentUser = BuildPrincipal(record);
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_currentUser)));

        _logger.LogInformation("Admin '{Username}' logged in with role '{Role}'.", username, record.Role);
        return Task.FromResult(true);
    }

    public Task LogoutAsync()
    {
        _currentUser = new ClaimsPrincipal(new ClaimsIdentity());
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_currentUser)));
        return Task.CompletedTask;
    }

    private static ClaimsPrincipal BuildPrincipal(AdminSessionRecord record)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, record.Username),
            new(ClaimTypes.Role, record.Role),
        };

        if (!string.IsNullOrEmpty(record.FullName))
            claims.Add(new(ClaimTypes.GivenName, record.FullName));

        if (!string.IsNullOrEmpty(record.AdminId))
            claims.Add(new("admin_id", record.AdminId));

        if (!string.IsNullOrEmpty(record.BranchId))
            claims.Add(new("branch_id", record.BranchId));

        var identity = new ClaimsIdentity(claims, "AdminPortal");
        return new ClaimsPrincipal(identity);
    }

    /// <summary>
    /// Dev seed accounts. Replace with gRPC AuthenticateAdmin call in production.
    /// </summary>
    private static AdminSessionRecord? ResolveCredentials(string username, string password)
    {
        var seed = new Dictionary<string, (string Password, string Role, string FullName)>(StringComparer.OrdinalIgnoreCase)
        {
            ["admin"]      = ("admin",       AdminRoles.Admin,             "System Administrator"),
            ["kyc"]        = ("kyc",          AdminRoles.KycOfficer,        "KYC Officer"),
            ["fraud"]      = ("fraud",        AdminRoles.FraudAnalyst,      "Fraud Analyst"),
            ["support"]    = ("support",      AdminRoles.CustomerService,   "Customer Service"),
            ["loans"]      = ("loans",        AdminRoles.LoanOfficer,       "Loan Officer"),
            ["compliance"] = ("compliance",   AdminRoles.ComplianceOfficer, "Compliance Officer"),
            ["branch"]     = ("branch",       AdminRoles.BranchManager,     "Branch Manager"),
        };

        if (!seed.TryGetValue(username, out var entry) || entry.Password != password)
            return null;

        return new AdminSessionRecord(
            AdminId:  Guid.NewGuid().ToString("N"),
            Username: username,
            FullName: entry.FullName,
            Role:     entry.Role,
            BranchId: null);
    }
}

public sealed record AdminSessionRecord(
    string AdminId,
    string Username,
    string FullName,
    string Role,
    string? BranchId);
