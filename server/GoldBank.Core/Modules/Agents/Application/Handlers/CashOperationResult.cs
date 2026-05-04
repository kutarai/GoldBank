namespace GoldBank.Core.Modules.Agents.Application.Handlers;

/// <summary>
/// Result returned after a successful cash-in or cash-out operation.
/// </summary>
public sealed record CashOperationResult(
    Guid TransactionId,
    string Reference,
    decimal Amount,
    decimal CustomerFee,
    decimal Tax,
    decimal Commission,
    decimal NewFloatBalance,
    string Currency,
    DateTime CompletedAt);
