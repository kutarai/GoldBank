using Microsoft.Extensions.Logging;

namespace UniBank.Notifications.Services;

/// <summary>
/// Development stub that logs SMS messages to the console instead of sending them
/// through a real SMS gateway. Replace with a real provider (Twilio, Africa's Talking, etc.)
/// for production deployments.
/// </summary>
public sealed class ConsoleSmsProvider : ISmsProvider
{
    private readonly ILogger<ConsoleSmsProvider> _logger;

    public ConsoleSmsProvider(ILogger<ConsoleSmsProvider> logger)
    {
        _logger = logger;
    }

    public Task<bool> SendAsync(string phoneNumber, string message, CancellationToken cancellationToken = default)
    {
        var maskedPhone = MaskPhoneNumber(phoneNumber);

        _logger.LogInformation(
            "[CONSOLE SMS] To: {PhoneNumber} | Message: {Message}",
            maskedPhone,
            message);

        return Task.FromResult(true);
    }

    /// <summary>
    /// Masks the phone number for logging, showing only the last 4 digits.
    /// Never log full phone numbers even in development.
    /// </summary>
    private static string MaskPhoneNumber(string phoneNumber)
    {
        if (string.IsNullOrEmpty(phoneNumber) || phoneNumber.Length < 4)
        {
            return "***";
        }

        return string.Concat("***", phoneNumber.AsSpan(phoneNumber.Length - 4));
    }
}
