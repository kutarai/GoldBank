using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SynergySwitch.Data;
using SynergySwitch.Data.Entities;

namespace SynergySwitch.Core.Gateway;

/// <summary>
/// Manages gateway CRUD, BIN routing table, and an in-memory cache for
/// high-speed BIN lookups during transaction routing.
/// </summary>
public class GatewayManager
{
    private readonly SwitchDbContext _db;
    private readonly ILogger<GatewayManager> _logger;

    // ── Cached routing table (rebuilt on any change) ──
    private static readonly object _cacheLock = new();
    private static List<CachedGateway> _cachedGateways = [];
    private static List<CachedBinRoute> _cachedBinRoutes = [];
    private static DateTime _cacheBuiltAt = DateTime.MinValue;

    public GatewayManager(SwitchDbContext db, ILogger<GatewayManager> logger)
    {
        _db = db;
        _logger = logger;
    }

    // ── Query ──

    public async Task<List<GatewayEntity>> GetAllGatewaysAsync()
        => await _db.Gateways.Include(g => g.BinRoutes)
            .OrderBy(g => g.Priority).ThenBy(g => g.Name)
            .AsNoTracking().ToListAsync();

    public async Task<GatewayEntity?> GetGatewayByIdAsync(int id)
        => await _db.Gateways.Include(g => g.BinRoutes)
            .FirstOrDefaultAsync(g => g.Id == id);

    public async Task<List<GatewayAuditLogEntity>> GetAuditLogAsync(int? gatewayId = null, int limit = 50)
    {
        var q = _db.GatewayAuditLogs.AsNoTracking().OrderByDescending(a => a.Timestamp).AsQueryable();
        if (gatewayId.HasValue) q = q.Where(a => a.GatewayId == gatewayId);
        return await q.Take(limit).ToListAsync();
    }

    // ── CRUD ──

    public async Task<GatewayEntity> CreateGatewayAsync(GatewayEntity gateway)
    {
        gateway.CreatedAt = DateTime.UtcNow;
        gateway.UpdatedAt = DateTime.UtcNow;
        _db.Gateways.Add(gateway);
        await _db.SaveChangesAsync();

        await AuditAsync(gateway.Id, "GATEWAY_CREATED",
            $"Gateway '{gateway.Name}' created: {gateway.Host}:{gateway.Port}, pool={gateway.PoolSize}, enabled={gateway.IsEnabled}");
        await RefreshCacheAsync();

        _logger.LogInformation("Gateway created: {Name} ({Host}:{Port})", gateway.Name, gateway.Host, gateway.Port);
        return gateway;
    }

    public async Task UpdateGatewayAsync(GatewayEntity gateway)
    {
        gateway.UpdatedAt = DateTime.UtcNow;
        _db.Gateways.Update(gateway);
        await _db.SaveChangesAsync();

        await AuditAsync(gateway.Id, "GATEWAY_UPDATED",
            $"Gateway '{gateway.Name}' updated: {gateway.Host}:{gateway.Port}, pool={gateway.PoolSize}, enabled={gateway.IsEnabled}, priority={gateway.Priority}");
        await RefreshCacheAsync();

        _logger.LogInformation("Gateway updated: {Name}", gateway.Name);
    }

    public async Task SetGatewayEnabledAsync(int gatewayId, bool enabled)
    {
        var gw = await _db.Gateways.FindAsync(gatewayId);
        if (gw == null) return;

        gw.IsEnabled = enabled;
        gw.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        var action = enabled ? "GATEWAY_ENABLED" : "GATEWAY_DISABLED";
        await AuditAsync(gatewayId, action, $"Gateway '{gw.Name}' {(enabled ? "enabled" : "disabled")}");
        await RefreshCacheAsync();

        _logger.LogInformation("Gateway {Action}: {Name}", action, gw.Name);
    }

    public async Task DeleteGatewayAsync(int gatewayId)
    {
        var gw = await _db.Gateways.FindAsync(gatewayId);
        if (gw == null) return;

        var name = gw.Name;
        _db.Gateways.Remove(gw);
        await _db.SaveChangesAsync();

        await AuditAsync(gatewayId, "GATEWAY_DELETED", $"Gateway '{name}' deleted");
        await RefreshCacheAsync();

        _logger.LogInformation("Gateway deleted: {Name}", name);
    }

    // ── BIN routes ──

    public async Task AddBinRouteAsync(int gatewayId, string binPrefix, string? description = null)
    {
        var route = new GatewayBinRouteEntity
        {
            GatewayId = gatewayId,
            BinPrefix = binPrefix.Trim(),
            Description = description,
            CreatedAt = DateTime.UtcNow
        };
        _db.GatewayBinRoutes.Add(route);
        await _db.SaveChangesAsync();

        var gw = await _db.Gateways.FindAsync(gatewayId);
        await AuditAsync(gatewayId, "BIN_ADDED",
            $"BIN prefix '{binPrefix}' added to gateway '{gw?.Name}'");
        await RefreshCacheAsync();

        _logger.LogInformation("BIN route added: {Bin} -> gateway {GatewayId}", binPrefix, gatewayId);
    }

    public async Task RemoveBinRouteAsync(int binRouteId)
    {
        var route = await _db.GatewayBinRoutes.Include(r => r.Gateway).FirstOrDefaultAsync(r => r.Id == binRouteId);
        if (route == null) return;

        var binPrefix = route.BinPrefix;
        var gwName = route.Gateway.Name;
        var gwId = route.GatewayId;

        _db.GatewayBinRoutes.Remove(route);
        await _db.SaveChangesAsync();

        await AuditAsync(gwId, "BIN_REMOVED", $"BIN prefix '{binPrefix}' removed from gateway '{gwName}'");
        await RefreshCacheAsync();

        _logger.LogInformation("BIN route removed: {Bin} from gateway {GatewayName}", binPrefix, gwName);
    }

    // ── Routing (uses cache) ──

    /// <summary>
    /// Find the best gateway for a given PAN. Matches the longest BIN prefix.
    /// Falls back to any enabled gateway with no BIN routes (default gateway).
    /// </summary>
    public static CachedGateway? RouteByPan(string pan)
    {
        List<CachedGateway> gateways;
        List<CachedBinRoute> routes;

        lock (_cacheLock)
        {
            gateways = _cachedGateways;
            routes = _cachedBinRoutes;
        }

        if (gateways.Count == 0)
            return null;

        // Find the longest matching BIN prefix
        CachedBinRoute? bestRoute = null;
        foreach (var route in routes)
        {
            if (pan.StartsWith(route.BinPrefix, StringComparison.Ordinal))
            {
                if (bestRoute == null || route.BinPrefix.Length > bestRoute.BinPrefix.Length)
                    bestRoute = route;
            }
        }

        if (bestRoute != null)
        {
            return gateways.FirstOrDefault(g => g.Id == bestRoute.GatewayId && g.IsEnabled);
        }

        // No specific BIN match — use any enabled gateway with no BIN routes (default)
        // ordered by priority
        var defaultGateways = gateways
            .Where(g => g.IsEnabled && !routes.Any(r => r.GatewayId == g.Id))
            .OrderBy(g => g.Priority)
            .ToList();

        if (defaultGateways.Count > 0)
            return defaultGateways[Random.Shared.Next(defaultGateways.Count)];

        // Last resort: any enabled gateway
        var enabled = gateways.Where(g => g.IsEnabled).OrderBy(g => g.Priority).ToList();
        return enabled.Count > 0 ? enabled[Random.Shared.Next(enabled.Count)] : null;
    }

    /// <summary>
    /// Get a snapshot of all cached gateways (for dashboard/connection pool).
    /// </summary>
    public static IReadOnlyList<CachedGateway> GetCachedGateways()
    {
        lock (_cacheLock)
            return _cachedGateways.ToList();
    }

    // ── Cache management ──

    public async Task RefreshCacheAsync()
    {
        var gateways = await _db.Gateways
            .Include(g => g.BinRoutes)
            .AsNoTracking()
            .OrderBy(g => g.Priority).ThenBy(g => g.Name)
            .ToListAsync();

        var cachedGateways = gateways.Select(g => new CachedGateway
        {
            Id = g.Id,
            Name = g.Name,
            Host = g.Host,
            Port = g.Port,
            AcquiringInstitutionId = g.AcquiringInstitutionId,
            NetworkId = g.NetworkId,
            PoolSize = g.PoolSize,
            TimeoutSeconds = g.TimeoutSeconds,
            SendLengthHeader = g.SendLengthHeader,
            IsEnabled = g.IsEnabled,
            OfflineMode = g.OfflineMode,
            Priority = g.Priority,
            Protocol = g.Protocol
        }).ToList();

        var cachedRoutes = gateways
            .SelectMany(g => g.BinRoutes.Select(r => new CachedBinRoute
            {
                GatewayId = g.Id,
                BinPrefix = r.BinPrefix
            }))
            .OrderByDescending(r => r.BinPrefix.Length) // longest-prefix-first for fast matching
            .ToList();

        lock (_cacheLock)
        {
            _cachedGateways = cachedGateways;
            _cachedBinRoutes = cachedRoutes;
            _cacheBuiltAt = DateTime.UtcNow;
        }

        _logger.LogInformation(
            "Gateway cache refreshed: {GatewayCount} gateways, {RouteCount} BIN routes",
            cachedGateways.Count, cachedRoutes.Count);
    }

    // ── Audit ──

    private async Task AuditAsync(int? gatewayId, string action, string details)
    {
        _db.GatewayAuditLogs.Add(new GatewayAuditLogEntity
        {
            GatewayId = gatewayId,
            Action = action,
            Details = details,
            Timestamp = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
    }
}

/// <summary>In-memory cached gateway config for routing lookups (no DB hit).</summary>
public record CachedGateway
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public string Host { get; init; } = "";
    public int Port { get; init; }
    public string AcquiringInstitutionId { get; init; } = "";
    public string NetworkId { get; init; } = "002";
    public int PoolSize { get; init; } = 4;
    public int TimeoutSeconds { get; init; } = 30;
    public bool SendLengthHeader { get; init; } = true;
    public bool IsEnabled { get; init; }
    public bool OfflineMode { get; init; }
    public int Priority { get; init; }
    public GatewayProtocol Protocol { get; init; }
}

/// <summary>In-memory cached BIN route for prefix matching.</summary>
public record CachedBinRoute
{
    public int GatewayId { get; init; }
    public string BinPrefix { get; init; } = "";
}
