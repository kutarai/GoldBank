namespace UniBank.Notifications.Services;

/// <summary>
/// Abstraction for SMS delivery. Implementations may use Twilio, Africa's Talking,
/// Clickatell, or a console stub for development.
/// </summary>
public interface ISmsProvider
{
    /// <summary>
    /// Sends an SMS message to the specified phone number.
    /// </summary>
    /// <param name="phoneNumber">Recipient phone number in E.164 format.</param>
    /// <param name="message">SMS body text. Should be kept within 160 characters for single-segment delivery.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the message was accepted for delivery; false otherwise.</returns>
    Task<bool> SendAsync(string phoneNumber, string message, CancellationToken cancellationToken = default);
}
