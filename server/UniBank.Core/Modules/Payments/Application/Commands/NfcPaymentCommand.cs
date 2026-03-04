namespace UniBank.Core.Modules.Payments.Application.Commands;

/// <summary>
/// Command to initiate an NFC contactless payment at a POS terminal (STORY-023).
/// If the payment amount exceeds the high-value threshold and no PIN is provided,
/// the transaction is placed in "pending_pin" status.
/// </summary>
public sealed record NfcPaymentCommand(
    Guid AccountId,
    string MerchantId,
    string TerminalId,
    decimal Amount,
    string Currency,
    string NfcData,
    string? Pin,
    string TenantId);
