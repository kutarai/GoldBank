using SynergySwitch.Data.Entities;

namespace SynergySwitch.Core.Interfaces;

public interface IMobileMoneyPaymentManager
{
    /// <summary>
    /// Register a new mobile money payment request with PENDING status.
    /// </summary>
    Task<MobileMoneyPaymentEntity> RegisterPaymentAsync(
        string paymentReference, string terminalId, string merchantId,
        string currency, long amount, string mobileNumber,
        double? latitude = null, double? longitude = null);

    /// <summary>
    /// Mark a PENDING payment as CONFIRMED when the provider confirms.
    /// Returns the updated entity, or null if not found / not in PENDING state.
    /// </summary>
    Task<MobileMoneyPaymentEntity?> ConfirmPaymentAsync(
        string paymentReference, string authorizationCode, string? providerReference);

    /// <summary>
    /// Mark a PENDING payment as DECLINED.
    /// </summary>
    Task MarkDeclinedAsync(string paymentReference);

    /// <summary>
    /// Mark a PENDING payment as TIMED_OUT when the terminal closes the stream.
    /// </summary>
    Task MarkTimedOutAsync(string paymentReference);

    /// <summary>
    /// Get the current status of a mobile money payment.
    /// </summary>
    Task<MobileMoneyPaymentEntity?> GetPaymentAsync(string paymentReference);
}
