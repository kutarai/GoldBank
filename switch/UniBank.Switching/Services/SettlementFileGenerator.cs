using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;
using UniBank.SharedKernel.Results;
using UniBank.Switching.Models;

namespace UniBank.Switching.Services;

/// <summary>
/// Generates settlement files in a standard delimited format for the national
/// payment switch reconciliation process. Files include header records, transaction
/// details, and trailer summaries suitable for exchange between institutions.
/// </summary>
public sealed class SettlementFileGenerator
{
    private readonly ILogger<SettlementFileGenerator> _logger;
    private readonly string _outputDirectory;

    public SettlementFileGenerator(ILogger<SettlementFileGenerator> logger)
    {
        _logger = logger;
        _outputDirectory = Path.Combine(AppContext.BaseDirectory, "settlement");
    }

    /// <summary>
    /// Generates a settlement file for the given reconciliation report.
    /// The file is written to the configured output directory and the full path is returned.
    /// </summary>
    public async Task<Result<string>> GenerateSettlementFileAsync(
        ReconciliationReport report, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(report);

        try
        {
            Directory.CreateDirectory(_outputDirectory);

            var fileName = $"SETTLE_{report.InstitutionId}_{report.ReportDate:yyyyMMdd}_{report.ReportId[..8]}.csv";
            var filePath = Path.Combine(_outputDirectory, fileName);

            var content = GenerateFileContent(report);

            await File.WriteAllTextAsync(filePath, content, Encoding.UTF8, cancellationToken);

            _logger.LogInformation(
                "Settlement file generated: {FilePath} ({LineCount} records)",
                filePath, report.Records.Count);

            return Result.Success(filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate settlement file for {InstitutionId}", report.InstitutionId);
            return Result.Failure<string>(
                new Error("Settlement.FileError", $"Failed to generate settlement file: {ex.Message}"));
        }
    }

    /// <summary>
    /// Generates the content of a settlement file as a string (without writing to disk).
    /// Useful for returning the content via gRPC or API responses.
    /// </summary>
    public Result<string> GenerateFileContentResult(ReconciliationReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        try
        {
            return Result.Success(GenerateFileContent(report));
        }
        catch (Exception ex)
        {
            return Result.Failure<string>(
                new Error("Settlement.ContentError", $"Failed to generate settlement content: {ex.Message}"));
        }
    }

    /// <summary>
    /// Generates the settlement file content with header, detail records, discrepancy
    /// section, and trailer summary.
    /// </summary>
    private static string GenerateFileContent(ReconciliationReport report)
    {
        var sb = new StringBuilder(4096);
        var ci = CultureInfo.InvariantCulture;

        // === File Header ===
        sb.AppendLine("H,SETTLEMENT_FILE,1.0");
        sb.AppendLine(ci, $"H,REPORT_ID,{report.ReportId}");
        sb.AppendLine(ci, $"H,INSTITUTION,{report.InstitutionId}");
        sb.AppendLine(ci, $"H,REPORT_DATE,{report.ReportDate:yyyy-MM-dd}");
        sb.AppendLine(ci, $"H,GENERATED_AT,{report.GeneratedAt:yyyy-MM-ddTHH:mm:ssZ}");
        sb.AppendLine(ci, $"H,CURRENCY,{report.Currency}");
        sb.AppendLine();

        // === Transaction Detail Header ===
        sb.AppendLine("D,TRANSACTION_ID,DIRECTION,SOURCE,DESTINATION,AMOUNT,CURRENCY,RESPONSE_CODE,SUCCESS,MATCHED,TIMESTAMP");

        // === Transaction Detail Records ===
        foreach (var record in report.Records.OrderBy(r => r.Timestamp))
        {
            sb.Append("D,");
            sb.Append(record.TransactionId);
            sb.Append(',');
            sb.Append(record.Direction);
            sb.Append(',');
            sb.Append(record.SourceInstitution);
            sb.Append(',');
            sb.Append(record.DestinationInstitution);
            sb.Append(',');
            sb.Append(record.Amount.ToString("F2", ci));
            sb.Append(',');
            sb.Append(record.Currency);
            sb.Append(',');
            sb.Append(record.ResponseCode);
            sb.Append(',');
            sb.Append(record.Success ? "Y" : "N");
            sb.Append(',');
            sb.Append(record.Matched ? "Y" : "N");
            sb.Append(',');
            sb.AppendLine(record.Timestamp.ToString("yyyy-MM-ddTHH:mm:ss", ci));
        }

        sb.AppendLine();

        // === Discrepancy Section ===
        if (report.DiscrepancyDetails.Count > 0)
        {
            sb.AppendLine("X,TRANSACTION_ID,TYPE,DESCRIPTION,AMOUNT,CURRENCY");

            foreach (var disc in report.DiscrepancyDetails)
            {
                sb.Append("X,");
                sb.Append(disc.TransactionId);
                sb.Append(',');
                sb.Append(disc.Type);
                sb.Append(',');
                sb.Append(EscapeCsvField(disc.Description));
                sb.Append(',');
                sb.Append(disc.Amount.ToString("F2", ci));
                sb.Append(',');
                sb.AppendLine(disc.Currency);
            }

            sb.AppendLine();
        }

        // === Trailer Summary ===
        sb.AppendLine("T,SUMMARY");
        sb.AppendLine(ci, $"T,TOTAL_SENT,{report.TotalSent}");
        sb.AppendLine(ci, $"T,TOTAL_RECEIVED,{report.TotalReceived}");
        sb.AppendLine(ci, $"T,MATCHED,{report.Matched}");
        sb.AppendLine(ci, $"T,DISCREPANCIES,{report.Discrepancies}");
        sb.AppendLine(ci, $"T,TOTAL_OUTBOUND_AMOUNT,{report.TotalOutboundAmount.ToString("F2", ci)}");
        sb.AppendLine(ci, $"T,TOTAL_INBOUND_AMOUNT,{report.TotalInboundAmount.ToString("F2", ci)}");
        sb.AppendLine(ci, $"T,NET_SETTLEMENT,{report.NetSettlementAmount.ToString("F2", ci)}");
        sb.AppendLine(ci, $"T,CURRENCY,{report.Currency}");

        // Net position explanation
        if (report.NetSettlementAmount > 0)
        {
            sb.AppendLine(ci, $"T,POSITION,DEBIT,{report.InstitutionId} owes {report.NetSettlementAmount.ToString("F2", ci)} {report.Currency}");
        }
        else if (report.NetSettlementAmount < 0)
        {
            sb.AppendLine(ci, $"T,POSITION,CREDIT,{report.InstitutionId} is owed {Math.Abs(report.NetSettlementAmount).ToString("F2", ci)} {report.Currency}");
        }
        else
        {
            sb.AppendLine("T,POSITION,BALANCED,No net settlement required");
        }

        sb.AppendLine(ci, $"T,RECORD_COUNT,{report.Records.Count}");
        sb.AppendLine("T,END_OF_FILE");

        return sb.ToString();
    }

    /// <summary>
    /// Escapes a CSV field value by quoting it if it contains commas, quotes, or newlines.
    /// </summary>
    private static string EscapeCsvField(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }
}
