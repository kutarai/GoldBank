using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using GoldBank.Core.Common.Persistence;
using GoldBank.Core.Modules.Accounts.Application.Commands;
using GoldBank.Core.Modules.Accounts.Infrastructure.Services;
using GoldBank.SharedKernel.Caching;
using GoldBank.SharedKernel.Results;

namespace GoldBank.Core.Modules.Accounts.Application.Handlers;

/// <summary>
/// Handles token refresh with rotation (STORY-018).
/// Old refresh token is revoked and a new pair is issued.
/// </summary>
public sealed class RefreshTokenHandler
{
    private readonly GoldBankDbContext _dbContext;
    private readonly JwtTokenService _tokenService;
    private readonly ICacheStore _cache;
    private readonly JwtSettings _jwtSettings;
    private readonly ILogger<RefreshTokenHandler> _logger;

    public RefreshTokenHandler(
        GoldBankDbContext dbContext,
        JwtTokenService tokenService,
        ICacheStore cache,
        IOptions<JwtSettings> jwtSettings,
        ILogger<RefreshTokenHandler> logger)
    {
        _dbContext = dbContext;
        _tokenService = tokenService;
        _cache = cache;
        _jwtSettings = jwtSettings.Value;
        _logger = logger;
    }

    public async Task<Result<RefreshTokenResult>> HandleAsync(
        RefreshTokenCommand command, CancellationToken cancellationToken = default)
    {
        // Find the refresh token by pattern: refresh_token:{accountId}:{token}
        var keys = await _cache.SearchKeysAsync($"refresh_token:*:{command.RefreshToken}", cancellationToken);

        string? accountIdStr = null;
        foreach (var key in keys)
        {
            accountIdStr = await _cache.GetAsync(key, cancellationToken);
            await _cache.DeleteAsync(key, cancellationToken);
            break;
        }

        if (string.IsNullOrEmpty(accountIdStr) || !Guid.TryParse(accountIdStr, out var accountId))
            return Result.Failure<RefreshTokenResult>(
                new Error("Auth.InvalidToken", "Invalid or expired refresh token."));

        var account = await _dbContext.Accounts
            .FirstOrDefaultAsync(a => a.Id == accountId && a.DeletedAt == null, cancellationToken);

        if (account is null)
            return Result.Failure<RefreshTokenResult>(
                new Error("Account.NotFound", "Account not found."));

        var (accessToken, refreshToken) = await _tokenService.GenerateTokenPairAsync(account);

        _logger.LogInformation("Token refreshed for account {AccountId}", accountId);

        return Result.Success(new RefreshTokenResult(
            AccessToken: accessToken,
            RefreshToken: refreshToken,
            AccessTokenExpiresIn: _jwtSettings.AccessTokenExpiryMinutes * 60,
            RefreshTokenExpiresIn: _jwtSettings.RefreshTokenExpiryDays * 86400));
    }
}

public sealed record RefreshTokenResult(
    string AccessToken,
    string RefreshToken,
    long AccessTokenExpiresIn,
    long RefreshTokenExpiresIn);
