namespace UniBank.Core.Modules.Accounts.Application.Commands;

public sealed record InitiateDeviceTransferCommand(
    string PhoneNumber,
    string NewDeviceId,
    string TenantId);
