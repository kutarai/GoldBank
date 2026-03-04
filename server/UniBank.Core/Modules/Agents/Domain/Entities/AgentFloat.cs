using UniBank.SharedKernel.Domain;

namespace UniBank.Core.Modules.Agents.Domain.Entities;

/// <summary>
/// Tracks the float (cash) balance for an agent merchant.
/// Agents deposit money up-front (float) which is used to fund cash-in transactions
/// and replenished by cash-out transactions.
/// </summary>
public sealed class AgentFloat : AggregateRoot
{
    public Guid MerchantId { get; set; }
    public decimal FloatBalance { get; set; }
    public decimal FloatLimit { get; set; }
    public string Currency { get; set; } = "ZWG";
    public string TenantId { get; set; } = default!;
    public DateTime? DeletedAt { get; set; }
}
