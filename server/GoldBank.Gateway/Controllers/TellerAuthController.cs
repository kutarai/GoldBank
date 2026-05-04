using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BCrypt.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using GoldBank.Core.Common.Persistence;
using GoldBank.Core.Modules.Admin.Domain.Entities;
using GoldBank.Gateway.Configuration;

namespace GoldBank.Gateway.Controllers;

/// <summary>
/// Login endpoint for the bank-teller front-end (EPIC-021).
/// Authenticates an admin_users row by BCrypt verifying their password and
/// returns a JWT carrying their role + tenant + branch claims.
/// Restricted to Teller, BranchManager, or Admin roles.
/// </summary>
[ApiController]
[Route("api/teller/auth")]
[EnableCors("BankClient")]
public class TellerAuthController : ControllerBase
{
    private readonly GoldBankDbContext _db;
    private readonly JwtSettings _jwtSettings;

    public TellerAuthController(GoldBankDbContext db, IOptions<JwtSettings> jwtSettings)
    {
        _db = db;
        _jwtSettings = jwtSettings.Value;
    }

    public sealed record LoginRequest(string Username, string Password);

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest req)
    {
        if (req == null || string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password))
            return BadRequest(new { error = "missing_credentials" });

        var user = await _db.AdminUsers
            .FirstOrDefaultAsync(u => u.Username == req.Username && u.IsActive);
        if (user == null)
            return Unauthorized(new { error = "invalid_credentials" });

        bool ok;
        try
        {
            ok = BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash);
        }
        catch
        {
            // Malformed BCrypt hash (legacy demo placeholders) — treat as invalid
            ok = false;
        }
        if (!ok)
            return Unauthorized(new { error = "invalid_credentials" });

        // Bank-teller is restricted to Teller and BranchManager roles only.
        // Admin and other privileged roles are explicitly blocked — separation of
        // duties: high-privilege accounts must not perform counter work, and the
        // audit trail must show the actual teller, not a generic super-admin.
        if (user.Role != AdminRole.Teller && user.Role != AdminRole.BranchManager)
        {
            return StatusCode(403, new { error = "role_not_allowed", role = user.Role.ToString() });
        }

        // Generate JWT
        var roleString = user.Role.ToString();
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new("tenant_id", user.TenantId ?? "goldbank"),
            new("role", roleString),
            new(ClaimTypes.Role, roleString),
            new("username", user.Username),
        };
        if (user.BranchId.HasValue)
            claims.Add(new Claim("branch_id", user.BranchId.Value.ToString()));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.SecretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: creds);
        var jwt = new JwtSecurityTokenHandler().WriteToken(token);

        // Update last login
        user.LastLoginAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        // Resolve branch name for the teller header
        string? branchName = null;
        if (user.BranchId.HasValue)
        {
            branchName = await _db.Branches
                .Where(b => b.Id == user.BranchId.Value)
                .Select(b => b.Name)
                .FirstOrDefaultAsync();
        }

        return Ok(new
        {
            accessToken = jwt,
            user = new
            {
                id         = user.Id,
                username   = user.Username,
                fullName   = user.FullName,
                email      = user.Email,
                role       = roleString,
                branchId   = user.BranchId,
                branchName = branchName,
                tenantId   = user.TenantId,
            }
        });
    }
}
