using System.Diagnostics;
using Microsoft.Extensions.Logging;
using SynergySwitch.Core.Gateway;
using SynergySwitch.Core.Iso20022;
using SynergySwitch.Core.Iso20022Bank;
using SynergySwitch.Core.Models;
using SynergySwitch.Data.Entities;

namespace SynergySwitch.Core.Iso8583;

/// <summary>
/// Handles the full authorisation flow with BIN-based gateway routing:
/// 1. Route PAN to the correct gateway via BIN prefix table
/// 2. Detect gateway protocol (ISO 8583 TCP or ISO 20022 gRPC)
/// 3. Send to the selected gateway using the appropriate client
/// 4. Parse response back to domain model
///
/// Falls back to offline approval when per-gateway OfflineMode is enabled.
/// </summary>
public class BankAuthorisationService
{
    private readonly GatewayConnectionPool _connectionPool;
    private readonly Iso20022GrpcClient _iso20022Client;
    private readonly ILogger<BankAuthorisationService> _logger;

    public BankAuthorisationService(
        GatewayConnectionPool connectionPool,
        Iso20022GrpcClient iso20022Client,
        ILogger<BankAuthorisationService> logger)
    {
        _connectionPool = connectionPool;
        _iso20022Client = iso20022Client;
        _logger = logger;
    }

    public async Task<AuthorisationResponse> AuthoriseAsync(
        AuthorisationRequest request,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        // ── Route by BIN ──
        var gateway = GatewayManager.RouteByPan(request.Pan);
        if (gateway == null)
        {
            _logger.LogWarning("No gateway available for PAN=...{Last4}",
                request.Pan.Length >= 4 ? request.Pan[^4..] : "****");
            sw.Stop();
            SwitchMetrics.ObserveTransactionDuration(sw.Elapsed.TotalSeconds, "TechnicalError");
            SwitchMetrics.RecordTransaction("TechnicalError", request.CardEntryMode, request.Currency);
            return new AuthorisationResponse
            {
                ExchangeId = request.ExchangeId,
                TransactionReference = request.TransactionReference,
                ResponseCode = AuthorisationResponseCode.TechnicalError,
                ResponseReason = "0091",
                EmvResponseCode = ResponseCodeMapper.ToEmvTag8A("0091"),
                DisplayMessage = "No gateway available"
            };
        }

        // ── Per-gateway offline mode ──
        if (gateway.OfflineMode)
        {
            _logger.LogInformation(
                "Gateway '{Gateway}' OfflineMode: approving locally for PAN=...{Last4}",
                gateway.Name, request.Pan.Length >= 4 ? request.Pan[^4..] : "****");
            var offlineResponse = BuildOfflineApproval(request);
            sw.Stop();
            SwitchMetrics.ObserveTransactionDuration(sw.Elapsed.TotalSeconds, "Approved");
            SwitchMetrics.RecordTransaction("Approved", request.CardEntryMode, request.Currency);
            SwitchMetrics.RecordGatewayTransaction(gateway.Name, "Approved");
            return offlineResponse;
        }

        try
        {
            AuthorisationResponse response;

            // ── Route by protocol ──
            if (gateway.Protocol == GatewayProtocol.Iso20022Grpc)
            {
                _logger.LogInformation(
                    "[ISO20022] Routing PAN=...{Last4} to gateway '{Gateway}' ({Host}:{Port})",
                    request.Pan.Length >= 4 ? request.Pan[^4..] : "****",
                    gateway.Name, gateway.Host, gateway.Port);

                response = await _iso20022Client.AuthoriseAsync(gateway, request, ct);
            }
            else
            {
                // Default: ISO 8583 over TCP
                var gwSettings = new BankConnectionSettings
                {
                    Host = gateway.Host,
                    Port = gateway.Port,
                    AcquiringInstitutionId = gateway.AcquiringInstitutionId,
                    NetworkId = gateway.NetworkId,
                    TimeoutSeconds = gateway.TimeoutSeconds,
                    SendLengthHeader = gateway.SendLengthHeader
                };

                var isoRequest = Iso8583MessageBuilder.BuildAuthorisationRequest(request, gwSettings);

                _logger.LogInformation(
                    "[ISO8583] Routing PAN=...{Last4} to gateway '{Gateway}' ({Host}:{Port})",
                    request.Pan.Length >= 4 ? request.Pan[^4..] : "****",
                    gateway.Name, gateway.Host, gateway.Port);

                var isoResponse = await _connectionPool.SendAuthorisationAsync(gateway, isoRequest, ct);
                response = Iso8583ResponseParser.Parse(isoResponse, request);
            }

            sw.Stop();
            SwitchMetrics.ObserveTransactionDuration(sw.Elapsed.TotalSeconds, response.ResponseCode.ToString());
            SwitchMetrics.RecordTransaction(response.ResponseCode.ToString(), request.CardEntryMode, request.Currency);
            SwitchMetrics.RecordGatewayTransaction(gateway.Name, response.ResponseCode.ToString());
            SwitchMetrics.ObserveGatewayRoundTrip(gateway.Name, sw.Elapsed.TotalSeconds);

            _logger.LogInformation(
                "Gateway '{Gateway}' [{Protocol}] response: {ResponseCode}, authCode={AuthCode}, elapsed={Elapsed:F0}ms",
                gateway.Name, gateway.Protocol, response.ResponseCode,
                response.AuthorisationCode ?? "N/A", sw.Elapsed.TotalMilliseconds);

            return response;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            sw.Stop();
            SwitchMetrics.ObserveTransactionDuration(sw.Elapsed.TotalSeconds, "TechnicalError");
            SwitchMetrics.RecordTransaction("TechnicalError", request.CardEntryMode, request.Currency);
            _logger.LogError(ex, "Gateway '{Gateway}' [{Protocol}] failed after {Elapsed:F0}ms",
                gateway.Name, gateway.Protocol, sw.Elapsed.TotalMilliseconds);

            return new AuthorisationResponse
            {
                ExchangeId = request.ExchangeId,
                TransactionReference = request.TransactionReference,
                ResponseCode = AuthorisationResponseCode.TechnicalError,
                ResponseReason = "0091",
                AuthorisationCode = null,
                EmvResponseCode = ResponseCodeMapper.ToEmvTag8A("0091"),
                DisplayMessage = $"Gateway '{gateway.Name}' unavailable"
            };
        }
    }

    private static AuthorisationResponse BuildOfflineApproval(AuthorisationRequest request)
    {
        return new AuthorisationResponse
        {
            ExchangeId = request.ExchangeId,
            TransactionReference = request.TransactionReference,
            ResponseCode = AuthorisationResponseCode.Approved,
            ResponseReason = "0000",
            AuthorisationCode = ResponseCodeMapper.GenerateAuthCode(),
            EmvResponseCode = ResponseCodeMapper.ToEmvTag8A("0000"),
            DisplayMessage = "Approved"
        };
    }
}
