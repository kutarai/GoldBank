namespace UniBank.Gateway.Configuration;

/// <summary>
/// Rate limiting configuration bound from appsettings.json "RateLimit" section.
/// Uses Redis sliding-window counters.
/// </summary>
public sealed class RateLimitSettings
{
    public const string SectionName = "RateLimit";

    /// <summary>Whether rate limiting is enabled. Default true.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>Maximum requests per user per window. Default 100.</summary>
    public int UserMaxRequests { get; init; } = 100;

    /// <summary>User rate limit window in seconds. Default 60.</summary>
    public int UserWindowSeconds { get; init; } = 60;

    /// <summary>Maximum requests per tenant per window. Default 10000.</summary>
    public int TenantMaxRequests { get; init; } = 10_000;

    /// <summary>Tenant rate limit window in seconds. Default 60.</summary>
    public int TenantWindowSeconds { get; init; } = 60;

    /// <summary>
    /// gRPC methods exempt from rate limiting (e.g. health checks).
    /// </summary>
    public List<string> ExemptMethods { get; init; } = [];
}
