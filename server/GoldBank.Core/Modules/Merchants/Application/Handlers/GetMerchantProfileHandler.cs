using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using GoldBank.Core.Common.Persistence;
using GoldBank.Core.Modules.Merchants.Domain.Entities;
using GoldBank.SharedKernel.Results;

namespace GoldBank.Core.Modules.Merchants.Application.Handlers;

/// <summary>
/// Retrieves merchant profile details (STORY-051).
/// </summary>
public sealed class GetMerchantProfileHandler
{
    private readonly GoldBankDbContext _dbContext;
    private readonly ILogger<GetMerchantProfileHandler> _logger;

    public GetMerchantProfileHandler(GoldBankDbContext dbContext, ILogger<GetMerchantProfileHandler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<Result<MerchantProfileResult>> HandleAsync(
        Guid merchantId, CancellationToken cancellationToken = default)
    {
        var merchant = await _dbContext.Set<Merchant>()
            .FirstOrDefaultAsync(m => m.Id == merchantId, cancellationToken);

        if (merchant is null)
            return Result.Failure<MerchantProfileResult>(
                new Error("Merchant.NotFound", "Merchant not found."));

        return Result.Success(new MerchantProfileResult(
            MerchantId: merchant.Id.ToString(),
            MerchantCode: merchant.MerchantCode,
            BusinessName: merchant.BusinessName,
            BusinessType: merchant.BusinessType,
            CategoryCode: merchant.CategoryCode,
            BusinessAddress: merchant.BusinessAddress,
            GpsLatitude: merchant.GpsLatitude,
            GpsLongitude: merchant.GpsLongitude,
            Status: merchant.Status,
            KycStatus: merchant.KycStatus,
            IsAgent: merchant.IsAgent,
            CreatedAt: merchant.CreatedAt));
    }
}

public sealed record MerchantProfileResult(
    string MerchantId,
    string MerchantCode,
    string BusinessName,
    string BusinessType,
    string? CategoryCode,
    string? BusinessAddress,
    decimal? GpsLatitude,
    decimal? GpsLongitude,
    string Status,
    string KycStatus,
    bool IsAgent,
    DateTime CreatedAt);
