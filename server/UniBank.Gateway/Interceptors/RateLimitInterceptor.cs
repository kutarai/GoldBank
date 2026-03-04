using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using UniBank.Gateway.Configuration;

namespace UniBank.Gateway.Interceptors;

/// <summary>
/// Redis-backed sliding-window rate limiter for gRPC calls.
/// Enforces per-user (100/min default) and per-tenant (10000/min default) limits.
/// Uses a Redis sorted-set sliding window for accurate counting.
/// </summary>
public sealed class RateLimitInterceptor : Interceptor
{
    private readonly RateLimitSettings _settings;
    private readonly IConnectionMultiplexer? _redis;
    private readonly HashSet<string> _exemptMethods;
    private readonly Serilog.ILogger _logger = Serilog.Log.ForContext<RateLimitInterceptor>();

    /// <summary>
    /// Lua script for atomic sliding window rate limiting.
    /// KEYS[1] = rate limit key, ARGV[1] = window start timestamp,
    /// ARGV[2] = current timestamp, ARGV[3] = unique member id,
    /// ARGV[4] = max requests, ARGV[5] = window TTL in seconds.
    /// Returns: 1 if allowed, 0 if denied.
    /// </summary>
    private const string SlidingWindowLuaScript = """
        local key = KEYS[1]
        local windowStart = tonumber(ARGV[1])
        local now = tonumber(ARGV[2])
        local member = ARGV[3]
        local maxRequests = tonumber(ARGV[4])
        local windowTtl = tonumber(ARGV[5])

        -- Remove expired entries outside the sliding window
        redis.call('ZREMRANGEBYSCORE', key, '-inf', windowStart)

        -- Count current requests in window
        local currentCount = redis.call('ZCARD', key)

        if currentCount >= maxRequests then
            return 0
        end

        -- Add this request with current timestamp as score
        redis.call('ZADD', key, now, member)
        redis.call('EXPIRE', key, windowTtl)

        return 1
        """;

    private readonly LuaScript? _preparedScript;

    public RateLimitInterceptor(
        IOptions<RateLimitSettings> settings,
        IConnectionMultiplexer? redis = null)
    {
        _settings = settings.Value;
        _redis = redis;
        _exemptMethods = new HashSet<string>(_settings.ExemptMethods, StringComparer.OrdinalIgnoreCase);

        if (_redis is not null)
        {
            _preparedScript = LuaScript.Prepare(SlidingWindowLuaScript);
        }
    }

    // ----------------------------------------------------------------
    // Unary
    // ----------------------------------------------------------------
    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        await EnforceRateLimitAsync(context);
        return await continuation(request, context);
    }

    // ----------------------------------------------------------------
    // Server streaming
    // ----------------------------------------------------------------
    public override async Task ServerStreamingServerHandler<TRequest, TResponse>(
        TRequest request,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context,
        ServerStreamingServerMethod<TRequest, TResponse> continuation)
    {
        await EnforceRateLimitAsync(context);
        await continuation(request, responseStream, context);
    }

    // ----------------------------------------------------------------
    // Client streaming
    // ----------------------------------------------------------------
    public override async Task<TResponse> ClientStreamingServerHandler<TRequest, TResponse>(
        IAsyncStreamReader<TRequest> requestStream,
        ServerCallContext context,
        ClientStreamingServerMethod<TRequest, TResponse> continuation)
    {
        await EnforceRateLimitAsync(context);
        return await continuation(requestStream, context);
    }

    // ----------------------------------------------------------------
    // Duplex streaming
    // ----------------------------------------------------------------
    public override async Task DuplexStreamingServerHandler<TRequest, TResponse>(
        IAsyncStreamReader<TRequest> requestStream,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context,
        DuplexStreamingServerMethod<TRequest, TResponse> continuation)
    {
        await EnforceRateLimitAsync(context);
        await continuation(requestStream, responseStream, context);
    }

    // ----------------------------------------------------------------
    // Core rate limiting logic
    // ----------------------------------------------------------------
    private async Task EnforceRateLimitAsync(ServerCallContext context)
    {
        if (!_settings.Enabled)
            return;

        if (_redis is null || _preparedScript is null)
        {
            _logger.Debug("Rate limiting skipped: Redis not available");
            return;
        }

        var method = context.Method;

        if (_exemptMethods.Contains(method))
            return;

        var db = _redis.GetDatabase();
        var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var requestId = $"{nowMs}:{Guid.NewGuid():N}";

        // --- Per-user rate limit ---
        var userId = context.UserState.TryGetValue("UserId", out var uid) ? uid as string : null;
        if (!string.IsNullOrWhiteSpace(userId))
        {
            var userKey = $"rl:user:{userId}";
            var userWindowStartMs = nowMs - (_settings.UserWindowSeconds * 1000L);

            var userAllowed = (int)await db.ScriptEvaluateAsync(
                _preparedScript,
                new { KEYS = new RedisKey[] { userKey },
                      ARGV = new RedisValue[] {
                          userWindowStartMs, nowMs, requestId,
                          _settings.UserMaxRequests, _settings.UserWindowSeconds + 10
                      }
                });

            if (userAllowed == 0)
            {
                _logger.Warning(
                    "User rate limit exceeded for {UserId} on {Method}. Limit: {Limit}/{Window}s",
                    userId, method, _settings.UserMaxRequests, _settings.UserWindowSeconds);

                var metadata = new Metadata
                {
                    { "retry-after", _settings.UserWindowSeconds.ToString() },
                    { "x-ratelimit-limit", _settings.UserMaxRequests.ToString() },
                    { "x-ratelimit-scope", "user" }
                };

                throw new RpcException(
                    new Status(StatusCode.ResourceExhausted,
                        $"User rate limit exceeded. Maximum {_settings.UserMaxRequests} requests per {_settings.UserWindowSeconds} seconds."),
                    metadata);
            }
        }

        // --- Per-tenant rate limit ---
        var tenantId = context.UserState.TryGetValue("TenantId", out var tid) ? tid as string : null;
        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            var tenantKey = $"rl:tenant:{tenantId}";
            var tenantWindowStartMs = nowMs - (_settings.TenantWindowSeconds * 1000L);

            var tenantAllowed = (int)await db.ScriptEvaluateAsync(
                _preparedScript,
                new { KEYS = new RedisKey[] { tenantKey },
                      ARGV = new RedisValue[] {
                          tenantWindowStartMs, nowMs, requestId,
                          _settings.TenantMaxRequests, _settings.TenantWindowSeconds + 10
                      }
                });

            if (tenantAllowed == 0)
            {
                _logger.Warning(
                    "Tenant rate limit exceeded for {TenantId} on {Method}. Limit: {Limit}/{Window}s",
                    tenantId, method, _settings.TenantMaxRequests, _settings.TenantWindowSeconds);

                var metadata = new Metadata
                {
                    { "retry-after", _settings.TenantWindowSeconds.ToString() },
                    { "x-ratelimit-limit", _settings.TenantMaxRequests.ToString() },
                    { "x-ratelimit-scope", "tenant" }
                };

                throw new RpcException(
                    new Status(StatusCode.ResourceExhausted,
                        $"Tenant rate limit exceeded. Maximum {_settings.TenantMaxRequests} requests per {_settings.TenantWindowSeconds} seconds."),
                    metadata);
            }
        }
    }
}
