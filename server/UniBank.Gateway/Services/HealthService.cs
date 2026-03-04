using Grpc.Core;
using Grpc.Health.V1;
using StackExchange.Redis;

namespace UniBank.Gateway.Services;

/// <summary>
/// gRPC Health Checking Protocol (grpc.health.v1) implementation.
/// Reports overall gateway health including Redis connectivity.
/// </summary>
public sealed class HealthService : Health.HealthBase
{
    private readonly IConnectionMultiplexer? _redis;
    private readonly Serilog.ILogger _logger = Serilog.Log.ForContext<HealthService>();

    public HealthService(IConnectionMultiplexer? redis = null)
    {
        _redis = redis;
    }

    public override async Task<HealthCheckResponse> Check(
        HealthCheckRequest request,
        ServerCallContext context)
    {
        var status = await EvaluateHealthAsync(request.Service, context.CancellationToken);

        return new HealthCheckResponse { Status = status };
    }

    public override async Task Watch(
        HealthCheckRequest request,
        IServerStreamWriter<HealthCheckResponse> responseStream,
        ServerCallContext context)
    {
        while (!context.CancellationToken.IsCancellationRequested)
        {
            var status = await EvaluateHealthAsync(request.Service, context.CancellationToken);

            await responseStream.WriteAsync(
                new HealthCheckResponse { Status = status },
                context.CancellationToken);

            await Task.Delay(TimeSpan.FromSeconds(5), context.CancellationToken);
        }
    }

    private async Task<HealthCheckResponse.Types.ServingStatus> EvaluateHealthAsync(
        string serviceName,
        CancellationToken ct)
    {
        try
        {
            // Check Redis connectivity
            if (_redis is not null)
            {
                var db = _redis.GetDatabase();
                var pong = await db.PingAsync();

                if (pong > TimeSpan.FromSeconds(5))
                {
                    _logger.Warning("Redis ping latency is high: {LatencyMs}ms", pong.TotalMilliseconds);
                    return HealthCheckResponse.Types.ServingStatus.NotServing;
                }
            }

            // If a specific service is requested, check it
            if (!string.IsNullOrEmpty(serviceName))
            {
                // All known services are healthy if we reached here
                _logger.Debug("Health check for service {ServiceName}: SERVING", serviceName);
            }

            return HealthCheckResponse.Types.ServingStatus.Serving;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Health check failed for service {ServiceName}", serviceName);
            return HealthCheckResponse.Types.ServingStatus.NotServing;
        }
    }
}
