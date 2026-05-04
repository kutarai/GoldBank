using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using GoldBank.Core.Common.Persistence;
using GoldBank.Core.Modules.Accounts.Application.Commands;
using GoldBank.Core.Modules.Accounts.Infrastructure.Services;
using GoldBank.SharedKernel.Events;
using GoldBank.SharedKernel.Messaging;
using GoldBank.SharedKernel.Results;

namespace GoldBank.Core.Modules.Accounts.Application.Handlers;

/// <summary>
/// Handles PIN-based authentication (STORY-018).
/// Verifies phone + PIN + device, issues JWT tokens, tracks failed attempts.
/// </summary>
public sealed class AuthenticateHandler
{
    private readonly GoldBankDbContext _dbContext;
    private readonly PinHashingService _pinHasher;
    private readonly JwtTokenService _tokenService;
    private readonly LockoutService _lockoutService;
    private readonly IMessageBus _messageBus;
    private readonly JwtSettings _jwtSettings;
    private readonly ILogger<AuthenticateHandler> _logger;

    public AuthenticateHandler(
        GoldBankDbContext dbContext,
        PinHashingService pinHasher,
        JwtTokenService tokenService,
        LockoutService lockoutService,
        IMessageBus messageBus,
        IOptions<JwtSettings> jwtSettings,
        ILogger<AuthenticateHandler> logger)
    {
        _dbContext = dbContext;
        _pinHasher = pinHasher;
        _tokenService = tokenService;
        _lockoutService = lockoutService;
        _messageBus = messageBus;
        _jwtSettings = jwtSettings.Value;
        _logger = logger;
    }

    public async Task<Result<AuthenticateResult>> HandleAsync(
        AuthenticateCommand command, CancellationToken cancellationToken = default)
    {
        // Find account by phone
        var account = await _dbContext.Accounts
            .FirstOrDefaultAsync(
                a => a.PhoneNumber == command.PhoneNumber && a.DeletedAt == null,
                cancellationToken);

        if (account is null)
            return Result.Failure<AuthenticateResult>(
                new Error("Auth.InvalidCredentials", "Invalid phone number or PIN."));

        // Check lockout
        var (isLocked, remaining, lockoutSeconds) = await _lockoutService.CheckLockoutAsync(account.Id);
        if (isLocked)
            return Result.Failure<AuthenticateResult>(
                new Error("Auth.Locked", $"Account is locked. Try again in {lockoutSeconds} seconds."));

        // Verify PIN
        if (!account.HasPinSet || !_pinHasher.VerifyPin(command.Pin, account.PinHash!))
        {
            await _lockoutService.RecordFailedAttemptAsync(account.Id);
            var (_, attemptsLeft, _) = await _lockoutService.CheckLockoutAsync(account.Id);

            _logger.LogWarning("Failed authentication attempt for account {AccountId}", account.Id);

            return Result.Failure<AuthenticateResult>(
                new Error("Auth.InvalidCredentials", "Invalid phone number or PIN."));
        }

        // Verify device binding
        if (!string.IsNullOrEmpty(account.DeviceId) && account.DeviceId != command.DeviceId)
            return Result.Failure<AuthenticateResult>(
                new Error("Auth.DeviceMismatch", "This device is not registered. Please initiate a device transfer."));

        // Reset failed attempts on success
        await _lockoutService.ResetAttemptsAsync(account.Id);

        // Update last login
        account.LastLoginAt = DateTime.UtcNow;
        account.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Generate tokens
        var (accessToken, refreshToken) = await _tokenService.GenerateTokenPairAsync(account);

        // Publish event
        await _messageBus.PublishAsync(
            new UserAuthenticated(account.Id, command.DeviceId)
            {
                TenantId = command.TenantId
            },
            cancellationToken);

        _logger.LogInformation("Account {AccountId} authenticated successfully", account.Id);

        return Result.Success(new AuthenticateResult(
            AccessToken: accessToken,
            RefreshToken: refreshToken,
            AccessTokenExpiresIn: _jwtSettings.AccessTokenExpiryMinutes * 60,
            RefreshTokenExpiresIn: _jwtSettings.RefreshTokenExpiryDays * 86400,
            AccountId: account.Id.ToString(),
            CustomerId: account.CustomerId.ToString()));
    }
}

public sealed record AuthenticateResult(
    string AccessToken,
    string RefreshToken,
    long AccessTokenExpiresIn,
    long RefreshTokenExpiresIn,
    string AccountId,
    string CustomerId);
