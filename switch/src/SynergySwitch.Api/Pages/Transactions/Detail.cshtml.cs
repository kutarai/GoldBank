using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SynergySwitch.Core.Interfaces;
using SynergySwitch.Core.Models;

namespace SynergySwitch.Api.Pages.Transactions;

public class DetailModel : PageModel
{
    private readonly IDashboardService _dashboard;

    public DetailModel(IDashboardService dashboard)
    {
        _dashboard = dashboard;
    }

    public TransactionSummary? Transaction { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        Transaction = await _dashboard.GetTransactionByIdAsync(id);
        if (Transaction == null)
            return NotFound();
        return Page();
    }
}
