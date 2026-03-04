using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.Extensions.Logging;

namespace UniBank.Core.Modules.WhiteLabel.Infrastructure;

/// <summary>
/// gRPC interceptor that validates tenant_id is present on all calls
/// and matches the authenticated tenant context (STORY-069).
/// Ensures data isolation between tenants at the service boundary.
/// </summary>
public sealed class TenantIsolationMiddleware : Interceptor
{
    private readonly ILogger<TenantIsolationMiddleware> _logger;

    public TenantIsolationMiddleware(ILogger<TenantIsolationMiddleware> logger)
    {
        _logger = logger;
    }

    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        ValidateTenantContext(context);
        return await continuation(request, context);
    }

    public override async Task ServerStreamingServerHandler<TRequest, TResponse>(
        TRequest request,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context,
        ServerStreamingServerMethod<TRequest, TResponse> continuation)
    {
        ValidateTenantContext(context);
        await continuation(request, responseStream, context);
    }

    private void ValidateTenantContext(ServerCallContext context)
    {
        var requestTenantId = context.RequestHeaders.GetValue("x-tenant-id");
        var authenticatedTenantId = context.RequestHeaders.GetValue("x-authenticated-tenant-id");

        if (string.IsNullOrWhiteSpace(requestTenantId))
        {
            _logger.LogWarning(
                "Tenant isolation violation: missing x-tenant-id header on {Method}",
                context.Method);
            throw new RpcException(new Status(
                StatusCode.InvalidArgument,
                "x-tenant-id header is required for all requests."));
        }

        // If authenticated tenant context is available, verify it matches
        if (!string.IsNullOrWhiteSpace(authenticatedTenantId) &&
            !string.Equals(requestTenantId, authenticatedTenantId, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "Tenant isolation violation: request tenant '{RequestTenant}' does not match authenticated tenant '{AuthTenant}' on {Method}",
                requestTenantId, authenticatedTenantId, context.Method);
            throw new RpcException(new Status(
                StatusCode.PermissionDenied,
                "Access denied: tenant context mismatch."));
        }
    }
}
