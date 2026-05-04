using GoldBank.SharedKernel.Domain;

namespace GoldBank.Core.Modules.CardTransactions.Domain.Entities;

/// <summary>
/// Represents a card transaction processed by the issuing bank (EPIC-015).
/// Records purchases, deposits, balance enquiries, and statement enquiries
/// initiated via card at POS terminals or ATMs and routed through the switch.
/// </summary>
public sealed class CardTransaction : AggregateRoot
{
    public Guid AccountId { get; set; }
    public Guid? MerchantAccountId { get; set; }
    public string? MerchantId { get; set; }
    public string? MerchantName { get; set; }
    public string TransactionType { get; set; } = default!;
    public decimal Amount { get; set; }
    public decimal Fee { get; set; }
    public string Currency { get; set; } = "ZWG";
    public string Status { get; set; } = "pending";
    public string? ResponseCode { get; set; }
    public string? AuthorizationCode { get; set; }
    public string? Reference { get; set; }
    public string? RetrievalReference { get; set; }
    public string? Stan { get; set; }
    public string? TerminalId { get; set; }
    public string? ProcessingCode { get; set; }
    public string? SourceInstitution { get; set; }
    public string? AcquiringInstitution { get; set; }
    public decimal BalanceAfter { get; set; }
    public string TenantId { get; set; } = default!;
    public DateTime? CompletedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
}
