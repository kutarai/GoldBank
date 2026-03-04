using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using UniBank.Core.Modules.Accounts.Application.Interfaces;
using UniBank.SharedKernel.Caching;
using UniBank.SharedKernel.Results;

namespace UniBank.Core.Modules.Accounts.Infrastructure.Services;

/// <summary>
/// OTP service backed by ICacheStore (PostgreSQL or Redis).
/// OTPs are hashed with SHA-256 before storage.
/// Rate limiting: 3 OTP requests per phone number per hour.
/// Attempt tracking: max 5 failed attempts per OTP before invalidation.
/// </summary>
public sealed class OtpService : IOtpService
{
    private readonly ICacheStore _cache;
    private readonly ILogger<OtpService> _logger;

    private const int OtpLength = 6;
    private const int OtpTtlSeconds = 300; // 5 minutes
    private const int MaxAttempts = 5;
    private const int RateLimitPerHour = 3;

    public OtpService(ICacheStore cache, ILogger<OtpService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<Result<string>> GenerateAndStoreOtpAsync(
        string phoneNumber, string registrationId, CancellationToken cancellationToken = default)
    {
        // Check rate limit
        var rateLimitKey = $"otp:ratelimit:{phoneNumber}";
        var requestCount = await _cache.IncrementAsync(rateLimitKey, cancellationToken);
        if (requestCount == 1)
        {
            await _cache.SetExpiryAsync(rateLimitKey, TimeSpan.FromHours(1), cancellationToken);
        }

        if (requestCount > RateLimitPerHour)
        {
            var ttl = await _cache.GetTimeToLiveAsync(rateLimitKey, cancellationToken);
            var minutesRemaining = ttl?.TotalMinutes ?? 60;
            return Result.Failure<string>(new Error(
                "Otp.RateLimitExceeded",
                $"Rate limit exceeded. Try again in {minutesRemaining:F0} minutes."));
        }

        // Generate cryptographically secure OTP
        var otp = GenerateSecureOtp();

        // Hash OTP before storing -- never store plaintext
        var hashedOtp = HashOtp(otp);

        // Store in cache with TTL
        var otpKey = $"otp:{registrationId}";
        var fields = new Dictionary<string, string>
        {
            ["hash"] = hashedOtp,
            ["phone"] = phoneNumber,
            ["attempts"] = "0"
        };

        await _cache.HashSetAsync(otpKey, fields, TimeSpan.FromSeconds(OtpTtlSeconds), cancellationToken);

        _logger.LogInformation(
            "OTP generated for registration {RegistrationId}, stored with {TtlSeconds}s TTL",
            registrationId, OtpTtlSeconds);

        // Log OTP in development mode for testing (never logs in production)
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
        if (string.Equals(environment, "Development", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("[DEV] OTP for {RegistrationId}: {Otp}", registrationId, otp);
        }

        return Result.Success(otp);
    }

    public async Task<Result<bool>> ValidateOtpAsync(
        string registrationId, string otp, string phoneNumber, CancellationToken cancellationToken = default)
    {
        var otpKey = $"otp:{registrationId}";

        // Check if OTP exists (may have expired)
        var exists = await _cache.ExistsAsync(otpKey, cancellationToken);
        if (!exists)
        {
            return Result.Failure<bool>(OtpErrors.Expired);
        }

        // Verify phone number matches
        var storedPhone = await _cache.HashGetAsync(otpKey, "phone", cancellationToken);
        if (storedPhone != phoneNumber)
        {
            return Result.Failure<bool>(OtpErrors.PhoneMismatch);
        }

        // Check attempt count
        var attemptsStr = await _cache.HashGetAsync(otpKey, "attempts", cancellationToken);
        var attempts = int.TryParse(attemptsStr, out var a) ? a : 0;
        if (attempts >= MaxAttempts)
        {
            await _cache.DeleteAsync(otpKey, cancellationToken);
            return Result.Failure<bool>(OtpErrors.Locked);
        }

        // Validate OTP hash
        var storedHash = await _cache.HashGetAsync(otpKey, "hash", cancellationToken);
        var providedHash = HashOtp(otp);

        if (storedHash != providedHash)
        {
            await _cache.HashIncrementAsync(otpKey, "attempts", 1, cancellationToken);
            var remaining = MaxAttempts - attempts - 1;
            return Result.Failure<bool>(new Error(
                "Otp.Invalid",
                $"Invalid OTP. {remaining} attempt{(remaining != 1 ? "s" : "")} remaining."));
        }

        // OTP is valid -- delete it to prevent reuse
        await _cache.DeleteAsync(otpKey, cancellationToken);

        _logger.LogInformation(
            "OTP validated successfully for registration {RegistrationId}",
            registrationId);

        return Result.Success(true);
    }

    private static string GenerateSecureOtp()
    {
        var number = RandomNumberGenerator.GetInt32(0, 1_000_000);
        return number.ToString().PadLeft(OtpLength, '0');
    }

    private static string HashOtp(string otp)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(otp));
        return Convert.ToBase64String(bytes);
    }
}

public static class OtpErrors
{
    public static readonly Error Expired = new("Otp.Expired", "OTP has expired. Please request a new one.");
    public static readonly Error PhoneMismatch = new("Otp.PhoneMismatch", "Phone number mismatch.");
    public static readonly Error Locked = new("Otp.Locked", "OTP verification locked due to too many failed attempts.");
    public static readonly Error RateLimitExceeded = new("Otp.RateLimitExceeded", "Rate limit exceeded.");
}
