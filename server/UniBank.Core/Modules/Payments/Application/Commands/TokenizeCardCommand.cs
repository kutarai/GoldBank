namespace UniBank.Core.Modules.Payments.Application.Commands;

/// <summary>
/// Command to tokenize a card PAN for NFC contactless payments (STORY-022).
/// The token is a format-preserving representation of the card PAN used for tap-to-pay.
/// </summary>
public sealed record TokenizeCardCommand(
    Guid AccountId,
    string CardPan,
    string DeviceId,
    string TenantId);
