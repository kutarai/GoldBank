using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using UniBank.Core.Common.Persistence;
using UniBank.Core.Modules.Admin.Domain.Entities;
using UniBank.SharedKernel.Results;

namespace UniBank.Core.Modules.Admin.Application.Handlers;

/// <summary>
/// Authenticates admin users with BCrypt password verification and JWT token generation (STORY-055).
/// </summary>
public sealed class AuthenticateAdminHandler
{
    private readonly UniBankDbContext _dbContext;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthenticateAdminHandler> _logger;

    public AuthenticateAdminHandler(
        UniBankDbContext dbContext,
        IConfiguration configuration,
        ILogger<AuthenticateAdminHandler> logger)
    {
        _dbContext = dbContext;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<Result<AdminAuthResult>> HandleAsync(
        string username, string password, CancellationToken cancellationToken = default)
    {
        var adminUser = await _dbContext.Set<AdminUser>()
            .FirstOrDefaultAsync(u => u.Username == username, cancellationToken);

        if (adminUser is null)
        {
            _logger.LogWarning("Admin login failed: user {Username} not found", username);
            return Result.Failure<AdminAuthResult>(AdminErrors.InvalidCredentials);
        }

        if (!adminUser.IsActive)
        {
            _logger.LogWarning("Admin login failed: user {Username} is inactive", username);
            return Result.Failure<AdminAuthResult>(AdminErrors.AccountInactive);
        }

        if (!BCrypt.Net.BCrypt.Verify(password, adminUser.PasswordHash))
        {
            _logger.LogWarning("Admin login failed: invalid password for {Username}", username);
            return Result.Failure<AdminAuthResult>(AdminErrors.InvalidCredentials);
        }

        adminUser.LastLoginAt = DateTime.UtcNow;
        adminUser.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        var token = GenerateJwtToken(adminUser);

        _logger.LogInformation("Admin user {Username} authenticated successfully", username);

        return Result.Success(new AdminAuthResult(
            Token: token,
            AdminId: adminUser.Id.ToString(),
            FullName: adminUser.FullName,
            Role: adminUser.Role.ToString(),
            ExpiresInSeconds: 3600));
    }

    private string GenerateJwtToken(AdminUser adminUser)
    {
        var secretKey = _configuration["Jwt:SecretKey"] ?? "default-admin-secret-key-replace-in-production";
        var issuer = _configuration["Jwt:Issuer"] ?? "UniBank";
        var audience = _configuration["Jwt:Audience"] ?? "UniBank.Admin";

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, adminUser.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("role", adminUser.Role.ToString()),
            new("username", adminUser.Username),
            new("full_name", adminUser.FullName)
        };

        if (adminUser.TenantId is not null)
        {
            claims.Add(new Claim("tenant_id", adminUser.TenantId));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public sealed record AdminAuthResult(
    string Token,
    string AdminId,
    string FullName,
    string Role,
    int ExpiresInSeconds);

public static class AdminErrors
{
    public static readonly Error InvalidCredentials = new(
        "Admin.InvalidCredentials",
        "Invalid username or password.");

    public static readonly Error AccountInactive = new(
        "Admin.AccountInactive",
        "Admin account is inactive.");
}
