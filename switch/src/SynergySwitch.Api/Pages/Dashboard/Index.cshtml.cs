using Microsoft.AspNetCore.Mvc.RazorPages;
using SynergySwitch.Core.Interfaces;
using SynergySwitch.Core.Iso8583;
using SynergySwitch.Core.Models;

namespace SynergySwitch.Api.Pages.Dashboard;

public class IndexModel : PageModel
{
    private readonly IDashboardService _dashboard;
    private readonly GatewayConnectionPool _connectionPool;

    public IndexModel(IDashboardService dashboard, GatewayConnectionPool connectionPool)
    {
        _dashboard = dashboard;
        _connectionPool = connectionPool;
    }

    public DashboardSummary Summary { get; set; } = default!;
    public DashboardSummary TodaySummary { get; set; } = default!;
    public IReadOnlyList<HourlyThroughput> HourlyData { get; set; } = [];
    public List<GatewayPoolStatus> GatewayStatuses { get; set; } = [];

    public async Task OnGetAsync()
    {
        Summary = await _dashboard.GetSummaryAsync();

        var todayStart = DateTime.UtcNow.Date;
        TodaySummary = await _dashboard.GetSummaryAsync(from: todayStart);

        HourlyData = await _dashboard.GetHourlyThroughputAsync(
            DateTime.UtcNow.AddHours(-24), DateTime.UtcNow);

        GatewayStatuses = _connectionPool.GetAllPoolStatuses();
    }
}
