using Microsoft.EntityFrameworkCore;
using Prometheus;
using SynergySwitch.Api.Services;
using SynergySwitch.Core.Dashboard;
using SynergySwitch.Core.Interfaces;
using SynergySwitch.Core.Iso20022;
using SynergySwitch.Core.Gateway;
using SynergySwitch.Core.Iso8583;
using SynergySwitch.Core.Iso20022Bank;
using SynergySwitch.Core.MobileMoneyPayment;
using SynergySwitch.Core.QrPayment;
using SynergySwitch.Core.Terminal;
using SynergySwitch.Data;

var builder = WebApplication.CreateBuilder(args);

// gRPC services
builder.Services.AddGrpc(options =>
{
    options.MaxReceiveMessageSize = 1 * 1024 * 1024; // 1 MB
    options.MaxSendMessageSize = 1 * 1024 * 1024;
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
});

// Razor Pages for admin dashboard
builder.Services.AddRazorPages();

// EF Core with PostgreSQL
var connectionString = builder.Configuration.GetConnectionString("SwitchDb")
    ?? "Host=localhost;Database=synergy_switch;Username=postgres;Password=postgres";

builder.Services.AddDbContext<SwitchDbContext>(options =>
    options.UseNpgsql(connectionString));

// Bank connection (ISO 8583 via Zimswitch — multi-gateway)
builder.Services.Configure<BankConnectionSettings>(
    builder.Configuration.GetSection(BankConnectionSettings.SectionName));
builder.Services.AddSingleton<Iso8583MessageLogger>();
builder.Services.AddSingleton<GatewayConnectionPool>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<GatewayConnectionPool>());
builder.Services.AddSingleton<Iso20022GrpcClient>();
builder.Services.AddSingleton<BankAuthorisationService>();
builder.Services.AddScoped<GatewayManager>();

// Business logic
builder.Services.AddScoped<IAuthorisationProcessor, AuthorisationProcessor>();
builder.Services.AddScoped<ITerminalManager, TerminalManager>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IQrPaymentManager, QrPaymentManager>();
builder.Services.AddScoped<IMobileMoneyPaymentManager, MobileMoneyPaymentManager>();

var app = builder.Build();

// Load gateway routing cache on startup
using (var scope = app.Services.CreateScope())
{
    var gatewayManager = scope.ServiceProvider.GetRequiredService<GatewayManager>();
    await gatewayManager.RefreshCacheAsync();
}

app.UseStaticFiles();
app.UseRouting();

// Prometheus metrics endpoint (/metrics)
app.UseHttpMetrics();
app.MapMetrics();

// Map gRPC services
app.MapGrpcService<PaymentGrpcService>();
app.MapGrpcService<TerminalManagementGrpcService>();

// Map Razor Pages (admin dashboard)
app.MapRazorPages();

app.MapGet("/", () => Results.Redirect("/Dashboard"));

await app.RunAsync();
