using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using UniBank.Core.Modules.Accounts.Domain.Entities;
using UniBank.SharedKernel.Caching;

namespace UniBank.Core.Modules.Accounts.Infrastructure.Services;

/// <summary>
/// Generates JWT access tokens and opaque refresh tokens for authenticated accounts.
/// Access tokens carry claims needed by the API Gateway and downstream services.
/// Refresh tokens are stored via ICacheStore with a configurable TTL.
/// </summary>
public sealed class JwtTokenService
{
    private readonly JwtSettings _settings;
    private readonly ICacheStore _cache;

    public JwtTokenService(IOptions<JwtSettings> settings, ICacheStore cache)
    {
        _settings = settings.Value;
        _cache = cache;
    }

    /// <summary>
    /// Generates a temporary JWT token with limited scope (pin_creation) after successful OTP verification.
    /// Valid for 30 minutes. Used in the registration flow before PIN is set (STORY-009 -> STORY-010).
    /// </summary>
    public string GenerateTemporaryToken(Account account)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, account.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("tenant_id", account.TenantId),
            new("scope", "pin_creation"),
            new("phone", account.PhoneNumber),
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.SecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_settings.TemporaryTokenExpiryMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Generates a full JWT access token and a refresh token for the given account.
    /// </summary>
    public async Task<(string AccessToken, string RefreshToken)> GenerateTokenPairAsync(Account account)
    {
        var accessToken = GenerateAccessToken(account);
        var refreshToken = await GenerateAndStoreRefreshTokenAsync(account);

        return (accessToken, refreshToken);
    }

    private string GenerateAccessToken(Account account)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, account.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("tenant_id", account.TenantId),
            new("role", "customer"),
            new("kyc_level", account.KycLevel.ToString())
        };

        if (!string.IsNullOrEmpty(account.DeviceId))
        {
            claims.Add(new Claim("device_id", account.DeviceId));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.SecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_settings.AccessTokenExpiryMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private async Task<string> GenerateAndStoreRefreshTokenAsync(Account account)
    {
        var tokenBytes = RandomNumberGenerator.GetBytes(32);
        var refreshToken = $"rt_{Convert.ToBase64String(tokenBytes)}";

        var cacheKey = $"refresh_token:{account.Id}:{refreshToken}";
        var expiry = TimeSpan.FromDays(_settings.RefreshTokenExpiryDays);

        await _cache.SetAsync(cacheKey, account.Id.ToString(), expiry);

        return refreshToken;
    }
}
