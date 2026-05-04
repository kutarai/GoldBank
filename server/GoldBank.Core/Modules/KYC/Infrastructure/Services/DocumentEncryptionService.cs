using System.Security.Cryptography;

namespace GoldBank.Core.Modules.KYC.Infrastructure.Services;

/// <summary>
/// Encrypts KYC documents using AES-256-GCM before storage (STORY-011).
/// In production, key management should use an HSM or KMS.
/// </summary>
public sealed class DocumentEncryptionService
{
    /// <summary>
    /// Encrypts document data and returns (encryptedData, keyReference).
    /// The key reference can be used to retrieve the decryption key from KMS.
    /// </summary>
    public (byte[] EncryptedData, string KeyReference) Encrypt(byte[] plainData)
    {
        var key = RandomNumberGenerator.GetBytes(32); // AES-256
        var nonce = RandomNumberGenerator.GetBytes(12); // GCM nonce
        var tag = new byte[16]; // GCM auth tag
        var ciphertext = new byte[plainData.Length];

        using var aes = new AesGcm(key, 16);
        aes.Encrypt(nonce, plainData, ciphertext, tag);

        // Combine nonce + tag + ciphertext for storage
        var combined = new byte[nonce.Length + tag.Length + ciphertext.Length];
        Buffer.BlockCopy(nonce, 0, combined, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, combined, nonce.Length, tag.Length);
        Buffer.BlockCopy(ciphertext, 0, combined, nonce.Length + tag.Length, ciphertext.Length);

        // In production: store key in HSM/KMS and return key ID
        var keyRef = $"local:{Convert.ToBase64String(key)}";

        return (combined, keyRef);
    }

    public string ComputeChecksum(byte[] data)
    {
        var hash = SHA256.HashData(data);
        return Convert.ToHexStringLower(hash);
    }
}
