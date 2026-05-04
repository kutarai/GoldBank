using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using GoldBank.Core.Common.Persistence;
using GoldBank.SharedKernel.Caching;

namespace GoldBank.Core.Common.Caching;

/// <summary>
/// PostgreSQL-backed implementation of <see cref="ICacheStore"/>.
/// Uses a <c>cache_entries</c> table in the <c>bank</c> schema.
/// Expired rows are filtered in queries and cleaned up periodically.
/// </summary>
public sealed class PostgresCacheStore : ICacheStore
{
    private readonly PublicDbContext _db;
    private readonly ILogger<PostgresCacheStore> _logger;

    public PostgresCacheStore(PublicDbContext db, ILogger<PostgresCacheStore> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<string?> GetAsync(string key, CancellationToken ct = default)
    {
        var entry = await _db.CacheEntries
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Key == key, ct);

        if (entry is null) return null;
        if (entry.ExpiresAt.HasValue && entry.ExpiresAt.Value < DateTime.UtcNow)
        {
            // Expired — clean up asynchronously
            _db.CacheEntries.Remove(entry);
            await _db.SaveChangesAsync(ct);
            return null;
        }

        return entry.Value;
    }

    public async Task SetAsync(string key, string value, TimeSpan? expiry = null, CancellationToken ct = default)
    {
        var expiresAt = expiry.HasValue ? DateTime.UtcNow.Add(expiry.Value) : (DateTime?)null;

        var entry = await _db.CacheEntries.FirstOrDefaultAsync(e => e.Key == key, ct);
        if (entry is not null)
        {
            entry.Value = value;
            entry.ExpiresAt = expiresAt;
            entry.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            _db.CacheEntries.Add(new CacheEntry
            {
                Key = key,
                Value = value,
                ExpiresAt = expiresAt,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task<bool> DeleteAsync(string key, CancellationToken ct = default)
    {
        var rows = await _db.CacheEntries.Where(e => e.Key == key).ExecuteDeleteAsync(ct);
        return rows > 0;
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken ct = default)
    {
        return await _db.CacheEntries
            .AsNoTracking()
            .AnyAsync(e => e.Key == key && (!e.ExpiresAt.HasValue || e.ExpiresAt.Value > DateTime.UtcNow), ct);
    }

    public async Task<long> IncrementAsync(string key, CancellationToken ct = default)
    {
        var entry = await _db.CacheEntries.FirstOrDefaultAsync(e => e.Key == key, ct);
        if (entry is null)
        {
            entry = new CacheEntry
            {
                Key = key,
                Value = "1",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _db.CacheEntries.Add(entry);
            await _db.SaveChangesAsync(ct);
            return 1;
        }

        if (entry.ExpiresAt.HasValue && entry.ExpiresAt.Value < DateTime.UtcNow)
        {
            entry.Value = "1";
            entry.ExpiresAt = null;
            entry.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
            return 1;
        }

        var current = long.TryParse(entry.Value, out var val) ? val : 0;
        current++;
        entry.Value = current.ToString();
        entry.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return current;
    }

    public async Task<TimeSpan?> GetTimeToLiveAsync(string key, CancellationToken ct = default)
    {
        var entry = await _db.CacheEntries
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Key == key, ct);

        if (entry?.ExpiresAt is null) return null;

        var remaining = entry.ExpiresAt.Value - DateTime.UtcNow;
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    public async Task SetExpiryAsync(string key, TimeSpan expiry, CancellationToken ct = default)
    {
        var entry = await _db.CacheEntries.FirstOrDefaultAsync(e => e.Key == key, ct);
        if (entry is not null)
        {
            entry.ExpiresAt = DateTime.UtcNow.Add(expiry);
            entry.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task HashSetAsync(string key, IDictionary<string, string> fields, TimeSpan? expiry = null, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(fields);
        var expiresAt = expiry.HasValue ? DateTime.UtcNow.Add(expiry.Value) : (DateTime?)null;

        var entry = await _db.CacheEntries.FirstOrDefaultAsync(e => e.Key == key, ct);
        if (entry is not null)
        {
            // Merge fields into existing hash
            var existing = TryDeserializeHash(entry.Value);
            foreach (var kv in fields)
                existing[kv.Key] = kv.Value;
            entry.Value = JsonSerializer.Serialize(existing);
            entry.ExpiresAt = expiresAt ?? entry.ExpiresAt;
            entry.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            _db.CacheEntries.Add(new CacheEntry
            {
                Key = key,
                Value = json,
                ExpiresAt = expiresAt,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task<string?> HashGetAsync(string key, string field, CancellationToken ct = default)
    {
        var entry = await _db.CacheEntries
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Key == key, ct);

        if (entry is null) return null;
        if (entry.ExpiresAt.HasValue && entry.ExpiresAt.Value < DateTime.UtcNow) return null;

        var hash = TryDeserializeHash(entry.Value);
        return hash.TryGetValue(field, out var value) ? value : null;
    }

    public async Task<long> HashIncrementAsync(string key, string field, long value = 1, CancellationToken ct = default)
    {
        var entry = await _db.CacheEntries.FirstOrDefaultAsync(e => e.Key == key, ct);
        if (entry is null) return 0;

        var hash = TryDeserializeHash(entry.Value);
        var current = hash.TryGetValue(field, out var raw) && long.TryParse(raw, out var parsed) ? parsed : 0;
        current += value;
        hash[field] = current.ToString();
        entry.Value = JsonSerializer.Serialize(hash);
        entry.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return current;
    }

    public async Task<IReadOnlyList<string>> SearchKeysAsync(string pattern, CancellationToken ct = default)
    {
        // Convert Redis KEYS glob pattern (e.g. "session:abc:*") to SQL LIKE
        var likePattern = pattern.Replace("*", "%");

        return await _db.CacheEntries
            .AsNoTracking()
            .Where(e => EF.Functions.Like(e.Key, likePattern)
                        && (!e.ExpiresAt.HasValue || e.ExpiresAt.Value > DateTime.UtcNow))
            .Select(e => e.Key)
            .ToListAsync(ct);
    }

    public async Task<long> DeleteByPatternAsync(string pattern, CancellationToken ct = default)
    {
        var likePattern = pattern.Replace("*", "%");
        return await _db.CacheEntries
            .Where(e => EF.Functions.Like(e.Key, likePattern))
            .ExecuteDeleteAsync(ct);
    }

    private static Dictionary<string, string> TryDeserializeHash(string? json)
    {
        if (string.IsNullOrEmpty(json)) return new Dictionary<string, string>();
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
        }
        catch
        {
            return new Dictionary<string, string>();
        }
    }
}
