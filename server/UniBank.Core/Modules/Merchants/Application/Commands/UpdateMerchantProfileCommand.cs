namespace UniBank.Core.Modules.Merchants.Application.Commands;

public sealed record UpdateMerchantProfileCommand(
    Guid MerchantId,
    string? BusinessName,
    string? CategoryCode,
    string? BusinessAddress,
    double? GpsLatitude,
    double? GpsLongitude,
    double? GpsAccuracyMeters,
    string TenantId);
