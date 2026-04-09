using UniBank.SharedKernel.Domain;

namespace UniBank.Core.Modules.BranchCash.Domain.Entities;

/// <summary>
/// A teller's open cash drawer for a single business day at a single branch (STORY-148).
/// Tracks opening float, running balances, and end-of-day variance.
/// </summary>
public sealed class TellerDrawerSession : AggregateRoot
{
    public Guid TellerId { get; set; }
    public Guid BranchId { get; set; }
    public DateOnly BusinessDate { get; set; }
    public string Status { get; set; } = "Open"; // Open, Closed, Suspended

    /// <summary>
    /// JSON: { "USD": { "total": 5000.00, "denominations": [...] }, "ZWG": { ... } }
    /// </summary>
    public string OpeningFloatJson { get; set; } = "{}";

    /// <summary>
    /// JSON: same shape as opening float — populated on close.
    /// </summary>
    public string? ClosingBalanceJson { get; set; }

    /// <summary>
    /// JSON: system-computed expected closing balance (running totals).
    /// </summary>
    public string? ExpectedClosingJson { get; set; }

    /// <summary>
    /// JSON: per-currency variance (counted − expected).
    /// </summary>
    public string? VarianceJson { get; set; }

    public DateTime OpenedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public Guid? ClosedBySupervisorId { get; set; }

    /// <summary>
    /// Filesystem path of the generated end-of-day PDF report (STORY-159).
    /// Populated on drawer close. Reprint serves directly from disk.
    /// </summary>
    public string? EodReportPath { get; set; }

    public string TenantId { get; set; } = default!;
}
