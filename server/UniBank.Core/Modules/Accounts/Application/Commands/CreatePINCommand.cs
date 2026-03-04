namespace UniBank.Core.Modules.Accounts.Application.Commands;

/// <summary>
/// Command to create a PIN for an account during the registration flow.
/// Issued after OTP verification (STORY-009) completes successfully.
/// The raw PIN fields are never logged or persisted.
/// </summary>
public sealed record CreatePINCommand(
    Guid AccountId,
    string Pin,
    string PinConfirmation,
    string TenantId,
    string? DeviceId);
