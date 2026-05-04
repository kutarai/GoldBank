namespace GoldBank.Core.Modules.BillPay.Application.Handlers;

/// <summary>
/// Result of a successful bill payment operation (STORY-038).
/// Contains the transaction details, token (for prepaid utilities), and updated balance.
/// </summary>
public sealed record PayBillResult(
    string TransactionId,
    string Reference,
    string? Token,
    decimal Amount,
    decimal Fee,
    decimal NewBalance,
    string Currency,
    DateTime CompletedAt);
