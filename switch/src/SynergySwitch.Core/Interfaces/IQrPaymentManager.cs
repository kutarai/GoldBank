using SynergySwitch.Data.Entities;

namespace SynergySwitch.Core.Interfaces;

public interface IQrPaymentManager
{
    /// <summary>
    /// Register a new QR payment with PENDING status.
    /// </summary>
    Task<QrPaymentEntity> RegisterPaymentAsync(
        string paymentReference, string terminalId, string merchantId,
        string currency, long amount, string? qrPayload,
        double? latitude = null, double? longitude = null);

    /// <summary>
    /// Mark a PENDING payment as CLAIMED when the customer's bank confirms payment.
    /// Returns the updated entity, or null if not found / not in PENDING state.
    /// </summary>
    Task<QrPaymentEntity?> ClaimPaymentAsync(
        string paymentReference, string authorizationCode, string? providerReference);

    /// <summary>
    /// Mark a PENDING payment as TIMED_OUT when the terminal closes the stream.
    /// </summary>
    Task MarkTimedOutAsync(string paymentReference);

    /// <summary>
    /// Get the current status of a QR payment.
    /// </summary>
    Task<QrPaymentEntity?> GetPaymentAsync(string paymentReference);
}
