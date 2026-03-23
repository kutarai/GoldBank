using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UniBank.Core.Common.Persistence;
using UniBank.Core.Modules.Accounts.Application.Commands;
using UniBank.Core.Modules.Accounts.Application.Validators;
using UniBank.Core.Modules.Accounts.Infrastructure.Services;
using UniBank.Protos.Accounts;
using UniBank.SharedKernel.Events;
using UniBank.SharedKernel.Messaging;

namespace UniBank.Core.Modules.Accounts.Application.Handlers;

/// <summary>
/// Handles the CreatePIN command as part of the registration flow (STORY-010).
/// After OTP verification, the user creates a PIN to secure their account.
/// On success, the account is assigned a bcrypt-hashed PIN, a full JWT and
/// refresh token are issued, and a PINCreated domain event is published.
///
/// SECURITY: The raw PIN is never logged, stored in plaintext, or returned in any response.
/// </summary>
public sealed class CreatePINHandler
{
    private readonly UniBankDbContext _dbContext;
    private readonly PinHashingService _pinHasher;
    private readonly JwtTokenService _tokenService;
    private readonly IMessageBus _messageBus;
    private readonly ILogger<CreatePINHandler> _logger;

    public CreatePINHandler(
        UniBankDbContext dbContext,
        PinHashingService pinHasher,
        JwtTokenService tokenService,
        IMessageBus messageBus,
        ILogger<CreatePINHandler> logger)
    {
        _dbContext = dbContext;
        _pinHasher = pinHasher;
        _tokenService = tokenService;
        _messageBus = messageBus;
        _logger = logger;
    }

    /// <summary>
    /// Processes a PIN creation request: validates the PIN, hashes it,
    /// persists it, publishes a domain event, and returns auth tokens.
    /// </summary>
    /// <param name="command">The command containing account ID, PIN, and confirmation.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A CreatePINResponse with auth tokens on success.</returns>
    /// <exception cref="RpcException">Thrown for validation failures, missing accounts, or duplicate PIN creation.</exception>
    public async Task<CreatePINResponse> HandleAsync(
        CreatePINCommand command,
        CancellationToken cancellationToken = default)
    {
        // 1. Validate PIN format, strength, and confirmation match
        var validationResult = PINValidator.Validate(command.Pin, command.PinConfirmation);
        if (validationResult.IsFailure)
        {
            throw new RpcException(new Status(
                StatusCode.InvalidArgument,
                validationResult.Error.Message));
        }

        // 2. Load the primary account
        var account = await _dbContext.Accounts
            .FirstOrDefaultAsync(a => a.Id == command.AccountId, cancellationToken);

        if (account is null)
        {
            throw new RpcException(new Status(
                StatusCode.NotFound,
                "Account not found"));
        }

        // 3. Prevent re-creation of PIN (idempotency guard)
        if (account.HasPinSet)
        {
            throw new RpcException(new Status(
                StatusCode.FailedPrecondition,
                "PIN has already been set for this account"));
        }

        // 4. Hash the PIN with bcrypt (cost factor 12) and set on ALL accounts for this phone
        var pinHash = _pinHasher.HashPin(command.Pin);
        var now = DateTime.UtcNow;

        var allAccounts = await _dbContext.Accounts
            .Where(a => a.PhoneNumber == account.PhoneNumber && a.DeletedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var acct in allAccounts)
        {
            acct.PinHash = pinHash;
            acct.UpdatedAt = now;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        // 5. Publish domain event
        await _messageBus.PublishAsync(
            new PINCreated(
                UserId: account.Id,
                AccountId: account.Id,
                PINType: "numeric")
            {
                TenantId = command.TenantId
            },
            cancellationToken);

        // 6. Generate full auth tokens (JWT + refresh token)
        var (accessToken, refreshToken) = await _tokenService.GenerateTokenPairAsync(account);

        _logger.LogInformation(
            "PIN created successfully for account {AccountId} in tenant {TenantId}",
            account.Id,
            command.TenantId);

        return new CreatePINResponse
        {
            Success = true,
            Message = "PIN created successfully. Welcome to UniBank!",
            AuthToken = accessToken,
            RefreshToken = refreshToken
        };
    }
}
