namespace UniBank.SharedKernel.MultiTenancy;

public interface ITenantProvider
{
    string GetTenantId();
    TenantInfo GetTenantInfo();
    Task<TenantInfo?> GetTenantByCodeAsync(string code, CancellationToken ct = default);
}
