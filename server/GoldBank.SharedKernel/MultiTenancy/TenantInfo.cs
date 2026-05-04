namespace GoldBank.SharedKernel.MultiTenancy;

public sealed record TenantInfo(
    Guid Id,
    string Name,
    string Code,
    string SchemaName,
    string CountryCode,
    string CurrencyCode,
    string Timezone,
    string Status,
    bool IsActive)
{
    public static TenantInfo Default => new(
        Guid.Empty, "GoldBank Default", "goldbank_default", "bank",
        "ZWE", "ZWG", "Africa/Harare", "active", true);
}
