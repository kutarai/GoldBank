namespace UniBank.TerminalManager.Mqtt;

/// <summary>
/// Validates terminal connections to the MQTT broker.
/// Implementations should verify terminal credentials against the terminal registry,
/// check that the terminal is active/approved, and enforce tenant isolation.
/// </summary>
public interface ITerminalAuthenticator
{
    /// <summary>
    /// Validates connection credentials for a terminal attempting to connect to the MQTT broker.
    /// </summary>
    /// <param name="clientId">The MQTT client ID, typically the terminal's unique identifier.</param>
    /// <param name="username">The username provided by the terminal (e.g., terminal serial number).</param>
    /// <param name="password">The password or authentication token provided by the terminal.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An authentication result containing success/failure and tenant context.</returns>
    Task<TerminalAuthResult> AuthenticateAsync(string clientId, string? username, string? password,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of terminal authentication against the MQTT broker.
/// </summary>
public sealed record TerminalAuthResult
{
    public bool IsAuthenticated { get; init; }
    public string? TenantId { get; init; }
    public string? TerminalId { get; init; }
    public string? AgentId { get; init; }
    public string? RejectReason { get; init; }

    public static TerminalAuthResult Success(string tenantId, string terminalId, string? agentId = null)
        => new()
        {
            IsAuthenticated = true,
            TenantId = tenantId,
            TerminalId = terminalId,
            AgentId = agentId
        };

    public static TerminalAuthResult Failure(string reason)
        => new()
        {
            IsAuthenticated = false,
            RejectReason = reason
        };
}
