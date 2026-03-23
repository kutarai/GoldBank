namespace SynergySwitch.Data.Entities;

public class MerchantEntity
{
    public int Id { get; set; }
    public required string MerchantId { get; set; }
    public required string Name { get; set; }
    public string CategoryCode { get; set; } = "5999";
    public string CountryCode { get; set; } = "716";
    public string CurrencyCode { get; set; } = "USD";
}
