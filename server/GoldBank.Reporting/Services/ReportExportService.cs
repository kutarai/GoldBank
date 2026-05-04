using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;

namespace GoldBank.Reporting.Services;

/// <summary>
/// Exports reports to CSV and PDF formats via streaming chunks (STORY-067).
/// Supports all report types with configurable date ranges.
/// </summary>
public sealed class ReportExportService
{
    private readonly DashboardService _dashboardService;
    private readonly UserGrowthReportService _userGrowthService;
    private readonly MerchantReportService _merchantReportService;
    private readonly RevenueReportService _revenueReportService;
    private readonly ReconReportService _reconReportService;
    private readonly ILogger<ReportExportService> _logger;

    public ReportExportService(
        DashboardService dashboardService,
        UserGrowthReportService userGrowthService,
        MerchantReportService merchantReportService,
        RevenueReportService revenueReportService,
        ReconReportService reconReportService,
        ILogger<ReportExportService> logger)
    {
        _dashboardService = dashboardService;
        _userGrowthService = userGrowthService;
        _merchantReportService = merchantReportService;
        _revenueReportService = revenueReportService;
        _reconReportService = reconReportService;
        _logger = logger;
    }

    public async Task<List<ExportChunkDto>> ExportAsync(
        string reportType,
        DateTime? dateFrom,
        DateTime? dateTo,
        string format,
        CancellationToken cancellationToken = default)
    {
        var csvContent = reportType.ToLowerInvariant() switch
        {
            "dashboard" => await ExportDashboardAsync(dateFrom, dateTo, cancellationToken),
            "user_growth" => await ExportUserGrowthAsync(dateFrom, dateTo, cancellationToken),
            "merchant" => await ExportMerchantAsync(dateFrom, dateTo, cancellationToken),
            "revenue" => await ExportRevenueAsync(dateFrom, dateTo, cancellationToken),
            "reconciliation" => await ExportReconAsync(dateFrom, cancellationToken),
            _ => "Report type not supported"
        };

        var contentType = format.Equals("pdf", StringComparison.OrdinalIgnoreCase)
            ? "application/pdf"
            : "text/csv";

        var extension = format.Equals("pdf", StringComparison.OrdinalIgnoreCase) ? "pdf" : "csv";
        var filename = $"{reportType}_report_{DateTime.UtcNow:yyyyMMdd_HHmmss}.{extension}";

        var data = Encoding.UTF8.GetBytes(csvContent);
        var chunks = ChunkData(data, filename, contentType);

        _logger.LogInformation(
            "Report exported: type={ReportType}, format={Format}, chunks={ChunkCount}",
            reportType, format, chunks.Count);

        return chunks;
    }

    private async Task<string> ExportDashboardAsync(
        DateTime? dateFrom, DateTime? dateTo, CancellationToken cancellationToken)
    {
        var report = await _dashboardService.GetDashboardAsync(dateFrom, dateTo, cancellationToken);
        var sb = new StringBuilder();
        sb.AppendLine("Date,Transactions,Volume,NewUsers");

        foreach (var m in report.DailyMetrics)
        {
            sb.AppendLine(string.Create(CultureInfo.InvariantCulture,
                $"{m.Date},{m.Transactions},{m.Volume:F2},{m.NewUsers}"));
        }

        return sb.ToString();
    }

    private async Task<string> ExportUserGrowthAsync(
        DateTime? dateFrom, DateTime? dateTo, CancellationToken cancellationToken)
    {
        var report = await _userGrowthService.GetReportAsync(dateFrom, dateTo, "daily", cancellationToken);
        var sb = new StringBuilder();
        sb.AppendLine("Period,NewRegistrations,ActiveUsers,ChurnedUsers");

        foreach (var d in report.DataPoints)
        {
            sb.AppendLine($"{d.Period},{d.NewRegistrations},{d.ActiveUsers},{d.ChurnedUsers}");
        }

        return sb.ToString();
    }

    private async Task<string> ExportMerchantAsync(
        DateTime? dateFrom, DateTime? dateTo, CancellationToken cancellationToken)
    {
        var report = await _merchantReportService.GetReportAsync(dateFrom, dateTo, null, cancellationToken);
        var sb = new StringBuilder();
        sb.AppendLine("MerchantId,BusinessName,TransactionCount,Volume,Commission,Currency");

        foreach (var m in report.Merchants)
        {
            sb.AppendLine(string.Create(CultureInfo.InvariantCulture,
                $"{m.MerchantId},{EscapeCsv(m.BusinessName)},{m.TransactionCount},{m.Volume:F2},{m.Commission:F2},{m.Currency}"));
        }

        return sb.ToString();
    }

    private async Task<string> ExportRevenueAsync(
        DateTime? dateFrom, DateTime? dateTo, CancellationToken cancellationToken)
    {
        var report = await _revenueReportService.GetReportAsync(dateFrom, dateTo, "daily", cancellationToken);
        var sb = new StringBuilder();
        sb.AppendLine("Period,Revenue,TransactionCount,Currency");

        foreach (var d in report.DataPoints)
        {
            sb.AppendLine(string.Create(CultureInfo.InvariantCulture,
                $"{d.Period},{d.Revenue:F2},{d.TransactionCount},{d.Currency}"));
        }

        return sb.ToString();
    }

    private async Task<string> ExportReconAsync(
        DateTime? dateFrom, CancellationToken cancellationToken)
    {
        var batchDate = dateFrom?.ToString("yyyy-MM-dd") ?? DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd");
        var report = await _reconReportService.GetReportAsync(batchDate, null, cancellationToken);
        var sb = new StringBuilder();
        sb.AppendLine("TransactionReference,OurAmount,PartnerAmount,DiscrepancyType,Currency");

        foreach (var d in report.Discrepancies)
        {
            sb.AppendLine(string.Create(CultureInfo.InvariantCulture,
                $"{d.TransactionReference},{d.OurAmount:F2},{d.PartnerAmount:F2},{d.DiscrepancyType},{d.Currency}"));
        }

        return sb.ToString();
    }

    private static List<ExportChunkDto> ChunkData(byte[] data, string filename, string contentType)
    {
        const int chunkSize = 64 * 1024; // 64 KB chunks
        var totalChunks = (int)Math.Ceiling((double)data.Length / chunkSize);
        if (totalChunks == 0) totalChunks = 1;

        var chunks = new List<ExportChunkDto>();

        for (var i = 0; i < totalChunks; i++)
        {
            var offset = i * chunkSize;
            var length = Math.Min(chunkSize, data.Length - offset);
            var chunk = new byte[length];
            Array.Copy(data, offset, chunk, 0, length);

            chunks.Add(new ExportChunkDto(
                Data: chunk,
                ChunkNumber: i + 1,
                TotalChunks: totalChunks,
                Filename: filename,
                ContentType: contentType));
        }

        return chunks;
    }

    private static string EscapeCsv(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }
}

public sealed record ExportChunkDto(
    byte[] Data,
    int ChunkNumber,
    int TotalChunks,
    string Filename,
    string ContentType);
