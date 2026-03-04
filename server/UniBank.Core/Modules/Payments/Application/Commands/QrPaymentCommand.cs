namespace UniBank.Core.Modules.Payments.Application.Commands;

/// <summary>
/// Command to process a QR code payment after scanning (STORY-027).
/// The payer scans the merchant-presented QR code and confirms payment with PIN.
/// </summary>
public sealed record QrPaymentCommand(
    Guid AccountId,
    string QrCodeData,
    string Pin,
    string TenantId);
