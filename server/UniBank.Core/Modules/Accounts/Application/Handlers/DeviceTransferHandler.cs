using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UniBank.Core.Common.Persistence;
using UniBank.Core.Modules.Accounts.Application.Commands;
using UniBank.Core.Modules.Accounts.Application.Interfaces;
using UniBank.Core.Modules.Accounts.Domain.Entities;
using UniBank.Core.Modules.Accounts.Infrastructure.Services;
using UniBank.SharedKernel.Events;
using UniBank.SharedKernel.Messaging;
using UniBank.SharedKernel.Results;

namespace UniBank.Core.Modules.Accounts.Application.Handlers;

/// <summary>
/// Handles device transfer: initiate (sends OTP) and complete (verifies OTP + PIN) (STORY-014).
/// </summary>
public sealed class DeviceTransferHandler
{
    private readonly UniBankDbContext _dbContext;
    private readonly IOtpService _otpService;
    private readonly ISmsGateway _smsGateway;
    private readonly PinHashingService _pinHasher;
    private readonly JwtTokenService _tokenService;
    private readonly IMessageBus _messageBus;
    private readonly ILogger<DeviceTransferHandler> _logger;

    public DeviceTransferHandler(
        UniBankDbContext dbContext,
        IOtpService otpService,
        ISmsGateway smsGateway,
        PinHashingService pinHasher,
        JwtTokenService tokenService,
        IMessageBus messageBus,
        ILogger<DeviceTransferHandler> logger)
    {
        _dbContext = dbContext;
        _otpService = otpService;
        _smsGateway = smsGateway;
        _pinHasher = pinHasher;
        _tokenService = tokenService;
        _messageBus = messageBus;
        _logger = logger;
    }

    public async Task<Result<InitiateDeviceTransferResult>> InitiateAsync(
        InitiateDeviceTransferCommand command, CancellationToken cancellationToken = default)
    {
        var account = await _dbContext.Accounts
            .FirstOrDefaultAsync(
                a => a.PhoneNumber == command.PhoneNumber && a.DeletedAt == null,
                cancellationToken);

        if (account is null)
            return Result.Failure<InitiateDeviceTransferResult>(
                new Error("Account.NotFound", "Account not found."));

        if (!account.HasPinSet)
            return Result.Failure<InitiateDeviceTransferResult>(
                new Error("Account.NoPIN", "Account must have a PIN set before device transfer."));

        var transferRef = Guid.NewGuid().ToString();

        // Store transfer request
        var transfer = new DeviceTransferRequest
        {
            AccountId = account.Id,
            TransferReference = transferRef,
            OldDeviceId = account.DeviceId ?? "unknown",
            NewDeviceId = command.NewDeviceId,
            Status = "pending",
            TenantId = command.TenantId,
            ExpiresAt = DateTime.UtcNow.AddMinutes(10),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.Set<DeviceTransferRequest>().Add(transfer);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Generate OTP
        var otpResult = await _otpService.GenerateAndStoreOtpAsync(
            command.PhoneNumber, transferRef, cancellationToken);

        if (otpResult.IsFailure)
            return Result.Failure<InitiateDeviceTransferResult>(otpResult.Error);

        await _smsGateway.SendOtpAsync(
            command.PhoneNumber,
            $"Your device transfer code is: {otpResult.Value}. Valid for 10 minutes.",
            cancellationToken);

        _logger.LogInformation("Device transfer initiated for account {AccountId}, ref: {Ref}",
            account.Id, transferRef);

        return Result.Success(new InitiateDeviceTransferResult(transferRef, 600));
    }

    public async Task<Result<CompleteDeviceTransferResult>> CompleteAsync(
        CompleteDeviceTransferCommand command, CancellationToken cancellationToken = default)
    {
        var transfer = await _dbContext.Set<DeviceTransferRequest>()
            .FirstOrDefaultAsync(
                t => t.TransferReference == command.TransferReference && t.Status == "pending",
                cancellationToken);

        if (transfer is null)
            return Result.Failure<CompleteDeviceTransferResult>(
                new Error("Transfer.NotFound", "Transfer request not found or expired."));

        if (transfer.ExpiresAt < DateTime.UtcNow)
        {
            transfer.Status = "expired";
            await _dbContext.SaveChangesAsync(cancellationToken);
            return Result.Failure<CompleteDeviceTransferResult>(
                new Error("Transfer.Expired", "Transfer request has expired."));
        }

        // Load account for phone number and PIN verification
        var account = await _dbContext.Accounts
            .FirstOrDefaultAsync(a => a.Id == transfer.AccountId, cancellationToken);

        if (account is null)
            return Result.Failure<CompleteDeviceTransferResult>(
                new Error("Account.NotFound", "Account not found."));

        // Verify OTP (requires phone number)
        var otpValid = await _otpService.ValidateOtpAsync(
            transfer.TransferReference, command.Otp, account.PhoneNumber, cancellationToken);

        if (otpValid.IsFailure)
            return Result.Failure<CompleteDeviceTransferResult>(otpValid.Error);

        if (!_pinHasher.VerifyPin(command.Pin, account.PinHash!))
            return Result.Failure<CompleteDeviceTransferResult>(
                new Error("Auth.InvalidPIN", "Invalid PIN."));

        // Update device
        var oldDeviceId = account.DeviceId ?? "unknown";
        account.DeviceId = command.NewDeviceId;
        account.UpdatedAt = DateTime.UtcNow;

        transfer.Status = "completed";
        transfer.CompletedAt = DateTime.UtcNow;
        transfer.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Generate new tokens
        var (accessToken, refreshToken) = await _tokenService.GenerateTokenPairAsync(account);

        // Publish event
        await _messageBus.PublishAsync(
            new DeviceTransferred(account.Id, oldDeviceId, command.NewDeviceId)
            {
                TenantId = transfer.TenantId
            },
            cancellationToken);

        _logger.LogInformation("Device transfer completed for account {AccountId}", account.Id);

        return Result.Success(new CompleteDeviceTransferResult(accessToken, refreshToken));
    }
}

public sealed record InitiateDeviceTransferResult(string TransferReference, int OtpExpirySeconds);
public sealed record CompleteDeviceTransferResult(string AccessToken, string RefreshToken);
