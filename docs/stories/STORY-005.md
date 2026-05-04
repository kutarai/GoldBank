# STORY-005: API Gateway with gRPC Interceptors

**Epic:** EPIC-000 Infrastructure
**Priority:** Must Have
**Story Points:** 8
**Status:** Not Started
**Assigned To:** Unassigned
**Created:** 2026-02-24
**Sprint:** 1

---

## User Story

As a developer,
I want an API Gateway that routes gRPC calls with auth, rate limiting, and tenant context,
So that all client requests are authenticated and properly routed.

---

## Description

### Background
The API Gateway is the single entry point for all client-facing gRPC traffic in the GoldBank platform. Every request from mobile apps, POS terminals, and the admin portal flows through this gateway. It is responsible for cross-cutting concerns: TLS termination, JWT authentication, tenant identification, rate limiting, request logging with PII masking, and routing to downstream services.

The gateway uses ASP.NET Core's gRPC interceptor pipeline, which is analogous to HTTP middleware but specific to gRPC calls. Interceptors execute in order for each request, forming a chain: TLS -> JWT Validation -> Tenant Extraction -> Rate Limiting -> PII Masking Logger -> Route to Downstream Service.

### Scope

**In scope:**
- gRPC host configuration with TLS support
- JWT validation interceptor (validates access tokens, extracts claims)
- Tenant identification interceptor (resolves tenant from JWT claims or gRPC metadata)
- Rate limiting interceptor (per-user and per-tenant, using Redis sliding window)
- Request/response logging interceptor with PII masking
- Routing configuration to downstream Core Banking service
- Health check endpoint (`/health`)
- gRPC reflection for development tooling (grpcurl, Postman)
- Configuration via `appsettings.json` with environment variable overrides

**Out of scope:**
- JWT token issuance (handled by AccountService after authentication)
- gRPC-Web support (future story)
- Load balancing across multiple Core instances
- API versioning middleware (future consideration)
- Circuit breaker patterns (future resilience story)

### User Flow
1. Mobile app sends gRPC request to Gateway (e.g., `AccountService.GetBalance`)
2. TLS terminates at the gateway
3. JWT interceptor extracts and validates the `Authorization` Bearer token
4. Tenant interceptor reads `tenant_id` from JWT claims, resolves tenant info, and adds to call context
5. Rate limiting interceptor checks per-user and per-tenant limits in Redis
6. Logging interceptor logs the request method, tenant, duration (with PII masked)
7. Gateway forwards the call to the Core Banking gRPC service
8. Response flows back through the interceptor chain
9. Logging interceptor logs the response status and duration
10. Response is returned to the mobile app

---

## Acceptance Criteria

- [ ] gRPC gateway starts and listens on configured HTTP/2 port (5000) and HTTPS port (5001)
- [ ] TLS is configured with development certificate for HTTPS
- [ ] JWT validation interceptor rejects requests without valid Bearer token (returns `UNAUTHENTICATED`)
- [ ] JWT validation interceptor extracts claims: `sub` (user ID), `tenant_id`, `device_id`, `roles`
- [ ] Tenant identification interceptor resolves tenant from JWT `tenant_id` claim
- [ ] Tenant identification interceptor falls back to `x-tenant-id` gRPC metadata header for unauthenticated endpoints (Register, VerifyOTP)
- [ ] Rate limiting interceptor enforces per-user limits (configurable, default 100 req/min)
- [ ] Rate limiting interceptor enforces per-tenant limits (configurable, default 10,000 req/min)
- [ ] Rate limiting returns `RESOURCE_EXHAUSTED` gRPC status when limit exceeded
- [ ] Logging interceptor logs all requests with: method, tenant, user (masked), duration, status
- [ ] PII masking replaces phone numbers (`+27*****1234`) and account IDs (`acc_****abcd`) in logs
- [ ] Gateway routes calls to Core Banking service successfully
- [ ] Health check endpoint returns healthy status
- [ ] gRPC reflection is enabled when `ASPNETCORE_ENVIRONMENT=Development`
- [ ] Rate limits are configurable per tenant via `system_config` table

---

## Technical Notes

### Components

**Project:** `GoldBank.Gateway`

**File Structure:**
```
GoldBank.Gateway/
  Program.cs
  appsettings.json
  appsettings.Development.json
  Interceptors/
    AuthInterceptor.cs
    TenantInterceptor.cs
    RateLimitInterceptor.cs
    LoggingInterceptor.cs
  Services/
    HealthService.cs
  Configuration/
    JwtSettings.cs
    RateLimitSettings.cs
    RoutingSettings.cs
  Middleware/
    PiiMasker.cs
  Extensions/
    ServiceCollectionExtensions.cs
```

### Interceptor Pipeline Implementation

**Program.cs Configuration:**
```csharp
var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel for HTTP/2
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(5000, listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http2;
    });
    options.ListenAnyIP(5001, listenOptions =>
    {
        listenOptions.Protocols = HttpProtocols.Http2;
        listenOptions.UseHttps();
    });
});

// Add services
builder.Services.AddGrpc(options =>
{
    options.Interceptors.Add<LoggingInterceptor>();
    options.Interceptors.Add<AuthInterceptor>();
    options.Interceptors.Add<TenantInterceptor>();
    options.Interceptors.Add<RateLimitInterceptor>();
    options.MaxReceiveMessageSize = 4 * 1024 * 1024; // 4 MB
    options.MaxSendMessageSize = 16 * 1024 * 1024;   // 16 MB
});

builder.Services.AddGrpcReflection();
builder.Services.AddGrpcHealthChecks();

// Redis for rate limiting
builder.Services.AddSingleton<IConnectionMultiplexer>(
    ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis")!));

// JWT configuration
builder.Services.Configure<JwtSettings>(
    builder.Configuration.GetSection("Jwt"));

// Serilog
builder.Host.UseSerilog((context, config) =>
    config.ReadFrom.Configuration(context.Configuration));

var app = builder.Build();

// gRPC reflection (dev only)
if (app.Environment.IsDevelopment())
{
    app.MapGrpcReflectionService();
}

app.MapGrpcHealthChecksService();
// Map gRPC service routes...
app.Run();
```

**AuthInterceptor.cs:**
```csharp
public class AuthInterceptor : Interceptor
{
    private readonly JwtSettings _jwtSettings;
    private readonly ILogger<AuthInterceptor> _logger;

    // Methods that don't require authentication
    private static readonly HashSet<string> _anonymousMethods = new()
    {
        "/goldbank.v1.accounts.AccountService/Register",
        "/goldbank.v1.accounts.AccountService/VerifyOTP",
        "/grpc.health.v1.Health/Check"
    };

    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        var method = context.Method;

        if (_anonymousMethods.Contains(method))
            return await continuation(request, context);

        var token = context.RequestHeaders
            .FirstOrDefault(h => h.Key == "authorization")?.Value;

        if (string.IsNullOrEmpty(token) || !token.StartsWith("Bearer "))
            throw new RpcException(new Status(StatusCode.Unauthenticated,
                "Missing or invalid authorization token"));

        var jwt = token.Substring("Bearer ".Length);
        var principal = ValidateToken(jwt);

        // Add claims to call context for downstream interceptors
        context.UserState["UserId"] = principal.FindFirst("sub")?.Value;
        context.UserState["TenantId"] = principal.FindFirst("tenant_id")?.Value;
        context.UserState["DeviceId"] = principal.FindFirst("device_id")?.Value;
        context.UserState["Roles"] = principal.FindAll("role").Select(c => c.Value).ToList();

        return await continuation(request, context);
    }

    // Similar overrides for ServerStreamingServerHandler, etc.
}
```

**TenantInterceptor.cs:**
```csharp
public class TenantInterceptor : Interceptor
{
    private readonly ITenantProvider _tenantProvider;

    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        // Try JWT claim first
        var tenantId = context.UserState.ContainsKey("TenantId")
            ? context.UserState["TenantId"] as string
            : null;

        // Fall back to gRPC metadata header (for anonymous endpoints)
        if (string.IsNullOrEmpty(tenantId))
        {
            tenantId = context.RequestHeaders
                .FirstOrDefault(h => h.Key == "x-tenant-id")?.Value;
        }

        if (string.IsNullOrEmpty(tenantId))
            throw new RpcException(new Status(StatusCode.InvalidArgument,
                "Tenant identification required"));

        var tenantInfo = await _tenantProvider.GetTenantByIdAsync(Guid.Parse(tenantId));
        if (tenantInfo == null || tenantInfo.Status != "active")
            throw new RpcException(new Status(StatusCode.PermissionDenied,
                "Tenant not found or inactive"));

        context.UserState["TenantInfo"] = tenantInfo;
        return await continuation(request, context);
    }
}
```

**RateLimitInterceptor.cs (Redis Sliding Window):**
```csharp
public class RateLimitInterceptor : Interceptor
{
    private readonly IConnectionMultiplexer _redis;
    private readonly RateLimitSettings _settings;

    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        var userId = context.UserState.ContainsKey("UserId")
            ? context.UserState["UserId"] as string : "anonymous";
        var tenantId = context.UserState.ContainsKey("TenantId")
            ? context.UserState["TenantId"] as string : "unknown";

        var db = _redis.GetDatabase();
        var now = DateTimeOffset.UtcNow;
        var windowStart = now.AddSeconds(-_settings.WindowSeconds);

        // Per-user rate limit
        var userKey = $"ratelimit:user:{userId}";
        await CheckRateLimit(db, userKey, windowStart, now,
            _settings.PerUserLimit, "Per-user rate limit exceeded");

        // Per-tenant rate limit
        var tenantKey = $"ratelimit:tenant:{tenantId}";
        await CheckRateLimit(db, tenantKey, windowStart, now,
            _settings.PerTenantLimit, "Per-tenant rate limit exceeded");

        return await continuation(request, context);
    }

    private async Task CheckRateLimit(IDatabase db, string key,
        DateTimeOffset windowStart, DateTimeOffset now,
        int limit, string errorMessage)
    {
        // Remove entries outside the window
        await db.SortedSetRemoveRangeByScoreAsync(key, 0,
            windowStart.ToUnixTimeMilliseconds());

        // Count entries in the window
        var count = await db.SortedSetLengthAsync(key);

        if (count >= limit)
            throw new RpcException(new Status(StatusCode.ResourceExhausted,
                errorMessage));

        // Add current request
        await db.SortedSetAddAsync(key, Guid.NewGuid().ToString(),
            now.ToUnixTimeMilliseconds());

        // Set TTL on the key
        await db.KeyExpireAsync(key, TimeSpan.FromSeconds(_settings.WindowSeconds + 10));
    }
}
```

**LoggingInterceptor.cs with PII Masking:**
```csharp
public class LoggingInterceptor : Interceptor
{
    private readonly ILogger<LoggingInterceptor> _logger;
    private readonly PiiMasker _masker;

    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        var stopwatch = Stopwatch.StartNew();
        var method = context.Method;
        var tenantId = context.UserState.ContainsKey("TenantId")
            ? context.UserState["TenantId"] as string : "unknown";
        var userId = context.UserState.ContainsKey("UserId")
            ? _masker.MaskId(context.UserState["UserId"] as string) : "anonymous";

        _logger.LogInformation(
            "gRPC Request: {Method} | Tenant: {TenantId} | User: {UserId}",
            method, tenantId, userId);

        try
        {
            var response = await continuation(request, context);
            stopwatch.Stop();

            _logger.LogInformation(
                "gRPC Response: {Method} | Status: OK | Duration: {Duration}ms",
                method, stopwatch.ElapsedMilliseconds);

            return response;
        }
        catch (RpcException ex)
        {
            stopwatch.Stop();
            _logger.LogWarning(
                "gRPC Response: {Method} | Status: {Status} | Duration: {Duration}ms | Message: {Message}",
                method, ex.StatusCode, stopwatch.ElapsedMilliseconds,
                _masker.MaskPii(ex.Status.Detail));
            throw;
        }
    }
}
```

**PiiMasker.cs:**
```csharp
public class PiiMasker
{
    // +27821234567 -> +27*****4567
    private static readonly Regex PhoneRegex = new(@"\+\d{2}(\d{5})(\d{4})",
        RegexOptions.Compiled);

    // UUID -> ****last4
    public string MaskId(string? id)
    {
        if (string.IsNullOrEmpty(id) || id.Length < 4) return "****";
        return $"****{id[^4..]}";
    }

    public string MaskPhone(string phone)
    {
        return PhoneRegex.Replace(phone, m => $"+{m.Value[1..3]}*****{m.Groups[2].Value}");
    }

    public string MaskPii(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        var masked = PhoneRegex.Replace(text, m =>
            $"+{m.Value[1..3]}*****{m.Groups[2].Value}");
        return masked;
    }
}
```

### Configuration Models

**JwtSettings.cs:**
```csharp
public class JwtSettings
{
    public string Secret { get; set; } = string.Empty;
    public string Issuer { get; set; } = "goldbank";
    public string Audience { get; set; } = "goldbank-api";
    public int AccessTokenExpiryMinutes { get; set; } = 30;
    public int RefreshTokenExpiryDays { get; set; } = 30;
}
```

**RateLimitSettings.cs:**
```csharp
public class RateLimitSettings
{
    public int PerUserLimit { get; set; } = 100;        // requests per window
    public int PerTenantLimit { get; set; } = 10000;    // requests per window
    public int WindowSeconds { get; set; } = 60;         // sliding window size
}
```

**appsettings.json:**
```json
{
  "ConnectionStrings": {
    "Redis": "localhost:6379"
  },
  "Jwt": {
    "Secret": "${JWT_SECRET}",
    "Issuer": "goldbank",
    "Audience": "goldbank-api",
    "AccessTokenExpiryMinutes": 30
  },
  "RateLimit": {
    "PerUserLimit": 100,
    "PerTenantLimit": 10000,
    "WindowSeconds": 60
  },
  "Routing": {
    "CoreBankingUrl": "https://localhost:5002"
  },
  "Serilog": {
    "MinimumLevel": "Information",
    "WriteTo": [
      { "Name": "Console" }
    ]
  }
}
```

### API / gRPC Endpoints

The Gateway does not define its own gRPC services (except Health). It proxies all calls to downstream services.

**Health Endpoint:**
- Method: `grpc.health.v1.Health/Check`
- No authentication required
- Returns: `SERVING` when gateway is operational

**Downstream Routing:**
| Client Request | Downstream Service | Downstream URL |
|---|---|---|
| `goldbank.v1.accounts.*` | Core Banking | `https://core:5002` |
| `goldbank.v1.payments.*` | Core Banking | `https://core:5002` |
| `goldbank.v1.transfers.*` | Core Banking | `https://core:5002` |
| `goldbank.v1.agents.*` | Core Banking | `https://core:5002` |
| `goldbank.v1.billpay.*` | Core Banking | `https://core:5002` |
| `goldbank.v1.merchants.*` | Core Banking | `https://core:5002` |
| `goldbank.v1.terminals.*` | Terminal Manager | `https://terminal-manager:5004` |
| `goldbank.v1.admin.*` | Core Banking | `https://core:5002` |
| `goldbank.v1.reporting.*` | Reporting | `https://reporting:5006` |
| `goldbank.v1.hsm.*` | HSM (internal only) | Not routed from Gateway |

### Database Changes
None directly. Rate limit configuration can optionally be loaded from `system_config` table (STORY-003).

### Security Considerations
- JWT secret must be at minimum 256 bits (32 bytes) for HS256
- Tokens must use short expiry (30 minutes for access, 30 days for refresh)
- Rate limiting prevents brute force attacks on PIN verification
- PII masking in logs prevents phone number and account ID exposure in log aggregation systems
- gRPC reflection must be disabled in production (`ASPNETCORE_ENVIRONMENT=Production`)
- HSM service should never be routable from the external gateway
- Consider implementing request signing for mobile app requests (future)
- TLS certificate should use at minimum TLS 1.2

### Edge Cases
- Expired JWT: Return `UNAUTHENTICATED` with clear message for client to refresh
- Invalid tenant ID in JWT: Return `PERMISSION_DENIED`
- Redis unavailable for rate limiting: Fail open (allow requests) or fail closed (deny)? -- recommend fail open with alert, configurable
- gRPC streaming: Interceptors must handle streaming calls differently (logging at stream start/end)
- Clock skew between services: JWT validation should allow small clock skew (30 seconds)
- Concurrent requests from same user: Redis sorted set handles this correctly with atomic operations
- Large request payloads: Configure `MaxReceiveMessageSize` to prevent abuse

---

## Dependencies

**Prerequisite Stories:**
- STORY-004: gRPC Proto Definitions & Shared Contracts (need compiled proto types for routing)

**Blocked Stories:**
- STORY-009: User Self-Registration (needs Gateway for client-facing endpoint)
- STORY-010: Create Account PIN (needs Gateway for authenticated endpoint)

**External Dependencies:**
- Redis instance (from STORY-002 Docker Compose)
- Valid TLS certificate (development certificate for local, real cert for staging/production)
- JWT library: `Microsoft.AspNetCore.Authentication.JwtBearer`

---

## Definition of Done

- [ ] Code implemented and committed
- [ ] Unit tests written and passing (>=80% coverage) for all interceptors
- [ ] Integration tests passing (gateway accepts and routes gRPC calls)
- [ ] Code reviewed and approved
- [ ] Documentation updated (gateway configuration, rate limit tuning guide)
- [ ] Acceptance criteria validated
- [ ] Deployed to staging

---

## Progress Tracking

**Status History:**
- 2026-02-24: Created

**Actual Effort:** TBD

---

**This story was created using BMAD Method v6 - Phase 4 (Implementation)**
