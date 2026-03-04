using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using UniBank.TerminalManager.Infrastructure;

namespace UniBank.TerminalManager.Services;

/// <summary>
/// Tracks terminal online/offline status and generates alerts (STORY-048).
/// Uses a configurable offline threshold (default 5 minutes) to detect
/// terminals that have stopped sending heartbeats.
/// </summary>
public sealed class TerminalMonitoringService
{
    private readonly TerminalDbContext _dbContext;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TerminalMonitoringService> _logger;

    public TerminalMonitoringService(
        TerminalDbContext dbContext,
        IConfiguration configuration,
        ILogger<TerminalMonitoringService> logger)
    {
        _dbContext = dbContext;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Processes a heartbeat from a terminal, updating its status and last-seen timestamp.
    /// </summary>
    public async Task ProcessHeartbeatAsync(
        string tenantId,
        string terminalId,
        string payload,
        CancellationToken cancellationToken = default)
    {
        var terminal = await _dbContext.Terminals
            .FirstOrDefaultAsync(
                t => t.SerialNumber == terminalId && t.TenantId == tenantId,
                cancellationToken);

        if (terminal is null)
        {
            _logger.LogWarning(
                "Heartbeat from unknown terminal: TenantId={TenantId}, TerminalId={TerminalId}",
                tenantId, terminalId);
            return;
        }

        var previousStatus = terminal.Status;
        terminal.LastHeartbeat = DateTime.UtcNow;

        // Parse heartbeat payload for additional status data
        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;

            if (root.TryGetProperty("status", out var statusProp))
            {
                var reportedStatus = statusProp.GetString();
                if (!string.IsNullOrEmpty(reportedStatus))
                {
                    terminal.Status = reportedStatus;
                }
            }

            if (root.TryGetProperty("ipAddress", out var ipProp))
            {
                terminal.IpAddress = ipProp.GetString();
            }

            // Log additional telemetry data
            if (root.TryGetProperty("battery", out var batteryProp))
            {
                _logger.LogDebug(
                    "Terminal {TerminalId} battery: {Battery}%",
                    terminalId, batteryProp.GetInt32());
            }

            if (root.TryGetProperty("paperLevel", out var paperProp))
            {
                var paperLevel = paperProp.GetInt32();
                if (paperLevel < 10)
                {
                    _logger.LogWarning(
                        "Terminal {TerminalId} paper level low: {PaperLevel}%",
                        terminalId, paperLevel);
                }
            }

            if (root.TryGetProperty("signalStrength", out var signalProp))
            {
                _logger.LogDebug(
                    "Terminal {TerminalId} signal strength: {Signal}dBm",
                    terminalId, signalProp.GetInt32());
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex,
                "Failed to parse heartbeat payload from terminal {TerminalId}", terminalId);
        }

        // Mark as active if it was previously offline or inactive
        if (terminal.Status is "offline" or "inactive")
        {
            terminal.Status = "active";
            if (terminal.ActivatedAt is null)
            {
                terminal.ActivatedAt = DateTime.UtcNow;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Detect status change and generate alert
        if (previousStatus != terminal.Status)
        {
            _logger.LogInformation(
                "Terminal status changed: TerminalId={TerminalId}, TenantId={TenantId}, From={From}, To={To}",
                terminalId, tenantId, previousStatus, terminal.Status);
        }
    }

    /// <summary>
    /// Checks all terminals for offline status based on the configurable threshold.
    /// </summary>
    public async Task<int> DetectOfflineTerminalsAsync(CancellationToken cancellationToken = default)
    {
        var thresholdMinutes = _configuration.GetValue("Terminal:OfflineThresholdMinutes", 5);
        var threshold = DateTime.UtcNow.AddMinutes(-thresholdMinutes);

        var offlineTerminals = await _dbContext.Terminals
            .Where(t => t.Status == "active" && t.LastHeartbeat < threshold)
            .ToListAsync(cancellationToken);

        foreach (var terminal in offlineTerminals)
        {
            terminal.Status = "offline";
            _logger.LogWarning(
                "Terminal went offline: TerminalId={TerminalId}, TenantId={TenantId}, LastHeartbeat={LastHeartbeat}",
                terminal.SerialNumber, terminal.TenantId, terminal.LastHeartbeat);
        }

        if (offlineTerminals.Count > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return offlineTerminals.Count;
    }
}
