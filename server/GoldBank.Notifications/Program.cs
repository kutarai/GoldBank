using System.Reflection;
using Serilog;
using GoldBank.Notifications.Configuration;
using GoldBank.Notifications.Services;
using GoldBank.SharedKernel.Messaging;

var builder = Host.CreateApplicationBuilder(args);

// Structured logging via Serilog
builder.Services.AddSerilog(configuration =>
    configuration.ReadFrom.Configuration(builder.Configuration));

// Bind notification settings from configuration
builder.Services.Configure<NotificationSettings>(
    builder.Configuration.GetSection(NotificationSettings.SectionName));

// Register notification channel providers (console stubs for development)
builder.Services.AddSingleton<ISmsProvider, ConsoleSmsProvider>();
builder.Services.AddSingleton<IPushNotificationProvider, ConsolePushProvider>();

// Register template and preference stores (in-memory for development)
builder.Services.AddSingleton<INotificationTemplateStore, InMemoryNotificationTemplateStore>();
builder.Services.AddSingleton<INotificationPreferenceStore, DefaultNotificationPreferenceStore>();

// Register core notification services
builder.Services.AddSingleton<TemplateEngine>();
builder.Services.AddSingleton<NotificationRateLimiter>();
builder.Services.AddScoped<NotificationOrchestrator>();

// Register in-process message bus and auto-discover all IMessageHandler<T> implementations
// in this assembly (Handlers/ folder). When WolverineFx ships .NET 10 support, replace
// this with Wolverine's built-in host configuration.
builder.Services.AddInProcessMessaging(Assembly.GetExecutingAssembly());

var host = builder.Build();
host.Run();
