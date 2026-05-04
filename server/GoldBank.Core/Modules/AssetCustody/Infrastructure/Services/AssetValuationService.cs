using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using GoldBank.Core.Common.Persistence;
using GoldBank.Core.Modules.AssetCustody.Domain.Entities;

namespace GoldBank.Core.Modules.AssetCustody.Infrastructure.Services;

/// <summary>
/// Calculates current market value for individual assets and entire portfolios,
/// using today's spot prices from <see cref="PriceFeedService"/> (STORY-140).
///
/// Valuation formula for precious metals:
///   value_usd = quantity × weight_grams × purity × price_per_gram_usd
///
/// For non-metal assets (PreciousStone, Other) the last recorded valuation amount
/// is returned as-is (already in USD).
/// </summary>
public sealed class AssetValuationService
{
    private readonly GoldBankDbContext _db;
    private readonly PriceFeedService _priceFeedService;
    private readonly ILogger<AssetValuationService> _logger;

    public AssetValuationService(
        GoldBankDbContext db,
        PriceFeedService priceFeedService,
        ILogger<AssetValuationService> logger)
    {
        _db = db;
        _priceFeedService = priceFeedService;
        _logger = logger;
    }

    /// <summary>
    /// Calculates the current USD value of a single asset.
    /// Returns null when a required spot price is unavailable.
    /// </summary>
    public async Task<decimal?> CalculateAssetValueAsync(
        Asset asset,
        CancellationToken cancellationToken = default)
    {
        var metalKey = MetalKey(asset.AssetType);

        if (metalKey is null)
        {
            // Non-metal: use last recorded valuation
            return asset.LastValuationAmount;
        }

        var pricePerGram = await _priceFeedService.GetCurrentPriceAsync(metalKey, cancellationToken);

        if (pricePerGram is null)
        {
            _logger.LogWarning(
                "No today's price for {Metal} — cannot calculate value for asset {AssetId}.",
                metalKey, asset.Id);
            return null;
        }

        if (!asset.WeightGrams.HasValue)
        {
            _logger.LogWarning(
                "Asset {AssetId} is a {Type} but has no weight — cannot calculate market value.",
                asset.Id, asset.AssetType);
            return null;
        }

        var purity = asset.Purity ?? 1m;
        return asset.Quantity * asset.WeightGrams.Value * purity * pricePerGram.Value;
    }

    /// <summary>
    /// Calculates the total portfolio value in USD for all active assets owned by
    /// the given customer.
    /// </summary>
    public async Task<decimal> CalculatePortfolioValueAsync(
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        var assets = await _db.Assets
            .Where(a => a.CustomerId == customerId &&
                        a.Status == AssetStatus.Active &&
                        !a.IsDeleted)
            .ToListAsync(cancellationToken);

        // Pre-load today's prices for all metals in one pass to avoid N+1 queries
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var spotPrices = await _db.DailyPrices
            .Where(p => p.Date == today)
            .ToDictionaryAsync(p => p.AssetType, p => p.PricePerGramUsd, cancellationToken);

        var total = 0m;

        foreach (var asset in assets)
        {
            var metalKey = MetalKey(asset.AssetType);

            if (metalKey is null)
            {
                // Non-metal: use last recorded valuation
                total += asset.LastValuationAmount;
                continue;
            }

            if (!spotPrices.TryGetValue(metalKey, out var pricePerGram) ||
                !asset.WeightGrams.HasValue)
            {
                _logger.LogDebug(
                    "Skipping asset {AssetId} ({Type}) from portfolio total — missing price or weight.",
                    asset.Id, asset.AssetType);
                continue;
            }

            var purity = asset.Purity ?? 1m;
            total += asset.Quantity * asset.WeightGrams.Value * purity * pricePerGram;
        }

        _logger.LogInformation(
            "Portfolio value for customer {CustomerId}: {Total:F2} USD ({Count} active assets).",
            customerId, total, assets.Count);

        return total;
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Maps an <see cref="AssetType"/> to the lower-case metal key used in
    /// <see cref="DailyPrice.AssetType"/>. Returns null for non-metal assets.
    /// </summary>
    private static string? MetalKey(AssetType assetType) => assetType switch
    {
        AssetType.GoldCoin or AssetType.GoldBar => "gold",
        AssetType.Silver                         => "silver",
        AssetType.Platinum                       => "platinum",
        _                                        => null,
    };
}
