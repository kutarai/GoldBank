using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UniBank.Core.Common.Persistence;
using UniBank.Core.Modules.AssetCustody.Domain.Entities;
using UniBank.Protos.Assets;
using UniBank.SharedKernel.MultiTenancy;

namespace UniBank.Core.Modules.AssetCustody.Grpc;

/// <summary>
/// gRPC service implementation for the Asset Custody module (EPIC-020 / STORY-137).
/// Handles asset registration, listing, daily price queries, portfolio valuation,
/// deposit house management, valuation submission, and asset release workflow.
///
/// Multi-tenancy is enforced at the database schema level by UniBankDbContext, so EF Core
/// queries do not need an additional TenantId filter column. TenantId is still stored on
/// newly-created records for audit purposes.
/// </summary>
public sealed class AssetGrpcService : AssetService.AssetServiceBase
{
    private readonly UniBankDbContext _db;
    private readonly ITenantProvider _tenantProvider;
    private readonly ILogger<AssetGrpcService> _logger;

    // ZWG/USD exchange rate constant.
    // In production this would come from ExchangeRateService; kept simple for STORY-137.
    private const decimal ZwgToUsdRate = 0.0012m;

    public AssetGrpcService(
        UniBankDbContext db,
        ITenantProvider tenantProvider,
        ILogger<AssetGrpcService> logger)
    {
        _db = db;
        _tenantProvider = tenantProvider;
        _logger = logger;
    }

    // =========================================================================
    // Customer RPCs
    // =========================================================================

    public override async Task<AssetResponse> RegisterAsset(
        RegisterAssetRequest request, ServerCallContext context)
    {
        var tenantId = ParseTenantId(_tenantProvider.GetTenantId());

        if (!Guid.TryParse(request.AccountId, out var accountId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Valid account_id is required."));

        if (!Guid.TryParse(request.DepositHouseId, out var depositHouseId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Valid deposit_house_id is required."));

        if (string.IsNullOrWhiteSpace(request.ReceiptNumber))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "receipt_number is required."));

        if (string.IsNullOrWhiteSpace(request.AssetType))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "asset_type is required."));

        if (!System.Enum.TryParse<AssetType>(request.AssetType, ignoreCase: true, out var assetType))
            throw new RpcException(new Status(StatusCode.InvalidArgument,
                $"Unknown asset_type '{request.AssetType}'. Valid values: GoldCoin, GoldBar, Silver, Platinum, PreciousStone, Other."));

        if (!decimal.TryParse(request.Quantity, out var quantity) || quantity <= 0)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Valid positive quantity is required."));

        // Validate deposit house exists and is active
        var depositHouse = await _db.DepositHouses
            .FirstOrDefaultAsync(d => d.Id == depositHouseId, context.CancellationToken);

        if (depositHouse is null)
            throw new RpcException(new Status(StatusCode.NotFound, "Deposit house not found."));

        if (!depositHouse.IsActive)
            throw new RpcException(new Status(StatusCode.FailedPrecondition, "Deposit house is not active."));

        // Enforce unique receipt number per deposit house
        var duplicate = await _db.Assets.AnyAsync(
            a => a.DepositHouseId == depositHouseId &&
                 a.ReceiptNumber == request.ReceiptNumber &&
                 !a.IsDeleted,
            context.CancellationToken);

        if (duplicate)
            throw new RpcException(new Status(StatusCode.AlreadyExists,
                "An asset with this receipt number already exists for the deposit house."));

        var receiptDate = request.ReceiptDate is not null
            ? request.ReceiptDate.ToDateTime()
            : DateTime.UtcNow;

        var asset = new Asset
        {
            AccountId = accountId,
            DepositHouseId = depositHouseId,
            ReceiptNumber = request.ReceiptNumber,
            AssetType = assetType,
            Description = request.Description,
            Quantity = quantity,
            Unit = string.IsNullOrWhiteSpace(request.Unit) ? "units" : request.Unit,
            WeightGrams = decimal.TryParse(request.WeightGrams, out var wg) ? wg : null,
            Purity = decimal.TryParse(request.Purity, out var pur) ? pur : null,
            ReceiptImagePath = request.ReceiptImagePath ?? string.Empty,
            ReceiptDate = DateTime.SpecifyKind(receiptDate, DateTimeKind.Utc),
            LastValuationAmount = 0m,
            VerificationStatus = VerificationStatus.Pending,
            Status = AssetStatus.PendingVerification,
            TenantId = tenantId,
        };

        _db.Assets.Add(asset);
        await _db.SaveChangesAsync(context.CancellationToken);

        _logger.LogInformation(
            "Asset registered: {AssetId} for account {AccountId}, type {AssetType}, receipt {Receipt}",
            asset.Id, accountId, assetType, request.ReceiptNumber);

        return MapToAssetResponse(asset, depositHouse.Name);
    }

    public override async Task<ListMyAssetsResponse> ListMyAssets(
        ListMyAssetsRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.AccountId, out var accountId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Valid account_id is required."));

        var query = _db.Assets
            .Include(a => a.DepositHouse)
            .Where(a => a.AccountId == accountId && !a.IsDeleted);

        if (!string.IsNullOrWhiteSpace(request.StatusFilter))
        {
            var filter = request.StatusFilter.ToLowerInvariant();
            query = filter switch
            {
                "active" => query.Where(a => a.Status == AssetStatus.Active),
                "pending_verification" => query.Where(a => a.Status == AssetStatus.PendingVerification),
                "released" => query.Where(a => a.Status == AssetStatus.Released),
                "suspended" => query.Where(a => a.Status == AssetStatus.Suspended),
                _ => query,
            };
        }

        var assets = await query
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(context.CancellationToken);

        // Fetch today's spot prices for metal value calculations
        var prices = await GetTodayPricesAsync(context.CancellationToken);

        var response = new ListMyAssetsResponse();
        foreach (var asset in assets)
        {
            var value = CalculateCurrentValue(asset, prices);
            response.Assets.Add(MapToAssetResponse(asset, asset.DepositHouse?.Name ?? string.Empty, value));
        }

        return response;
    }

    public override async Task<AssetDetailResponse> GetAssetDetail(
        GetAssetDetailRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.AssetId, out var assetId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Valid asset_id is required."));

        if (!Guid.TryParse(request.AccountId, out var accountId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Valid account_id is required."));

        var asset = await _db.Assets
            .Include(a => a.DepositHouse)
            .Include(a => a.Valuations.OrderByDescending(v => v.CreatedAt))
            .FirstOrDefaultAsync(
                a => a.Id == assetId && a.AccountId == accountId && !a.IsDeleted,
                context.CancellationToken);

        if (asset is null)
            throw new RpcException(new Status(StatusCode.NotFound, "Asset not found."));

        var prices = await GetTodayPricesAsync(context.CancellationToken);
        var value = CalculateCurrentValue(asset, prices);

        var response = new AssetDetailResponse
        {
            Id = asset.Id.ToString(),
            AccountId = asset.AccountId.ToString(),
            ReceiptNumber = asset.ReceiptNumber,
            AssetType = asset.AssetType.ToString(),
            Description = asset.Description,
            Quantity = asset.Quantity.ToString("F6"),
            Unit = asset.Unit,
            WeightGrams = asset.WeightGrams?.ToString("F6") ?? string.Empty,
            Purity = asset.Purity?.ToString("F6") ?? string.Empty,
            CurrentValue = new UniBank.Protos.Common.Money
            {
                Amount = value.ToString("F2"),
                Currency = "ZWG",
            },
            VerificationStatus = asset.VerificationStatus.ToString(),
            Status = asset.Status.ToString(),
            ReceiptDate = Timestamp.FromDateTime(DateTime.SpecifyKind(asset.ReceiptDate, DateTimeKind.Utc)),
            CreatedAt = Timestamp.FromDateTime(DateTime.SpecifyKind(asset.CreatedAt, DateTimeKind.Utc)),
        };

        if (asset.LastValuationDate.HasValue)
            response.LastValuationDate = Timestamp.FromDateTime(
                DateTime.SpecifyKind(asset.LastValuationDate.Value, DateTimeKind.Utc));

        if (asset.LastVerificationDate.HasValue)
            response.LastVerificationDate = Timestamp.FromDateTime(
                DateTime.SpecifyKind(asset.LastVerificationDate.Value, DateTimeKind.Utc));

        foreach (var v in asset.Valuations)
        {
            response.Valuations.Add(new ValuationEntry
            {
                Id = v.Id.ToString(),
                Amount = new UniBank.Protos.Common.Money
                {
                    Amount = v.ValuationAmount.ToString("F2"),
                    Currency = v.Currency,
                },
                ValuerName = v.ValuerName,
                ValuerLicense = v.ValuerLicense,
                Notes = v.Notes,
                CreatedAt = Timestamp.FromDateTime(DateTime.SpecifyKind(v.CreatedAt, DateTimeKind.Utc)),
            });
        }

        if (asset.DepositHouse is not null)
        {
            response.DepositHouse = new DepositHouseInfo
            {
                Id = asset.DepositHouse.Id.ToString(),
                Name = asset.DepositHouse.Name,
                Address = asset.DepositHouse.Address,
                City = asset.DepositHouse.City,
                ContactPhone = asset.DepositHouse.ContactPhone,
                ContactEmail = asset.DepositHouse.ContactEmail,
                LicenseNumber = asset.DepositHouse.LicenseNumber,
                TrustStatus = asset.DepositHouse.TrustStatus.ToString(),
                IsActive = asset.DepositHouse.IsActive,
            };
        }

        return response;
    }

    public override async Task<UniBank.Protos.Common.StatusResponse> RequestAssetRelease(
        RequestAssetReleaseRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.AssetId, out var assetId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Valid asset_id is required."));

        if (!Guid.TryParse(request.AccountId, out var accountId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Valid account_id is required."));

        var asset = await _db.Assets
            .FirstOrDefaultAsync(
                a => a.Id == assetId && a.AccountId == accountId && !a.IsDeleted,
                context.CancellationToken);

        if (asset is null)
            throw new RpcException(new Status(StatusCode.NotFound, "Asset not found."));

        if (asset.Status == AssetStatus.Released)
            throw new RpcException(new Status(StatusCode.FailedPrecondition, "Asset has already been released."));

        if (asset.Status == AssetStatus.Suspended)
            throw new RpcException(new Status(StatusCode.FailedPrecondition, "Asset is suspended and cannot be released."));

        // Mark as suspended (pending admin approval) — admin will move to Released via ApproveAssetRelease
        asset.Status = AssetStatus.Suspended;
        asset.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(context.CancellationToken);

        _logger.LogInformation(
            "Asset release requested: {AssetId} by account {AccountId}. Reason: {Reason}",
            assetId, accountId, request.Reason);

        return new UniBank.Protos.Common.StatusResponse
        {
            Success = true,
            Message = "Release request submitted. Pending admin approval.",
        };
    }

    // =========================================================================
    // Price / Portfolio RPCs
    // =========================================================================

    public override async Task<DailyPricesResponse> GetDailyPrices(
        GetDailyPricesRequest request, ServerCallContext context)
    {
        var query = _db.DailyPrices.AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.AssetType))
            query = query.Where(p => p.AssetType == request.AssetType.ToLowerInvariant());

        DateOnly filterDate;
        if (!string.IsNullOrWhiteSpace(request.Date) && DateOnly.TryParse(request.Date, out filterDate))
            query = query.Where(p => p.Date == filterDate);
        else
            query = query.Where(p => p.Date == DateOnly.FromDateTime(DateTime.UtcNow));

        var prices = await query
            .OrderBy(p => p.AssetType)
            .ToListAsync(context.CancellationToken);

        var response = new DailyPricesResponse();
        foreach (var p in prices)
        {
            response.Prices.Add(new DailyPriceEntry
            {
                AssetType = p.AssetType,
                PricePerGramUsd = p.PricePerGramUsd.ToString("F6"),
                PricePerOzUsd = p.PricePerOzUsd.ToString("F6"),
                Date = p.Date.ToString("yyyy-MM-dd"),
                Source = p.Source,
            });
        }

        return response;
    }

    public override async Task<PortfolioValueResponse> GetPortfolioValue(
        GetPortfolioValueRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.AccountId, out var accountId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Valid account_id is required."));

        var assets = await _db.Assets
            .Where(a => a.AccountId == accountId &&
                        a.Status == AssetStatus.Active &&
                        !a.IsDeleted)
            .ToListAsync(context.CancellationToken);

        var prices = await GetTodayPricesAsync(context.CancellationToken);

        var totalZwg = 0m;
        var byType = new Dictionary<string, (int Count, decimal Total)>();

        foreach (var asset in assets)
        {
            var valueZwg = CalculateCurrentValue(asset, prices);
            totalZwg += valueZwg;

            var key = asset.AssetType.ToString();
            if (!byType.TryGetValue(key, out var existing))
                byType[key] = (1, valueZwg);
            else
                byType[key] = (existing.Count + 1, existing.Total + valueZwg);
        }

        var totalUsd = totalZwg * ZwgToUsdRate;

        var response = new PortfolioValueResponse
        {
            TotalValueZwg = new UniBank.Protos.Common.Money
            {
                Amount = totalZwg.ToString("F2"),
                Currency = "ZWG",
            },
            TotalValueUsd = new UniBank.Protos.Common.Money
            {
                Amount = totalUsd.ToString("F2"),
                Currency = "USD",
            },
            TotalAssetCount = assets.Count,
            CalculatedAt = Timestamp.FromDateTime(DateTime.UtcNow),
        };

        foreach (var (typeName, (count, total)) in byType)
        {
            response.AssetsByType.Add(new AssetTypeSummary
            {
                AssetType = typeName,
                Count = count,
                TotalValue = new UniBank.Protos.Common.Money
                {
                    Amount = total.ToString("F2"),
                    Currency = "ZWG",
                },
            });
        }

        return response;
    }

    // =========================================================================
    // Admin RPCs — Deposit Houses
    // =========================================================================

    public override async Task<ListDepositHousesResponse> ListDepositHouses(
        ListDepositHousesRequest request, ServerCallContext context)
    {
        var query = _db.DepositHouses.AsQueryable();

        if (!request.IncludeInactive)
            query = query.Where(d => d.IsActive);

        if (!string.IsNullOrWhiteSpace(request.TrustStatusFilter) &&
            System.Enum.TryParse<TrustStatus>(request.TrustStatusFilter, ignoreCase: true, out var ts))
        {
            query = query.Where(d => d.TrustStatus == ts);
        }

        var houses = await query
            .OrderBy(d => d.Name)
            .ToListAsync(context.CancellationToken);

        var response = new ListDepositHousesResponse();
        foreach (var h in houses)
            response.DepositHouses.Add(MapToDepositHouseResponse(h));

        return response;
    }

    public override async Task<DepositHouseResponse> CreateDepositHouse(
        CreateDepositHouseRequest request, ServerCallContext context)
    {
        var tenantId = ParseTenantId(_tenantProvider.GetTenantId());

        if (string.IsNullOrWhiteSpace(request.Name))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "name is required."));

        if (string.IsNullOrWhiteSpace(request.LicenseNumber))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "license_number is required."));

        if (!System.Enum.TryParse<TrustStatus>(request.TrustStatus, ignoreCase: true, out var trustStatus))
            trustStatus = TrustStatus.Probationary;

        var house = new DepositHouse
        {
            Name = request.Name,
            Address = request.Address,
            City = request.City,
            ContactPhone = request.ContactPhone,
            ContactEmail = request.ContactEmail,
            LicenseNumber = request.LicenseNumber,
            ApiEndpoint = string.IsNullOrWhiteSpace(request.ApiEndpoint) ? null : request.ApiEndpoint,
            TrustStatus = trustStatus,
            IsActive = true,
            TenantId = tenantId,
        };

        _db.DepositHouses.Add(house);
        await _db.SaveChangesAsync(context.CancellationToken);

        _logger.LogInformation(
            "Deposit house created: {HouseId} — '{Name}' (license: {License})",
            house.Id, house.Name, house.LicenseNumber);

        return MapToDepositHouseResponse(house);
    }

    public override async Task<DepositHouseResponse> UpdateDepositHouse(
        UpdateDepositHouseRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.DepositHouseId, out var houseId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Valid deposit_house_id is required."));

        var house = await _db.DepositHouses
            .FirstOrDefaultAsync(d => d.Id == houseId, context.CancellationToken);

        if (house is null)
            throw new RpcException(new Status(StatusCode.NotFound, "Deposit house not found."));

        if (!string.IsNullOrWhiteSpace(request.Name))
            house.Name = request.Name;
        if (!string.IsNullOrWhiteSpace(request.Address))
            house.Address = request.Address;
        if (!string.IsNullOrWhiteSpace(request.City))
            house.City = request.City;
        if (!string.IsNullOrWhiteSpace(request.ContactPhone))
            house.ContactPhone = request.ContactPhone;
        if (!string.IsNullOrWhiteSpace(request.ContactEmail))
            house.ContactEmail = request.ContactEmail;
        if (!string.IsNullOrWhiteSpace(request.LicenseNumber))
            house.LicenseNumber = request.LicenseNumber;
        if (!string.IsNullOrWhiteSpace(request.ApiEndpoint))
            house.ApiEndpoint = request.ApiEndpoint;
        if (!string.IsNullOrWhiteSpace(request.TrustStatus) &&
            System.Enum.TryParse<TrustStatus>(request.TrustStatus, ignoreCase: true, out var ts))
            house.TrustStatus = ts;

        house.IsActive = request.IsActive;
        house.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(context.CancellationToken);

        _logger.LogInformation(
            "Deposit house updated: {HouseId} — '{Name}', active={Active}, trust={Trust}",
            house.Id, house.Name, house.IsActive, house.TrustStatus);

        return MapToDepositHouseResponse(house);
    }

    // =========================================================================
    // Admin RPCs — Valuations & Certificates
    // =========================================================================

    public override async Task<ValuationResponse> SubmitValuation(
        SubmitValuationRequest request, ServerCallContext context)
    {
        var tenantId = ParseTenantId(_tenantProvider.GetTenantId());

        if (!Guid.TryParse(request.AssetId, out var assetId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Valid asset_id is required."));

        if (!decimal.TryParse(request.ValuationAmount, out var amount) || amount <= 0)
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Valid positive valuation_amount is required."));

        if (string.IsNullOrWhiteSpace(request.ValuerName))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "valuer_name is required."));

        if (string.IsNullOrWhiteSpace(request.ValuerLicense))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "valuer_license is required."));

        var asset = await _db.Assets
            .FirstOrDefaultAsync(a => a.Id == assetId && !a.IsDeleted, context.CancellationToken);

        if (asset is null)
            throw new RpcException(new Status(StatusCode.NotFound, "Asset not found."));

        var currency = string.IsNullOrWhiteSpace(request.Currency) ? "ZWG" : request.Currency;

        var valuation = new AssetValuation
        {
            AssetId = assetId,
            ValuationAmount = amount,
            Currency = currency,
            ValuerName = request.ValuerName,
            ValuerLicense = request.ValuerLicense,
            ReportImagePath = string.IsNullOrWhiteSpace(request.ReportImagePath)
                ? null : request.ReportImagePath,
            Notes = request.Notes ?? string.Empty,
            TenantId = tenantId,
        };

        // Update asset's last valuation snapshot
        asset.LastValuationAmount = amount;
        asset.LastValuationDate = DateTime.UtcNow;
        asset.UpdatedAt = DateTime.UtcNow;

        // If asset was pending verification and now has a valuation, promote to active
        if (asset.Status == AssetStatus.PendingVerification)
            asset.Status = AssetStatus.Active;

        _db.AssetValuations.Add(valuation);
        await _db.SaveChangesAsync(context.CancellationToken);

        _logger.LogInformation(
            "Valuation submitted for asset {AssetId}: {Amount} {Currency} by {Valuer}",
            assetId, amount, currency, request.ValuerName);

        return new ValuationResponse
        {
            Success = true,
            Message = "Valuation recorded successfully.",
            ValuationId = valuation.Id.ToString(),
            AssetId = assetId.ToString(),
            NewValue = new UniBank.Protos.Common.Money
            {
                Amount = amount.ToString("F2"),
                Currency = currency,
            },
            CreatedAt = Timestamp.FromDateTime(DateTime.SpecifyKind(valuation.CreatedAt, DateTimeKind.Utc)),
        };
    }

    public override async Task<VerifyCertificateResponse> VerifyCertificate(
        VerifyCertificateRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.AssetId, out var assetId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Valid asset_id is required."));

        if (!System.Enum.TryParse<VerificationStatus>(request.VerificationStatus, ignoreCase: true, out var status))
            throw new RpcException(new Status(StatusCode.InvalidArgument,
                "Unknown verification_status. Valid values: Verified, Failed, Expired."));

        var asset = await _db.Assets
            .FirstOrDefaultAsync(a => a.Id == assetId && !a.IsDeleted, context.CancellationToken);

        if (asset is null)
            throw new RpcException(new Status(StatusCode.NotFound, "Asset not found."));

        asset.VerificationStatus = status;
        asset.LastVerificationDate = DateTime.UtcNow;
        asset.UpdatedAt = DateTime.UtcNow;

        // Suspend asset if verification failed or certificate expired
        if (status is VerificationStatus.Failed or VerificationStatus.Expired)
            asset.Status = AssetStatus.Suspended;
        else if (status == VerificationStatus.Verified && asset.Status == AssetStatus.Suspended)
            asset.Status = AssetStatus.Active;

        await _db.SaveChangesAsync(context.CancellationToken);

        _logger.LogInformation(
            "Certificate verification recorded for asset {AssetId}: {Status}",
            assetId, status);

        return new VerifyCertificateResponse
        {
            Success = true,
            Message = $"Certificate verification status updated to {status}.",
            AssetId = assetId.ToString(),
            VerificationStatus = status.ToString(),
            VerifiedAt = Timestamp.FromDateTime(DateTime.UtcNow),
        };
    }

    public override async Task<UniBank.Protos.Common.StatusResponse> ApproveAssetRelease(
        ApproveAssetReleaseRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.AssetId, out var assetId))
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Valid asset_id is required."));

        var asset = await _db.Assets
            .FirstOrDefaultAsync(a => a.Id == assetId && !a.IsDeleted, context.CancellationToken);

        if (asset is null)
            throw new RpcException(new Status(StatusCode.NotFound, "Asset not found."));

        asset.Status = AssetStatus.Released;
        asset.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(context.CancellationToken);

        _logger.LogInformation(
            "Asset release approved: {AssetId}. Admin notes: {Notes}",
            assetId, request.AdminNotes);

        return new UniBank.Protos.Common.StatusResponse
        {
            Success = true,
            Message = "Asset release approved. Asset has been removed from the active portfolio.",
        };
    }

    // =========================================================================
    // Private helpers
    // =========================================================================

    /// <summary>
    /// Returns today's spot prices keyed by lower-case metal name ("gold", "silver", "platinum").
    /// Falls back to an empty dictionary if no prices have been loaded for today yet.
    /// </summary>
    private async Task<Dictionary<string, DailyPrice>> GetTodayPricesAsync(
        CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var prices = await _db.DailyPrices
            .Where(p => p.Date == today)
            .ToListAsync(cancellationToken);

        return prices.ToDictionary(p => p.AssetType.ToLowerInvariant());
    }

    /// <summary>
    /// Calculates the current ZWG value of an asset.
    /// Precious metals: quantity × weight_grams × purity × price_per_gram_usd ÷ ZWG_rate.
    /// Other / PreciousStone: last recorded valuation amount.
    /// </summary>
    private static decimal CalculateCurrentValue(Asset asset, Dictionary<string, DailyPrice> prices)
    {
        var metalKey = asset.AssetType switch
        {
            AssetType.GoldCoin or AssetType.GoldBar => "gold",
            AssetType.Silver => "silver",
            AssetType.Platinum => "platinum",
            _ => null,
        };

        if (metalKey is not null &&
            prices.TryGetValue(metalKey, out var price) &&
            asset.WeightGrams.HasValue)
        {
            var purity = asset.Purity ?? 1m;
            var valueUsd = asset.Quantity * asset.WeightGrams.Value * purity * price.PricePerGramUsd;
            return valueUsd / ZwgToUsdRate;   // convert USD → ZWG
        }

        // Non-metal or no price data available: use the last recorded valuation
        return asset.LastValuationAmount;
    }

    private static AssetResponse MapToAssetResponse(
        Asset asset, string depositHouseName, decimal? overrideValue = null)
    {
        var value = overrideValue ?? asset.LastValuationAmount;
        return new AssetResponse
        {
            Id = asset.Id.ToString(),
            AccountId = asset.AccountId.ToString(),
            ReceiptNumber = asset.ReceiptNumber,
            AssetType = asset.AssetType.ToString(),
            Description = asset.Description,
            Quantity = asset.Quantity.ToString("F6"),
            Unit = asset.Unit,
            WeightGrams = asset.WeightGrams?.ToString("F6") ?? string.Empty,
            Purity = asset.Purity?.ToString("F6") ?? string.Empty,
            CurrentValue = new UniBank.Protos.Common.Money
            {
                Amount = value.ToString("F2"),
                Currency = "ZWG",
            },
            VerificationStatus = asset.VerificationStatus.ToString(),
            Status = asset.Status.ToString(),
            DepositHouseId = asset.DepositHouseId.ToString(),
            DepositHouseName = depositHouseName,
            ReceiptDate = Timestamp.FromDateTime(DateTime.SpecifyKind(asset.ReceiptDate, DateTimeKind.Utc)),
            CreatedAt = Timestamp.FromDateTime(DateTime.SpecifyKind(asset.CreatedAt, DateTimeKind.Utc)),
        };
    }

    private static DepositHouseResponse MapToDepositHouseResponse(DepositHouse house)
    {
        return new DepositHouseResponse
        {
            Id = house.Id.ToString(),
            Name = house.Name,
            Address = house.Address,
            City = house.City,
            ContactPhone = house.ContactPhone,
            ContactEmail = house.ContactEmail,
            LicenseNumber = house.LicenseNumber,
            ApiEndpoint = house.ApiEndpoint ?? string.Empty,
            TrustStatus = house.TrustStatus.ToString(),
            IsActive = house.IsActive,
            CreatedAt = Timestamp.FromDateTime(DateTime.SpecifyKind(house.CreatedAt, DateTimeKind.Utc)),
        };
    }

    /// <summary>
    /// Converts the string tenant identifier returned by ITenantProvider to a Guid.
    /// Asset Custody entities use Guid TenantId (unlike the Loans module which uses string).
    /// </summary>
    private static Guid ParseTenantId(string tenantId)
    {
        if (Guid.TryParse(tenantId, out var guid))
            return guid;

        // Fall back to a deterministic namespace-UUID so the code never throws on startup
        return Guid.Empty;
    }
}
