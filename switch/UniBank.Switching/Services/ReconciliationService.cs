using Microsoft.Extensions.Logging;
using UniBank.SharedKernel.Results;
using UniBank.Switching.Models;
using UniBank.Switching.Routing;

namespace UniBank.Switching.Services;

/// <summary>
/// Generates daily reconciliation reports by comparing outbound (sent) transactions
/// against inbound (received) transactions. Identifies discrepancies such as missing
/// responses, amount mismatches, and unmatched transactions, then calculates net
/// settlement positions for each institution.
/// </summary>
public sealed class ReconciliationService
{
    private readonly OutboundRouter _outboundRouter;
    private readonly InboundProcessor _inboundProcessor;
    private readonly InstitutionRegistry _institutionRegistry;
    private readonly SettlementFileGenerator _settlementFileGenerator;
    private readonly ILogger<ReconciliationService> _logger;

    public ReconciliationService(
        OutboundRouter outboundRouter,
        InboundProcessor inboundProcessor,
        InstitutionRegistry institutionRegistry,
        SettlementFileGenerator settlementFileGenerator,
        ILogger<ReconciliationService> logger)
    {
        _outboundRouter = outboundRouter;
        _inboundProcessor = inboundProcessor;
        _institutionRegistry = institutionRegistry;
        _settlementFileGenerator = settlementFileGenerator;
        _logger = logger;
    }

    /// <summary>
    /// Generates a reconciliation report for the specified date and institution.
    /// Compares outbound and inbound transactions, identifies discrepancies,
    /// and calculates the net settlement position.
    /// </summary>
    public Result<ReconciliationReport> GenerateReport(DateTime reportDate, string institutionId)
    {
        _logger.LogInformation(
            "Generating reconciliation report for {InstitutionId} on {ReportDate:yyyy-MM-dd}",
            institutionId, reportDate);

        try
        {
            var report = new ReconciliationReport
            {
                ReportDate = reportDate,
                InstitutionId = institutionId
            };

            // Gather outbound transactions for this institution on the given date
            var outboundTxns = _outboundRouter.GetTransactionsByDate(reportDate)
                .Where(t => string.Equals(t.SourceInstitution, institutionId, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(t.DestinationInstitution, institutionId, StringComparison.OrdinalIgnoreCase))
                .ToList();

            // Gather inbound transactions for this institution on the given date
            var inboundTxns = _inboundProcessor.GetTransactionsByDate(reportDate)
                .Where(t => string.Equals(t.SourceInstitution, institutionId, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(t.DestinationInstitution, institutionId, StringComparison.OrdinalIgnoreCase))
                .ToList();

            // Build reconciliation records from outbound transactions
            var outboundRecords = outboundTxns.Select(t => new ReconciliationRecord
            {
                TransactionId = t.TransactionId,
                Direction = TransactionDirection.Outbound,
                SourceInstitution = t.SourceInstitution,
                DestinationInstitution = t.DestinationInstitution,
                Amount = t.Amount,
                Currency = t.Currency,
                ResponseCode = t.ResponseCode,
                Success = t.Success,
                Timestamp = t.Timestamp
            }).ToList();

            // Build reconciliation records from inbound transactions
            var inboundRecords = inboundTxns.Select(t => new ReconciliationRecord
            {
                TransactionId = t.TransactionId,
                Direction = TransactionDirection.Inbound,
                SourceInstitution = t.SourceInstitution,
                DestinationInstitution = t.DestinationInstitution,
                Amount = t.Amount,
                Currency = t.Currency,
                ResponseCode = t.ResponseCode,
                Success = t.Success,
                Timestamp = t.Timestamp
            }).ToList();

            // Perform matching: try to match outbound records with inbound counterparts
            var inboundLookup = inboundRecords
                .GroupBy(r => r.TransactionId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var matchedCount = 0;
            var discrepancies = new List<ReconciliationDiscrepancyDetail>();

            foreach (var outbound in outboundRecords)
            {
                if (inboundLookup.TryGetValue(outbound.TransactionId, out var matchedInbounds))
                {
                    var matched = matchedInbounds.FirstOrDefault(i => !i.Matched);
                    if (matched is not null)
                    {
                        // Check for amount mismatch
                        if (matched.Amount != outbound.Amount)
                        {
                            discrepancies.Add(new ReconciliationDiscrepancyDetail
                            {
                                TransactionId = outbound.TransactionId,
                                Type = DiscrepancyType.AmountMismatch.ToString(),
                                Description = $"Outbound amount {outbound.Amount:F2} does not match inbound amount {matched.Amount:F2}",
                                Amount = Math.Abs(outbound.Amount - matched.Amount),
                                Currency = outbound.Currency
                            });
                        }
                        else
                        {
                            outbound.Matched = true;
                            outbound.MatchedTransactionId = matched.TransactionId;
                            matched.Matched = true;
                            matched.MatchedTransactionId = outbound.TransactionId;
                            matchedCount++;
                        }
                    }
                }
                else if (!outbound.Success)
                {
                    // Failed outbound - expected to have no inbound match
                    discrepancies.Add(new ReconciliationDiscrepancyDetail
                    {
                        TransactionId = outbound.TransactionId,
                        Type = DiscrepancyType.Declined.ToString(),
                        Description = $"Outbound transaction declined with response code {outbound.ResponseCode}",
                        Amount = outbound.Amount,
                        Currency = outbound.Currency
                    });
                }
                else
                {
                    // Successful outbound with no matching inbound response
                    discrepancies.Add(new ReconciliationDiscrepancyDetail
                    {
                        TransactionId = outbound.TransactionId,
                        Type = DiscrepancyType.MissingResponse.ToString(),
                        Description = "Outbound transaction sent but no matching inbound response received",
                        Amount = outbound.Amount,
                        Currency = outbound.Currency
                    });
                }
            }

            // Check for unmatched inbound transactions
            foreach (var inbound in inboundRecords.Where(r => !r.Matched))
            {
                var hasOutbound = outboundRecords.Any(o =>
                    string.Equals(o.TransactionId, inbound.TransactionId, StringComparison.OrdinalIgnoreCase));

                if (!hasOutbound)
                {
                    discrepancies.Add(new ReconciliationDiscrepancyDetail
                    {
                        TransactionId = inbound.TransactionId,
                        Type = DiscrepancyType.UnmatchedInbound.ToString(),
                        Description = "Inbound transaction received with no matching outbound record",
                        Amount = inbound.Amount,
                        Currency = inbound.Currency
                    });
                }
            }

            // Calculate settlement amounts
            var successfulOutbound = outboundRecords
                .Where(r => r.Success &&
                            string.Equals(r.SourceInstitution, institutionId, StringComparison.OrdinalIgnoreCase))
                .Sum(r => r.Amount);

            var successfulInbound = inboundRecords
                .Where(r => r.Success &&
                            string.Equals(r.DestinationInstitution, institutionId, StringComparison.OrdinalIgnoreCase))
                .Sum(r => r.Amount);

            report.TotalSent = outboundRecords.Count;
            report.TotalReceived = inboundRecords.Count;
            report.Matched = matchedCount;
            report.Discrepancies = discrepancies.Count;
            report.TotalOutboundAmount = successfulOutbound;
            report.TotalInboundAmount = successfulInbound;
            report.NetSettlementAmount = successfulOutbound - successfulInbound;
            report.DiscrepancyDetails = discrepancies;
            report.Records = [.. outboundRecords, .. inboundRecords];
            report.Currency = outboundRecords.FirstOrDefault()?.Currency
                              ?? inboundRecords.FirstOrDefault()?.Currency
                              ?? "ZWG";

            _logger.LogInformation(
                "Reconciliation report generated for {InstitutionId}: " +
                "{TotalSent} sent, {TotalReceived} received, {Matched} matched, " +
                "{Discrepancies} discrepancies, net settlement {NetSettlement:F2} {Currency}",
                institutionId, report.TotalSent, report.TotalReceived,
                report.Matched, report.Discrepancies,
                report.NetSettlementAmount, report.Currency);

            return Result.Success(report);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error generating reconciliation report for {InstitutionId} on {ReportDate:yyyy-MM-dd}",
                institutionId, reportDate);
            return Result.Failure<ReconciliationReport>(
                new Error("Reconciliation.GenerateError",
                    $"Failed to generate reconciliation report: {ex.Message}"));
        }
    }

    /// <summary>
    /// Generates reconciliation reports for all registered institutions for the given date
    /// and produces settlement files.
    /// </summary>
    public async Task<Result<List<ReconciliationReport>>> GenerateDailySettlementAsync(
        DateTime settlementDate, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Generating daily settlement for {SettlementDate:yyyy-MM-dd}", settlementDate);

        var reports = new List<ReconciliationReport>();
        var institutions = _institutionRegistry.GetAll();

        foreach (var institution in institutions)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var reportResult = GenerateReport(settlementDate, institution.InstitutionId);
            if (reportResult.IsSuccess)
            {
                reports.Add(reportResult.Value);
            }
            else
            {
                _logger.LogWarning(
                    "Failed to generate report for {InstitutionId}: {Error}",
                    institution.InstitutionId, reportResult.Error.Message);
            }
        }

        // Generate settlement files for each report
        foreach (var report in reports)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var fileResult = await _settlementFileGenerator.GenerateSettlementFileAsync(
                report, cancellationToken);

            if (fileResult.IsFailure)
            {
                _logger.LogWarning(
                    "Failed to generate settlement file for {InstitutionId}: {Error}",
                    report.InstitutionId, fileResult.Error.Message);
            }
        }

        _logger.LogInformation(
            "Daily settlement completed: {ReportCount} reports generated", reports.Count);

        return Result.Success(reports);
    }
}
