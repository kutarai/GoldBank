using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

namespace UniBank.Admin.Services;

/// <summary>
/// Custom Blazor Server AuthenticationStateProvider for the admin portal.
/// Stores a minimal session record in ProtectedSessionStorage (server-side encrypted cookie).
/// LoginAsync validates credentials and, on success, builds a ClaimsPrincipal from the
/// returned role + username. LogoutAsync clears the session and notifies subscribers.
/// </summary>
public sealed class AdminAuthStateProvider : AuthenticationStateProvider
{
    private const string SessionKey = "admin_session";

    private readonly ProtectedSessionStorage _session;
    private readonly ILogger<AdminAuthStateProvider> _logger;

    // Cached in-memory state for the current circuit to avoid redundant storage reads.
    private ClaimsPrincipal _currentUser = new(new ClaimsIdentity());

    public AdminAuthStateProvider(
        ProtectedSessionStorage session,
        ILogger<AdminAuthStateProvider> logger)
    {
        _session = session;
        _logger = logger;
    }

    // -------------------------------------------------------------------------
    // AuthenticationStateProvider contract
    // -------------------------------------------------------------------------

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        // If we already resolved this circuit's identity, return it directly.
        if (_currentUser.Identity?.IsAuthenticated == true)
            return new AuthenticationState(_currentUser);

        try
        {
            var result = await _session.GetAsync<AdminSessionRecord>(SessionKey);
            if (result.Success && result.Value is { } record && !string.IsNullOrEmpty(record.Username))
            {
                _currentUser = BuildPrincipal(record);
                return new AuthenticationState(_currentUser);
            }
        }
        catch (Exception ex)
        {
            // ProtectedSessionStorage can throw during pre-rendering; treat as anonymous.
            _logger.LogWarning(ex, "Could not read admin session from protected storage.");
        }

        return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
    }

    // -------------------------------------------------------------------------
    // Login / Logout
    // -------------------------------------------------------------------------

    /// <summary>
    /// Validates the supplied credentials and, if successful, persists the session
    /// and notifies the Blazor auth pipeline.
    /// Returns true on success, false on invalid credentials.
    /// </summary>
    public async Task<bool> LoginAsync(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return false;

        // Resolve the role for this user. In a full implementation this calls
        // AdminService.AuthenticateAdmin (gRPC) and receives a JWT; the role is
        // extracted from the token's "role" claim. For now we use a local credential
        // store until the AuthenticateAdmin RPC is added to the admin proto.
        var record = ResolveCredentials(username, password);
        if (record is null)
        {
            _logger.LogWarning("Failed login attempt for username '{Username}'.", username);
            return false;
        }

        await _session.SetAsync(SessionKey, record);
        _currentUser = BuildPrincipal(record);
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_currentUser)));

        _logger.LogInformation("Admin '{Username}' logged in with role '{Role}'.", username, record.Role);
        return true;
    }

    /// <summary>Clears the session and marks the user as anonymous.</summary>
    public async Task LogoutAsync()
    {
        await _session.DeleteAsync(SessionKey);
        _currentUser = new ClaimsPrincipal(new ClaimsIdentity());
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_currentUser)));
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

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
    /// Placeholder credential store — replace this body with a call to
    /// AdminService.AuthenticateAdmin once the RPC is added to admin_service.proto.
    /// </summary>
    private static AdminSessionRecord? ResolveCredentials(string username, string password)
    {
        // Hardcoded seed accounts for development / initial deployment.
        // Production: remove this method and call the gRPC auth endpoint.
        var seed = new Dictionary<string, (string Password, string Role, string FullName)>(StringComparer.OrdinalIgnoreCase)
        {
            ["admin"]      = ("Admin@1234",      AdminRoles.Admin,              "System Administrator"),
            ["kyc"]        = ("Kyc@1234",         AdminRoles.KycOfficer,         "KYC Officer"),
            ["fraud"]      = ("Fraud@1234",       AdminRoles.FraudAnalyst,       "Fraud Analyst"),
            ["support"]    = ("Support@1234",     AdminRoles.CustomerService,    "Customer Service"),
            ["loans"]      = ("Loans@1234",       AdminRoles.LoanOfficer,        "Loan Officer"),
            ["compliance"] = ("Compliance@1234",  AdminRoles.ComplianceOfficer,  "Compliance Officer"),
            ["branch"]     = ("Branch@1234",      AdminRoles.BranchManager,      "Branch Manager"),
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

/// <summary>Minimal session payload stored in ProtectedSessionStorage.</summary>
public sealed record AdminSessionRecord(
    string AdminId,
    string Username,
    string FullName,
    string Role,
    string? BranchId);
