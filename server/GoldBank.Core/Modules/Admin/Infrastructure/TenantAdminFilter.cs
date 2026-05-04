namespace GoldBank.Core.Modules.Admin.Infrastructure;

/// <summary>
/// Query filter helper that applies tenant-scoped filtering for admin operations (STORY-071).
/// Super admins (TenantId == null) bypass the filter and see all tenant data.
/// Tenant-scoped admins see only data matching their assigned tenant.
/// </summary>
public sealed class TenantAdminFilter
{
    /// <summary>
    /// Applies tenant filtering to a queryable data source.
    /// Returns unfiltered results for super admins (null tenantId).
    /// Returns tenant-filtered results for scoped admins.
    /// </summary>
    /// <typeparam name="T">Entity type with a TenantId property.</typeparam>
    /// <param name="query">The source queryable.</param>
    /// <param name="adminTenantId">The admin user's tenant ID, or null for super admins.</param>
    /// <param name="tenantIdSelector">A function to extract the TenantId from the entity.</param>
    /// <returns>Filtered or unfiltered queryable based on admin scope.</returns>
    public IQueryable<T> Apply<T>(
        IQueryable<T> query,
        string? adminTenantId,
        Func<T, string> tenantIdSelector)
    {
        if (string.IsNullOrEmpty(adminTenantId))
            return query;

        return query.Where(x => tenantIdSelector(x) == adminTenantId);
    }

    /// <summary>
    /// Applies tenant filtering using a LINQ expression for EF Core compatibility.
    /// This overload produces a server-side translatable expression.
    /// </summary>
    /// <typeparam name="T">Entity type with a TenantId property.</typeparam>
    /// <param name="query">The source queryable.</param>
    /// <param name="adminTenantId">The admin user's tenant ID, or null for super admins.</param>
    /// <param name="tenantIdSelector">An expression to extract the TenantId from the entity.</param>
    /// <returns>Filtered or unfiltered queryable based on admin scope.</returns>
    public IQueryable<T> ApplyExpression<T>(
        IQueryable<T> query,
        string? adminTenantId,
        System.Linq.Expressions.Expression<Func<T, string>> tenantIdSelector)
    {
        if (string.IsNullOrEmpty(adminTenantId))
            return query;

        var parameter = tenantIdSelector.Parameters[0];
        var body = System.Linq.Expressions.Expression.Equal(
            tenantIdSelector.Body,
            System.Linq.Expressions.Expression.Constant(adminTenantId));
        var predicate = System.Linq.Expressions.Expression.Lambda<Func<T, bool>>(body, parameter);

        return query.Where(predicate);
    }

    /// <summary>
    /// Checks whether a super admin is accessing the system (no tenant restriction).
    /// </summary>
    /// <param name="adminTenantId">The admin user's tenant ID.</param>
    /// <returns>True if the admin is a super admin with unrestricted access.</returns>
    public bool IsSuperAdmin(string? adminTenantId)
    {
        return string.IsNullOrEmpty(adminTenantId);
    }
}
