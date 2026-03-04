using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Protocol;
using MQTTnet.Server;

namespace UniBank.TerminalManager.Mqtt;

/// <summary>
/// Hosted service that runs an embedded MQTTnet MQTT broker for terminal communication.
///
/// The embedded broker approach is used because:
/// - UniBank terminals communicate over MQTT for low-bandwidth, high-reliability messaging
/// - An embedded broker simplifies deployment (no external MQTT broker infrastructure needed)
/// - Terminal authentication is integrated directly with the UniBank terminal registry
/// - Topic-based routing maps naturally to the tenant/terminal hierarchy
///
/// Default configuration:
/// - Port 1883 (standard MQTT) - configurable via Mqtt:Port
/// - Authenticated connections only (no anonymous access)
/// - Topic validation enforced per terminal's tenant scope
/// </summary>
public sealed class MqttBrokerService : BackgroundService
{
    private readonly ITerminalAuthenticator _authenticator;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<MqttBrokerService> _logger;
    private MqttServer? _mqttServer;

    public MqttBrokerService(
        ITerminalAuthenticator authenticator,
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<MqttBrokerService> logger)
    {
        _authenticator = authenticator;
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var port = _configuration.GetValue("Mqtt:Port", 1883);

        var optionsBuilder = new MqttServerOptionsBuilder()
            .WithDefaultEndpoint()
            .WithDefaultEndpointPort(port)
            .WithDefaultCommunicationTimeout(TimeSpan.FromSeconds(30));

        var mqttFactory = new MqttFactory();
        _mqttServer = mqttFactory.CreateMqttServer(optionsBuilder.Build());

        // Wire up event handlers
        _mqttServer.ValidatingConnectionAsync += ValidateConnectionAsync;
        _mqttServer.ClientConnectedAsync += OnClientConnectedAsync;
        _mqttServer.ClientDisconnectedAsync += OnClientDisconnectedAsync;
        _mqttServer.InterceptingPublishAsync += OnMessageReceivedAsync;
        _mqttServer.InterceptingSubscriptionAsync += OnSubscriptionRequestAsync;

        await _mqttServer.StartAsync();
        _logger.LogInformation("MQTT broker started on port {Port}", port);

        // Wait for shutdown signal
        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Expected during graceful shutdown
        }

        await _mqttServer.StopAsync();
        _logger.LogInformation("MQTT broker stopped");
    }

    private async Task ValidateConnectionAsync(ValidatingConnectionEventArgs args)
    {
        var clientId = args.ClientId;
        var username = args.UserName;
        var password = args.Password;

        var result = await _authenticator.AuthenticateAsync(clientId, username, password);

        if (!result.IsAuthenticated)
        {
            _logger.LogWarning(
                "MQTT connection rejected: ClientId={ClientId}, Reason={Reason}",
                clientId, result.RejectReason);

            args.ReasonCode = MqttConnectReasonCode.BadUserNameOrPassword;
            args.ReasonString = result.RejectReason;
            return;
        }

        // Store tenant context in session items for topic validation
        args.SessionItems["TenantId"] = result.TenantId;
        args.SessionItems["TerminalId"] = result.TerminalId;
        if (result.AgentId is not null)
            args.SessionItems["AgentId"] = result.AgentId;

        args.ReasonCode = MqttConnectReasonCode.Success;

        _logger.LogDebug(
            "MQTT connection accepted: ClientId={ClientId}, TenantId={TenantId}, TerminalId={TerminalId}",
            clientId, result.TenantId, result.TerminalId);
    }

    private Task OnClientConnectedAsync(ClientConnectedEventArgs args)
    {
        _logger.LogInformation("Terminal connected: ClientId={ClientId}", args.ClientId);
        return Task.CompletedTask;
    }

    private Task OnClientDisconnectedAsync(ClientDisconnectedEventArgs args)
    {
        _logger.LogInformation(
            "Terminal disconnected: ClientId={ClientId}, Reason={DisconnectType}",
            args.ClientId, args.DisconnectType);
        return Task.CompletedTask;
    }

    private async Task OnMessageReceivedAsync(InterceptingPublishEventArgs args)
    {
        var topic = args.ApplicationMessage.Topic;
        var payload = args.ApplicationMessage.PayloadSegment.ToArray();
        var clientId = args.ClientId;

        // Validate that the terminal is publishing to its own tenant/terminal topic
        if (!ValidateTopicAccess(args, topic, isPublish: true))
        {
            args.ProcessPublish = false;
            return;
        }

        try
        {
            // Create a scope for the scoped MqttTopicHandler and its dependencies (DbContext etc.)
            using var scope = _scopeFactory.CreateScope();
            var topicHandler = scope.ServiceProvider.GetRequiredService<MqttTopicHandler>();
            await topicHandler.HandleMessageAsync(topic, payload, clientId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error handling MQTT message: Topic={Topic}, ClientId={ClientId}", topic, clientId);
        }
    }

    private Task OnSubscriptionRequestAsync(InterceptingSubscriptionEventArgs args)
    {
        var topic = args.TopicFilter.Topic;

        if (!ValidateTopicAccess(args, topic, isPublish: false))
        {
            args.ProcessSubscription = false;
            args.Response.ReasonCode = MqttSubscribeReasonCode.NotAuthorized;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Enforces tenant isolation by validating that terminals can only publish/subscribe
    /// to topics within their own tenant scope.
    /// </summary>
    private bool ValidateTopicAccess(InterceptingPublishEventArgs args, string topic, bool isPublish)
    {
        if (args.SessionItems.Contains("TenantId") &&
            args.SessionItems["TenantId"] is string tenantId)
        {
            // Terminals can only access topics under their tenant prefix
            var requiredPrefix = $"terminal/{tenantId}/";
            if (!topic.StartsWith(requiredPrefix, StringComparison.OrdinalIgnoreCase) &&
                !topic.StartsWith($"system/{tenantId}/", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "Topic access denied: ClientId={ClientId}, Topic={Topic}, TenantId={TenantId}, Action={Action}",
                    args.ClientId, topic, tenantId, isPublish ? "Publish" : "Subscribe");
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Overload for subscription events which use a different event args type.
    /// </summary>
    private bool ValidateTopicAccess(InterceptingSubscriptionEventArgs args, string topic, bool isPublish)
    {
        if (args.SessionItems.Contains("TenantId") &&
            args.SessionItems["TenantId"] is string tenantId)
        {
            var requiredPrefix = $"terminal/{tenantId}/";
            if (!topic.StartsWith(requiredPrefix, StringComparison.OrdinalIgnoreCase) &&
                !topic.StartsWith($"system/{tenantId}/", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "Topic access denied: ClientId={ClientId}, Topic={Topic}, TenantId={TenantId}, Action={Action}",
                    args.ClientId, topic, tenantId, isPublish ? "Publish" : "Subscribe");
                return false;
            }
        }

        return true;
    }

    public override void Dispose()
    {
        _mqttServer?.Dispose();
        base.Dispose();
    }
}
