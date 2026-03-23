namespace SynergySwitch.Core.Models;

/// <summary>
/// Domain model for an incoming authorisation request.
/// Maps from ISO 20022 caaa.001 / protobuf AcceptorAuthorisationRequest.
/// </summary>
public record AuthorisationRequest
{
    // Header
    public required string ExchangeId { get; init; }
    public required string TerminalId { get; init; }

    // Merchant
    public required string MerchantId { get; init; }
    public string? MerchantName { get; init; }
    public string? MerchantCategoryCode { get; init; }

    // Card
    public required string Pan { get; init; }
    public string? CardSequenceNumber { get; init; }
    public string? ExpiryDate { get; init; }
    public string? Track2EquivalentData { get; init; }

    // Cardholder verification
    public string CvmMethod { get; init; } = "NO_CVM";
    public byte[]? EncryptedPinBlock { get; init; }

    // Transaction
    public required string TransactionReference { get; init; }
    public required string Currency { get; init; }
    public long Amount { get; init; }
    public string CardEntryMode { get; init; } = "CHIP";

    // EMV ICC data
    public byte[]? IccRelatedData { get; init; }
}

/// <summary>
/// Domain model for an authorisation response.
/// Maps to ISO 20022 caaa.002 / protobuf AcceptorAuthorisationResponse.
/// </summary>
public record AuthorisationResponse
{
    public required string ExchangeId { get; init; }
    public required string TransactionReference { get; init; }
    public required AuthorisationResponseCode ResponseCode { get; init; }
    public required string ResponseReason { get; init; }
    public string? AuthorisationCode { get; init; }
    public required string EmvResponseCode { get; init; }
    public string? DisplayMessage { get; init; }
}

public enum AuthorisationResponseCode
{
    Approved,
    Declined,
    Partial,
    TechnicalError
}
