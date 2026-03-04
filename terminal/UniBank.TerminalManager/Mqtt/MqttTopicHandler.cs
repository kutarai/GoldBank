using System.Text;
using Microsoft.Extensions.Logging;
using UniBank.TerminalManager.Services;

namespace UniBank.TerminalManager.Mqtt;

/// <summary>
/// Handles incoming MQTT messages routed by topic pattern.
///
/// Topic hierarchy convention for UniBank terminals:
///   terminal/{tenantId}/{terminalId}/status      - Terminal heartbeat and status reports (STORY-048)
///   terminal/{tenantId}/{terminalId}/txn          - Transaction results from POS terminals
///   terminal/{tenantId}/{terminalId}/config/req   - Terminal requesting configuration
///   terminal/{tenantId}/{terminalId}/alert        - Terminal-initiated alerts (paper low, tamper, etc.)
///   terminal/{tenantId}/{terminalId}/keys         - Key injection responses (STORY-047)
///   terminal/{tenantId}/{terminalId}/update/ack   - Update acknowledgments (STORY-049)
///   system/{tenantId}/broadcast                   - System-wide broadcasts to all terminals in a tenant
/// </summary>
public sealed class MqttTopicHandler
{
    private readonly TerminalMonitoringService _monitoringService;
    private readonly TerminalKeyManager _keyManager;
    private readonly TerminalUpdateService _updateService;
    private readonly ILogger<MqttTopicHandler> _logger;

    public MqttTopicHandler(
        TerminalMonitoringService monitoringService,
        TerminalKeyManager keyManager,
        TerminalUpdateService updateService,
        ILogger<MqttTopicHandler> logger)
    {
        _monitoringService = monitoringService;
        _keyManager = keyManager;
        _updateService = updateService;
        _logger = logger;
    }

    /// <summary>
    /// Routes an incoming MQTT message to the appropriate handler based on the topic pattern.
    /// </summary>
    /// <param name="topic">The MQTT topic the message was published to.</param>
    /// <param name="payload">The raw message payload bytes.</param>
    /// <param name="clientId">The MQTT client ID of the publisher.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task HandleMessageAsync(string topic, byte[] payload, string clientId,
        CancellationToken cancellationToken = default)
    {
        var segments = topic.Split('/');
        var payloadText = Encoding.UTF8.GetString(payload);

        _logger.LogDebug(
            "MQTT message received: Topic={Topic}, ClientId={ClientId}, PayloadLength={PayloadLength}",
            topic, clientId, payload.Length);

        if (segments.Length < 4 || segments[0] != "terminal")
        {
            _logger.LogWarning("Unrecognized topic pattern: {Topic} from ClientId={ClientId}", topic, clientId);
            return;
        }

        var tenantId = segments[1];
        var terminalId = segments[2];
        var messageType = segments[3];

        switch (messageType)
        {
            case "status":
                await HandleTerminalStatusAsync(tenantId, terminalId, payloadText, cancellationToken);
                break;

            case "txn":
                await HandleTransactionResultAsync(tenantId, terminalId, payloadText, cancellationToken);
                break;

            case "config":
                if (segments.Length > 4 && segments[4] == "req")
                    await HandleConfigRequestAsync(tenantId, terminalId, payloadText, cancellationToken);
                break;

            case "alert":
                await HandleTerminalAlertAsync(tenantId, terminalId, payloadText, cancellationToken);
                break;

            case "keys":
                await HandleKeyInjectionResponseAsync(tenantId, terminalId, payloadText, cancellationToken);
                break;

            case "update":
                if (segments.Length > 4 && segments[4] == "ack")
                    await HandleUpdateAcknowledgmentAsync(tenantId, terminalId, payloadText, cancellationToken);
                break;

            default:
                _logger.LogWarning(
                    "Unknown message type '{MessageType}' from terminal {TerminalId} in tenant {TenantId}",
                    messageType, terminalId, tenantId);
                break;
        }
    }

    /// <summary>
    /// Handles terminal heartbeat/status messages (STORY-048).
    /// Parses heartbeat JSON (status, battery, paperLevel, signalStrength),
    /// updates terminal last-seen, and detects status changes.
    /// </summary>
    private async Task HandleTerminalStatusAsync(string tenantId, string terminalId, string payload,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Terminal status update: TenantId={TenantId}, TerminalId={TerminalId}",
            tenantId, terminalId);

        await _monitoringService.ProcessHeartbeatAsync(tenantId, terminalId, payload, cancellationToken);
    }

    private Task HandleTransactionResultAsync(string tenantId, string terminalId, string payload,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Transaction result from terminal: TenantId={TenantId}, TerminalId={TerminalId}",
            tenantId, terminalId);

        // TODO: Parse transaction result, forward to Core via gRPC for settlement,
        // raise TransactionCompleted/TransactionFailed domain event
        return Task.CompletedTask;
    }

    private Task HandleConfigRequestAsync(string tenantId, string terminalId, string payload,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Config request from terminal: TenantId={TenantId}, TerminalId={TerminalId}",
            tenantId, terminalId);

        // TODO: Fetch terminal configuration from Core, publish response to
        // terminal/{tenantId}/{terminalId}/config/resp topic
        return Task.CompletedTask;
    }

    private Task HandleTerminalAlertAsync(string tenantId, string terminalId, string payload,
        CancellationToken cancellationToken)
    {
        _logger.LogWarning(
            "Terminal alert: TenantId={TenantId}, TerminalId={TerminalId}, Payload={Payload}",
            tenantId, terminalId, payload);

        // TODO: Parse alert type, raise appropriate domain event,
        // forward to notification service for operator alerts
        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles key injection responses from terminals (STORY-047).
    /// </summary>
    private async Task HandleKeyInjectionResponseAsync(string tenantId, string terminalId, string payload,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Key injection response: TenantId={TenantId}, TerminalId={TerminalId}",
            tenantId, terminalId);

        await _keyManager.HandleKeyInjectionResponseAsync(tenantId, terminalId, payload, cancellationToken);
    }

    /// <summary>
    /// Handles update acknowledgments from terminals (STORY-049).
    /// </summary>
    private async Task HandleUpdateAcknowledgmentAsync(string tenantId, string terminalId, string payload,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Update acknowledgment: TenantId={TenantId}, TerminalId={TerminalId}",
            tenantId, terminalId);

        await _updateService.HandleUpdateAcknowledgmentAsync(tenantId, terminalId, payload, cancellationToken);
    }
}
