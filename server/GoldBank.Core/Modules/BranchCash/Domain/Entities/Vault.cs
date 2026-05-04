using GoldBank.SharedKernel.Domain;

namespace GoldBank.Core.Modules.BranchCash.Domain.Entities;

/// <summary>
/// Branch vault — 1:1 with a Branch (STORY-164). Holds reserve cash that the
/// vault manager issues to teller drawers each morning and receives back at end of day.
/// </summary>
public sealed class Vault : AggregateRoot
{
    public Guid BranchId { get; set; }
    public string Name { get; set; } = "";
    public Guid? VaultManagerId { get; set; }
    public string SpotCheckCron { get; set; } = "daily";
    public DateTime? LastSpotCheckAt { get; set; }
    public string LastSpotCheckResult { get; set; } = "NotYet"; // NotYet, Balanced, Variance
    public bool IsActive { get; set; } = true;
    public string TenantId { get; set; } = "goldbank";
}

/// <summary>Materialised count of one denomination held in one vault.</summary>
public sealed class VaultDenominationStock : AggregateRoot
{
    public Guid VaultId { get; set; }
    public string Currency { get; set; } = "USD";
    public Guid DenominationId { get; set; }
    public int Count { get; set; }
}

/// <summary>
/// Append-only ledger of every cash movement into or out of a vault
/// (CIT in, CIT out, drawer issue, drawer surrender, spot-check adjustment).
/// </summary>
public sealed class VaultMovement : AggregateRoot
{
    public Guid VaultId { get; set; }
    public string Type { get; set; } = ""; // CashInjection, CashWithdrawal, DrawerIssue, DrawerSurrender, SpotCheckAdjust, Transfer
    public string Direction { get; set; } = "In"; // In or Out
    public string Currency { get; set; } = "USD";
    public decimal TotalAmount { get; set; }
    public string DenominationBreakdownJson { get; set; } = "[]";
    public Guid? TellerId { get; set; }
    public Guid? DrawerSessionId { get; set; }
    public Guid PerformedBy { get; set; }
    public Guid? WitnessId { get; set; }
    public string? Reference { get; set; }
    public string? Notes { get; set; }
    public string? ReceiptPdfPath { get; set; }
    public string TenantId { get; set; } = "goldbank";
}

/// <summary>
/// Result of a vault spot check — expected count vs actual physical count.
/// </summary>
public sealed class VaultSpotCheck : AggregateRoot
{
    public Guid VaultId { get; set; }
    public Guid PerformedBy { get; set; }
    public Guid WitnessId { get; set; }
    public string ExpectedJson { get; set; } = "{}";
    public string ActualJson { get; set; } = "{}";
    public string VarianceJson { get; set; } = "{}";
    public bool HasVariance { get; set; }
    public Guid? AdjustmentMovementId { get; set; }
    public string? ReportPdfPath { get; set; }
    public string TenantId { get; set; } = "goldbank";
}
