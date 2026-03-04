using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using UniBank.Gateway.Configuration;

namespace UniBank.Gateway.Interceptors;

/// <summary>
/// Server-side gRPC interceptor that validates JWT bearer tokens from the
/// "authorization" metadata header. Methods listed in <see cref="JwtSettings.AnonymousMethods"/>
/// bypass authentication. On success, extracted claims are injected into
/// <see cref="ServerCallContext.UserState"/> for downstream interceptors.
/// </summary>
public sealed class AuthInterceptor : Interceptor
{
    private readonly JwtSettings _jwtSettings;
    private readonly TokenValidationParameters _tokenValidation;
    private readonly JwtSecurityTokenHandler _tokenHandler = new();
    private readonly HashSet<string> _anonymousMethods;
    private readonly Serilog.ILogger _logger = Serilog.Log.ForContext<AuthInterceptor>();

    public AuthInterceptor(IOptions<JwtSettings> jwtSettings)
    {
        _jwtSettings = jwtSettings.Value;
        _anonymousMethods = new HashSet<string>(_jwtSettings.AnonymousMethods, StringComparer.OrdinalIgnoreCase);

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.SecretKey));
        _tokenValidation = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _jwtSettings.Issuer,
            ValidateAudience = true,
            ValidAudience = _jwtSettings.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = key,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(_jwtSettings.ClockSkewSeconds),
            RequireExpirationTime = true,
        };
    }

    // ----------------------------------------------------------------
    // Unary
    // ----------------------------------------------------------------
    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        AuthenticateOrThrow(context);
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
        AuthenticateOrThrow(context);
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
        AuthenticateOrThrow(context);
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
        AuthenticateOrThrow(context);
        await continuation(requestStream, responseStream, context);
    }

    // ----------------------------------------------------------------
    // Core authentication logic
    // ----------------------------------------------------------------
    private void AuthenticateOrThrow(ServerCallContext context)
    {
        var method = context.Method;

        // Allow anonymous methods through without token
        if (_anonymousMethods.Contains(method))
        {
            _logger.Debug("Anonymous access granted for {Method}", method);
            return;
        }

        var authHeader = context.RequestHeaders.GetValue("authorization");

        if (string.IsNullOrWhiteSpace(authHeader))
        {
            _logger.Warning("Missing authorization header for {Method}", method);
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Authorization header is required."));
        }

        // Support "Bearer <token>" format
        var token = authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? authHeader["Bearer ".Length..].Trim()
            : authHeader.Trim();

        if (string.IsNullOrWhiteSpace(token))
        {
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Bearer token is required."));
        }

        try
        {
            var principal = _tokenHandler.ValidateToken(token, _tokenValidation, out var validatedToken);

            // Store claims in UserState for downstream interceptors (TenantInterceptor, etc.)
            context.UserState["ClaimsPrincipal"] = principal;
            context.UserState["UserId"] = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
                                          ?? principal.FindFirst("sub")?.Value
                                          ?? "unknown";
            context.UserState["TenantId"] = principal.FindFirst("tenant_id")?.Value ?? string.Empty;
            context.UserState["Roles"] = principal.FindAll(ClaimTypes.Role)
                .Select(c => c.Value)
                .ToArray();

            _logger.Debug(
                "Authenticated user {UserId} for {Method}",
                context.UserState["UserId"],
                method);
        }
        catch (SecurityTokenExpiredException)
        {
            _logger.Warning("Expired token for {Method}", method);
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Token has expired."));
        }
        catch (SecurityTokenValidationException ex)
        {
            _logger.Warning(ex, "Invalid token for {Method}", method);
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid authentication token."));
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Unexpected authentication error for {Method}", method);
            throw new RpcException(new Status(StatusCode.Internal, "Authentication processing error."));
        }
    }
}
