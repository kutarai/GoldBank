using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UniBank.Core.Common.Persistence;
using UniBank.Core.Modules.AssetCustody.Domain.Entities;
using UniBank.SharedKernel.Results;
using Error = UniBank.SharedKernel.Results.Error;

namespace UniBank.Core.Modules.AssetCustody.Application.Handlers;

/// <summary>
/// Orchestrates the two-step asset release workflow introduced in STORY-145:
///
/// 1. <see cref="RequestReleaseAsync"/> — called by the customer to indicate they want their
///    physical asset returned.  The asset transitions from <see cref="AssetStatus.Active"/>
///    to <see cref="AssetStatus.PendingRelease"/> and the request is logged for admin review.
///
/// 2. <see cref="ApproveReleaseAsync"/> — called by an admin once the deposit house has
///    confirmed the asset has been physically released.  The asset transitions to
///    <see cref="AssetStatus.Released"/> and is soft-deleted so that it no longer appears
///    in the customer's active portfolio while still being retained for audit purposes.
///
/// Registration: <c>services.AddScoped&lt;AssetReleaseHandler&gt;()</c>.
/// </summary>
public sealed class AssetReleaseHandler
{
    private readonly UniBankDbContext _db;
    private readonly ILogger<AssetReleaseHandler> _logger;

    public AssetReleaseHandler(UniBankDbContext db, ILogger<AssetReleaseHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    // =========================================================================
    // Step 1 — Customer requests release
    // =========================================================================

    /// <summary>
    /// Validates that the asset belongs to the given account and is currently
    /// <see cref="AssetStatus.Active"/>, then marks it as
    /// <see cref="AssetStatus.PendingRelease"/> pending admin approval.
    /// </summary>
    /// <param name="accountId">The customer account that owns the asset.</param>
    /// <param name="assetId">The asset to be released.</param>
    /// <param name="reason">Free-text reason supplied by the customer.</param>
    public async Task<Result> RequestReleaseAsync(
        Guid accountId,
        Guid assetId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var asset = await _db.Assets
            .FirstOrDefaultAsync(
                a => a.Id == assetId && a.AccountId == accountId && !a.IsDeleted,
                cancellationToken);

        if (asset is null)
            return Result.Failure(new Error(
                "AssetRelease.NotFound",
                $"Asset {assetId} not found for account {accountId}."));

        if (asset.Status != AssetStatus.Active)
            return Result.Failure(new Error(
                "AssetRelease.InvalidStatus",
                $"Asset {assetId} cannot be released from status '{asset.Status}'. " +
                "Only Active assets may be requested for release."));

        asset.Status = AssetStatus.PendingRelease;
        asset.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Asset release requested: {AssetId} by account {AccountId}. Reason: {Reason}",
            assetId, accountId, reason);

        return Result.Success();
    }

    // =========================================================================
    // Step 2 — Admin approves release
    // =========================================================================

    /// <summary>
    /// Marks the asset as <see cref="AssetStatus.Released"/> and soft-deletes it
    /// so that it is excluded from active portfolio queries while still being
    /// retained in the database for audit purposes.
    ///
    /// TODO: notify the deposit house (via deposit house API or email) that the
    /// asset has been approved for physical collection by the customer.
    /// </summary>
    /// <param name="assetId">The asset being approved for release.</param>
    /// <param name="adminId">The admin user approving the release.</param>
    public async Task<Result> ApproveReleaseAsync(
        Guid assetId,
        Guid adminId,
        CancellationToken cancellationToken = default)
    {
        var asset = await _db.Assets
            .FirstOrDefaultAsync(a => a.Id == assetId && !a.IsDeleted, cancellationToken);

        if (asset is null)
            return Result.Failure(new Error(
                "AssetRelease.NotFound",
                $"Asset {assetId} not found."));

        if (asset.Status != AssetStatus.PendingRelease)
            return Result.Failure(new Error(
                "AssetRelease.InvalidStatus",
                $"Asset {assetId} is not in PendingRelease status (current: '{asset.Status}'). " +
                "Only assets that have been requested for release may be approved."));

        var now = DateTime.UtcNow;

        asset.Status = AssetStatus.Released;
        asset.UpdatedAt = now;

        // Soft-delete: asset is excluded from active queries but retained for audit
        asset.IsDeleted = true;
        asset.DeletedAt = now;
        asset.DeletedBy = adminId.ToString();

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Asset release approved: {AssetId} by admin {AdminId}. Asset soft-deleted.",
            assetId, adminId);

        // TODO(STORY-145): Notify deposit house that the asset has been approved for
        // physical release so that it can be prepared for customer collection.

        return Result.Success();
    }
}
