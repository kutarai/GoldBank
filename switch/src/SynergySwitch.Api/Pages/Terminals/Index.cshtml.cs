using Microsoft.AspNetCore.Mvc.RazorPages;
using SynergySwitch.Core.Interfaces;
using SynergySwitch.Core.Models;

namespace SynergySwitch.Api.Pages.Terminals;

public class IndexModel : PageModel
{
    private readonly IDashboardService _dashboard;

    public IndexModel(IDashboardService dashboard)
    {
        _dashboard = dashboard;
    }

    public IReadOnlyList<TerminalStatus> Terminals { get; set; } = [];

    public async Task OnGetAsync()
    {
        Terminals = await _dashboard.GetTerminalStatusesAsync();
    }
}
