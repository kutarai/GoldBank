using GoldBank.SharedKernel.Results;

namespace GoldBank.Core.Modules.Accounts.Application.Interfaces;

/// <summary>
/// Handles OTP generation, secure storage in Redis (hashed with SHA-256), and validation.
/// Implements rate limiting (3 requests per phone per hour) and attempt tracking (max 5 attempts).
/// </summary>
public interface IOtpService
{
    /// <summary>
    /// Generates a cryptographically secure 6-digit OTP, hashes it, and stores it in Redis with a 5-minute TTL.
    /// Returns the plaintext OTP for SMS delivery.
    /// </summary>
    Task<Result<string>> GenerateAndStoreOtpAsync(string phoneNumber, string registrationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates the provided OTP against the stored hash in Redis.
    /// Tracks failed attempts and invalidates after 5 consecutive failures.
    /// </summary>
    Task<Result<bool>> ValidateOtpAsync(string registrationId, string otp, string phoneNumber,
        CancellationToken cancellationToken = default);
}
