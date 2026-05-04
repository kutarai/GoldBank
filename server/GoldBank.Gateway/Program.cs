using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.IdentityModel.Tokens;
using Prometheus;
using Serilog;
using Serilog.Events;
using StackExchange.Redis;
using GoldBank.Core.Common.Extensions;
using GoldBank.Core.Modules.Accounts.Grpc;
using GoldBank.Core.Modules.Admin.Grpc;
using GoldBank.Core.Modules.Agents.Grpc;
using GoldBank.Core.Modules.BillPay.Grpc;
using GoldBank.Core.Modules.KYC.Grpc;
using GoldBank.Core.Modules.Merchants.Grpc;
using GoldBank.Core.Modules.Payments.Grpc;
using GoldBank.Core.Modules.Transfers.Grpc;
using GoldBank.Core.Modules.Loans.Grpc;
using GoldBank.Core.Modules.WhiteLabel.Grpc;
using GoldBank.Core.Modules.CardTransactions.Grpc;
using GoldBank.Core.Modules.AI.Grpc;
using GoldBank.Core.Modules.AssetCustody.Grpc;
using GoldBank.Core.Modules.Ekub.Grpc;
using GoldBank.Gateway.Configuration;
using GoldBank.Gateway.Interceptors;
using GoldBank.Gateway.Services;
using GoldBank.Reporting;
using GoldBank.Reporting.Grpc;

// ---------------------------------------------------------------------------
// Bootstrap Serilog early so startup errors are captured
// ---------------------------------------------------------------------------
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting GoldBank API Gateway");

    var builder = WebApplication.CreateBuilder(args);

    // ---------------------------------------------------------------------------
    // Serilog - full configuration from appsettings
    // ---------------------------------------------------------------------------
    builder.Host.UseSerilog((context, services, configuration) =>
        configuration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", "GoldBank.Gateway")
            .Enrich.WithProperty("Environment", context.HostingEnvironment.EnvironmentName));

    // ---------------------------------------------------------------------------
    // Kestrel - HTTP/2 configuration for gRPC
    // ---------------------------------------------------------------------------
    builder.WebHost.ConfigureKestrel(options =>
    {
        // Primary gRPC port (HTTP/2 only, no TLS for inter-service / dev)
        options.ListenAnyIP(1111, listenOptions =>
        {
            listenOptions.Protocols = HttpProtocols.Http2;
        });

        // HTTP/1.1 port for health checks, metrics, and REST endpoints
        options.ListenAnyIP(1112, listenOptions =>
        {
            listenOptions.Protocols = HttpProtocols.Http1AndHttp2;
        });
    });

    // ---------------------------------------------------------------------------
    // Configuration bindings
    // ---------------------------------------------------------------------------
    var jwtSection = builder.Configuration.GetSection(JwtSettings.SectionName);
    var rateLimitSection = builder.Configuration.GetSection(RateLimitSettings.SectionName);

    builder.Services.Configure<JwtSettings>(jwtSection);
    builder.Services.Configure<RateLimitSettings>(rateLimitSection);

    var jwtSettings = jwtSection.Get<JwtSettings>()
        ?? throw new InvalidOperationException("JWT settings are not configured. Add a 'Jwt' section to appsettings.json.");

    // ---------------------------------------------------------------------------
    // JWT Authentication
    // ---------------------------------------------------------------------------
    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
            options.SaveToken = true;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwtSettings.Issuer,
                ValidateAudience = true,
                ValidAudience = jwtSettings.Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SecretKey)),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(jwtSettings.ClockSkewSeconds),
            };
        });

    builder.Services.AddAuthorization();

    // ---------------------------------------------------------------------------
    // REST API controllers + CORS for the bank-client React app (localhost:5173)
    // ---------------------------------------------------------------------------
    builder.Services.AddControllers();

    builder.Services.AddCors(options => options.AddPolicy("BankClient", p =>
        p.WithOrigins(
            "http://localhost:5173",  // bank-client (admin portal)
            "http://localhost:5174")  // bank-teller (EPIC-021)
         .AllowAnyHeader()
         .AllowAnyMethod()));

    // ---------------------------------------------------------------------------
    // Redis (optional — used only by RateLimitInterceptor for gRPC rate limiting)
    // All other caching uses ICacheStore (PostgreSQL-backed) registered in AddCoreModules.
    // ---------------------------------------------------------------------------
    var redisConnectionString = builder.Configuration.GetValue<string>("Redis:ConnectionString");
    if (!string.IsNullOrWhiteSpace(redisConnectionString))
    {
        try
        {
            var redisOptions = ConfigurationOptions.Parse(redisConnectionString);
            redisOptions.AbortOnConnectFail = false;
            redisOptions.ConnectRetry = 3;
            redisOptions.ConnectTimeout = 5000;

            var redis = ConnectionMultiplexer.Connect(redisOptions);
            builder.Services.AddSingleton<IConnectionMultiplexer>(redis);
            Log.Information("Redis connected: {Endpoints}", string.Join(", ", redisOptions.EndPoints));
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Redis connection failed. Rate limiting will be disabled. Endpoint: {Endpoint}",
                redisConnectionString);
        }
    }
    else
    {
        Log.Information("Redis not configured. gRPC rate limiting will be disabled. All caching uses PostgreSQL.");
    }

    // ---------------------------------------------------------------------------
    // Core modules (EF Core, handlers, tenant provider) + Reporting services
    // ---------------------------------------------------------------------------
    builder.Services.AddCoreModules(builder.Configuration);
    builder.Services.AddReporting();

    // ---------------------------------------------------------------------------
    // gRPC interceptors (registered in pipeline order: outermost first)
    // Order: Logging -> Auth -> Tenant -> RateLimit -> [service handler]
    // ---------------------------------------------------------------------------
    builder.Services.AddSingleton<LoggingInterceptor>();
    builder.Services.AddSingleton<AuthInterceptor>();
    builder.Services.AddSingleton<TenantInterceptor>();
    builder.Services.AddSingleton<RateLimitInterceptor>();

    // BranchCash module services (STORY-151)
    builder.Services.AddScoped<GoldBank.Core.Modules.BranchCash.Application.Services.DenominationValidationService>();
    builder.Services.AddScoped<GoldBank.Core.Modules.BranchCash.Application.Services.VaultStockService>();
    builder.Services.AddScoped<GoldBank.Gateway.Services.VaultReportPdfService>();

    // Receipt PDF service (STORY-158)
    builder.Services.AddScoped<GoldBank.Gateway.Services.ReceiptPdfService>();
    // EOD report PDF service (STORY-159)
    builder.Services.AddScoped<GoldBank.Gateway.Services.EodReportPdfService>();

    // ---------------------------------------------------------------------------
    // gRPC services
    // ---------------------------------------------------------------------------
    builder.Services.AddGrpc(options =>
    {
        options.EnableDetailedErrors = builder.Environment.IsDevelopment();
        options.MaxReceiveMessageSize = 16 * 1024 * 1024;  // 16 MB
        options.MaxSendMessageSize = 16 * 1024 * 1024;     // 16 MB

        // Interceptor pipeline order: first added = outermost (executed first)
        options.Interceptors.Add<LoggingInterceptor>();
        options.Interceptors.Add<AuthInterceptor>();
        options.Interceptors.Add<TenantInterceptor>();
        options.Interceptors.Add<RateLimitInterceptor>();
    });

    builder.Services.AddGrpcReflection();
    builder.Services.AddHttpContextAccessor();

    // ---------------------------------------------------------------------------
    // Build the application
    // ---------------------------------------------------------------------------
    var app = builder.Build();

    // ---------------------------------------------------------------------------
    // Middleware pipeline
    // ---------------------------------------------------------------------------
    app.UseSerilogRequestLogging(options =>
    {
        options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        {
            diagnosticContext.Set("RequestHost", httpContext.Request.Host.Value ?? "unknown");
            diagnosticContext.Set("UserAgent", httpContext.Request.Headers.UserAgent.ToString());
        };
    });

    app.UseCors("BankClient");
    app.UseAuthentication();
    app.UseAuthorization();

    // Prometheus metrics endpoint on HTTP/1.1 port
    app.UseMetricServer();

    // ---------------------------------------------------------------------------
    // Map gRPC services
    // ---------------------------------------------------------------------------

    // gRPC Health checking service
    app.MapGrpcService<HealthService>();

    // Core banking gRPC services
    app.MapGrpcService<AccountGrpcService>();
    app.MapGrpcService<PaymentGrpcService>();
    app.MapGrpcService<TransferGrpcService>();
    app.MapGrpcService<BillPayGrpcService>();
    app.MapGrpcService<AgentGrpcService>();
    app.MapGrpcService<KycGrpcService>();
    app.MapGrpcService<MerchantGrpcService>();
    app.MapGrpcService<WhiteLabelGrpcService>();
    app.MapGrpcService<LoanGrpcService>();

    // Card transaction processing gRPC service (Sprint 9)
    app.MapGrpcService<CardTransactionGrpcService>();

    // Admin and Reporting gRPC services
    app.MapGrpcService<AdminGrpcService>();
    app.MapGrpcService<AiGrpcService>();
    app.MapGrpcService<ReportingGrpcService>();

    // Sprint 22 - Asset Custody gRPC service (EPIC-020, STORY-137)
    app.MapGrpcService<AssetGrpcService>();
    app.MapGrpcService<EkubGrpcService>();

    // gRPC Server Reflection (always enabled - clients need service discovery)
    app.MapGrpcReflectionService();

    // ---------------------------------------------------------------------------
    // HTTP health / readiness endpoints (for load balancers and Kubernetes)
    // ---------------------------------------------------------------------------
    app.MapGet("/health", () => Results.Ok(new
    {
        Status = "Healthy",
        Service = "GoldBank.Gateway",
        Timestamp = DateTime.UtcNow,
        Version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "1.0.0"
    })).RequireCors("BankClient");

    app.MapGet("/ready", async (IServiceProvider sp) =>
    {
        var checks = new Dictionary<string, object>();
        var isReady = true;

        // Check Redis
        var redis = sp.GetService<IConnectionMultiplexer>();
        if (redis is not null)
        {
            try
            {
                var db = redis.GetDatabase();
                var latency = await db.PingAsync();
                checks["redis"] = new { Status = "Connected", LatencyMs = latency.TotalMilliseconds };
            }
            catch (Exception ex)
            {
                checks["redis"] = new { Status = "Disconnected", Error = ex.Message };
                isReady = false;
            }
        }
        else
        {
            checks["redis"] = new { Status = "NotConfigured" };
        }

        return isReady
            ? Results.Ok(new { Status = "Ready", Checks = checks, Timestamp = DateTime.UtcNow })
            : Results.Json(new { Status = "NotReady", Checks = checks, Timestamp = DateTime.UtcNow },
                statusCode: StatusCodes.Status503ServiceUnavailable);
    });

    // REST API controllers (admin endpoints for bank-client)
    app.MapControllers();

    // ---------------------------------------------------------------------------
    // Run
    // ---------------------------------------------------------------------------
    Log.Information("GoldBank API Gateway started. gRPC on :1111, HTTP on :1112");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "GoldBank API Gateway terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
