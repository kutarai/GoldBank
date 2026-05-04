namespace GoldBank.Core.Modules.Accounts.Application.Commands;

/// <summary>
/// Command to verify an OTP and complete account creation.
/// </summary>
public sealed record VerifyOtpCommand(
    string RegistrationId,
    string Otp,
    string PhoneNumber,
    string TenantId,
    string? DeviceId);
