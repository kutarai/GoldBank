using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UniBank.Core.Common.Persistence;
using UniBank.Core.Modules.Payments.Infrastructure.Services;
using UniBank.SharedKernel.Caching;
using UniBank.SharedKernel.Results;

namespace UniBank.Core.Modules.Payments.Application.Handlers;

/// <summary>
/// Handles EMV QR code generation for merchant-presented payments (STORY-026).
/// </summary>
public sealed class GenerateQrHandler
{
    private const string QrPaymentKeyPrefix = "qr_payment:";
    private const int DefaultTtlSeconds = 300; // 5 minutes
    private const int MaxTtlSeconds = 900;     // 15 minutes
    private readonly UniBankDbContext _dbContext;
    private readonly EmvQrCodeService _qrService;
    private readonly ICacheStore _cache;
    private readonly ILogger<GenerateQrHandler> _logger;

    public GenerateQrHandler(
        UniBankDbContext dbContext,
        EmvQrCodeService qrService,
        ICacheStore cache,
        ILogger<GenerateQrHandler> logger)
    {
        _dbContext = dbContext;
        _qrService = qrService;
        _cache = cache;
        _logger = logger;
    }

    public async Task<Result<GenerateQrResult>> HandleAsync(
        Commands.GenerateQrCommand command, CancellationToken cancellationToken = default)
    {
        if (command.Amount <= 0)
            return Result.Failure<GenerateQrResult>(
                new Error("QR.InvalidAmount", "Amount must be greater than zero."));

        var merchant = await _dbContext.Merchants
            .FirstOrDefaultAsync(
                m => m.MerchantCode == command.MerchantId && m.Status == "active",
                cancellationToken);

        if (merchant is null)
            return Result.Failure<GenerateQrResult>(
                new Error("Merchant.NotFound", "Merchant not found or not active."));

        var paymentReference = GeneratePaymentReference();

        var ttlSeconds = command.TtlSeconds > 0
            ? Math.Min(command.TtlSeconds, MaxTtlSeconds)
            : DefaultTtlSeconds;

        var expiresAt = DateTime.UtcNow.AddSeconds(ttlSeconds);
        var currencyNumeric = EmvQrCodeService.GetCurrencyNumeric(command.Currency);

        var qrCodeData = _qrService.Generate(
            merchantId: command.MerchantId,
            merchantName: merchant.BusinessName,
            amount: command.Amount,
            currencyNumeric: currencyNumeric,
            countryCode: null,
            paymentReference: paymentReference,
            categoryCode: merchant.CategoryCode);

        var cacheKey = $"{QrPaymentKeyPrefix}{paymentReference}";
        var cacheValue = $"{command.MerchantId}|{merchant.OwnerAccountId}|{command.Amount:F2}|{command.Currency}|{command.Description}|{merchant.BusinessName}";

        await _cache.SetAsync(cacheKey, cacheValue, TimeSpan.FromSeconds(ttlSeconds), cancellationToken);

        _logger.LogInformation(
            "QR code generated for merchant {MerchantId}, amount {Amount} {Currency}, ref {Reference}, TTL {Ttl}s",
            command.MerchantId, command.Amount, command.Currency, paymentReference, ttlSeconds);

        return Result.Success(new GenerateQrResult(
            QrCodeData: qrCodeData,
            PaymentReference: paymentReference,
            ExpiresAt: expiresAt));
    }

    private static string GeneratePaymentReference()
    {
        return $"QR-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid():N}"[..27].ToUpperInvariant();
    }
}

public sealed record GenerateQrResult(
    string QrCodeData,
    string PaymentReference,
    DateTime ExpiresAt);
