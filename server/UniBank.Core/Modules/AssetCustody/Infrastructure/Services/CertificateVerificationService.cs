using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UniBank.Core.Common.Persistence;
using UniBank.Core.Modules.AssetCustody.Domain.Entities;

namespace UniBank.Core.Modules.AssetCustody.Infrastructure.Services;

/// <summary>
/// Verifies safe deposit certificates by calling each deposit house API (where available)
/// with the receipt numbers of its Active assets, then updating <see cref="Asset.VerificationStatus"/>
/// and <see cref="Asset.LastVerificationDate"/> accordingly (STORY-144).
///
/// For deposit houses that have no API endpoint, assets whose last check is older than
/// 90 days are set to <see cref="VerificationStatus.Pending"/> so that staff can action
/// a manual review queue.
///
/// Registration: <c>services.AddScoped&lt;CertificateVerificationService&gt;()</c>.
/// Scheduling: call <see cref="VerifyAllCertificatesAsync"/> from a BackgroundService timer
/// or a Quartz/Hangfire job.
/// </summary>
public sealed class CertificateVerificationService
{
    /// <summary>
    /// Assets whose <see cref="Asset.LastVerificationDate"/> is older than this threshold
    /// will be queued for manual review when the deposit house has no API.
    /// </summary>
    private static readonly TimeSpan ManualReviewThreshold = TimeSpan.FromDays(90);

    private readonly UniBankDbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<CertificateVerificationService> _logger;

    public CertificateVerificationService(
        UniBankDbContext db,
        IHttpClientFactory httpClientFactory,
        ILogger<CertificateVerificationService> logger)
    {
        _db = db;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    // =========================================================================
    // Public API
    // =========================================================================

    /// <summary>
    /// Runs a full certificate verification sweep for all Active assets, grouped by
    /// deposit house.  Houses with an API endpoint are verified automatically; the
    /// others are flagged for manual verification when overdue.
    /// </summary>
    public async Task VerifyAllCertificatesAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting full certificate verification sweep.");

        // Load all Active assets and their deposit houses in one round-trip
        var assets = await _db.Assets
            .Include(a => a.DepositHouse)
            .Where(a => a.Status == AssetStatus.Active && !a.IsDeleted)
            .ToListAsync(cancellationToken);

        if (assets.Count == 0)
        {
            _logger.LogInformation("No active assets found — verification sweep complete.");
            return;
        }

        // Group by deposit house
        var grouped = assets.GroupBy(a => a.DepositHouseId);

        foreach (var group in grouped)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var depositHouse = group.First().DepositHouse;
            if (depositHouse is null)
            {
                _logger.LogWarning(
                    "Deposit house {DepositHouseId} not loaded — skipping {Count} asset(s).",
                    group.Key, group.Count());
                continue;
            }

            if (!string.IsNullOrWhiteSpace(depositHouse.ApiEndpoint))
                await VerifyViaApiAsync(depositHouse, group.ToList(), cancellationToken);
            else
                QueueForManualVerification(depositHouse, group.ToList());
        }

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Certificate verification sweep complete. {Total} asset(s) processed across {Houses} deposit house(s).",
            assets.Count, grouped.Count());
    }

    /// <summary>
    /// Verifies a single asset's certificate on-demand, using the deposit house API when
    /// available, otherwise marking the asset as <see cref="VerificationStatus.Pending"/>.
    /// </summary>
    public async Task VerifySingleCertificateAsync(
        Guid assetId,
        CancellationToken cancellationToken = default)
    {
        var asset = await _db.Assets
            .Include(a => a.DepositHouse)
            .FirstOrDefaultAsync(a => a.Id == assetId && !a.IsDeleted, cancellationToken);

        if (asset is null)
        {
            _logger.LogWarning("VerifySingleCertificate: asset {AssetId} not found.", assetId);
            return;
        }

        var depositHouse = asset.DepositHouse;
        if (depositHouse is null)
        {
            _logger.LogWarning(
                "VerifySingleCertificate: deposit house not loaded for asset {AssetId}.", assetId);
            return;
        }

        if (!string.IsNullOrWhiteSpace(depositHouse.ApiEndpoint))
            await VerifyViaApiAsync(depositHouse, [asset], cancellationToken);
        else
            QueueForManualVerification(depositHouse, [asset]);

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "On-demand certificate verification complete for asset {AssetId}: {VerificationStatus}",
            assetId, asset.VerificationStatus);
    }

    // =========================================================================
    // Private helpers
    // =========================================================================

    /// <summary>
    /// Calls the deposit house verification API with the receipt numbers of every asset
    /// in <paramref name="assets"/> and updates each asset's verification status from
    /// the response.
    ///
    /// Expected API response shape (one entry per receipt):
    /// <code>
    /// { "RCT-001": "valid", "RCT-002": "invalid", "RCT-003": "expired" }
    /// </code>
    /// </summary>
    private async Task VerifyViaApiAsync(
        DepositHouse depositHouse,
        IReadOnlyList<Asset> assets,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug(
            "Verifying {Count} asset(s) via API for deposit house '{Name}' ({HouseId}).",
            assets.Count, depositHouse.Name, depositHouse.Id);

        var receiptNumbers = assets.Select(a => a.ReceiptNumber).ToArray();

        Dictionary<string, string>? apiResults;
        try
        {
            var client = _httpClientFactory.CreateClient("DepositHouseVerification");
            var response = await client.PostAsJsonAsync(
                depositHouse.ApiEndpoint!,
                new { receipt_numbers = receiptNumbers },
                cancellationToken);

            response.EnsureSuccessStatusCode();

            // Deserialise: { "RCT-001": "valid", "RCT-002": "invalid", ... }
            apiResults = await response.Content
                .ReadFromJsonAsync<Dictionary<string, string>>(cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Deposit house API call failed for '{Name}' ({HouseId}). Skipping {Count} asset(s).",
                depositHouse.Name, depositHouse.Id, assets.Count);
            return;
        }

        if (apiResults is null)
        {
            _logger.LogWarning(
                "Deposit house '{Name}' returned a null/empty response — skipping verification.",
                depositHouse.Name);
            return;
        }

        var now = DateTime.UtcNow;

        foreach (var asset in assets)
        {
            if (!apiResults.TryGetValue(asset.ReceiptNumber, out var statusStr))
            {
                _logger.LogDebug(
                    "Receipt {ReceiptNumber} (asset {AssetId}) not in API response — leaving unchanged.",
                    asset.ReceiptNumber, asset.Id);
                continue;
            }

            var previousStatus = asset.VerificationStatus;

            (asset.VerificationStatus, asset.Status) = statusStr.ToLowerInvariant() switch
            {
                "valid"   => (VerificationStatus.Verified, AssetStatus.Active),
                "invalid" => (VerificationStatus.Failed,   AssetStatus.Suspended),
                "expired" => (VerificationStatus.Expired,  AssetStatus.Suspended),
                _         => (VerificationStatus.Pending,  asset.Status),
            };

            asset.LastVerificationDate = now;
            asset.UpdatedAt = now;

            if (asset.VerificationStatus is VerificationStatus.Failed or VerificationStatus.Expired)
            {
                _logger.LogWarning(
                    "Certificate verification failed for asset {AssetId} (receipt {ReceiptNumber}) " +
                    "at deposit house '{HouseName}': API returned '{ApiStatus}'. Asset suspended.",
                    asset.Id, asset.ReceiptNumber, depositHouse.Name, statusStr);
            }
            else if (asset.VerificationStatus != previousStatus)
            {
                _logger.LogInformation(
                    "Asset {AssetId} (receipt {ReceiptNumber}): verification status changed " +
                    "{Old} → {New}.",
                    asset.Id, asset.ReceiptNumber, previousStatus, asset.VerificationStatus);
            }
        }
    }

    /// <summary>
    /// For deposit houses with no API, marks as <see cref="VerificationStatus.Pending"/>
    /// any asset whose last verification is absent or older than 90 days, so that a
    /// human reviewer can action the queue.
    /// </summary>
    private void QueueForManualVerification(
        DepositHouse depositHouse,
        IReadOnlyList<Asset> assets)
    {
        var threshold = DateTime.UtcNow - ManualReviewThreshold;
        var now = DateTime.UtcNow;
        var queued = 0;

        foreach (var asset in assets)
        {
            var overdue = !asset.LastVerificationDate.HasValue ||
                          asset.LastVerificationDate.Value < threshold;

            if (!overdue)
                continue;

            if (asset.VerificationStatus != VerificationStatus.Pending)
            {
                asset.VerificationStatus = VerificationStatus.Pending;
                asset.UpdatedAt = now;
                queued++;
            }
        }

        if (queued > 0)
        {
            _logger.LogInformation(
                "Deposit house '{Name}' ({HouseId}) has no API. " +
                "Queued {Count} asset(s) for manual verification (last check older than {Days} days).",
                depositHouse.Name, depositHouse.Id, queued, ManualReviewThreshold.TotalDays);
        }
    }
}
