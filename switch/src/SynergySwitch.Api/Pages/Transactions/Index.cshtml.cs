using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SynergySwitch.Core.Interfaces;
using SynergySwitch.Core.Models;

namespace SynergySwitch.Api.Pages.Transactions;

public class IndexModel : PageModel
{
    private readonly IDashboardService _dashboard;

    public IndexModel(IDashboardService dashboard)
    {
        _dashboard = dashboard;
    }

    [BindProperty(SupportsGet = true)]
    public DateTime? FromDate { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? ToDate { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? TerminalId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? MerchantId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? ResponseCode { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? PanLastFour { get; set; }

    [BindProperty(SupportsGet = true)]
    public new int Page { get; set; } = 1;

    public PagedResult<TransactionSummary> Result { get; set; } = default!;

    public async Task OnGetAsync()
    {
        var filter = new TransactionFilter
        {
            FromDate = FromDate,
            ToDate = ToDate,
            TerminalId = TerminalId,
            MerchantId = MerchantId,
            ResponseCode = ResponseCode,
            PanLastFour = PanLastFour,
            Page = Page < 1 ? 1 : Page,
            PageSize = 25
        };

        Result = await _dashboard.GetTransactionsAsync(filter);
    }
}
