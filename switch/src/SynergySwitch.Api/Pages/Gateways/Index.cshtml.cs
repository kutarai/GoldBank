using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SynergySwitch.Core.Gateway;
using SynergySwitch.Core.Iso8583;
using SynergySwitch.Data.Entities;
using GatewayProtocol = SynergySwitch.Data.Entities.GatewayProtocol;

namespace SynergySwitch.Api.Pages.Gateways;

public class IndexModel : PageModel
{
    private readonly GatewayManager _gatewayManager;
    private readonly GatewayConnectionPool _connectionPool;

    public IndexModel(GatewayManager gatewayManager, GatewayConnectionPool connectionPool)
    {
        _gatewayManager = gatewayManager;
        _connectionPool = connectionPool;
    }

    public List<GatewayEntity> Gateways { get; set; } = [];
    public List<GatewayPoolStatus> PoolStatuses { get; set; } = [];
    public List<GatewayAuditLogEntity> AuditLog { get; set; } = [];

    // ── Form bindings ──
    [BindProperty] public string NewName { get; set; } = "";
    [BindProperty] public string NewHost { get; set; } = "";
    [BindProperty] public int NewPort { get; set; } = 9100;
    [BindProperty] public string NewAcquiringId { get; set; } = "000000";
    [BindProperty] public string NewNetworkId { get; set; } = "002";
    [BindProperty] public int NewPoolSize { get; set; } = 4;
    [BindProperty] public int NewTimeoutSeconds { get; set; } = 30;
    [BindProperty] public int NewPriority { get; set; } = 100;
    [BindProperty] public string? NewDescription { get; set; }
    [BindProperty] public string NewProtocol { get; set; } = "Iso8583";
    [BindProperty] public string NewBinPrefix { get; set; } = "";
    [BindProperty] public string? NewBinDescription { get; set; }

    // ── Edit form bindings ──
    [BindProperty] public int EditId { get; set; }
    [BindProperty] public string EditName { get; set; } = "";
    [BindProperty] public string EditHost { get; set; } = "";
    [BindProperty] public int EditPort { get; set; }
    [BindProperty] public string EditAcquiringId { get; set; } = "";
    [BindProperty] public string EditNetworkId { get; set; } = "";
    [BindProperty] public int EditPoolSize { get; set; }
    [BindProperty] public int EditTimeoutSeconds { get; set; }
    [BindProperty] public int EditPriority { get; set; }
    [BindProperty] public string? EditDescription { get; set; }
    [BindProperty] public bool EditSendLengthHeader { get; set; }
    [BindProperty] public bool EditOfflineMode { get; set; }
    [BindProperty] public string EditProtocol { get; set; } = "Iso8583";

    public int? EditingGatewayId { get; set; }

    public async Task OnGetAsync(int? editId = null)
    {
        await LoadDataAsync();
        if (editId.HasValue)
        {
            EditingGatewayId = editId;
            var gw = Gateways.FirstOrDefault(g => g.Id == editId);
            if (gw != null)
            {
                EditId = gw.Id;
                EditName = gw.Name;
                EditHost = gw.Host;
                EditPort = gw.Port;
                EditAcquiringId = gw.AcquiringInstitutionId;
                EditNetworkId = gw.NetworkId;
                EditPoolSize = gw.PoolSize;
                EditTimeoutSeconds = gw.TimeoutSeconds;
                EditPriority = gw.Priority;
                EditDescription = gw.Description;
                EditSendLengthHeader = gw.SendLengthHeader;
                EditOfflineMode = gw.OfflineMode;
                EditProtocol = gw.Protocol.ToString();
            }
        }
    }

    public async Task<IActionResult> OnPostAddGatewayAsync()
    {
        var gw = new GatewayEntity
        {
            Name = NewName.Trim(),
            Host = NewHost.Trim(),
            Port = NewPort,
            AcquiringInstitutionId = NewAcquiringId.Trim(),
            NetworkId = NewNetworkId.Trim(),
            PoolSize = NewPoolSize,
            TimeoutSeconds = NewTimeoutSeconds,
            Priority = NewPriority,
            Description = NewDescription?.Trim(),
            Protocol = Enum.TryParse<GatewayProtocol>(NewProtocol, out var p) ? p : GatewayProtocol.Iso8583,
            IsEnabled = true
        };
        await _gatewayManager.CreateGatewayAsync(gw);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostEditGatewayAsync()
    {
        var gw = await _gatewayManager.GetGatewayByIdAsync(EditId);
        if (gw == null) return RedirectToPage();

        gw.Name = EditName.Trim();
        gw.Host = EditHost.Trim();
        gw.Port = EditPort;
        gw.AcquiringInstitutionId = EditAcquiringId.Trim();
        gw.NetworkId = EditNetworkId.Trim();
        gw.PoolSize = EditPoolSize;
        gw.TimeoutSeconds = EditTimeoutSeconds;
        gw.Priority = EditPriority;
        gw.Description = EditDescription?.Trim();
        gw.SendLengthHeader = EditSendLengthHeader;
        gw.OfflineMode = EditOfflineMode;
        gw.Protocol = Enum.TryParse<GatewayProtocol>(EditProtocol, out var ep) ? ep : gw.Protocol;

        await _gatewayManager.UpdateGatewayAsync(gw);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostToggleAsync(int gatewayId, bool enable)
    {
        await _gatewayManager.SetGatewayEnabledAsync(gatewayId, enable);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteGatewayAsync(int gatewayId)
    {
        await _gatewayManager.DeleteGatewayAsync(gatewayId);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostAddBinAsync(int gatewayId)
    {
        if (!string.IsNullOrWhiteSpace(NewBinPrefix))
            await _gatewayManager.AddBinRouteAsync(gatewayId, NewBinPrefix.Trim(), NewBinDescription?.Trim());
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRemoveBinAsync(int binRouteId)
    {
        await _gatewayManager.RemoveBinRouteAsync(binRouteId);
        return RedirectToPage();
    }

    private async Task LoadDataAsync()
    {
        Gateways = await _gatewayManager.GetAllGatewaysAsync();
        PoolStatuses = _connectionPool.GetAllPoolStatuses();
        AuditLog = await _gatewayManager.GetAuditLogAsync(limit: 20);
    }
}
