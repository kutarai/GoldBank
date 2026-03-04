using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UniBank.Core.Common.Persistence;
using UniBank.SharedKernel.Results;

namespace UniBank.Core.Modules.FraudDetection.Application.Handlers;

/// <summary>
/// Lists fraud alerts with filtering by status, severity, date range, and pagination (STORY-072).
/// </summary>
public sealed class GetFraudAlertsHandler
{
    private readonly UniBankDbContext _dbContext;
    private readonly ILogger<GetFraudAlertsHandler> _logger;

    public GetFraudAlertsHandler(
        UniBankDbContext dbContext,
        ILogger<GetFraudAlertsHandler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<Result<FraudAlertsResult>> HandleAsync(
        GetFraudAlertsQuery query,
        CancellationToken cancellationToken = default)
    {
        var alertsQuery = _dbContext.FraudAlerts.AsQueryable();

        if (!string.IsNullOrEmpty(query.StatusFilter))
            alertsQuery = alertsQuery.Where(a => a.Status == query.StatusFilter);

        if (!string.IsNullOrEmpty(query.SeverityFilter))
            alertsQuery = alertsQuery.Where(a => a.Severity == query.SeverityFilter);

        if (query.DateFrom.HasValue)
            alertsQuery = alertsQuery.Where(a => a.CreatedAt >= query.DateFrom.Value);

        if (query.DateTo.HasValue)
            alertsQuery = alertsQuery.Where(a => a.CreatedAt <= query.DateTo.Value);

        if (!string.IsNullOrEmpty(query.TenantId))
            alertsQuery = alertsQuery.Where(a => a.TenantId == query.TenantId);

        var totalCount = await alertsQuery.CountAsync(cancellationToken);

        var pageSize = query.PageSize > 0 ? Math.Min(query.PageSize, 100) : 20;
        var page = query.Page > 0 ? query.Page : 1;
        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

        var alerts = await alertsQuery
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new FraudAlertSummary(
                a.Id.ToString(),
                a.AccountId.ToString(),
                a.TransactionId.ToString(),
                a.AlertType,
                a.Severity,
                a.Description,
                a.Status,
                a.CreatedAt))
            .ToListAsync(cancellationToken);

        _logger.LogInformation(
            "Retrieved {AlertCount} fraud alerts (page {Page}/{TotalPages})",
            alerts.Count, page, totalPages);

        return Result.Success(new FraudAlertsResult(
            alerts,
            totalCount,
            page,
            pageSize,
            totalPages));
    }
}

public sealed record GetFraudAlertsQuery(
    string? StatusFilter,
    string? SeverityFilter,
    DateTime? DateFrom,
    DateTime? DateTo,
    string? TenantId,
    int Page = 1,
    int PageSize = 20);

public sealed record FraudAlertSummary(
    string AlertId,
    string AccountId,
    string TransactionId,
    string AlertType,
    string Severity,
    string Description,
    string Status,
    DateTime CreatedAt);

public sealed record FraudAlertsResult(
    List<FraudAlertSummary> Alerts,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);
