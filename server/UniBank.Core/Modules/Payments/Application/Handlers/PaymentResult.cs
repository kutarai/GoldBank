namespace UniBank.Core.Modules.Payments.Application.Handlers;

/// <summary>
/// Result of a payment operation (NFC or QR).
/// When RequiresPin is true, the TransactionId should be used with ConfirmPaymentCommand.
/// </summary>
public sealed record PaymentResult(
    string TransactionId,
    string Reference,
    decimal Amount,
    decimal Fee,
    decimal NewBalance,
    string Currency,
    string Status,
    DateTime? CompletedAt,
    bool RequiresPin);
