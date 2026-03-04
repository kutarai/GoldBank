using Serilog;
using UniBank.HSM.Pkcs11;
using UniBank.HSM.Services;

var builder = WebApplication.CreateBuilder(args);

// ── Serilog ──────────────────────────────────────────────────────
builder.Host.UseSerilog((context, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration));

// ── gRPC ─────────────────────────────────────────────────────────
builder.Services.AddGrpc();

// ── HSM services ─────────────────────────────────────────────────
builder.Services.AddSingleton<SoftHsmProvider>();
builder.Services.AddSingleton<HsmCircuitBreaker>();

// ── Health checks ────────────────────────────────────────────────
builder.Services.AddHealthChecks();

var app = builder.Build();

// ── Middleware pipeline ──────────────────────────────────────────
app.MapGrpcService<HsmGrpcService>();
app.MapHealthChecks("/health");

app.Run();
