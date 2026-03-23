namespace SynergySwitch.Data.Entities;

public class TransactionLogEntity
{
    public int Id { get; set; }
    public required string ExchangeId { get; set; }
    public string? TransactionReference { get; set; }
    public required string TerminalId { get; set; }
    public required string MerchantId { get; set; }
    public required string PanLastFour { get; set; }
    public long Amount { get; set; }
    public required string Currency { get; set; }
    public required string CardEntryMode { get; set; }
    public required string CvmMethod { get; set; }
    public required string ResponseCode { get; set; }
    public required string ResponseReason { get; set; }
    public string? AuthorisationCode { get; set; }
    public DateTime RequestTimestamp { get; set; }
    public DateTime ResponseTimestamp { get; set; }
    public bool HasIccData { get; set; }
}
