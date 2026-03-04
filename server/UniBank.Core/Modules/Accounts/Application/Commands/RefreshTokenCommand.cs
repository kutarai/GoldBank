namespace UniBank.Core.Modules.Accounts.Application.Commands;

public sealed record RefreshTokenCommand(
    string RefreshToken,
    string DeviceId);
