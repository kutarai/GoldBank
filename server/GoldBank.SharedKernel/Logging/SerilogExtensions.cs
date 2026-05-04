using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.Elasticsearch;

namespace GoldBank.SharedKernel.Logging;

/// <summary>
/// Extension methods for configuring Serilog across all GoldBank services.
/// Provides a consistent logging pipeline with Console (JSON), Elasticsearch,
/// and standard enrichers for every microservice.
/// </summary>
public static class SerilogExtensions
{
    /// <summary>
    /// Configures Serilog with Console JSON sink, Elasticsearch sink, and
    /// environment/machine-name enrichers. Call this on the <see cref="IHostBuilder"/>
    /// in each service's Program.cs.
    /// </summary>
    /// <param name="hostBuilder">The host builder to configure.</param>
    /// <param name="serviceName">
    /// Logical service name (e.g. "GoldBank.Gateway"). Used as the Elasticsearch
    /// index prefix and added as a property to every log event.
    /// </param>
    /// <returns>The host builder for chaining.</returns>
    public static IHostBuilder UseGoldBankSerilog(
        this IHostBuilder hostBuilder,
        string serviceName)
    {
        return hostBuilder.UseSerilog((context, services, loggerConfiguration) =>
        {
            var elasticUri = context.Configuration["Elasticsearch:Uri"]
                             ?? "http://elasticsearch:9200";

            loggerConfiguration
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.Hosting.Lifetime", LogEventLevel.Information)
                .MinimumLevel.Override("System", LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .Enrich.WithMachineName()
                .Enrich.WithEnvironmentName()
                .Enrich.WithProperty("ServiceName", serviceName)
                .WriteTo.Console(new Serilog.Formatting.Json.JsonFormatter())
                .WriteTo.Elasticsearch(new ElasticsearchSinkOptions(new Uri(elasticUri))
                {
                    AutoRegisterTemplate = true,
                    AutoRegisterTemplateVersion = AutoRegisterTemplateVersion.ESv7,
                    IndexFormat = $"goldbank-logs-{serviceName.ToLowerInvariant().Replace(".", "-")}-{{0:yyyy.MM.dd}}",
                    NumberOfShards = 1,
                    NumberOfReplicas = 0,
                    MinimumLogEventLevel = LogEventLevel.Information
                });
        });
    }
}
