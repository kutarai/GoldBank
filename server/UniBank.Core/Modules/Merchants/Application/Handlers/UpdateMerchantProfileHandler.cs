using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UniBank.Core.Common.Persistence;
using UniBank.Core.Modules.Merchants.Application.Commands;
using UniBank.Core.Modules.Merchants.Domain.Entities;
using UniBank.SharedKernel.Results;

namespace UniBank.Core.Modules.Merchants.Application.Handlers;

/// <summary>
/// Updates non-KYC merchant profile fields (STORY-051).
/// KYC-related fields (registration number, tax ID) cannot be changed via this handler.
/// </summary>
public sealed class UpdateMerchantProfileHandler
{
    private readonly UniBankDbContext _dbContext;
    private readonly ILogger<UpdateMerchantProfileHandler> _logger;

    public UpdateMerchantProfileHandler(UniBankDbContext dbContext, ILogger<UpdateMerchantProfileHandler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<Result<MerchantProfileResult>> HandleAsync(
        UpdateMerchantProfileCommand command, CancellationToken cancellationToken = default)
    {
        var merchant = await _dbContext.Set<Merchant>()
            .FirstOrDefaultAsync(m => m.Id == command.MerchantId, cancellationToken);

        if (merchant is null)
            return Result.Failure<MerchantProfileResult>(
                new Error("Merchant.NotFound", "Merchant not found."));

        // Update non-KYC fields only
        if (!string.IsNullOrEmpty(command.BusinessName))
            merchant.BusinessName = command.BusinessName;

        if (!string.IsNullOrEmpty(command.CategoryCode))
            merchant.CategoryCode = command.CategoryCode;

        if (!string.IsNullOrEmpty(command.BusinessAddress))
            merchant.BusinessAddress = command.BusinessAddress;

        if (command.GpsLatitude.HasValue)
            merchant.GpsLatitude = (decimal)command.GpsLatitude.Value;

        if (command.GpsLongitude.HasValue)
            merchant.GpsLongitude = (decimal)command.GpsLongitude.Value;

        if (command.GpsAccuracyMeters.HasValue)
            merchant.GpsAccuracyMeters = (decimal)command.GpsAccuracyMeters.Value;

        merchant.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Merchant profile updated: {MerchantId}", command.MerchantId);

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
