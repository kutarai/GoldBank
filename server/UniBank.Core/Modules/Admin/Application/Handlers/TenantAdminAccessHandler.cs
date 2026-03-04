using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UniBank.Core.Common.Persistence;
using UniBank.Core.Modules.Admin.Infrastructure;
using UniBank.SharedKernel.Results;

namespace UniBank.Core.Modules.Admin.Application.Handlers;

/// <summary>
/// Validates admin user's tenant scope and filters all queries by tenant_id (STORY-071).
/// Super admins (TenantId == null) bypass the filter and can view all tenants.
/// Tenant-scoped admins see only their tenant's data for customer search,
/// merchant management, transaction search, and KYC review.
/// </summary>
public sealed class TenantAdminAccessHandler
{
    private readonly UniBankDbContext _dbContext;
    private readonly TenantAdminFilter _filter;
    private readonly ILogger<TenantAdminAccessHandler> _logger;

    public TenantAdminAccessHandler(
        UniBankDbContext dbContext,
        TenantAdminFilter filter,
        ILogger<TenantAdminAccessHandler> logger)
    {
        _dbContext = dbContext;
        _filter = filter;
        _logger = logger;
    }

    /// <summary>
    /// Searches customers with tenant-scoped filtering applied.
    /// </summary>
    public async Task<Result<TenantFilteredResult<CustomerSearchResult>>> SearchCustomersAsync(
        string? adminTenantId,
        string? searchQuery,
        string? statusFilter,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = _filter.ApplyExpression(
            _dbContext.Accounts.Where(a => a.DeletedAt == null),
            adminTenantId,
            a => a.TenantId);

        if (!string.IsNullOrEmpty(searchQuery))
        {
            query = query.Where(a =>
                a.PhoneNumber.Contains(searchQuery) ||
                (a.FirstName != null && a.FirstName.Contains(searchQuery)) ||
                (a.LastName != null && a.LastName.Contains(searchQuery)) ||
                (a.Email != null && a.Email.Contains(searchQuery)));
        }

        if (!string.IsNullOrEmpty(statusFilter))
            query = query.Where(a => a.Status == statusFilter);

        var totalCount = await query.CountAsync(cancellationToken);
        var validPageSize = Math.Min(Math.Max(pageSize, 1), 100);
        var validPage = Math.Max(page, 1);
        var totalPages = (int)Math.Ceiling((double)totalCount / validPageSize);

        var customers = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((validPage - 1) * validPageSize)
            .Take(validPageSize)
            .Select(a => new CustomerSearchResult(
                a.Id.ToString(),
                a.PhoneNumber,
                $"{a.FirstName ?? ""} {a.LastName ?? ""}".Trim(),
                a.Status,
                a.KycLevel,
                a.Balance,
                a.Currency,
                a.TenantId,
                a.CreatedAt))
            .ToListAsync(cancellationToken);

        _logger.LogInformation(
            "Admin tenant search: adminTenantId={AdminTenantId}, isSuperAdmin={IsSuperAdmin}, results={Count}",
            adminTenantId ?? "super", _filter.IsSuperAdmin(adminTenantId), customers.Count);

        return Result.Success(new TenantFilteredResult<CustomerSearchResult>(
            customers, totalCount, validPage, validPageSize, totalPages,
            _filter.IsSuperAdmin(adminTenantId)));
    }

    /// <summary>
    /// Searches transactions with tenant-scoped filtering applied.
    /// </summary>
    public async Task<Result<TenantFilteredResult<TransactionSearchResult>>> SearchTransactionsAsync(
        string? adminTenantId,
        string? accountId,
        string? reference,
        string? typeFilter,
        string? statusFilter,
        DateTime? dateFrom,
        DateTime? dateTo,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = _filter.ApplyExpression(
            _dbContext.Transactions.AsQueryable(),
            adminTenantId,
            t => t.TenantId);

        if (!string.IsNullOrEmpty(accountId) && Guid.TryParse(accountId, out var parsedAccountId))
            query = query.Where(t => t.AccountId == parsedAccountId);

        if (!string.IsNullOrEmpty(reference))
            query = query.Where(t => t.Reference != null && t.Reference.Contains(reference));

        if (!string.IsNullOrEmpty(typeFilter))
            query = query.Where(t => t.Type == typeFilter);

        if (!string.IsNullOrEmpty(statusFilter))
            query = query.Where(t => t.Status == statusFilter);

        if (dateFrom.HasValue)
            query = query.Where(t => t.CreatedAt >= dateFrom.Value);

        if (dateTo.HasValue)
            query = query.Where(t => t.CreatedAt <= dateTo.Value);

        var totalCount = await query.CountAsync(cancellationToken);
        var validPageSize = Math.Min(Math.Max(pageSize, 1), 100);
        var validPage = Math.Max(page, 1);
        var totalPages = (int)Math.Ceiling((double)totalCount / validPageSize);

        var transactions = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((validPage - 1) * validPageSize)
            .Take(validPageSize)
            .Select(t => new TransactionSearchResult(
                t.Id.ToString(),
                t.AccountId.ToString(),
                t.Type,
                t.Amount,
                t.Fee,
                t.Status,
                t.Reference,
                t.Currency,
                t.TenantId,
                t.CreatedAt))
            .ToListAsync(cancellationToken);

        return Result.Success(new TenantFilteredResult<TransactionSearchResult>(
            transactions, totalCount, validPage, validPageSize, totalPages,
            _filter.IsSuperAdmin(adminTenantId)));
    }

    /// <summary>
    /// Lists merchants with tenant-scoped filtering applied.
    /// </summary>
    public async Task<Result<TenantFilteredResult<MerchantSearchResult>>> SearchMerchantsAsync(
        string? adminTenantId,
        string? searchQuery,
        string? statusFilter,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = _filter.ApplyExpression(
            _dbContext.Merchants.AsQueryable(),
            adminTenantId,
            m => m.TenantId);

        if (!string.IsNullOrEmpty(searchQuery))
        {
            query = query.Where(m =>
                m.BusinessName.Contains(searchQuery) ||
                m.MerchantCode.Contains(searchQuery));
        }

        if (!string.IsNullOrEmpty(statusFilter))
            query = query.Where(m => m.Status == statusFilter);

        var totalCount = await query.CountAsync(cancellationToken);
        var validPageSize = Math.Min(Math.Max(pageSize, 1), 100);
        var validPage = Math.Max(page, 1);
        var totalPages = (int)Math.Ceiling((double)totalCount / validPageSize);

        var merchants = await query
            .OrderByDescending(m => m.CreatedAt)
            .Skip((validPage - 1) * validPageSize)
            .Take(validPageSize)
            .Select(m => new MerchantSearchResult(
                m.Id.ToString(),
                m.BusinessName,
                m.MerchantCode,
                m.Status,
                m.TenantId,
                m.CreatedAt))
            .ToListAsync(cancellationToken);

        return Result.Success(new TenantFilteredResult<MerchantSearchResult>(
            merchants, totalCount, validPage, validPageSize, totalPages,
            _filter.IsSuperAdmin(adminTenantId)));
    }

    /// <summary>
    /// Lists KYC documents for review with tenant-scoped filtering applied.
    /// </summary>
    public async Task<Result<TenantFilteredResult<KycReviewResult>>> GetKycDocumentsForReviewAsync(
        string? adminTenantId,
        string? statusFilter,
        int page = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = _filter.ApplyExpression(
            _dbContext.KycDocuments.AsQueryable(),
            adminTenantId,
            k => k.TenantId);

        if (!string.IsNullOrEmpty(statusFilter))
            query = query.Where(k => k.Status == statusFilter);

        var totalCount = await query.CountAsync(cancellationToken);
        var validPageSize = Math.Min(Math.Max(pageSize, 1), 100);
        var validPage = Math.Max(page, 1);
        var totalPages = (int)Math.Ceiling((double)totalCount / validPageSize);

        var documents = await query
            .OrderByDescending(k => k.CreatedAt)
            .Skip((validPage - 1) * validPageSize)
            .Take(validPageSize)
            .Select(k => new KycReviewResult(
                k.Id.ToString(),
                k.AccountId.ToString(),
                k.DocumentType,
                k.Status,
                k.TenantId,
                k.CreatedAt))
            .ToListAsync(cancellationToken);

        return Result.Success(new TenantFilteredResult<KycReviewResult>(
            documents, totalCount, validPage, validPageSize, totalPages,
            _filter.IsSuperAdmin(adminTenantId)));
    }
}

public sealed record TenantFilteredResult<T>(
    List<T> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages,
    bool IsSuperAdminView);

public sealed record CustomerSearchResult(
    string AccountId,
    string PhoneNumber,
    string FullName,
    string Status,
    int KycLevel,
    decimal Balance,
    string Currency,
    string TenantId,
    DateTime CreatedAt);

public sealed record TransactionSearchResult(
    string TransactionId,
    string AccountId,
    string Type,
    decimal Amount,
    decimal Fee,
    string Status,
    string? Reference,
    string Currency,
    string TenantId,
    DateTime CreatedAt);

public sealed record MerchantSearchResult(
    string MerchantId,
    string BusinessName,
    string MerchantCode,
    string Status,
    string TenantId,
    DateTime CreatedAt);

public sealed record KycReviewResult(
    string DocumentId,
    string AccountId,
    string DocumentType,
    string Status,
    string TenantId,
    DateTime CreatedAt);
