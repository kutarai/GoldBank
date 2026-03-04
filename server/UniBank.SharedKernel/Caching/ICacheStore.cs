namespace UniBank.SharedKernel.Caching;

/// <summary>
/// Abstraction over key-value caching with TTL support.
/// Replaces direct IConnectionMultiplexer (Redis) usage so that the
/// backing store can be PostgreSQL, Redis, or in-memory.
/// </summary>
public interface ICacheStore
{
    // --- String operations ---
    Task<string?> GetAsync(string key, CancellationToken ct = default);
    Task SetAsync(string key, string value, TimeSpan? expiry = null, CancellationToken ct = default);
    Task<bool> DeleteAsync(string key, CancellationToken ct = default);
    Task<bool> ExistsAsync(string key, CancellationToken ct = default);
    Task<long> IncrementAsync(string key, CancellationToken ct = default);
    Task<TimeSpan?> GetTimeToLiveAsync(string key, CancellationToken ct = default);
    Task SetExpiryAsync(string key, TimeSpan expiry, CancellationToken ct = default);

    // --- Hash operations ---
    Task HashSetAsync(string key, IDictionary<string, string> fields, TimeSpan? expiry = null, CancellationToken ct = default);
    Task<string?> HashGetAsync(string key, string field, CancellationToken ct = default);
    Task<long> HashIncrementAsync(string key, string field, long value = 1, CancellationToken ct = default);

    // --- Pattern operations ---
    Task<IReadOnlyList<string>> SearchKeysAsync(string pattern, CancellationToken ct = default);
    Task<long> DeleteByPatternAsync(string pattern, CancellationToken ct = default);
}
