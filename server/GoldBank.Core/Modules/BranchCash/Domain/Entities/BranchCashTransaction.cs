using GoldBank.SharedKernel.Domain;

namespace GoldBank.Core.Modules.BranchCash.Domain.Entities;

/// <summary>
/// A single physical cash transaction at a branch counter (STORY-148).
/// One row per deposit, withdrawal, or reversal handled by a teller.
/// </summary>
public sealed class BranchCashTransaction : AggregateRoot
{
    public Guid TransactionId { get; set; } // FK → bank.transactions
    public Guid DrawerSessionId { get; set; }
    public Guid TellerId { get; set; }
    public Guid BranchId { get; set; }
    public Guid AccountId { get; set; }

    public string Direction { get; set; } = default!; // Deposit, Withdrawal, Reversal
    public string Currency { get; set; } = default!;
    public decimal Amount { get; set; }
    public string DepositorName { get; set; } = default!;

    /// <summary>
    /// JSON: [{ "faceValue": 100, "type": "Note", "count": 5 }, ...]
    /// </summary>
    public string DenominationBreakdownJson { get; set; } = "[]";

    public bool IdentityVerified { get; set; }

    public Guid? SupervisorApproverId { get; set; }
    public DateTime? SupervisorApprovedAt { get; set; }

    public string? ReceiptPdfPath { get; set; }

    public Guid? ReversedByTransactionId { get; set; }
    public DateTime? ReversedAt { get; set; }

    public string Status { get; set; } = "completed"; // pending_supervisor_approval, completed, reversed

    public string TenantId { get; set; } = default!;
}
