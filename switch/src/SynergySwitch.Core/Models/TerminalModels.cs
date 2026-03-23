namespace SynergySwitch.Core.Models;

/// <summary>
/// Domain model for terminal registration.
/// </summary>
public record TerminalRegistration
{
    public required string TerminalId { get; init; }
    public required string MerchantId { get; init; }
    public string? SerialNumber { get; init; }
    public string? FirmwareVersion { get; init; }
    public string? AppVersion { get; init; }
}

public record TerminalRegistrationResult
{
    public bool Success { get; init; }
    public string? Message { get; init; }
    public TerminalConfig? Configuration { get; init; }
}

public record TerminalConfig
{
    public string? MerchantName { get; init; }
    public string? MerchantCategoryCode { get; init; }
    public string? CountryCode { get; init; }
    public string? CurrencyCode { get; init; }
    public long ContactlessFloorLimit { get; init; }
    public long CvmRequiredLimit { get; init; }
}
