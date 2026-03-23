namespace UniBank.Core.Modules.CardTransactions.Application.Handlers;

/// <summary>
/// Result of a financial card transaction (purchase or deposit).
/// </summary>
public sealed record CardTransactionResult(
    string TransactionId,
    bool Success,
    string ResponseCode,
    string? AuthorizationCode,
    string Message,
    decimal AvailableBalance,
    string Currency,
    DateTime? ProcessedAt);

/// <summary>
/// Result of a balance enquiry transaction.
/// </summary>
public sealed record BalanceEnquiryResult(
    string TransactionId,
    bool Success,
    string ResponseCode,
    string Message,
    decimal AvailableBalance,
    decimal LedgerBalance,
    string Currency);

/// <summary>
/// A single entry in a mini-statement response.
/// </summary>
public sealed record StatementEntryResult(
    DateTime Date,
    string Description,
    decimal Amount,
    string Type,
    string Reference,
    decimal BalanceAfter,
    string Currency);

/// <summary>
/// Result of a statement enquiry transaction.
/// </summary>
public sealed record StatementEnquiryResult(
    string TransactionId,
    bool Success,
    string ResponseCode,
    string Message,
    IReadOnlyList<StatementEntryResult> Entries,
    decimal AvailableBalance,
    string Currency);
