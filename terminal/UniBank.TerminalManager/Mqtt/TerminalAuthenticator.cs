using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace UniBank.TerminalManager.Mqtt;

/// <summary>
/// Default terminal authenticator that validates MQTT connection credentials.
/// In production, this would query the terminal registry database or call the Core gRPC service
/// to verify the terminal's identity and retrieve its tenant context.
///
/// Current implementation uses a shared secret from configuration for initial development.
/// TODO: Replace with gRPC call to Core terminal registry when STORY-XXX is implemented.
/// </summary>
public sealed class TerminalAuthenticator : ITerminalAuthenticator
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<TerminalAuthenticator> _logger;

    public TerminalAuthenticator(IConfiguration configuration, ILogger<TerminalAuthenticator> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public Task<TerminalAuthResult> AuthenticateAsync(string clientId, string? username, string? password,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Authenticating terminal connection: ClientId={ClientId}, Username={Username}",
            clientId, username);

        // Validate that required fields are present
        if (string.IsNullOrWhiteSpace(clientId))
        {
            _logger.LogWarning("Terminal authentication failed: empty client ID");
            return Task.FromResult(TerminalAuthResult.Failure("Client ID is required"));
        }

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            _logger.LogWarning(
                "Terminal authentication failed: missing credentials for ClientId={ClientId}", clientId);
            return Task.FromResult(TerminalAuthResult.Failure("Username and password are required"));
        }

        // Validate against the configured shared secret (development/staging only)
        var sharedSecret = _configuration["Mqtt:TerminalSecret"] ?? "unibank-terminal-dev-secret";
        if (password != sharedSecret)
        {
            _logger.LogWarning(
                "Terminal authentication failed: invalid credentials for ClientId={ClientId}, Username={Username}",
                clientId, username);
            return Task.FromResult(TerminalAuthResult.Failure("Invalid credentials"));
        }

        // Extract tenant from the client ID convention: {tenantCode}:{terminalId}
        // Example: "mobibank:TRM-001" or "unibank:POS-12345"
        var parts = clientId.Split(':', 2);
        var tenantId = parts.Length == 2 ? parts[0] : "default";
        var terminalId = parts.Length == 2 ? parts[1] : clientId;

        _logger.LogInformation(
            "Terminal authenticated: TerminalId={TerminalId}, TenantId={TenantId}, Username={Username}",
            terminalId, tenantId, username);

        return Task.FromResult(TerminalAuthResult.Success(tenantId, terminalId));
    }
}
