namespace GoldBank.Core.Modules.CardTransactions.Application.Commands;

/// <summary>
/// Command to process a card mini-statement enquiry from the switch (STORY-083).
/// On-us: returns full statement entries with counterparty details.
/// Off-us: returns sanitized entries (counterparty info excluded for external terminals).
/// </summary>
public sealed record StatementEnquiryCommand(
    string TransactionId,
    string CardHolderAccount,
    string TerminalId,
    string SourceInstitution,
    string Stan,
    string RetrievalReference,
    int MaxRecords,
    bool IsOnUs,
    string TenantId);
