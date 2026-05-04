using Google.Protobuf;
using Grpc.Core;
using GoldBank.Protos.Common;
using GoldBank.Protos.Reporting;
using GoldBank.Reporting.Services;

namespace GoldBank.Reporting.Grpc;

/// <summary>
/// gRPC service implementation for all reporting and analytics operations.
/// Covers: STORY-062 through STORY-067.
/// </summary>
public sealed class ReportingGrpcService : ReportingService.ReportingServiceBase
{
    private readonly DashboardService _dashboardService;
    private readonly UserGrowthReportService _userGrowthService;
    private readonly MerchantReportService _merchantReportService;
    private readonly RevenueReportService _revenueReportService;
    private readonly ReconReportService _reconReportService;
    private readonly ReportExportService _exportService;

    public ReportingGrpcService(
        DashboardService dashboardService,
        UserGrowthReportService userGrowthService,
        MerchantReportService merchantReportService,
        RevenueReportService revenueReportService,
        ReconReportService reconReportService,
        ReportExportService exportService)
    {
        _dashboardService = dashboardService;
        _userGrowthService = userGrowthService;
        _merchantReportService = merchantReportService;
        _revenueReportService = revenueReportService;
        _reconReportService = reconReportService;
        _exportService = exportService;
    }

    // ── STORY-062: Dashboard ────────────────────────────────────────────

    public override async Task<DashboardResponse> GetDashboard(
        DashboardRequest request, ServerCallContext context)
    {
        var metrics = await _dashboardService.GetDashboardAsync(
            request.DateRange?.From?.ToDateTime(),
            request.DateRange?.To?.ToDateTime(),
            context.CancellationToken);

        var response = new DashboardResponse
        {
            TotalUsers = metrics.TotalUsers,
            ActiveUsers = metrics.ActiveUsers,
            TotalTransactions = metrics.TotalTransactions,
            TotalVolume = new Money { Amount = metrics.TotalVolume.ToString("F2"), Currency = "ZWG" },
            TotalRevenue = new Money { Amount = metrics.TotalRevenue.ToString("F2"), Currency = "ZWG" },
            ActiveMerchants = metrics.ActiveMerchants,
            ActiveAgents = metrics.ActiveAgents,
            ActiveTerminals = metrics.ActiveTerminals
        };

        foreach (var m in metrics.DailyMetrics)
        {
            response.DailyMetrics.Add(new DailyMetric
            {
                Date = m.Date,
                Transactions = m.Transactions,
                Volume = m.Volume.ToString("F2"),
                NewUsers = m.NewUsers
            });
        }

        return response;
    }

    // ── STORY-063: User Growth ──────────────────────────────────────────

    public override async Task<UserGrowthResponse> GetUserGrowthReport(
        UserGrowthRequest request, ServerCallContext context)
    {
        var report = await _userGrowthService.GetReportAsync(
            request.DateRange?.From?.ToDateTime(),
            request.DateRange?.To?.ToDateTime(),
            string.IsNullOrWhiteSpace(request.Granularity) ? "daily" : request.Granularity,
            context.CancellationToken);

        var response = new UserGrowthResponse
        {
            TotalRegistered = report.TotalRegistered,
            TotalActive = report.TotalActive,
            GrowthRate = report.GrowthRate
        };

        foreach (var dp in report.DataPoints)
        {
            response.DataPoints.Add(new GrowthDataPoint
            {
                Period = dp.Period,
                NewRegistrations = dp.NewRegistrations,
                ActiveUsers = dp.ActiveUsers,
                ChurnedUsers = dp.ChurnedUsers
            });
        }

        return response;
    }

    // ── STORY-064: Merchant Report ──────────────────────────────────────

    public override async Task<MerchantReportResponse> GetMerchantReport(
        MerchantReportRequest request, ServerCallContext context)
    {
        var report = await _merchantReportService.GetReportAsync(
            request.DateRange?.From?.ToDateTime(),
            request.DateRange?.To?.ToDateTime(),
            request.MerchantId,
            context.CancellationToken);

        var response = new MerchantReportResponse
        {
            TotalVolume = new Money { Amount = report.TotalVolume.ToString("F2"), Currency = report.Currency },
            TotalTransactions = report.TotalTransactions
        };

        foreach (var m in report.Merchants)
        {
            response.Merchants.Add(new MerchantMetric
            {
                MerchantId = m.MerchantId,
                BusinessName = m.BusinessName,
                TransactionCount = m.TransactionCount,
                Volume = new Money { Amount = m.Volume.ToString("F2"), Currency = m.Currency },
                Commission = new Money { Amount = m.Commission.ToString("F2"), Currency = m.Currency }
            });
        }

        return response;
    }

    // ── STORY-065: Revenue Report ───────────────────────────────────────

    public override async Task<RevenueReportResponse> GetRevenueReport(
        RevenueReportRequest request, ServerCallContext context)
    {
        var report = await _revenueReportService.GetReportAsync(
            request.DateRange?.From?.ToDateTime(),
            request.DateRange?.To?.ToDateTime(),
            string.IsNullOrWhiteSpace(request.Granularity) ? "daily" : request.Granularity,
            context.CancellationToken);

        var response = new RevenueReportResponse
        {
            TotalRevenue = new Money { Amount = report.TotalRevenue.ToString("F2"), Currency = report.Currency }
        };

        foreach (var dp in report.DataPoints)
        {
            response.DataPoints.Add(new RevenueDataPoint
            {
                Period = dp.Period,
                Revenue = new Money { Amount = dp.Revenue.ToString("F2"), Currency = dp.Currency },
                TransactionCount = dp.TransactionCount
            });
        }

        foreach (var r in report.RevenueByType)
        {
            response.RevenueByType.Add(new RevenueByType
            {
                TransactionType = r.TransactionType,
                Revenue = new Money { Amount = r.Revenue.ToString("F2"), Currency = r.Currency },
                Count = r.Count,
                Percentage = r.Percentage
            });
        }

        return response;
    }

    // ── STORY-066: Reconciliation Report ────────────────────────────────

    public override async Task<ReconReportResponse> GetReconReport(
        ReconReportRequest request, ServerCallContext context)
    {
        var report = await _reconReportService.GetReportAsync(
            request.BatchDate,
            request.PartnerCode,
            context.CancellationToken);

        var response = new ReconReportResponse
        {
            BatchDate = report.BatchDate,
            PartnerCode = report.PartnerCode,
            TotalTransactions = report.TotalTransactions,
            TotalAmount = new Money { Amount = report.TotalAmount.ToString("F2"), Currency = report.Currency },
            MatchedCount = report.MatchedCount,
            UnmatchedCount = report.UnmatchedCount,
            Status = report.Status
        };

        foreach (var d in report.Discrepancies)
        {
            response.Discrepancies.Add(new ReconDiscrepancy
            {
                TransactionReference = d.TransactionReference,
                OurAmount = new Money { Amount = d.OurAmount.ToString("F2"), Currency = d.Currency },
                PartnerAmount = new Money { Amount = d.PartnerAmount.ToString("F2"), Currency = d.Currency },
                DiscrepancyType = d.DiscrepancyType
            });
        }

        return response;
    }

    // ── STORY-067: Export Report (streaming) ────────────────────────────

    public override async Task ExportReport(
        ExportReportRequest request,
        IServerStreamWriter<ExportChunk> responseStream,
        ServerCallContext context)
    {
        var chunks = await _exportService.ExportAsync(
            request.ReportType,
            request.DateRange?.From?.ToDateTime(),
            request.DateRange?.To?.ToDateTime(),
            string.IsNullOrWhiteSpace(request.Format) ? "csv" : request.Format,
            context.CancellationToken);

        foreach (var chunk in chunks)
        {
            await responseStream.WriteAsync(new ExportChunk
            {
                Data = ByteString.CopyFrom(chunk.Data),
                ChunkNumber = chunk.ChunkNumber,
                TotalChunks = chunk.TotalChunks,
                Filename = chunk.Filename,
                ContentType = chunk.ContentType
            }, context.CancellationToken);
        }
    }
}
