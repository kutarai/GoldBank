using Microsoft.AspNetCore.Http;
using UniBank.SharedKernel.MultiTenancy;

namespace UniBank.Core.Common.Persistence;

public class GrpcTenantProvider : ITenantProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly PublicDbContext _publicDb;
    private TenantInfo? _cachedTenant;

    public GrpcTenantProvider(IHttpContextAccessor httpContextAccessor, PublicDbContext publicDb)
    {
        _httpContextAccessor = httpContextAccessor;
        _publicDb = publicDb;
    }

    public string GetTenantId()
    {
        return GetTenantInfo().Id.ToString();
    }

    public TenantInfo GetTenantInfo()
    {
        if (_cachedTenant is not null)
            return _cachedTenant;

        var httpContext = _httpContextAccessor.HttpContext;
        var tenantCode = httpContext?.User?.FindFirst("tenant_id")?.Value
            ?? httpContext?.Request.Headers["x-tenant-id"].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(tenantCode))
        {
            // In development, fall back to default tenant
            _cachedTenant = TenantInfo.Default;
            return _cachedTenant;
        }

        var entity = _publicDb.Tenants.FirstOrDefault(t => t.Code == tenantCode || t.Id.ToString() == tenantCode);
        if (entity is null)
        {
            _cachedTenant = TenantInfo.Default;
            return _cachedTenant;
        }

        _cachedTenant = new TenantInfo(
            entity.Id, entity.Name, entity.Code, entity.SchemaName,
            entity.CountryCode, entity.CurrencyCode, entity.Timezone,
            entity.Status, entity.Status == "active");

        return _cachedTenant;
    }

    public async Task<TenantInfo?> GetTenantByCodeAsync(string code, CancellationToken ct = default)
    {
        var entity = await Task.Run(() => _publicDb.Tenants.FirstOrDefault(t => t.Code == code), ct);
        if (entity is null) return null;

        return new TenantInfo(
            entity.Id, entity.Name, entity.Code, entity.SchemaName,
            entity.CountryCode, entity.CurrencyCode, entity.Timezone,
            entity.Status, entity.Status == "active");
    }
}
