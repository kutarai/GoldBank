namespace SynergySwitch.Data.Entities;

public class TerminalEntity
{
    public int Id { get; set; }
    public required string TerminalId { get; set; }
    public required string MerchantId { get; set; }
    public string? SerialNumber { get; set; }
    public string? FirmwareVersion { get; set; }
    public string? AppVersion { get; set; }
    public DateTime LastHeartbeat { get; set; }
    public int BatteryLevel { get; set; }
    public long TransactionCount { get; set; }
    public bool IsActive { get; set; }
    public DateTime RegisteredAt { get; set; }
}
