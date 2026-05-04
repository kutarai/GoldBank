using Grpc.Core;
using Grpc.Core.Interceptors;

namespace GoldBank.Gateway.Interceptors;

/// <summary>
/// Resolves the tenant context for every gRPC call.
/// <para>
/// Resolution priority:
/// 1. "tenant_id" claim extracted by <see cref="AuthInterceptor"/> (JWT).
/// 2. "x-tenant-id" gRPC metadata header.
/// </para>
/// The resolved tenant identifier is placed into <see cref="ServerCallContext.UserState"/>
/// and also injected into the <see cref="HttpContext.Items"/> for consumption by
/// <see cref="GoldBank.Core.Common.Persistence.GrpcTenantProvider"/>.
/// </summary>
public sealed class TenantInterceptor : Interceptor
{
    private const string TenantHeader = "x-tenant-id";
    private const string TenantUserStateKey = "TenantId";

    private readonly Serilog.ILogger _logger = Serilog.Log.ForContext<TenantInterceptor>();

    // ----------------------------------------------------------------
    // Unary
    // ----------------------------------------------------------------
    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        ResolveTenantOrThrow(context);
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
        ResolveTenantOrThrow(context);
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
        ResolveTenantOrThrow(context);
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
        ResolveTenantOrThrow(context);
        await continuation(requestStream, responseStream, context);
    }

    // ----------------------------------------------------------------
    // Core tenant resolution
    // ----------------------------------------------------------------
    private void ResolveTenantOrThrow(ServerCallContext context)
    {
        // 1. Try JWT claim (set by AuthInterceptor)
        string? tenantId = null;
        if (context.UserState.TryGetValue(TenantUserStateKey, out var claimTenant))
        {
            tenantId = claimTenant as string;
        }

        // 2. Fallback to metadata header
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            tenantId = context.RequestHeaders.GetValue(TenantHeader);
        }

        if (string.IsNullOrWhiteSpace(tenantId))
        {
            _logger.Warning("Tenant ID missing for {Method}. Neither JWT claim nor x-tenant-id header provided.",
                context.Method);
            throw new RpcException(
                new Status(StatusCode.InvalidArgument,
                    "Tenant identification is required. Provide a 'tenant_id' JWT claim or 'x-tenant-id' header."));
        }

        // Store the resolved tenant in UserState for downstream use
        context.UserState[TenantUserStateKey] = tenantId;

        // Also propagate to HttpContext for GrpcTenantProvider
        var httpContext = context.GetHttpContext();
        httpContext.Items["TenantId"] = tenantId;

        // Set a response trailer so clients can confirm which tenant was resolved
        context.ResponseTrailers.Add("x-resolved-tenant-id", tenantId);

        _logger.Debug("Resolved tenant {TenantId} for {Method}", tenantId, context.Method);
    }
}
