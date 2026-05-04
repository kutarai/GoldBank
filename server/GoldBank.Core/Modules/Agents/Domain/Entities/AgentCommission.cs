using GoldBank.SharedKernel.Domain;

namespace GoldBank.Core.Modules.Agents.Domain.Entities;

/// <summary>
/// Records a commission earned by an agent for a cash-in or cash-out transaction.
/// Used for agent commission reporting and reconciliation.
/// </summary>
public sealed class AgentCommission : AggregateRoot
{
    public Guid MerchantId { get; set; }
    public Guid TransactionId { get; set; }
    public string TransactionType { get; set; } = default!;
    public decimal TransactionAmount { get; set; }
    public decimal CommissionRate { get; set; }
    public decimal CommissionAmount { get; set; }
    public string Currency { get; set; } = "ZWG";
    public string TenantId { get; set; } = default!;
}
