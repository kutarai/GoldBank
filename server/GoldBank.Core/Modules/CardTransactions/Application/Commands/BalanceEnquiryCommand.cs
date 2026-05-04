namespace GoldBank.Core.Modules.CardTransactions.Application.Commands;

/// <summary>
/// Command to process a card balance enquiry from the switch (STORY-082).
/// On-us: returns both available and ledger balances.
/// Off-us: returns available balance only (ledger balance withheld from external terminals).
/// </summary>
public sealed record BalanceEnquiryCommand(
    string TransactionId,
    string CardHolderAccount,
    string TerminalId,
    string SourceInstitution,
    string Stan,
    string RetrievalReference,
    bool IsOnUs,
    string TenantId);
