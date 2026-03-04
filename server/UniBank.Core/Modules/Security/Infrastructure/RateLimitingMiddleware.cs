using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace UniBank.Core.Modules.Security.Infrastructure;

/// <summary>
/// IP-based and user-based rate limiting middleware using Redis (STORY-075).
/// Configurable limits per endpoint type: auth (5/min), transaction (30/min), query (100/min).
/// Returns HTTP 429 Too Many Requests when limits are exceeded.
/// </summary>
public sealed class RateLimitingMiddleware
{
    private const string RateLimitKeyPrefix = "ratelimit:";
    private const int AuthLimitPerMinute = 5;
    private const int TransactionLimitPerMinute = 30;
    private const int QueryLimitPerMinute = 100;
    private const int DefaultLimitPerMinute = 60;
    private const int WindowSeconds = 60;

    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitingMiddleware> _logger;

    public RateLimitingMiddleware(RequestDelegate next, ILogger<RateLimitingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IConnectionMultiplexer redis)
    {
        var clientIdentifier = GetClientIdentifier(context);
        var endpointType = ClassifyEndpoint(context.Request.Path);
        var limit = GetLimitForEndpointType(endpointType);

        var db = redis.GetDatabase();
        var rateLimitKey = $"{RateLimitKeyPrefix}{endpointType}:{clientIdentifier}";

        var currentCount = await db.StringIncrementAsync(rateLimitKey);

        if (currentCount == 1)
        {
            await db.KeyExpireAsync(rateLimitKey, TimeSpan.FromSeconds(WindowSeconds));
        }

        // Add rate limit headers
        context.Response.Headers["X-RateLimit-Limit"] = limit.ToString();
        context.Response.Headers["X-RateLimit-Remaining"] = Math.Max(0, limit - currentCount).ToString();

        if (currentCount > limit)
        {
            _logger.LogWarning(
                "Rate limit exceeded for {ClientIdentifier} on {EndpointType}: {Count}/{Limit} per minute",
                clientIdentifier, endpointType, currentCount, limit);

            context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
            context.Response.Headers["Retry-After"] = WindowSeconds.ToString();
            await context.Response.WriteAsync("Rate limit exceeded. Please try again later.");
            return;
        }

        await _next(context);
    }

    private static string GetClientIdentifier(HttpContext context)
    {
        // Prefer user identity for authenticated requests, fall back to IP
        var userId = context.User?.Identity?.Name;
        if (!string.IsNullOrEmpty(userId))
            return $"user:{userId}";

        var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return $"ip:{ipAddress}";
    }

    private static string ClassifyEndpoint(PathString path)
    {
        var pathValue = path.Value?.ToLowerInvariant() ?? string.Empty;

        if (pathValue.Contains("/auth") || pathValue.Contains("/login") ||
            pathValue.Contains("/register") || pathValue.Contains("/otp"))
            return "auth";

        if (pathValue.Contains("/transfer") || pathValue.Contains("/payment") ||
            pathValue.Contains("/transaction") || pathValue.Contains("/bill"))
            return "transaction";

        return "query";
    }

    private static int GetLimitForEndpointType(string endpointType)
    {
        return endpointType switch
        {
            "auth" => AuthLimitPerMinute,
            "transaction" => TransactionLimitPerMinute,
            "query" => QueryLimitPerMinute,
            _ => DefaultLimitPerMinute
        };
    }
}
