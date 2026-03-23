namespace SynergySwitch.Data.Entities;

public class MobileMoneyPaymentEntity
{
    public int Id { get; set; }
    public required string PaymentReference { get; set; }
    public required string TerminalId { get; set; }
    public required string MerchantId { get; set; }
    public required string Currency { get; set; }
    public long Amount { get; set; }
    public required string MobileNumber { get; set; }
    public required string Status { get; set; } // PENDING, CONFIRMED, DECLINED, TIMED_OUT
    public string? AuthorizationCode { get; set; }
    public string? ProviderReference { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
