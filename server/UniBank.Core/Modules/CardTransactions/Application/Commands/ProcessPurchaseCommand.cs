namespace UniBank.Core.Modules.CardTransactions.Application.Commands;

/// <summary>
/// Command to process a card purchase transaction from the switch (STORY-078, STORY-079).
/// Handles both on-us (merchant is bank client) and off-us (merchant at another bank) purchases.
/// </summary>
public sealed record ProcessPurchaseCommand(
    string TransactionId,
    string CardHolderAccount,
    string MerchantId,
    string MerchantName,
    string TerminalId,
    decimal Amount,
    string Currency,
    string ProcessingCode,
    string SourceInstitution,
    string AcquiringInstitution,
    string Stan,
    string RetrievalReference,
    bool IsOnUs,
    string TenantId);
