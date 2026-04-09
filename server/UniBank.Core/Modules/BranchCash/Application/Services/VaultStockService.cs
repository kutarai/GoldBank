using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using UniBank.Core.Common.Persistence;
using UniBank.Core.Modules.BranchCash.Domain.Entities;
using UniBank.SharedKernel.Results;

namespace UniBank.Core.Modules.BranchCash.Application.Services;

/// <summary>
/// Maintains <c>vault_denomination_stock</c> as a materialised aggregate of every
/// <c>vault_movements</c> row for the vault (STORY-165). Always called inside the
/// caller's DB transaction so the stock and the movement land atomically.
/// </summary>
public sealed class VaultStockService
{
    private readonly UniBankDbContext _db;
    public VaultStockService(UniBankDbContext db) { _db = db; }

    public sealed record BreakdownLine(Guid DenominationId, decimal Face, int Count);

    /// <summary>
    /// Apply a movement to the stock aggregate. Caller must be inside a DB
    /// transaction. Throws if the movement would drive any stock below zero.
    /// </summary>
    public async Task<Result> ApplyMovementAsync(VaultMovement m, CancellationToken ct)
    {
        // Pessimistic lock all stock rows for this vault for the duration of the txn
        await _db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM bank.vault_denomination_stock WHERE vault_id = {m.VaultId} FOR UPDATE", ct);

        var lines = ParseBreakdown(m.DenominationBreakdownJson);
        var sign = string.Equals(m.Direction, "In", StringComparison.OrdinalIgnoreCase) ? +1 : -1;

        foreach (var line in lines)
        {
            var row = await _db.VaultDenominationStock
                .FirstOrDefaultAsync(s => s.VaultId == m.VaultId && s.DenominationId == line.DenominationId, ct);

            if (row == null)
            {
                if (sign < 0)
                    return Result.Failure(new Error("Vault.NegativeStock", "Cannot withdraw a denomination that has zero stock."));
                row = new VaultDenominationStock
                {
                    VaultId = m.VaultId,
                    DenominationId = line.DenominationId,
                    Currency = m.Currency,
                    Count = line.Count,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                };
                _db.VaultDenominationStock.Add(row);
            }
            else
            {
                var newCount = row.Count + sign * line.Count;
                if (newCount < 0)
                    return Result.Failure(new Error("Vault.NegativeStock", $"Stock for denomination would go negative ({newCount})."));
                row.Count = newCount;
                row.UpdatedAt = DateTime.UtcNow;
            }
        }
        return Result.Success();
    }

    /// <summary>
    /// Drop and replay: wipe stock for the vault, then replay every movement
    /// in chronological order. Used for disaster recovery and audit verification.
    /// </summary>
    public async Task<Result> RebuildFromHistoryAsync(Guid vaultId, CancellationToken ct)
    {
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            await _db.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM bank.vault_denomination_stock WHERE vault_id = {vaultId}", ct);
            await _db.SaveChangesAsync(ct);

            var movements = await _db.VaultMovements
                .Where(m => m.VaultId == vaultId)
                .OrderBy(m => m.CreatedAt)
                .ToListAsync(ct);

            foreach (var m in movements)
            {
                var r = await ApplyMovementAsync(m, ct);
                if (r.IsFailure) { await tx.RollbackAsync(ct); return r; }
            }
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Result.Success();
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            return Result.Failure(new Error("Vault.RebuildFailed", ex.Message));
        }
    }

    /// <summary>Compare current stock vs replayed history; return drift if any.</summary>
    public async Task<IReadOnlyList<(Guid DenominationId, int Stock, int Replayed)>> VerifyAsync(Guid vaultId, CancellationToken ct)
    {
        var current = await _db.VaultDenominationStock
            .Where(s => s.VaultId == vaultId)
            .ToDictionaryAsync(s => s.DenominationId, s => s.Count, ct);

        var replayed = new Dictionary<Guid, int>();
        var movements = await _db.VaultMovements
            .Where(m => m.VaultId == vaultId)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(ct);
        foreach (var m in movements)
        {
            var sign = string.Equals(m.Direction, "In", StringComparison.OrdinalIgnoreCase) ? +1 : -1;
            foreach (var line in ParseBreakdown(m.DenominationBreakdownJson))
                replayed[line.DenominationId] = replayed.GetValueOrDefault(line.DenominationId) + sign * line.Count;
        }

        var drift = new List<(Guid, int, int)>();
        foreach (var k in current.Keys.Union(replayed.Keys))
        {
            var s = current.GetValueOrDefault(k);
            var r = replayed.GetValueOrDefault(k);
            if (s != r) drift.Add((k, s, r));
        }
        return drift;
    }

    private static IEnumerable<BreakdownLine> ParseBreakdown(string json)
    {
        // Accept either array of {denominationId, face, count} or object {denominationId: count}
        if (string.IsNullOrWhiteSpace(json)) yield break;
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                var idStr  = el.TryGetProperty("denominationId", out var idEl) ? idEl.GetString() : null;
                var face   = el.TryGetProperty("face", out var fEl) ? fEl.GetDecimal() : 0m;
                var count  = el.TryGetProperty("count", out var cEl) ? cEl.GetInt32() : 0;
                if (Guid.TryParse(idStr, out var id) && count > 0)
                    yield return new BreakdownLine(id, face, count);
            }
        }
    }
}
