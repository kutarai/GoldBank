namespace GoldBank.Core.Modules.Accounts.Application.Commands;

public sealed record CompleteDeviceTransferCommand(
    string TransferReference,
    string Otp,
    string Pin,
    string NewDeviceId,
    string TenantId);
