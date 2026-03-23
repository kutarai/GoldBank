using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SynergySwitch.Core.Interfaces;
using SynergySwitch.Data;
using SynergySwitch.Data.Entities;

namespace SynergySwitch.Core.QrPayment;

public class QrPaymentManager : IQrPaymentManager
{
    private readonly SwitchDbContext _db;
    private readonly ILogger<QrPaymentManager> _logger;

    public QrPaymentManager(SwitchDbContext db, ILogger<QrPaymentManager> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<QrPaymentEntity> RegisterPaymentAsync(
        string paymentReference, string terminalId, string merchantId,
        string currency, long amount, string? qrPayload,
        double? latitude = null, double? longitude = null)
    {
        var entity = new QrPaymentEntity
        {
            PaymentReference = paymentReference,
            TerminalId = terminalId,
            MerchantId = merchantId,
            Currency = currency,
            Amount = amount,
            QrPayload = qrPayload,
            Latitude = latitude,
            Longitude = longitude,
            Status = "PENDING",
            CreatedAt = DateTime.UtcNow
        };

        _db.QrPayments.Add(entity);
        await _db.SaveChangesAsync();

        _logger.LogInformation("QR payment registered: ref={Ref}, terminal={Terminal}, amount={Amount} {Currency}",
            paymentReference, terminalId, amount, currency);

        return entity;
    }

    public async Task<QrPaymentEntity?> ClaimPaymentAsync(
        string paymentReference, string authorizationCode, string? providerReference)
    {
        var entity = await _db.QrPayments
            .FirstOrDefaultAsync(p => p.PaymentReference == paymentReference && p.Status == "PENDING");

        if (entity is null)
        {
            _logger.LogWarning("Cannot claim QR payment {Ref}: not found or not PENDING", paymentReference);
            return null;
        }

        entity.Status = "CLAIMED";
        entity.AuthorizationCode = authorizationCode;
        entity.ProviderReference = providerReference;
        entity.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        _logger.LogInformation("QR payment claimed: ref={Ref}, auth={Auth}", paymentReference, authorizationCode);

        return entity;
    }

    public async Task MarkTimedOutAsync(string paymentReference)
    {
        var entity = await _db.QrPayments
            .FirstOrDefaultAsync(p => p.PaymentReference == paymentReference && p.Status == "PENDING");

        if (entity is null) return;

        entity.Status = "TIMED_OUT";
        entity.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        _logger.LogInformation("QR payment timed out: ref={Ref}", paymentReference);
    }

    public async Task<QrPaymentEntity?> GetPaymentAsync(string paymentReference)
    {
        return await _db.QrPayments
            .FirstOrDefaultAsync(p => p.PaymentReference == paymentReference);
    }
}
