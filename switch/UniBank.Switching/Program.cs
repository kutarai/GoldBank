using Serilog;
using UniBank.Switching.Adapters;
using UniBank.Switching.Routing;
using UniBank.Switching.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSerilog(configuration =>
    configuration.ReadFrom.Configuration(builder.Configuration));

// Register gRPC services
builder.Services.AddGrpc();

// Register adapters as singletons (stateless, thread-safe)
builder.Services.AddSingleton<Iso8583Adapter>();
builder.Services.AddSingleton<Iso20022Adapter>();

// Register the institution registry and seed defaults
builder.Services.AddSingleton<InstitutionRegistry>(sp =>
{
    var registry = new InstitutionRegistry();
    registry.SeedDefaults();
    return registry;
});

// Register routing components
builder.Services.AddSingleton<MessageRouter>();
builder.Services.AddSingleton<OutboundRouter>();
builder.Services.AddSingleton<InboundProcessor>();

// Register reconciliation services
builder.Services.AddSingleton<SettlementFileGenerator>();
builder.Services.AddSingleton<ReconciliationService>();

// Register background services
builder.Services.AddHostedService<TcpListenerService>();

var app = builder.Build();

// Map gRPC service endpoint
app.MapGrpcService<SwitchGrpcService>();

app.Run();
