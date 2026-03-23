using System.Collections.Concurrent;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.Extensions.Logging;
using SynergySwitch.BankProtos.CardTransactions;
using SynergySwitch.BankProtos.Common;
using SynergySwitch.Core.Gateway;
using SynergySwitch.Core.Iso8583;
using SynergySwitch.Core.Iso20022;
using SynergySwitch.Core.Models;

namespace SynergySwitch.Core.Iso20022Bank;

/// <summary>
/// Sends authorisation requests to banks that use ISO 20022 over gRPC.
/// Manages gRPC channels per gateway with continuous connectivity monitoring
/// via <see cref="GrpcChannel.WaitForStateChangedAsync"/>.
/// </summary>
public class Iso20022GrpcClient
{
    private readonly ILogger<Iso20022GrpcClient> _logger;
    private readonly ConcurrentDictionary<int, ManagedChannel> _channels = new();

    public Iso20022GrpcClient(ILogger<Iso20022GrpcClient> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Send an authorisation to an ISO 20022 gRPC bank gateway.
    /// </summary>
    public async Task<AuthorisationResponse> AuthoriseAsync(
        CachedGateway gateway,
        AuthorisationRequest request,
        CancellationToken ct = default)
    {
        var managed = GetOrCreateManagedChannel(gateway);
        var client = new CardTransactionService.CardTransactionServiceClient(managed.Channel);

        var bankRequest = MapToBankRequest(request, gateway);

        _logger.LogInformation(
            "[ISO20022] Sending to '{Gateway}' ({Host}:{Port}): PAN=...{Last4}, amount={Amount} {Currency}",
            gateway.Name, gateway.Host, gateway.Port,
            request.Pan.Length >= 4 ? request.Pan[^4..] : "****",
            request.Amount, request.Currency);

        try
        {
            SwitchMetrics.RecordIso20022Sent(gateway.Name, "ProcessPurchase");

            var bankResponse = await client.ProcessPurchaseAsync(
                bankRequest,
                deadline: DateTime.UtcNow.AddSeconds(gateway.TimeoutSeconds),
                cancellationToken: ct);

            SwitchMetrics.RecordIso20022Received(gateway.Name, bankResponse.ResponseCode?.Trim() ?? "??");

            return MapFromBankResponse(bankResponse, request);
        }
        catch (RpcException ex)
        {
            _logger.LogError(ex, "[ISO20022] gRPC error from '{Gateway}': {Status}",
                gateway.Name, ex.StatusCode);

            SwitchMetrics.RecordConnectionError("iso20022_grpc_error");

            return new AuthorisationResponse
            {
                ExchangeId = request.ExchangeId,
                TransactionReference = request.TransactionReference,
                ResponseCode = AuthorisationResponseCode.TechnicalError,
                ResponseReason = "0091",
                EmvResponseCode = ResponseCodeMapper.ToEmvTag8A("0091"),
                DisplayMessage = $"Bank unavailable — {ex.StatusCode}"
            };
        }
    }

    /// <summary>
    /// Whether the gateway's gRPC channel is connected (Ready or Idle).
    /// </summary>
    public bool IsChannelActive(int gatewayId)
    {
        if (!_channels.TryGetValue(gatewayId, out var managed))
            return false;
        var state = managed.Channel.State;
        return state is ConnectivityState.Ready or ConnectivityState.Idle;
    }

    /// <summary>
    /// Proactively connect the gRPC channel and start the state-change watcher.
    /// Called from the health loop for each enabled ISO 20022 gateway.
    /// </summary>
    public async Task ConnectAsync(CachedGateway gateway, CancellationToken ct = default)
    {
        try
        {
            var managed = GetOrCreateManagedChannel(gateway);

            // Request a connection if currently Idle
            if (managed.Channel.State == ConnectivityState.Idle)
            {
                managed.Channel.ConnectAsync(ct).Ignore();
            }

            // Wait briefly for it to reach Ready
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(5));

            await managed.Channel.WaitForStateChangedAsync(ConnectivityState.Idle, cts.Token);

            var newState = managed.Channel.State;
            _logger.LogInformation("[ISO20022] Channel for '{Gateway}' ({Host}:{Port}) → {State}",
                gateway.Name, gateway.Host, gateway.Port, newState);
        }
        catch (OperationCanceledException) { /* timeout or shutdown — ok */ }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[ISO20022] Connect failed for '{Gateway}' ({Host}:{Port})",
                gateway.Name, gateway.Host, gateway.Port);
        }
    }

    public void RemoveChannel(int gatewayId)
    {
        if (_channels.TryRemove(gatewayId, out var managed))
        {
            managed.Dispose();
        }
    }

    // ── Channel management with continuous state monitoring ──

    private ManagedChannel GetOrCreateManagedChannel(CachedGateway gateway)
    {
        return _channels.GetOrAdd(gateway.Id, _ =>
        {
            var address = $"http://{gateway.Host}:{gateway.Port}";
            _logger.LogInformation("[ISO20022] Creating gRPC channel to {Address}", address);

            var channel = GrpcChannel.ForAddress(address, new GrpcChannelOptions
            {
                MaxRetryAttempts = 2,
                HttpHandler = new SocketsHttpHandler
                {
                    EnableMultipleHttp2Connections = true,
                    PooledConnectionIdleTimeout = TimeSpan.FromMinutes(5),
                    KeepAlivePingDelay = TimeSpan.FromSeconds(15),
                    KeepAlivePingTimeout = TimeSpan.FromSeconds(5)
                }
            });

            var managed = new ManagedChannel(channel, gateway.Id, gateway.Name);

            // Start background state watcher
            Task.Run(() => WatchStateAsync(managed));

            return managed;
        });
    }

    /// <summary>
    /// Continuously monitors channel state transitions via WaitForStateChangedAsync.
    /// Logs connectivity changes and records metrics for drops/reconnects.
    /// </summary>
    private async Task WatchStateAsync(ManagedChannel managed)
    {
        var lastState = managed.Channel.State;
        _logger.LogInformation("[ISO20022] Watcher started for '{Gateway}', initial state={State}",
            managed.GatewayName, lastState);

        while (!managed.Disposed)
        {
            try
            {
                var currentState = managed.Channel.State;

                // Wait for the state to change (blocks until it does)
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                await managed.Channel.WaitForStateChangedAsync(currentState, cts.Token);

                var newState = managed.Channel.State;

                if (newState != lastState)
                {
                    _logger.LogInformation(
                        "[ISO20022] '{Gateway}' state changed: {OldState} → {NewState}",
                        managed.GatewayName, lastState, newState);

                    SwitchMetrics.RecordStateTransition(
                        managed.GatewayName, lastState.ToString(), newState.ToString());

                    // Update connectivity gauge
                    var connectivityValue = newState switch
                    {
                        ConnectivityState.Ready => 1.0,
                        ConnectivityState.Connecting => 0.5,
                        ConnectivityState.Idle => 0.5,
                        _ => 0.0
                    };
                    SwitchMetrics.SetGatewayConnectivity(managed.GatewayName, "ISO20022", connectivityValue);

                    // Detect drops
                    if (lastState == ConnectivityState.Ready &&
                        newState is ConnectivityState.TransientFailure or ConnectivityState.Idle or ConnectivityState.Shutdown)
                    {
                        _logger.LogWarning("[ISO20022] '{Gateway}' connection DROPPED ({OldState} → {NewState})",
                            managed.GatewayName, lastState, newState);
                        SwitchMetrics.RecordConnectionDropDetected(managed.GatewayName, "ISO20022");
                        SwitchMetrics.RecordConnectionError("iso20022_connection_drop");
                    }

                    // Detect reconnects
                    if (lastState is ConnectivityState.TransientFailure or ConnectivityState.Connecting
                        && newState == ConnectivityState.Ready)
                    {
                        _logger.LogInformation("[ISO20022] '{Gateway}' RECONNECTED", managed.GatewayName);
                        SwitchMetrics.RecordReconnectDetected(managed.GatewayName, "ISO20022");
                    }

                    lastState = newState;
                }

                // If in TransientFailure, trigger a reconnect attempt
                if (newState == ConnectivityState.TransientFailure || newState == ConnectivityState.Idle)
                {
                    managed.Channel.ConnectAsync().Ignore();
                }

                if (newState == ConnectivityState.Shutdown)
                    break;
            }
            catch (OperationCanceledException)
            {
                // Timeout on WaitForStateChangedAsync — no state change in 30s, loop and check again
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[ISO20022] Watcher error for '{Gateway}'", managed.GatewayName);
                await Task.Delay(1000);
            }
        }

        _logger.LogInformation("[ISO20022] Watcher stopped for '{Gateway}'", managed.GatewayName);
    }

    // ── Mapping: Domain → Bank PurchaseRequest ──

    private static PurchaseRequest MapToBankRequest(AuthorisationRequest request, CachedGateway gateway)
    {
        var amountDecimal = (request.Amount / 100m).ToString("F2");
        var currency = MapCurrencyNumericToAlpha(request.Currency);

        return new PurchaseRequest
        {
            TransactionId = request.ExchangeId,
            CardHolderAccount = request.Pan,
            MerchantId = request.MerchantId ?? "",
            MerchantName = request.MerchantName ?? "Synergy Merchant",
            TerminalId = request.TerminalId ?? "",
            Amount = new Money { Amount = amountDecimal, Currency = currency },
            ProcessingCode = "000000",
            SourceInstitution = gateway.AcquiringInstitutionId,
            AcquiringInstitution = gateway.AcquiringInstitutionId,
            Stan = request.TransactionReference?.PadRight(12)[..Math.Min(12, request.TransactionReference?.Length ?? 0)] ?? "",
            RetrievalReference = request.TransactionReference ?? "",
            IsOnUs = false,
            TenantId = ""
        };
    }

    // ── Mapping: Bank CardTransactionResponse → Domain ──

    private static AuthorisationResponse MapFromBankResponse(
        CardTransactionResponse bankResponse,
        AuthorisationRequest originalRequest)
    {
        var code = bankResponse.ResponseCode?.Trim() ?? "96";
        var reasonCode4 = code.Length == 2 ? $"00{code}" : code.PadRight(4, '0')[..4];

        var domainResponseCode = code == "00"
            ? AuthorisationResponseCode.Approved
            : (code is "91" or "96" or "28" or "68" or "90" or "92" or "95" or "98"
                ? AuthorisationResponseCode.TechnicalError
                : AuthorisationResponseCode.Declined);

        var displayMessage = !string.IsNullOrEmpty(bankResponse.Message)
            ? bankResponse.Message
            : ResponseCodeMapper.GetDisplayMessage(reasonCode4);

        return new AuthorisationResponse
        {
            ExchangeId = originalRequest.ExchangeId,
            TransactionReference = originalRequest.TransactionReference,
            ResponseCode = domainResponseCode,
            ResponseReason = reasonCode4,
            AuthorisationCode = bankResponse.Success ? bankResponse.AuthorizationCode : null,
            EmvResponseCode = ResponseCodeMapper.ToEmvTag8A(reasonCode4),
            DisplayMessage = displayMessage
        };
    }

    private static string MapCurrencyNumericToAlpha(string currency) => currency switch
    {
        "840" => "USD",
        "932" => "ZWL",
        "924" => "ZWG",
        "710" => "ZAR",
        "978" => "EUR",
        "826" => "GBP",
        _ => currency
    };
}

/// <summary>
/// Wraps a GrpcChannel with metadata for lifecycle management.
/// </summary>
internal class ManagedChannel : IDisposable
{
    public GrpcChannel Channel { get; }
    public int GatewayId { get; }
    public string GatewayName { get; }
    public volatile bool Disposed;

    public ManagedChannel(GrpcChannel channel, int gatewayId, string gatewayName)
    {
        Channel = channel;
        GatewayId = gatewayId;
        GatewayName = gatewayName;
    }

    public void Dispose()
    {
        Disposed = true;
        Channel.Dispose();
    }
}

/// <summary>Fire-and-forget extension for Tasks.</summary>
internal static class TaskExtensions
{
    public static void Ignore(this Task task)
    {
        if (task.IsCompleted)
        {
            _ = task.Exception;
            return;
        }
        _ = task.ContinueWith(
            t => _ = t.Exception,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);
    }
}
