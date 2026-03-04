using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UniBank.Core.Common.Persistence;
using UniBank.Core.Modules.Accounts.Application.Commands;
using UniBank.Core.Modules.Accounts.Application.Interfaces;
using UniBank.Core.Modules.Accounts.Domain.ValueObjects;
using UniBank.SharedKernel.Results;

namespace UniBank.Core.Modules.Accounts.Application.Handlers;

/// <summary>
/// Handles the Register command: validates phone number, checks for duplicates,
/// generates OTP, and dispatches SMS.
/// </summary>
public sealed class RegisterHandler
{
    private readonly UniBankDbContext _dbContext;
    private readonly IOtpService _otpService;
    private readonly ISmsGateway _smsGateway;
    private readonly ILogger<RegisterHandler> _logger;

    public RegisterHandler(
        UniBankDbContext dbContext,
        IOtpService otpService,
        ISmsGateway smsGateway,
        ILogger<RegisterHandler> logger)
    {
        _dbContext = dbContext;
        _otpService = otpService;
        _smsGateway = smsGateway;
        _logger = logger;
    }

    public async Task<Result<RegisterResult>> HandleAsync(
        RegisterCommand command, CancellationToken cancellationToken = default)
    {
        // Validate phone number (E.164 format, Southern African codes)
        var phoneResult = PhoneNumber.Create(command.PhoneNumber);
        if (phoneResult.IsFailure)
            return Result.Failure<RegisterResult>(phoneResult.Error);

        var phone = phoneResult.Value;

        // Check for duplicate (exclude soft-deleted accounts)
        var existingAccount = await _dbContext
            .Set<Domain.Entities.Account>()
            .FirstOrDefaultAsync(
                a => a.PhoneNumber == phone.Value && a.DeletedAt == null,
                cancellationToken);

        if (existingAccount is not null)
            return Result.Failure<RegisterResult>(RegisterErrors.DuplicatePhone);

        // Generate registration ID
        var registrationId = Guid.NewGuid().ToString();

        // Generate and store OTP in Redis
        var otpResult = await _otpService.GenerateAndStoreOtpAsync(
            phone.Value, registrationId, cancellationToken);

        if (otpResult.IsFailure)
            return Result.Failure<RegisterResult>(otpResult.Error);

        // Send OTP via SMS (the OTP value itself is only used here and never logged)
        await _smsGateway.SendOtpAsync(
            phone.Value,
            $"Your verification code is: {otpResult.Value}. Valid for 5 minutes.",
            cancellationToken);

        _logger.LogInformation(
            "OTP sent for registration {RegistrationId} to phone {MaskedPhone}",
            registrationId, phone.ToMasked());

        return Result.Success(new RegisterResult(
            RegistrationId: registrationId,
            OtpLength: 6,
            OtpTtlSeconds: 300));
    }
}

public sealed record RegisterResult(
    string RegistrationId,
    int OtpLength,
    int OtpTtlSeconds);

public static class RegisterErrors
{
    public static readonly Error DuplicatePhone = new(
        "Register.DuplicatePhone",
        "Phone number is already registered.");
}
