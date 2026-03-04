using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using UniBank.Core.Common.Persistence;
using UniBank.Core.Modules.Accounts.Infrastructure.Services;
using UniBank.Core.Modules.Payments.Infrastructure.Services;
using UniBank.SharedKernel.Caching;
using UniBank.SharedKernel.Results;

namespace UniBank.Core.Modules.Payments.Application.Handlers;

/// <summary>
/// Handles QR code payment processing after scanning (STORY-027).
/// </summary>
public sealed class QrPaymentHandler
{
    private const string QrPaymentKeyPrefix = "qr_payment:";
    private const decimal QrFeePercentage = 0.003m; // 0.3%
    private readonly UniBankDbContext _dbContext;
    private readonly EmvQrCodeService _qrService;
    private readonly PinHashingService _pinHasher;
    private readonly NfcPaymentHandler _paymentExecutor;
    private readonly ICacheStore _cache;
    private readonly ILogger<QrPaymentHandler> _logger;

    public QrPaymentHandler(
        UniBankDbContext dbContext,
        EmvQrCodeService qrService,
        PinHashingService pinHasher,
        NfcPaymentHandler paymentExecutor,
        ICacheStore cache,
        ILogger<QrPaymentHandler> logger)
    {
        _dbContext = dbContext;
        _qrService = qrService;
        _pinHasher = pinHasher;
        _paymentExecutor = paymentExecutor;
        _cache = cache;
        _logger = logger;
    }

    public async Task<Result<PaymentResult>> HandleAsync(
        Commands.QrPaymentCommand command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.QrCodeData))
            return Result.Failure<PaymentResult>(
                new Error("QR.InvalidData", "QR code data is required."));

        if (string.IsNullOrWhiteSpace(command.Pin))
            return Result.Failure<PaymentResult>(
                new Error("Payment.PinRequired", "PIN is required for QR payments."));

        var parsed = _qrService.Parse(command.QrCodeData);
        if (parsed is null)
            return Result.Failure<PaymentResult>(
                new Error("QR.InvalidFormat", "Invalid or corrupted QR code data."));

        var cacheKey = $"{QrPaymentKeyPrefix}{parsed.PaymentReference}";
        var cachedValue = await _cache.GetAsync(cacheKey, cancellationToken);

        if (cachedValue is null)
            return Result.Failure<PaymentResult>(
                new Error("QR.Expired", "QR code payment has expired. Please request a new QR code."));

        var parts = cachedValue.Split('|');
        if (parts.Length < 6)
            return Result.Failure<PaymentResult>(
                new Error("QR.CorruptData", "Stored QR payment data is corrupted."));

        var storedMerchantId = parts[0];
        var merchantAccountId = Guid.Parse(parts[1]);
        var storedAmount = decimal.Parse(parts[2]);
        var storedCurrency = parts[3];
        var merchantName = parts[5];

        var payerAccount = await _dbContext.Accounts
            .FirstOrDefaultAsync(
                a => a.Id == command.AccountId && a.DeletedAt == null,
                cancellationToken);

        if (payerAccount is null)
            return Result.Failure<PaymentResult>(
                new Error("Account.NotFound", "Payer account not found."));

        if (payerAccount.Status != "active")
            return Result.Failure<PaymentResult>(
                new Error("Account.Inactive", "Payer account is not active."));

        if (payerAccount.Id == merchantAccountId)
            return Result.Failure<PaymentResult>(
                new Error("Payment.SelfPayment", "Cannot make a payment to yourself."));

        if (string.IsNullOrEmpty(payerAccount.PinHash))
            return Result.Failure<PaymentResult>(
                new Error("Account.NoPinSet", "Account does not have a PIN configured."));

        if (!_pinHasher.VerifyPin(command.Pin, payerAccount.PinHash))
            return Result.Failure<PaymentResult>(
                new Error("Auth.InvalidPIN", "Invalid PIN."));

        var fee = Math.Round(storedAmount * QrFeePercentage, 2);
        var totalDebit = storedAmount + fee;

        if (payerAccount.AvailableBalance < totalDebit)
            return Result.Failure<PaymentResult>(
                new Error("Payment.InsufficientFunds",
                    $"Insufficient balance. Required: {totalDebit:F2}, Available: {payerAccount.AvailableBalance:F2}"));

        var merchantAccount = await _dbContext.Accounts
            .FirstOrDefaultAsync(
                a => a.Id == merchantAccountId && a.DeletedAt == null,
                cancellationToken);

        if (merchantAccount is null)
            return Result.Failure<PaymentResult>(
                new Error("Merchant.AccountNotFound", "Merchant account not found."));

        var result = await _paymentExecutor.ExecutePaymentAsync(
            payerAccount, merchantAccount, storedAmount, fee,
            storedCurrency, "qr", null, command.QrCodeData,
            null, command.TenantId, merchantName,
            cancellationToken);

        if (result.IsSuccess)
        {
            await _cache.DeleteAsync(cacheKey, cancellationToken);

            _logger.LogInformation(
                "QR payment processed: ref {Reference}, amount {Amount} {Currency}, payer {Payer}, merchant {MerchantId}",
                parsed.PaymentReference, storedAmount, storedCurrency, command.AccountId, storedMerchantId);
        }

        return result;
    }
}
