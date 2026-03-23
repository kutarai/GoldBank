namespace UniBank.Core.Modules.CardTransactions.Application.Commands;

/// <summary>
/// Command to process a card deposit transaction from the switch (STORY-080, STORY-081).
/// Handles both on-us (merchant is bank client) and off-us (merchant at another bank) deposits.
/// </summary>
public sealed record ProcessDepositCommand(
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
