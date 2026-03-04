namespace UniBank.Core.Modules.Accounts.Application.Commands;

public sealed record AuthenticateCommand(
    string PhoneNumber,
    string Pin,
    string DeviceId,
    string TenantId);
