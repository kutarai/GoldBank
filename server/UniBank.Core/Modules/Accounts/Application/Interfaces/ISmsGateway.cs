namespace UniBank.Core.Modules.Accounts.Application.Interfaces;

/// <summary>
/// Abstraction for SMS delivery. In development, the MockSmsGateway logs messages.
/// In production, an implementation will integrate with an actual SMS gateway provider.
/// </summary>
public interface ISmsGateway
{
    Task<bool> SendOtpAsync(string phoneNumber, string message, CancellationToken cancellationToken = default);
    Task<bool> SendAlertAsync(string phoneNumber, string message, CancellationToken cancellationToken = default);
}
