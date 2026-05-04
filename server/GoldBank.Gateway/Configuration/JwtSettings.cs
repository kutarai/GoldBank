namespace GoldBank.Gateway.Configuration;

/// <summary>
/// JWT authentication configuration bound from appsettings.json "Jwt" section.
/// </summary>
public sealed class JwtSettings
{
    public const string SectionName = "Jwt";

    /// <summary>Token issuer (iss claim).</summary>
    public required string Issuer { get; init; }

    /// <summary>Expected audience (aud claim).</summary>
    public required string Audience { get; init; }

    /// <summary>
    /// HMAC-SHA256 secret key used for token validation.
    /// In production, load from a vault or environment variable.
    /// </summary>
    public required string SecretKey { get; init; }

    /// <summary>Token lifetime in minutes. Default 60.</summary>
    public int TokenLifetimeMinutes { get; init; } = 60;

    /// <summary>Clock skew tolerance in seconds. Default 30.</summary>
    public int ClockSkewSeconds { get; init; } = 30;

    /// <summary>
    /// gRPC method full names that bypass authentication entirely.
    /// Example: "/goldbank.v1.accounts.AccountService/Register"
    /// </summary>
    public List<string> AnonymousMethods { get; init; } = [];
}
