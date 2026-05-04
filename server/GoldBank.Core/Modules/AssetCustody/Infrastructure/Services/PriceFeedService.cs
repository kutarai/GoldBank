using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using GoldBank.Core.Common.Persistence;
using GoldBank.Core.Modules.AssetCustody.Domain.Entities;

namespace GoldBank.Core.Modules.AssetCustody.Infrastructure.Services;

/// <summary>
/// Fetches daily spot prices for precious metals (gold, silver, platinum) from a
/// configured external price feed API, or reads manually-set fallback values from
/// SystemConfig when no API is configured (STORY-140).
///
/// Expected SystemConfig keys:
///   asset.price_feed_url        — base URL of the price feed API (optional)
///   asset.price_feed_api_key    — API key header value (optional)
///   asset.gold_price_manual_usd     — manual price per troy ounce in USD
///   asset.silver_price_manual_usd   — manual price per troy ounce in USD
///   asset.platinum_price_manual_usd — manual price per troy ounce in USD
/// </summary>
public sealed class PriceFeedService
{
    // 1 troy ounce = 31.1034768 grams
    private const decimal GramsPerTroyOz = 31.1034768m;

    private static readonly string[] SupportedMetals = ["gold", "silver", "platinum"];

    private readonly GoldBankDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<PriceFeedService> _logger;

    public PriceFeedService(
        GoldBankDbContext db,
        IHttpClientFactory httpClientFactory,
        ILogger<PriceFeedService> logger)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// Refreshes today's <see cref="DailyPrice"/> records for all supported metals.
    /// Calls the external API when configured; falls back to manual SystemConfig values.
    /// </summary>
    public async Task RefreshPricesAsync(CancellationToken cancellationToken = default)
    {
        var feedUrl = await ReadConfigAsync("asset.price_feed_url", cancellationToken);
        var apiKey  = await ReadConfigAsync("asset.price_feed_api_key", cancellationToken);

        Dictionary<string, decimal> pricesPerOz;

        if (!string.IsNullOrWhiteSpace(feedUrl))
        {
            pricesPerOz = await FetchFromApiAsync(feedUrl, apiKey, cancellationToken);
        }
        else
        {
            _logger.LogInformation("No price feed URL configured — using manual SystemConfig prices.");
            pricesPerOz = await ReadManualPricesAsync(cancellationToken);
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        foreach (var metal in SupportedMetals)
        {
            if (!pricesPerOz.TryGetValue(metal, out var pricePerOz) || pricePerOz <= 0)
            {
                _logger.LogWarning("No price available for {Metal} — skipping upsert.", metal);
                continue;
            }

            var pricePerGram = pricePerOz / GramsPerTroyOz;
            var source = string.IsNullOrWhiteSpace(feedUrl) ? "manual" : "api";

            await UpsertPriceAsync(metal, today, pricePerOz, pricePerGram, source, cancellationToken);
        }

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Daily price refresh complete for {Date}: {Count} metals updated.",
            today, pricesPerOz.Count);
    }

    /// <summary>
    /// Returns today's price per gram (USD) for the given asset type name
    /// ("gold", "silver", "platinum"). Returns null if no price exists for today.
    /// </summary>
    public async Task<decimal?> GetCurrentPriceAsync(
        string assetType,
        CancellationToken cancellationToken = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var key   = assetType.ToLowerInvariant();

        var price = await _db.DailyPrices
            .Where(p => p.AssetType == key && p.Date == today)
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return price?.PricePerGramUsd;
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private async Task<Dictionary<string, decimal>> FetchFromApiAsync(
        string feedUrl,
        string? apiKey,
        CancellationToken cancellationToken)
    {
        try
        {
            var client = _httpClientFactory.CreateClient("PriceFeed");

            if (!string.IsNullOrWhiteSpace(apiKey))
                client.DefaultRequestHeaders.TryAddWithoutValidation("X-API-Key", apiKey);

            // Standard metals price feed response: { "gold": 1950.25, "silver": 23.10, "platinum": 920.50 }
            var response = await client.GetFromJsonAsync<Dictionary<string, decimal>>(
                feedUrl, cancellationToken);

            if (response is null || response.Count == 0)
            {
                _logger.LogWarning("Price feed API returned an empty response from {Url}.", feedUrl);
                return [];
            }

            // Normalise keys to lower-case
            return response.ToDictionary(kv => kv.Key.ToLowerInvariant(), kv => kv.Value);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Price feed API call failed for {Url} — falling back to manual prices.", feedUrl);

            return await ReadManualPricesAsync(cancellationToken);
        }
    }

    private async Task<Dictionary<string, decimal>> ReadManualPricesAsync(
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, decimal>();

        foreach (var metal in SupportedMetals)
        {
            var raw = await ReadConfigAsync($"asset.{metal}_price_manual_usd", cancellationToken);
            if (decimal.TryParse(raw, out var price) && price > 0)
                result[metal] = price;
        }

        return result;
    }

    private async Task<string?> ReadConfigAsync(string key, CancellationToken cancellationToken)
    {
        var config = await _db.SystemConfigs
            .Where(c => c.Key == key)
            .OrderByDescending(c => c.TenantId)   // tenant-specific wins over global (null)
            .FirstOrDefaultAsync(cancellationToken);

        if (config is null) return null;

        // Values are stored as JSON strings (possibly with surrounding quotes)
        var raw = config.ValueJson.Trim();
        if (raw.StartsWith('"') && raw.EndsWith('"'))
            raw = raw[1..^1];

        return raw;
    }

    private async Task UpsertPriceAsync(
        string metal,
        DateOnly date,
        decimal pricePerOz,
        decimal pricePerGram,
        string source,
        CancellationToken cancellationToken)
    {
        var existing = await _db.DailyPrices
            .FirstOrDefaultAsync(
                p => p.AssetType == metal && p.Date == date,
                cancellationToken);

        if (existing is not null)
        {
            existing.PricePerOzUsd  = pricePerOz;
            existing.PricePerGramUsd = pricePerGram;
            existing.Source          = source;
            existing.UpdatedAt       = DateTime.UtcNow;
        }
        else
        {
            _db.DailyPrices.Add(new DailyPrice
            {
                AssetType       = metal,
                Date            = date,
                PricePerOzUsd   = pricePerOz,
                PricePerGramUsd = pricePerGram,
                Source          = source,
            });
        }
    }
}
