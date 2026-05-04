namespace GoldBank.Core.Modules.Transfers.Application.Handlers;

/// <summary>
/// Result of a P2P or cross-border transfer operation (STORY-029, STORY-030).
/// Contains transaction details, fees, exchange rate (for cross-border), and updated balance.
/// </summary>
public sealed record TransferResult(
    string TransactionId,
    string Reference,
    decimal AmountSent,
    decimal AmountReceived,
    decimal Fee,
    string Currency,
    string ReceiveCurrency,
    string? ExchangeRate,
    decimal NewBalance,
    string Status,
    DateTime? EstimatedDelivery);
