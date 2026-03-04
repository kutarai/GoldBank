namespace UniBank.Core.Modules.Transfers.Application.Commands;

/// <summary>
/// Command to initiate a cross-border P2P transfer with currency conversion (STORY-030).
/// The sender must be active, have a valid PIN, and sufficient balance to cover sendAmount + fee.
/// Cross-border transfers are set to "processing" status with an estimated delivery window.
/// </summary>
public sealed record CrossBorderTransferCommand(
    Guid SenderAccountId,
    string RecipientPhone,
    string RecipientName,
    string RecipientCountry,
    decimal SendAmount,
    string SendCurrency,
    string ReceiveCurrency,
    string? CorridorId,
    string Pin,
    string TenantId);
