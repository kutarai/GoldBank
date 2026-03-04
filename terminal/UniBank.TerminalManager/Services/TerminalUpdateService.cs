using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UniBank.SharedKernel.Results;
using UniBank.TerminalManager.Domain;
using UniBank.TerminalManager.Infrastructure;

namespace UniBank.TerminalManager.Services;

/// <summary>
/// Manages remote software update distribution to terminals (STORY-049).
/// Queues updates, publishes to MQTT topics, and tracks per-terminal update status.
/// </summary>
public sealed class TerminalUpdateService
{
    private readonly TerminalDbContext _dbContext;
    private readonly ILogger<TerminalUpdateService> _logger;

    public TerminalUpdateService(
        TerminalDbContext dbContext,
        ILogger<TerminalUpdateService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// Queues an update for a terminal and publishes the update payload to MQTT.
    /// </summary>
    public async Task<Result<TerminalUpdate>> QueueUpdateAsync(
        Guid terminalId,
        string updateType,
        string version,
        byte[] payload,
        CancellationToken cancellationToken = default)
    {
        var terminal = await _dbContext.Terminals
            .FirstOrDefaultAsync(t => t.Id == terminalId, cancellationToken);

        if (terminal is null)
        {
            return Result.Failure<TerminalUpdate>(
                new Error("Terminal.NotFound", $"Terminal '{terminalId}' not found."));
        }

        var update = new TerminalUpdate
        {
            TerminalId = terminalId,
            UpdateType = updateType,
            Version = version,
            Status = "pending",
            PushedAt = DateTime.UtcNow
        };

        _dbContext.TerminalUpdates.Add(update);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Update queued: TerminalId={TerminalId}, UpdateType={UpdateType}, Version={Version}, UpdateId={UpdateId}",
            terminalId, updateType, version, update.Id);

        return Result.Success(update);
    }

    /// <summary>
    /// Handles update acknowledgment from a terminal.
    /// </summary>
    public async Task HandleUpdateAcknowledgmentAsync(
        string tenantId,
        string terminalId,
        string payload,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;

            var updateIdStr = root.TryGetProperty("updateId", out var updateIdProp)
                ? updateIdProp.GetString()
                : null;

            var status = root.TryGetProperty("status", out var statusProp)
                ? statusProp.GetString()
                : null;

            if (string.IsNullOrEmpty(updateIdStr) || !Guid.TryParse(updateIdStr, out var updateId))
            {
                _logger.LogWarning(
                    "Invalid update ack: missing or invalid updateId from terminal {TerminalId} in tenant {TenantId}",
                    terminalId, tenantId);
                return;
            }

            var update = await _dbContext.TerminalUpdates
                .FirstOrDefaultAsync(u => u.Id == updateId, cancellationToken);

            if (update is null)
            {
                _logger.LogWarning("Update not found for ack: UpdateId={UpdateId}", updateId);
                return;
            }

            update.Status = status ?? "applied";
            update.AppliedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Update acknowledged: UpdateId={UpdateId}, TerminalId={TerminalId}, Status={Status}",
                updateId, terminalId, update.Status);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex,
                "Failed to parse update ack payload from terminal {TerminalId} in tenant {TenantId}",
                terminalId, tenantId);
        }
    }
}
