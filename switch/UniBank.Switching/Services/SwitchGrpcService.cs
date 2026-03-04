using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using UniBank.Protos.Switching;
using UniBank.Switching.Models;
using UniBank.Switching.Routing;

namespace UniBank.Switching.Services;

/// <summary>
/// gRPC service implementation for the national payment switch. The Core module
/// and other internal services call this to route outbound transactions and
/// query transaction status and reconciliation reports.
/// </summary>
public sealed class SwitchGrpcService : SwitchingService.SwitchingServiceBase
{
    private readonly OutboundRouter _outboundRouter;
    private readonly ReconciliationService _reconciliationService;
    private readonly ILogger<SwitchGrpcService> _logger;

    public SwitchGrpcService(
        OutboundRouter outboundRouter,
        ReconciliationService reconciliationService,
        ILogger<SwitchGrpcService> logger)
    {
        _outboundRouter = outboundRouter;
        _reconciliationService = reconciliationService;
        _logger = logger;
    }

    /// <summary>
    /// Routes an outbound transaction through the national payment switch.
    /// Called by the Core module when processing inter-bank transfers.
    /// </summary>
    public override async Task<OutboundTransactionResponse> RouteOutboundTransaction(
        OutboundTransactionRequest request, ServerCallContext context)
    {
        _logger.LogInformation(
            "Received outbound transaction request {TransactionId} from {Source} to {Destination}",
            request.TransactionId, request.SourceInstitution, request.DestinationInstitution);

        var canonical = new CanonicalMessage
        {
            TransactionId = request.TransactionId,
            SourceInstitution = request.SourceInstitution,
            DestinationInstitution = request.DestinationInstitution,
            DebitAccount = request.DebitAccount,
            CreditAccount = request.CreditAccount,
            Amount = decimal.TryParse(request.Amount?.Amount, out var amt) ? amt : 0m,
            Currency = request.Amount?.Currency ?? "ZWG",
            Reference = request.Reference,
            MessageType = MapMessageType(request.MessageType),
            Timestamp = DateTime.UtcNow
        };

        // Copy additional data
        foreach (var kvp in request.AdditionalData)
        {
            canonical.AdditionalData[kvp.Key] = kvp.Value;
        }

        var result = await _outboundRouter.RouteTransactionAsync(canonical, context.CancellationToken);

        if (result.IsFailure)
        {
            _logger.LogWarning(
                "Outbound transaction {TransactionId} failed: {Error}",
                request.TransactionId, result.Error.Message);

            return new OutboundTransactionResponse
            {
                Success = false,
                Message = result.Error.Message,
                TransactionId = request.TransactionId,
                ResponseCode = "96" // System malfunction
            };
        }

        var txResult = result.Value;

        return new OutboundTransactionResponse
        {
            Success = txResult.ResponseCode == "00",
            Message = txResult.ResponseCode == "00" ? "Transaction approved" : $"Declined: {txResult.ResponseCode}",
            TransactionId = txResult.TransactionId,
            SwitchReference = txResult.SwitchReference,
            ResponseCode = txResult.ResponseCode,
            AuthorizationCode = txResult.AuthorizationCode,
            ProcessedAt = Timestamp.FromDateTime(DateTime.SpecifyKind(txResult.ProcessedAt, DateTimeKind.Utc))
        };
    }

    /// <summary>
    /// Returns the current status of a transaction routed through the switch.
    /// </summary>
    public override Task<TransactionStatusResponse> GetTransactionStatus(
        TransactionStatusRequest request, ServerCallContext context)
    {
        _logger.LogInformation("Status query for transaction {TransactionId}", request.TransactionId);

        var log = _outboundRouter.GetTransactionLog();
        if (log.TryGetValue(request.TransactionId, out var entry))
        {
            return Task.FromResult(new TransactionStatusResponse
            {
                Success = true,
                TransactionId = entry.TransactionId,
                Status = entry.Success ? "COMPLETED" : "FAILED",
                ResponseCode = entry.ResponseCode,
                Message = entry.Message,
                LastUpdated = Timestamp.FromDateTime(DateTime.SpecifyKind(entry.Timestamp, DateTimeKind.Utc))
            });
        }

        return Task.FromResult(new TransactionStatusResponse
        {
            Success = false,
            TransactionId = request.TransactionId,
            Status = "NOT_FOUND",
            Message = "Transaction not found in switch logs."
        });
    }

    /// <summary>
    /// Generates a reconciliation report for the specified date and institution.
    /// </summary>
    public override Task<ReconciliationReportResponse> GetReconciliationReport(
        ReconciliationReportRequest request, ServerCallContext context)
    {
        _logger.LogInformation(
            "Reconciliation report requested for institution {InstitutionId}",
            request.InstitutionId);

        var reportDate = request.ReportDate?.ToDateTime() ?? DateTime.UtcNow.Date;

        var report = _reconciliationService.GenerateReport(reportDate, request.InstitutionId);

        if (report.IsFailure)
        {
            return Task.FromResult(new ReconciliationReportResponse
            {
                Success = false
            });
        }

        var r = report.Value;
        var response = new ReconciliationReportResponse
        {
            Success = true,
            ReportId = r.ReportId,
            ReportDate = Timestamp.FromDateTime(DateTime.SpecifyKind(r.ReportDate, DateTimeKind.Utc)),
            TotalSent = r.TotalSent,
            TotalReceived = r.TotalReceived,
            Matched = r.Matched,
            Discrepancies = r.Discrepancies,
            NetSettlementAmount = r.NetSettlementAmount.ToString("F2"),
            Currency = r.Currency
        };

        foreach (var disc in r.DiscrepancyDetails)
        {
            response.DiscrepancyDetails.Add(new ReconciliationDiscrepancy
            {
                TransactionId = disc.TransactionId,
                Type = disc.Type,
                Description = disc.Description,
                Amount = disc.Amount.ToString("F2"),
                Currency = disc.Currency
            });
        }

        return Task.FromResult(response);
    }

    private static CanonicalMessageType MapMessageType(string messageType)
    {
        return messageType.ToUpperInvariant() switch
        {
            "AUTHORIZATION" => CanonicalMessageType.AuthorizationRequest,
            "REVERSAL" => CanonicalMessageType.ReversalRequest,
            "STATUS" => CanonicalMessageType.StatusReport,
            _ => CanonicalMessageType.FinancialRequest
        };
    }
}
