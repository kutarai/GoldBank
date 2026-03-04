namespace UniBank.Core.Modules.Merchants.Application.Commands;

public sealed record RegisterMerchantCommand(
    Guid OwnerAccountId,
    string BusinessName,
    string BusinessType,
    string? RegistrationNumber,
    string? TaxId,
    string? CategoryCode,
    string BusinessAddress,
    double? GpsLatitude,
    double? GpsLongitude,
    double? GpsAccuracyMeters,
    bool IsAgent,
    bool AgentTermsAccepted,
    string TenantId);
