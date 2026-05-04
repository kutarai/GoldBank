namespace GoldBank.Core.Modules.Accounts.Infrastructure.Services;

/// <summary>
/// Provides PIN hashing and verification using bcrypt with a work factor of 12.
/// bcrypt cost factor 12 yields approximately 250ms per hash, making brute-force
/// attacks impractical (~4 hashes/second per CPU core).
///
/// SECURITY: The raw PIN must never be logged, stored in plaintext, or retained
/// beyond the scope of the hashing call.
/// </summary>
public sealed class PinHashingService
{
    private const int BcryptWorkFactor = 12;

    /// <summary>
    /// Hashes a PIN using bcrypt with the configured work factor.
    /// The raw PIN is not retained after this call.
    /// </summary>
    /// <param name="pin">The plaintext PIN to hash (4-6 ASCII digits).</param>
    /// <returns>A bcrypt hash string (60 characters).</returns>
    public string HashPin(string pin)
    {
        return BCrypt.Net.BCrypt.HashPassword(pin, BcryptWorkFactor);
    }

    /// <summary>
    /// Verifies a plaintext PIN against a stored bcrypt hash.
    /// bcrypt handles timing-safe comparison internally.
    /// </summary>
    /// <param name="pin">The plaintext PIN to verify.</param>
    /// <param name="hashedPin">The stored bcrypt hash.</param>
    /// <returns>True if the PIN matches the hash; false otherwise.</returns>
    public bool VerifyPin(string pin, string hashedPin)
    {
        return BCrypt.Net.BCrypt.Verify(pin, hashedPin);
    }
}
