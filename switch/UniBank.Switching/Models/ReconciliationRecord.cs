namespace UniBank.Switching.Models;

/// <summary>
/// Direction of a transaction through the switch.
/// </summary>
public enum TransactionDirection
{
    /// <summary>Transaction sent from this institution to another.</summary>
    Outbound,

    /// <summary>Transaction received from another institution.</summary>
    Inbound
}

/// <summary>
/// Type of reconciliation discrepancy found during settlement comparison.
/// </summary>
public enum DiscrepancyType
{
    /// <summary>Transaction sent but no matching response received.</summary>
    MissingResponse,

    /// <summary>Transaction received but not found in outbound logs.</summary>
    UnmatchedInbound,

    /// <summary>Amount mismatch between sent and received records.</summary>
    AmountMismatch,

    /// <summary>Transaction was declined or failed.</summary>
    Declined,

    /// <summary>Duplicate transaction detected.</summary>
    Duplicate
}

/// <summary>
/// A single reconciliation record capturing the details of a transaction
/// for settlement comparison between institutions.
/// </summary>
public sealed class ReconciliationRecord
{
    /// <summary>
    /// Unique identifier for this reconciliation record.
    /// </summary>
    public string RecordId { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// The original transaction identifier.
    /// </summary>
    public string TransactionId { get; set; } = string.Empty;

    /// <summary>
    /// Direction of the transaction (outbound or inbound).
    /// </summary>
    public TransactionDirection Direction { get; set; }

    /// <summary>
    /// Code of the originating institution.
    /// </summary>
    public string SourceInstitution { get; set; } = string.Empty;

    /// <summary>
    /// Code of the destination institution.
    /// </summary>
    public string DestinationInstitution { get; set; } = string.Empty;

    /// <summary>
    /// Transaction amount.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// ISO 4217 currency code.
    /// </summary>
    public string Currency { get; set; } = string.Empty;

    /// <summary>
    /// The response code received for this transaction.
    /// </summary>
    public string ResponseCode { get; set; } = string.Empty;

    /// <summary>
    /// Whether the transaction was successfully processed.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Timestamp when the transaction was processed.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Whether this record has been matched with its counterpart.
    /// </summary>
    public bool Matched { get; set; }

    /// <summary>
    /// Matched counterpart transaction ID (if matched).
    /// </summary>
    public string? MatchedTransactionId { get; set; }
}

/// <summary>
/// A discrepancy found during the reconciliation process.
/// </summary>
public sealed class ReconciliationDiscrepancyDetail
{
    /// <summary>
    /// The transaction that has a discrepancy.
    /// </summary>
    public string TransactionId { get; set; } = string.Empty;

    /// <summary>
    /// The type of discrepancy.
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable description of the discrepancy.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// The amount involved in the discrepancy.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Currency of the discrepancy amount.
    /// </summary>
    public string Currency { get; set; } = string.Empty;
}

/// <summary>
/// A complete reconciliation report for a given date and institution pair.
/// </summary>
public sealed class ReconciliationReport
{
    /// <summary>
    /// Unique identifier for this report.
    /// </summary>
    public string ReportId { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// The date covered by this report.
    /// </summary>
    public DateTime ReportDate { get; set; }

    /// <summary>
    /// The institution this report is generated for.
    /// </summary>
    public string InstitutionId { get; set; } = string.Empty;

    /// <summary>
    /// Total number of transactions sent by this institution.
    /// </summary>
    public int TotalSent { get; set; }

    /// <summary>
    /// Total number of transactions received by this institution.
    /// </summary>
    public int TotalReceived { get; set; }

    /// <summary>
    /// Number of transactions that were matched between sent and received.
    /// </summary>
    public int Matched { get; set; }

    /// <summary>
    /// Number of discrepancies found.
    /// </summary>
    public int Discrepancies { get; set; }

    /// <summary>
    /// Net settlement amount (positive = institution owes, negative = institution is owed).
    /// </summary>
    public decimal NetSettlementAmount { get; set; }

    /// <summary>
    /// Currency for the settlement.
    /// </summary>
    public string Currency { get; set; } = "ZWG";

    /// <summary>
    /// Total value of successful outbound transactions.
    /// </summary>
    public decimal TotalOutboundAmount { get; set; }

    /// <summary>
    /// Total value of successful inbound transactions.
    /// </summary>
    public decimal TotalInboundAmount { get; set; }

    /// <summary>
    /// Detailed list of any discrepancies found.
    /// </summary>
    public List<ReconciliationDiscrepancyDetail> DiscrepancyDetails { get; set; } = [];

    /// <summary>
    /// All reconciliation records included in this report.
    /// </summary>
    public List<ReconciliationRecord> Records { get; set; } = [];

    /// <summary>
    /// Timestamp when this report was generated.
    /// </summary>
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}
