namespace GoldBank.Core.Modules.Accounts.Application.Commands;

public sealed record LogoutCommand(
    Guid AccountId,
    bool AllDevices);
