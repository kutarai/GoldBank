using Microsoft.Extensions.DependencyInjection;
using UniBank.Reporting.Services;

namespace UniBank.Reporting;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddReporting(this IServiceCollection services)
    {
        // Sprint 7 - Reporting services (STORY-062 to STORY-067)
        services.AddScoped<DashboardService>();
        services.AddScoped<UserGrowthReportService>();
        services.AddScoped<MerchantReportService>();
        services.AddScoped<RevenueReportService>();
        services.AddScoped<ReconReportService>();
        services.AddScoped<ReportExportService>();

        return services;
    }
}
