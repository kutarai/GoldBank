namespace SynergySwitch.Core.Models;

public record DashboardSummary
{
    public long TotalTransactions { get; init; }
    public long ApprovedCount { get; init; }
    public long DeclinedCount { get; init; }
    public long TechnicalErrorCount { get; init; }
    public decimal ApprovalRatePercent { get; init; }
    public double AverageResponseTimeMs { get; init; }
    public long ActiveTerminals { get; init; }
    public long InactiveTerminals { get; init; }
    public long TotalAmountProcessed { get; init; }
    public string Currency { get; init; } = "USD";
}

public record TransactionSummary
{
    public int Id { get; init; }
    public required string ExchangeId { get; init; }
    public string? TransactionReference { get; init; }
    public required string TerminalId { get; init; }
    public required string MerchantId { get; init; }
    public required string PanLastFour { get; init; }
    public long Amount { get; init; }
    public required string Currency { get; init; }
    public required string CardEntryMode { get; init; }
    public required string ResponseCode { get; init; }
    public string? AuthorisationCode { get; init; }
    public DateTime RequestTimestamp { get; init; }
    public DateTime ResponseTimestamp { get; init; }
    public double ResponseTimeMs => (ResponseTimestamp - RequestTimestamp).TotalMilliseconds;
}

public record TransactionFilter
{
    public DateTime? FromDate { get; init; }
    public DateTime? ToDate { get; init; }
    public string? TerminalId { get; init; }
    public string? MerchantId { get; init; }
    public string? ResponseCode { get; init; }
    public string? PanLastFour { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 25;
}

public record PagedResult<T>
{
    public required IReadOnlyList<T> Items { get; init; }
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public record TerminalStatus
{
    public required string TerminalId { get; init; }
    public required string MerchantId { get; init; }
    public string? SerialNumber { get; init; }
    public string? FirmwareVersion { get; init; }
    public string? AppVersion { get; init; }
    public DateTime LastHeartbeat { get; init; }
    public int BatteryLevel { get; init; }
    public long TransactionCount { get; init; }
    public bool IsActive { get; init; }
    public DateTime RegisteredAt { get; init; }
    public string Status => IsActive && (DateTime.UtcNow - LastHeartbeat).TotalMinutes < 5
        ? "Online"
        : IsActive ? "Stale" : "Offline";
}

public record HourlyThroughput
{
    public DateTime Hour { get; init; }
    public int Count { get; init; }
    public int ApprovedCount { get; init; }
    public int DeclinedCount { get; init; }
}
