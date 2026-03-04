namespace UniBank.Core.Modules.Accounts.Application.Commands;

public sealed record UpdateProfileCommand(
    Guid AccountId,
    string? FirstName,
    string? LastName,
    string? Email,
    string? DateOfBirth,
    string? NationalId);
