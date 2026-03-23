using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SynergySwitch.Core.Interfaces;
using SynergySwitch.Core.Models;
using SynergySwitch.Data;

namespace SynergySwitch.Core.Dashboard;

public class DashboardService : IDashboardService
{
    private readonly SwitchDbContext _db;
    private readonly ILogger<DashboardService> _logger;

    public DashboardService(SwitchDbContext db, ILogger<DashboardService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<DashboardSummary> GetSummaryAsync(DateTime? from = null, DateTime? to = null)
    {
        var query = _db.TransactionLogs.AsNoTracking().AsQueryable();

        if (from.HasValue)
            query = query.Where(t => t.RequestTimestamp >= from.Value);
        if (to.HasValue)
            query = query.Where(t => t.RequestTimestamp <= to.Value);

        var total = await query.CountAsync();
        var approved = await query.CountAsync(t => t.ResponseCode == "Approved");
        var declined = await query.CountAsync(t => t.ResponseCode == "Declined");
        var techError = await query.CountAsync(t => t.ResponseCode == "TechnicalError");

        double avgResponseTime = 0;
        if (total > 0)
        {
            var timestamps = await query
                .Select(t => new { t.RequestTimestamp, t.ResponseTimestamp })
                .ToListAsync();
            avgResponseTime = timestamps
                .Average(t => (t.ResponseTimestamp - t.RequestTimestamp).TotalMilliseconds);
        }

        var totalAmount = total > 0
            ? await query.Where(t => t.ResponseCode == "Approved").SumAsync(t => t.Amount)
            : 0;

        var activeTerminals = await _db.Terminals.AsNoTracking().CountAsync(t => t.IsActive);
        var inactiveTerminals = await _db.Terminals.AsNoTracking().CountAsync(t => !t.IsActive);

        return new DashboardSummary
        {
            TotalTransactions = total,
            ApprovedCount = approved,
            DeclinedCount = declined,
            TechnicalErrorCount = techError,
            ApprovalRatePercent = total > 0 ? Math.Round((decimal)approved / total * 100, 1) : 0,
            AverageResponseTimeMs = Math.Round(avgResponseTime, 1),
            ActiveTerminals = activeTerminals,
            InactiveTerminals = inactiveTerminals,
            TotalAmountProcessed = totalAmount
        };
    }

    public async Task<PagedResult<TransactionSummary>> GetTransactionsAsync(TransactionFilter filter)
    {
        var query = _db.TransactionLogs.AsNoTracking().AsQueryable();

        if (filter.FromDate.HasValue)
            query = query.Where(t => t.RequestTimestamp >= filter.FromDate.Value);
        if (filter.ToDate.HasValue)
            query = query.Where(t => t.RequestTimestamp <= filter.ToDate.Value);
        if (!string.IsNullOrEmpty(filter.TerminalId))
            query = query.Where(t => t.TerminalId == filter.TerminalId);
        if (!string.IsNullOrEmpty(filter.MerchantId))
            query = query.Where(t => t.MerchantId == filter.MerchantId);
        if (!string.IsNullOrEmpty(filter.ResponseCode))
            query = query.Where(t => t.ResponseCode == filter.ResponseCode);
        if (!string.IsNullOrEmpty(filter.PanLastFour))
            query = query.Where(t => t.PanLastFour == filter.PanLastFour);

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(t => t.RequestTimestamp)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(t => new TransactionSummary
            {
                Id = t.Id,
                ExchangeId = t.ExchangeId,
                TransactionReference = t.TransactionReference,
                TerminalId = t.TerminalId,
                MerchantId = t.MerchantId,
                PanLastFour = t.PanLastFour,
                Amount = t.Amount,
                Currency = t.Currency,
                CardEntryMode = t.CardEntryMode,
                ResponseCode = t.ResponseCode,
                AuthorisationCode = t.AuthorisationCode,
                RequestTimestamp = t.RequestTimestamp,
                ResponseTimestamp = t.ResponseTimestamp
            })
            .ToListAsync();

        return new PagedResult<TransactionSummary>
        {
            Items = items,
            TotalCount = totalCount,
            Page = filter.Page,
            PageSize = filter.PageSize
        };
    }

    public async Task<TransactionSummary?> GetTransactionByIdAsync(int id)
    {
        return await _db.TransactionLogs.AsNoTracking()
            .Where(t => t.Id == id)
            .Select(t => new TransactionSummary
            {
                Id = t.Id,
                ExchangeId = t.ExchangeId,
                TransactionReference = t.TransactionReference,
                TerminalId = t.TerminalId,
                MerchantId = t.MerchantId,
                PanLastFour = t.PanLastFour,
                Amount = t.Amount,
                Currency = t.Currency,
                CardEntryMode = t.CardEntryMode,
                ResponseCode = t.ResponseCode,
                AuthorisationCode = t.AuthorisationCode,
                RequestTimestamp = t.RequestTimestamp,
                ResponseTimestamp = t.ResponseTimestamp
            })
            .FirstOrDefaultAsync();
    }

    public async Task<IReadOnlyList<TerminalStatus>> GetTerminalStatusesAsync()
    {
        return await _db.Terminals.AsNoTracking()
            .OrderByDescending(t => t.IsActive)
            .ThenByDescending(t => t.LastHeartbeat)
            .Select(t => new TerminalStatus
            {
                TerminalId = t.TerminalId,
                MerchantId = t.MerchantId,
                SerialNumber = t.SerialNumber,
                FirmwareVersion = t.FirmwareVersion,
                AppVersion = t.AppVersion,
                LastHeartbeat = t.LastHeartbeat,
                BatteryLevel = t.BatteryLevel,
                TransactionCount = t.TransactionCount,
                IsActive = t.IsActive,
                RegisteredAt = t.RegisteredAt
            })
            .ToListAsync();
    }

    public async Task<IReadOnlyList<HourlyThroughput>> GetHourlyThroughputAsync(
        DateTime from, DateTime to)
    {
        return await _db.TransactionLogs.AsNoTracking()
            .Where(t => t.RequestTimestamp >= from && t.RequestTimestamp <= to)
            .GroupBy(t => new { t.RequestTimestamp.Date, t.RequestTimestamp.Hour })
            .Select(g => new HourlyThroughput
            {
                Hour = g.Key.Date.AddHours(g.Key.Hour),
                Count = g.Count(),
                ApprovedCount = g.Count(t => t.ResponseCode == "Approved"),
                DeclinedCount = g.Count(t => t.ResponseCode == "Declined")
            })
            .OrderBy(h => h.Hour)
            .ToListAsync();
    }
}
