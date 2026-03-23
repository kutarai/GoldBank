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

        // Create dual-currency accounts (ZWG + USD) with virtual card PANs
        var now = DateTime.UtcNow;
        var currencies = new[] { "ZWG", "USD" };
        var accounts = new List<Account>();

        foreach (var currency in currencies)
        {
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
                Currency = currency,
                CardPan = VirtualCardGenerator.GeneratePan(),
                TenantId = command.TenantId,
                DeviceId = command.DeviceId,
                CreatedAt = now,
                UpdatedAt = now,
            };
            _dbContext.Set<Account>().Add(account);
            accounts.Add(account);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Primary account is ZWG (first created)
        var primaryAccount = accounts[0];

        _logger.LogInformation(
            "Dual accounts created for phone {MaskedPhone}: ZWG={ZwgId}, USD={UsdId}, tenant {TenantId}",
            phone.ToMasked(), accounts[0].Id, accounts[1].Id, command.TenantId);

        // Publish domain events for each account
        var correlationId = Guid.NewGuid().ToString();

        await _messageBus.PublishAsync(new UserRegistered(
            UserId: primaryAccount.Id,
            PhoneNumber: primaryAccount.PhoneNumber,
            FirstName: string.Empty,
            LastName: string.Empty,
            Email: null)
        {
            TenantId = command.TenantId,
            CorrelationId = correlationId,
        }, cancellationToken);

        foreach (var account in accounts)
        {
            await _messageBus.PublishAsync(new AccountCreated(
                AccountId: account.Id,
                UserId: primaryAccount.Id,
                PhoneNumber: account.PhoneNumber,
                AccountType: "personal",
                Currency: account.Currency)
            {
                TenantId = command.TenantId,
                CorrelationId = correlationId,
            }, cancellationToken);
        }

        // Generate temporary JWT token with pin_creation scope (uses primary ZWG account)
        var temporaryToken = _jwtTokenService.GenerateTemporaryToken(primaryAccount);

        return Result.Success(new VerifyOtpResult(
            AccountId: primaryAccount.Id.ToString(),
            TemporaryToken: temporaryToken));
    }
}

public sealed record VerifyOtpResult(
    string AccountId,
    string TemporaryToken);
