namespace UniBank.Core.Modules.Accounts.Infrastructure.Services;

/// <summary>
/// JWT configuration bound from appsettings.json "Jwt" section.
/// Used by <see cref="JwtTokenService"/> to generate access and refresh tokens.
/// </summary>
public sealed class JwtSettings
{
    public const string SectionName = "Jwt";

    /// <summary>Token issuer (iss claim).</summary>
    public required string Issuer { get; init; }

    /// <summary>Expected audience (aud claim).</summary>
    public required string Audience { get; init; }

    /// <summary>
    /// HMAC-SHA256 secret key used for token signing.
    /// In production, load from a vault or environment variable.
    /// Must be at least 32 bytes (256 bits) for HS256.
    /// </summary>
    public required string SecretKey { get; init; }

    /// <summary>Access token lifetime in minutes. Default 60.</summary>
    public int AccessTokenExpiryMinutes { get; init; } = 60;

    /// <summary>Refresh token lifetime in days. Default 30.</summary>
    public int RefreshTokenExpiryDays { get; init; } = 30;

    /// <summary>Temporary token lifetime in minutes for registration flow (pin_creation scope). Default 30.</summary>
    public int TemporaryTokenExpiryMinutes { get; init; } = 30;
}
