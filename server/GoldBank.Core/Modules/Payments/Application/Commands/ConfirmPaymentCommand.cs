namespace GoldBank.Core.Modules.Payments.Application.Commands;

/// <summary>
/// Command to confirm a high-value NFC payment with PIN entry (STORY-024).
/// The TransactionId must reference a payment in "pending_pin" status.
/// </summary>
public sealed record ConfirmPaymentCommand(
    Guid TransactionId,
    string Pin,
    Guid AccountId);
