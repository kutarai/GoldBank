using Microsoft.Extensions.Logging;
using UniBank.Core.Common.Persistence;
using UniBank.Core.Modules.Accounts.Application.Commands;
using UniBank.Core.Modules.Accounts.Application.Interfaces;
using UniBank.Core.Modules.Accounts.Domain.Entities;
using UniBank.Core.Modules.Accounts.Domain.ValueObjects;
using UniBank.Core.Modules.Accounts.Infrastructure.Services;
using UniBank.SharedKernel.Events;
using UniBank.SharedKernel.Messaging;
using UniBank.SharedKernel.Results;

namespace UniBank.Core.Modules.Accounts.Application.Handlers;

/// <summary>
/// Handles the VerifyOTP command: validates OTP, creates the account record,
/// publishes domain events, and returns a temporary JWT token for PIN creation.
/// </summary>
public sealed class VerifyOtpHandler
{
    private readonly UniBankDbContext _dbContext;
    private readonly IOtpService _otpService;
    private readonly IMessageBus _messageBus;
    private readonly JwtTokenService _jwtTokenService;
    private readonly ILogger<VerifyOtpHandler> _logger;

    public VerifyOtpHandler(
        UniBankDbContext dbContext,
        IOtpService otpService,
        IMessageBus messageBus,
        JwtTokenService jwtTokenService,
        ILogger<VerifyOtpHandler> logger)
    {
        _dbContext = dbContext;
        _otpService = otpService;
        _messageBus = messageBus;
        _jwtTokenService = jwtTokenService;
        _logger = logger;
    }

    public async Task<Result<VerifyOtpResult>> HandleAsync(
        VerifyOtpCommand command, CancellationToken cancellationToken = default)
    {
        // Validate OTP against Redis
        var validationResult = await _otpService.ValidateOtpAsync(
            command.RegistrationId, command.Otp, command.PhoneNumber, cancellationToken);

        if (validationResult.IsFailure)
            return Result.Failure<VerifyOtpResult>(validationResult.Error);

        // Parse and validate phone number
        var phoneResult = PhoneNumber.Create(command.PhoneNumber);
        if (phoneResult.IsFailure)
            return Result.Failure<VerifyOtpResult>(phoneResult.Error);

        var phone = phoneResult.Value;

        // Create account in tenant database
        var account = new Account
        {
            PhoneNumber = phone.Value,
            PhoneCountryCode = phone.CountryCode,
            Status = "pending_kyc",
            KycLevel = 0,
            DailyLimit = 1000.00m,
            MonthlyLimit = 5000.00m,
            Balance = 0.00m,
            AvailableBalance = 0.00m,
            Currency = "ZWG",
            TenantId = command.TenantId,
            DeviceId = command.DeviceId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        _dbContext.Set<Account>().Add(account);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Account {AccountId} created for phone {MaskedPhone} in tenant {TenantId}",
            account.Id, phone.ToMasked(), command.TenantId);

        // Publish domain events
        var correlationId = Guid.NewGuid().ToString();

        await _messageBus.PublishAsync(new UserRegistered(
            UserId: account.Id,
            PhoneNumber: account.PhoneNumber,
            FirstName: string.Empty,
            LastName: string.Empty,
            Email: null)
        {
            TenantId = command.TenantId,
            CorrelationId = correlationId,
        }, cancellationToken);

        await _messageBus.PublishAsync(new AccountCreated(
            AccountId: account.Id,
            UserId: account.Id,
            PhoneNumber: account.PhoneNumber,
            AccountType: "personal",
            Currency: account.Currency)
        {
            TenantId = command.TenantId,
            CorrelationId = correlationId,
        }, cancellationToken);

        // Generate temporary JWT token with pin_creation scope
        var temporaryToken = _jwtTokenService.GenerateTemporaryToken(account);

        return Result.Success(new VerifyOtpResult(
            AccountId: account.Id.ToString(),
            TemporaryToken: temporaryToken));
    }
}

public sealed record VerifyOtpResult(
    string AccountId,
    string TemporaryToken);
