using Microsoft.EntityFrameworkCore;
using Serilog;
using UniBank.TerminalManager.Grpc;
using UniBank.TerminalManager.Infrastructure;
using UniBank.TerminalManager.Mqtt;
using UniBank.TerminalManager.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSerilog(configuration =>
    configuration.ReadFrom.Configuration(builder.Configuration));

// STORY-046: EF Core DbContext for terminal data (separate from Core)
builder.Services.AddDbContext<TerminalDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("TerminalDb"),
        npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "terminal_mgmt")));

// STORY-007: MQTT broker for terminal communication
builder.Services.AddSingleton<ITerminalAuthenticator, TerminalAuthenticator>();

// STORY-046: Terminal registration service
builder.Services.AddScoped<TerminalRegistrationService>();

// STORY-047: Terminal key management via HSM
builder.Services.AddScoped<TerminalKeyManager>();

// STORY-048: Terminal status monitoring
builder.Services.AddScoped<TerminalMonitoringService>();

// STORY-049: Remote terminal software updates
builder.Services.AddScoped<TerminalUpdateService>();

// MQTT topic handler depends on scoped services, so register as scoped and
// use a scope in the MqttBrokerService when handling messages
builder.Services.AddScoped<MqttTopicHandler>();
builder.Services.AddHostedService<MqttBrokerService>();

// gRPC services (STORY-046, STORY-048, STORY-049)
builder.Services.AddGrpc(options =>
{
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
});

var app = builder.Build();

// Map gRPC service
app.MapGrpcService<TerminalGrpcService>();

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new
{
    Status = "Healthy",
    Service = "UniBank.TerminalManager",
    Timestamp = DateTime.UtcNow
}));

app.Run();
