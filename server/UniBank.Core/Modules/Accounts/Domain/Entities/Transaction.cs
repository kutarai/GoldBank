using UniBank.SharedKernel.Domain;

namespace UniBank.Core.Modules.Accounts.Domain.Entities;

/// <summary>
/// Represents a financial transaction on an account (STORY-017).
/// Records all money movements: transfers, payments, cash-in/out, etc.
/// </summary>
public class Transaction : BaseEntity
{
    public Guid AccountId { get; set; }
    public string Type { get; set; } = default!;
    public decimal Amount { get; set; }
    public decimal Fee { get; set; }
    public decimal Tax { get; set; }
    public string Status { get; set; } = "pending";
    public string? Reference { get; set; }
    public string? Description { get; set; }
    public string? CounterpartyName { get; set; }
    public string? CounterpartyPhone { get; set; }
    public decimal BalanceAfter { get; set; }
    public string Currency { get; set; } = "ZWG";
    public string TenantId { get; set; } = default!;
    public DateTime? CompletedAt { get; set; }
}
