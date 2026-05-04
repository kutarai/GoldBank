using Microsoft.Extensions.Logging;
using GoldBank.Core.Modules.Accounts.Application.Interfaces;

namespace GoldBank.Core.Modules.Accounts.Infrastructure.Services;

/// <summary>
/// Mock SMS gateway for development and testing.
/// Logs SMS messages instead of sending them. Never logs the raw OTP value in production;
/// in dev mode it logs a sanitized message placeholder.
/// </summary>
public sealed class MockSmsGateway : ISmsGateway
{
    private readonly ILogger<MockSmsGateway> _logger;

    public MockSmsGateway(ILogger<MockSmsGateway> logger)
    {
        _logger = logger;
    }

    public Task<bool> SendOtpAsync(string phoneNumber, string message, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[MOCK SMS] OTP message sent to {Phone} (message length: {Length})",
            MaskPhone(phoneNumber), message.Length);

        return Task.FromResult(true);
    }

    public Task<bool> SendAlertAsync(string phoneNumber, string message, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "[MOCK SMS] Alert sent to {Phone}: {Message}",
            MaskPhone(phoneNumber), message);

        return Task.FromResult(true);
    }

    private static string MaskPhone(string phone)
    {
        if (phone.Length <= 4)
            return phone;
        return phone[..^4] + "****";
    }
}
