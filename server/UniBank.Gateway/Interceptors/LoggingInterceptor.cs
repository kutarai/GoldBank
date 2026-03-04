using System.Diagnostics;
using Grpc.Core;
using Grpc.Core.Interceptors;
using UniBank.Gateway.Middleware;

namespace UniBank.Gateway.Interceptors;

/// <summary>
/// Structured logging interceptor for all gRPC calls.
/// Logs method, user, tenant, duration, status code, and request/response metadata.
/// All PII fields are masked via <see cref="PiiMasker"/> before logging.
/// </summary>
public sealed class LoggingInterceptor : Interceptor
{
    private readonly Serilog.ILogger _logger = Serilog.Log.ForContext<LoggingInterceptor>();

    // ----------------------------------------------------------------
    // Unary
    // ----------------------------------------------------------------
    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        var callInfo = BeginCall(context, "Unary");

        try
        {
            LogRequest(request, callInfo);
            var response = await continuation(request, context);
            EndCall(callInfo, StatusCode.OK);
            return response;
        }
        catch (RpcException ex)
        {
            EndCall(callInfo, ex.StatusCode, ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            EndCallWithError(callInfo, ex);
            throw;
        }
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
        var callInfo = BeginCall(context, "ServerStreaming");

        try
        {
            LogRequest(request, callInfo);
            await continuation(request, responseStream, context);
            EndCall(callInfo, StatusCode.OK);
        }
        catch (RpcException ex)
        {
            EndCall(callInfo, ex.StatusCode, ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            EndCallWithError(callInfo, ex);
            throw;
        }
    }

    // ----------------------------------------------------------------
    // Client streaming
    // ----------------------------------------------------------------
    public override async Task<TResponse> ClientStreamingServerHandler<TRequest, TResponse>(
        IAsyncStreamReader<TRequest> requestStream,
        ServerCallContext context,
        ClientStreamingServerMethod<TRequest, TResponse> continuation)
    {
        var callInfo = BeginCall(context, "ClientStreaming");

        try
        {
            var response = await continuation(requestStream, context);
            EndCall(callInfo, StatusCode.OK);
            return response;
        }
        catch (RpcException ex)
        {
            EndCall(callInfo, ex.StatusCode, ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            EndCallWithError(callInfo, ex);
            throw;
        }
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
        var callInfo = BeginCall(context, "DuplexStreaming");

        try
        {
            await continuation(requestStream, responseStream, context);
            EndCall(callInfo, StatusCode.OK);
        }
        catch (RpcException ex)
        {
            EndCall(callInfo, ex.StatusCode, ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            EndCallWithError(callInfo, ex);
            throw;
        }
    }

    // ----------------------------------------------------------------
    // Helpers
    // ----------------------------------------------------------------

    private CallInfo BeginCall(ServerCallContext context, string callType)
    {
        var info = new CallInfo
        {
            Method = context.Method,
            CallType = callType,
            Stopwatch = Stopwatch.StartNew(),
            UserId = context.UserState.TryGetValue("UserId", out var uid) ? uid as string ?? "anonymous" : "anonymous",
            TenantId = context.UserState.TryGetValue("TenantId", out var tid) ? tid as string ?? "unknown" : "unknown",
            Peer = context.Peer ?? "unknown",
        };

        _logger.Information(
            "gRPC {CallType} call started: {Method} | User: {UserId} | Tenant: {TenantId} | Peer: {Peer}",
            info.CallType, info.Method, info.UserId, info.TenantId, info.Peer);

        return info;
    }

    private void LogRequest<TRequest>(TRequest request, CallInfo callInfo) where TRequest : class
    {
        try
        {
            // Serialize request to string, then mask PII
            var requestText = request.ToString();
            if (!string.IsNullOrWhiteSpace(requestText))
            {
                var maskedRequest = PiiMasker.Mask(requestText);
                _logger.Debug(
                    "gRPC request payload for {Method}: {Payload}",
                    callInfo.Method, maskedRequest);
            }
        }
        catch (Exception ex)
        {
            _logger.Debug(ex, "Failed to serialize request for logging on {Method}", callInfo.Method);
        }
    }

    private void EndCall(CallInfo info, StatusCode statusCode, string? detail = null)
    {
        info.Stopwatch.Stop();
        var durationMs = info.Stopwatch.Elapsed.TotalMilliseconds;

        if (statusCode == StatusCode.OK)
        {
            _logger.Information(
                "gRPC {CallType} call completed: {Method} | Status: {StatusCode} | Duration: {DurationMs:F1}ms | User: {UserId} | Tenant: {TenantId}",
                info.CallType, info.Method, statusCode, durationMs, info.UserId, info.TenantId);
        }
        else
        {
            var maskedDetail = detail is not null ? PiiMasker.Mask(detail) : null;
            _logger.Warning(
                "gRPC {CallType} call failed: {Method} | Status: {StatusCode} | Duration: {DurationMs:F1}ms | User: {UserId} | Tenant: {TenantId} | Detail: {Detail}",
                info.CallType, info.Method, statusCode, durationMs, info.UserId, info.TenantId, maskedDetail);
        }

        // Log slow calls
        if (durationMs > 5000)
        {
            _logger.Warning(
                "Slow gRPC call detected: {Method} took {DurationMs:F1}ms | User: {UserId} | Tenant: {TenantId}",
                info.Method, durationMs, info.UserId, info.TenantId);
        }
    }

    private void EndCallWithError(CallInfo info, Exception ex)
    {
        info.Stopwatch.Stop();
        var durationMs = info.Stopwatch.Elapsed.TotalMilliseconds;

        _logger.Error(ex,
            "gRPC {CallType} call error: {Method} | Duration: {DurationMs:F1}ms | User: {UserId} | Tenant: {TenantId} | Error: {ErrorMessage}",
            info.CallType, info.Method, durationMs, info.UserId, info.TenantId,
            PiiMasker.Mask(ex.Message));
    }

    private sealed class CallInfo
    {
        public required string Method { get; init; }
        public required string CallType { get; init; }
        public required Stopwatch Stopwatch { get; init; }
        public required string UserId { get; init; }
        public required string TenantId { get; init; }
        public required string Peer { get; init; }
    }
}
