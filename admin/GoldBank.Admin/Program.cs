using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor.Services;
using GoldBank.Admin.Components;
using GoldBank.Admin.Services;
using GoldBank.Protos.Admin;
using GoldBank.Protos.Reporting;

var builder = WebApplication.CreateBuilder(args);

// MudBlazor
builder.Services.AddMudServices();

// Authentication + Authorization + custom auth state provider
builder.Services.AddAuthentication("Cookies")
    .AddCookie("Cookies", options =>
    {
        options.LoginPath = "/login";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
    });
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<AuthenticationStateProvider, AdminAuthStateProvider>();
builder.Services.AddScoped<AdminAuthStateProvider>(sp =>
    (AdminAuthStateProvider)sp.GetRequiredService<AuthenticationStateProvider>());

// Razor Components
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// gRPC clients — inject x-tenant-id header for TenantInterceptor
var gatewayUrl = builder.Configuration.GetValue<string>("GrpcGateway:Url") ?? "http://localhost:1111";
var tenantId = builder.Configuration.GetValue<string>("GrpcGateway:TenantId") ?? "default";

var callCredentials = CallCredentials.FromInterceptor((_, metadata) =>
{
    metadata.Add("x-tenant-id", tenantId);
    return Task.CompletedTask;
});

var channel = GrpcChannel.ForAddress(gatewayUrl, new GrpcChannelOptions
{
    Credentials = ChannelCredentials.Create(ChannelCredentials.Insecure, callCredentials),
    UnsafeUseInsecureChannelCallCredentials = true
});

builder.Services.AddSingleton(channel);
builder.Services.AddSingleton(new AdminService.AdminServiceClient(channel));
builder.Services.AddSingleton(new ReportingService.ReportingServiceClient(channel));

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
