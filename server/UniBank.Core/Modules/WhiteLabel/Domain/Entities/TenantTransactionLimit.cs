using UniBank.SharedKernel.Domain;

namespace UniBank.Core.Modules.WhiteLabel.Domain.Entities;

/// <summary>
/// Per-tenant transaction limit configuration aggregate root (STORY-070).
/// Defines per-transaction, daily, and monthly limits for each transaction type.
/// </summary>
public sealed class TenantTransactionLimit : AggregateRoot
{
    public string TenantId { get; set; } = default!;
    public string TransactionType { get; set; } = default!;
    public decimal PerTransactionLimit { get; set; }
    public decimal DailyLimit { get; set; }
    public decimal MonthlyLimit { get; set; }
    public string Currency { get; set; } = "ZWG";
}
