namespace UniBank.Core.Modules.Accounts.Application.Commands;

/// <summary>
/// Command to initiate user registration with phone number and OTP.
/// </summary>
public sealed record RegisterCommand(
    string PhoneNumber,
    string DeviceId,
    string TenantId);
