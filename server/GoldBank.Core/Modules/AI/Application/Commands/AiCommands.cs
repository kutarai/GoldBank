namespace GoldBank.Core.Modules.AI.Application.Commands;

public sealed record VerifyIdentityCommand(
    Guid AccountId,
    byte[] SelfieImage,
    byte[] IdDocumentImage,
    string TenantId);

public sealed record ExtractDocumentFieldsCommand(
    byte[] DocumentImage,
    string DocumentType,
    string TenantId);

public sealed record ExtractChequeFieldsCommand(
    Guid AccountId,
    byte[] ChequeImage,
    string TenantId);

public sealed record ExtractBillFieldsCommand(
    Guid AccountId,
    byte[] BillImage,
    string TenantId);

public sealed record ChatCommand(
    Guid AccountId,
    string Message,
    List<ChatHistoryEntry> History,
    string TenantId);

public sealed record ChatHistoryEntry(string Role, string Content);

public sealed record ExtractReceiptFieldsCommand(
    Guid AccountId,
    Guid TransactionId,
    byte[] ReceiptImage,
    string TenantId);

public sealed record GetSpendingInsightsCommand(
    Guid AccountId,
    string TenantId);

public sealed record CheckLoanEligibilityCommand(
    Guid AccountId,
    decimal DesiredAmount,
    string Currency,
    int TenureMonths,
    string Purpose,
    string TenantId);

public sealed record VerifyLoanDocumentsCommand(
    Guid AccountId,
    Guid LoanApplicationId,
    byte[] DocumentImage,
    string DocumentType,
    decimal DeclaredIncome,
    string TenantId);

public sealed record TriageDisputeCommand(
    Guid AccountId,
    Guid TransactionId,
    string Description,
    byte[]? EvidenceImage,
    string TenantId);

public sealed record ExplainFraudAlertCommand(
    Guid AccountId,
    Guid TransactionId,
    string FraudRulesTriggered,
    double RiskScore,
    string TenantId);

public sealed record VerifyProofOfAddressCommand(
    Guid AccountId,
    byte[] DocumentImage,
    string TenantId);

public sealed record GetModelStatusCommand();
