using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SynergySwitch.Core.Interfaces;
using SynergySwitch.Data;
using SynergySwitch.Data.Entities;

namespace SynergySwitch.Core.MobileMoneyPayment;

public class MobileMoneyPaymentManager : IMobileMoneyPaymentManager
{
    private readonly SwitchDbContext _db;
    private readonly ILogger<MobileMoneyPaymentManager> _logger;

    public MobileMoneyPaymentManager(SwitchDbContext db, ILogger<MobileMoneyPaymentManager> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<MobileMoneyPaymentEntity> RegisterPaymentAsync(
        string paymentReference, string terminalId, string merchantId,
        string currency, long amount, string mobileNumber,
        double? latitude = null, double? longitude = null)
    {
        var entity = new MobileMoneyPaymentEntity
        {
            PaymentReference = paymentReference,
            TerminalId = terminalId,
            MerchantId = merchantId,
            Currency = currency,
            Amount = amount,
            MobileNumber = mobileNumber,
            Latitude = latitude,
            Longitude = longitude,
            Status = "PENDING",
            CreatedAt = DateTime.UtcNow
        };

        _db.MobileMoneyPayments.Add(entity);
        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "Mobile money payment registered: ref={Ref}, terminal={Terminal}, mobile={Mobile}, amount={Amount} {Currency}",
            paymentReference, terminalId, mobileNumber, amount, currency);

        return entity;
    }

    public async Task<MobileMoneyPaymentEntity?> ConfirmPaymentAsync(
        string paymentReference, string authorizationCode, string? providerReference)
    {
        var entity = await _db.MobileMoneyPayments
            .FirstOrDefaultAsync(p => p.PaymentReference == paymentReference && p.Status == "PENDING");

        if (entity is null)
        {
            _logger.LogWarning(
                "Cannot confirm mobile money payment {Ref}: not found or not PENDING", paymentReference);
            return null;
        }

        entity.Status = "CONFIRMED";
        entity.AuthorizationCode = authorizationCode;
        entity.ProviderReference = providerReference;
        entity.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "Mobile money payment confirmed: ref={Ref}, auth={Auth}", paymentReference, authorizationCode);

        return entity;
    }

    public async Task MarkDeclinedAsync(string paymentReference)
    {
        var entity = await _db.MobileMoneyPayments
            .FirstOrDefaultAsync(p => p.PaymentReference == paymentReference && p.Status == "PENDING");

        if (entity is null) return;

        entity.Status = "DECLINED";
        entity.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        _logger.LogInformation("Mobile money payment declined: ref={Ref}", paymentReference);
    }

    public async Task MarkTimedOutAsync(string paymentReference)
    {
        var entity = await _db.MobileMoneyPayments
            .FirstOrDefaultAsync(p => p.PaymentReference == paymentReference && p.Status == "PENDING");

        if (entity is null) return;

        entity.Status = "TIMED_OUT";
        entity.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        _logger.LogInformation("Mobile money payment timed out: ref={Ref}", paymentReference);
    }

    public async Task<MobileMoneyPaymentEntity?> GetPaymentAsync(string paymentReference)
    {
        return await _db.MobileMoneyPayments
            .FirstOrDefaultAsync(p => p.PaymentReference == paymentReference);
    }
}
