using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace UniBank.HSM.Pkcs11;

/// <summary>
/// Software-based HSM simulation using System.Security.Cryptography.
/// Wraps AES-256 key operations, PIN block encoding (ISO 9564 Format 0),
/// MAC generation/verification, and format-preserving tokenization.
/// Intended for dev/test environments only; production should use a real PKCS#11 HSM.
/// </summary>
public sealed class SoftHsmProvider : IDisposable
{
    private readonly ConcurrentDictionary<string, byte[]> _keyStore = new();
    private readonly ConcurrentDictionary<string, string> _tokenVault = new();
    private readonly ILogger<SoftHsmProvider> _logger;
    private bool _disposed;

    public SoftHsmProvider(ILogger<SoftHsmProvider> logger)
    {
        _logger = logger;
    }

    // ── Key Generation ───────────────────────────────────────────────

    /// <summary>
    /// Generates an AES-256 master key, stores it in the in-memory key store,
    /// and returns the key ID, Key Check Value (KCV), and encrypted key material.
    /// KCV = first 3 bytes of AES-ECB encryption of an 8-byte zero block, rendered as hex.
    /// </summary>
    public (string KeyId, string Kcv, byte[] EncryptedKey) GenerateMasterKey(string keyLabel)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var keyId = Guid.NewGuid().ToString("N");
        var keyMaterial = RandomNumberGenerator.GetBytes(32); // AES-256

        _keyStore[keyId] = keyMaterial;

        var kcv = ComputeKcv(keyMaterial);

        // For the "encrypted key" output, encrypt the key material with itself
        // (in production this would be wrapped under a KEK / master wrapping key)
        var encryptedKey = EncryptAesEcbNoPadding(keyMaterial, keyMaterial);

        _logger.LogInformation(
            "Generated master key: KeyId={KeyId}, Label={KeyLabel}, KCV={Kcv}",
            keyId, keyLabel, kcv);

        return (keyId, kcv, encryptedKey);
    }

    // ── Session Key Derivation ───────────────────────────────────────

    /// <summary>
    /// Derives a session key from a stored master key using HMAC-SHA256.
    /// The derivation data (e.g. transaction counter, date) is hashed together
    /// with the master key to produce a 256-bit session key.
    /// </summary>
    public (string SessionKeyId, string Kcv) DeriveSessionKey(string masterKeyId, string derivationData)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_keyStore.TryGetValue(masterKeyId, out var masterKey))
            throw new CryptographicException($"Master key not found: {masterKeyId}");

        var derivedKeyMaterial = HMACSHA256.HashData(masterKey, Encoding.UTF8.GetBytes(derivationData));

        var sessionKeyId = Guid.NewGuid().ToString("N");
        _keyStore[sessionKeyId] = derivedKeyMaterial;

        var kcv = ComputeKcv(derivedKeyMaterial);

        _logger.LogInformation(
            "Derived session key: SessionKeyId={SessionKeyId}, MasterKeyId={MasterKeyId}, KCV={Kcv}",
            sessionKeyId, masterKeyId, kcv);

        return (sessionKeyId, kcv);
    }

    // ── PIN Block Operations ─────────────────────────────────────────

    /// <summary>
    /// Encodes a clear PIN into ISO 9564 Format 0 and encrypts with AES-CBC.
    /// Format 0: PIN field XOR PAN field.
    /// PIN field = 0x0 || PIN length || PIN digits || 0xF padding to 16 bytes.
    /// PAN field = 0x0000 || rightmost 12 digits of PAN (excluding check digit).
    /// </summary>
    public byte[] EncryptPinBlock(string pin, string accountNumber, string encryptionKeyId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_keyStore.TryGetValue(encryptionKeyId, out var key))
            throw new CryptographicException($"Encryption key not found: {encryptionKeyId}");

        var pinBlock = BuildIso9564Format0PinBlock(pin, accountNumber);

        using var aes = Aes.Create();
        aes.Key = key;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.None;
        aes.GenerateIV();

        var encrypted = aes.EncryptCbc(pinBlock, aes.IV, PaddingMode.None);

        // Prepend IV to ciphertext so decryption can extract it
        var result = new byte[aes.IV.Length + encrypted.Length];
        aes.IV.CopyTo(result, 0);
        encrypted.CopyTo(result, aes.IV.Length);

        _logger.LogInformation(
            "Encrypted PIN block: KeyId={KeyId}, AccountNumber={AccountSuffix}",
            encryptionKeyId, MaskAccount(accountNumber));

        return result;
    }

    /// <summary>
    /// Decrypts an AES-CBC-encrypted PIN block and decodes ISO 9564 Format 0 to recover the clear PIN.
    /// </summary>
    public string DecryptPinBlock(byte[] encryptedPinBlock, string accountNumber, string decryptionKeyId)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_keyStore.TryGetValue(decryptionKeyId, out var key))
            throw new CryptographicException($"Decryption key not found: {decryptionKeyId}");

        if (encryptedPinBlock.Length < 32) // 16-byte IV + at least 16-byte block
            throw new CryptographicException("Encrypted PIN block is too short.");

        var iv = encryptedPinBlock[..16];
        var ciphertext = encryptedPinBlock[16..];

        using var aes = Aes.Create();
        aes.Key = key;

        var pinBlock = aes.DecryptCbc(ciphertext, iv, PaddingMode.None);
        var clearPin = DecodeIso9564Format0PinBlock(pinBlock, accountNumber);

        _logger.LogInformation(
            "Decrypted PIN block: KeyId={KeyId}, AccountNumber={AccountSuffix}",
            decryptionKeyId, MaskAccount(accountNumber));

        return clearPin;
    }

    // ── MAC Operations ───────────────────────────────────────────────

    /// <summary>
    /// Generates a MAC over the provided data using the specified algorithm.
    /// Supports HMAC-SHA256 and AES-CMAC.
    /// </summary>
    public byte[] GenerateMac(byte[] data, string macKeyId, MacAlgorithm algorithm)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_keyStore.TryGetValue(macKeyId, out var key))
            throw new CryptographicException($"MAC key not found: {macKeyId}");

        var mac = algorithm switch
        {
            MacAlgorithm.HmacSha256 => HMACSHA256.HashData(key, data),
            MacAlgorithm.CmacAes => ComputeAesCmac(key, data),
            _ => throw new ArgumentOutOfRangeException(nameof(algorithm), algorithm, "Unsupported MAC algorithm.")
        };

        _logger.LogInformation(
            "Generated MAC: KeyId={KeyId}, Algorithm={Algorithm}, DataLength={DataLength}",
            macKeyId, algorithm, data.Length);

        return mac;
    }

    /// <summary>
    /// Verifies a MAC by recomputing it and comparing against the provided value.
    /// Uses constant-time comparison to prevent timing attacks.
    /// </summary>
    public bool VerifyMac(byte[] data, string macKeyId, MacAlgorithm algorithm, byte[] expectedMac)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var computedMac = GenerateMac(data, macKeyId, algorithm);
        var isValid = CryptographicOperations.FixedTimeEquals(computedMac, expectedMac);

        _logger.LogInformation(
            "Verified MAC: KeyId={KeyId}, Algorithm={Algorithm}, IsValid={IsValid}",
            macKeyId, algorithm, isValid);

        return isValid;
    }

    // ── Tokenization ─────────────────────────────────────────────────

    /// <summary>
    /// Generates a format-preserving token for the given PAN.
    /// The token preserves the first 6 and last 4 digits of the PAN,
    /// replacing the middle digits with random numeric values.
    /// A mapping is stored in the token vault for detokenization.
    /// </summary>
    public (string Token, string TokenReference) GenerateToken(string pan)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (pan.Length < 13)
            throw new ArgumentException("PAN must be at least 13 digits.", nameof(pan));

        var prefix = pan[..6];
        var suffix = pan[^4..];
        var middleLength = pan.Length - 10;

        var middleDigits = new char[middleLength];
        for (var i = 0; i < middleLength; i++)
        {
            middleDigits[i] = (char)('0' + RandomNumberGenerator.GetInt32(10));
        }

        var token = string.Concat(prefix, new string(middleDigits), suffix);
        var tokenReference = Guid.NewGuid().ToString("N");

        _tokenVault[tokenReference] = pan;

        _logger.LogInformation(
            "Generated token: TokenReference={TokenReference}, PANPrefix={Prefix}",
            tokenReference, prefix);

        return (token, tokenReference);
    }

    // ── ISO 9564 Format 0 Helpers ────────────────────────────────────

    /// <summary>
    /// Builds a 16-byte ISO 9564 Format 0 PIN block.
    /// PIN field: 0 || pin_length (1 nibble) || pin_digits || F-padding to 16 bytes.
    /// PAN field: 0000 || rightmost 12 digits of PAN excluding check digit || padding.
    /// Result: PIN field XOR PAN field.
    /// </summary>
    private static byte[] BuildIso9564Format0PinBlock(string pin, string accountNumber)
    {
        if (pin.Length < 4 || pin.Length > 12)
            throw new ArgumentException("PIN must be 4-12 digits.", nameof(pin));

        // Build PIN field: "0" + pin_length_hex + pin_digits + "F" padding to 32 hex chars (16 bytes)
        var pinHex = $"0{pin.Length:X}{pin}".PadRight(32, 'F');
        var pinField = Convert.FromHexString(pinHex);

        // Build PAN field: "0000" + rightmost 12 digits of PAN (excluding check digit) + "0" padding
        var panDigits = accountNumber.Replace(" ", "").Replace("-", "");
        // Take rightmost 13 digits, drop the check digit (last), giving 12 digits
        var panRight12 = panDigits.Length >= 13
            ? panDigits.Substring(panDigits.Length - 13, 12)
            : panDigits.PadLeft(12, '0')[..12];

        var panHex = $"0000{panRight12}".PadRight(32, '0');
        var panField = Convert.FromHexString(panHex);

        // XOR to get PIN block
        var pinBlock = new byte[16];
        for (var i = 0; i < 16; i++)
        {
            pinBlock[i] = (byte)(pinField[i] ^ panField[i]);
        }

        return pinBlock;
    }

    /// <summary>
    /// Decodes an ISO 9564 Format 0 PIN block back to a clear PIN string.
    /// </summary>
    private static string DecodeIso9564Format0PinBlock(byte[] pinBlock, string accountNumber)
    {
        // Reconstruct the PAN field
        var panDigits = accountNumber.Replace(" ", "").Replace("-", "");
        var panRight12 = panDigits.Length >= 13
            ? panDigits.Substring(panDigits.Length - 13, 12)
            : panDigits.PadLeft(12, '0')[..12];

        var panHex = $"0000{panRight12}".PadRight(32, '0');
        var panField = Convert.FromHexString(panHex);

        // XOR to recover the PIN field
        var pinField = new byte[16];
        for (var i = 0; i < 16; i++)
        {
            pinField[i] = (byte)(pinBlock[i] ^ panField[i]);
        }

        var pinFieldHex = Convert.ToHexString(pinField).ToUpperInvariant();

        // pinFieldHex starts with "0" + pin_length_hex_nibble + pin_digits + "FFF..."
        var pinLength = int.Parse(pinFieldHex[1].ToString(), System.Globalization.NumberStyles.HexNumber);
        var clearPin = pinFieldHex.Substring(2, pinLength);

        return clearPin;
    }

    // ── AES-CMAC ─────────────────────────────────────────────────────

    /// <summary>
    /// Computes AES-CMAC (RFC 4493) over the given data with the specified key.
    /// </summary>
    private static byte[] ComputeAesCmac(byte[] key, byte[] data)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.None;

        // Step 1: Generate subkeys K1, K2
        var zeroBlock = new byte[16];
        var l = aes.EncryptEcb(zeroBlock, PaddingMode.None);
        var k1 = LeftShiftAndConditionalXor(l);
        var k2 = LeftShiftAndConditionalXor(k1);

        // Step 2: Determine if last block is complete
        int blockCount = (data.Length + 15) / 16;
        bool lastBlockComplete;

        if (blockCount == 0)
        {
            blockCount = 1;
            lastBlockComplete = false;
        }
        else
        {
            lastBlockComplete = data.Length % 16 == 0;
        }

        // Step 3: Build the last block M_n (XOR with K1 or K2)
        var lastBlock = new byte[16];
        int lastBlockStart = (blockCount - 1) * 16;

        if (lastBlockComplete)
        {
            Buffer.BlockCopy(data, lastBlockStart, lastBlock, 0, 16);
            for (var i = 0; i < 16; i++)
                lastBlock[i] ^= k1[i];
        }
        else
        {
            var remaining = data.Length - lastBlockStart;
            if (remaining > 0)
                Buffer.BlockCopy(data, lastBlockStart, lastBlock, 0, remaining);
            lastBlock[remaining] = 0x80; // padding: 10000...0
            for (var i = 0; i < 16; i++)
                lastBlock[i] ^= k2[i];
        }

        // Step 4: CBC-MAC
        var x = new byte[16]; // C_0 = zero block

        for (int blockIdx = 0; blockIdx < blockCount - 1; blockIdx++)
        {
            var block = new byte[16];
            Buffer.BlockCopy(data, blockIdx * 16, block, 0, 16);
            for (var i = 0; i < 16; i++)
                x[i] ^= block[i];
            x = aes.EncryptEcb(x, PaddingMode.None);
        }

        // Process last (modified) block
        for (var i = 0; i < 16; i++)
            x[i] ^= lastBlock[i];
        x = aes.EncryptEcb(x, PaddingMode.None);

        return x;
    }

    /// <summary>
    /// Left-shifts a 128-bit value by 1 bit and XORs with Rb (0x87) if MSB was 1.
    /// Used in AES-CMAC subkey derivation.
    /// </summary>
    private static byte[] LeftShiftAndConditionalXor(byte[] input)
    {
        var output = new byte[16];
        byte carry = 0;

        for (int i = 15; i >= 0; i--)
        {
            output[i] = (byte)((input[i] << 1) | carry);
            carry = (byte)((input[i] >> 7) & 1);
        }

        if ((input[0] & 0x80) != 0)
            output[15] ^= 0x87;

        return output;
    }

    // ── Utility ──────────────────────────────────────────────────────

    /// <summary>
    /// Computes the Key Check Value: encrypt 8 zero bytes (padded to 16 for AES) with ECB,
    /// take the first 3 bytes as hex.
    /// </summary>
    private static string ComputeKcv(byte[] keyMaterial)
    {
        var zeroBlock = new byte[16];
        var encrypted = EncryptAesEcbNoPadding(keyMaterial, zeroBlock);
        return Convert.ToHexString(encrypted[..3]).ToUpperInvariant();
    }

    private static byte[] EncryptAesEcbNoPadding(byte[] key, byte[] plaintext)
    {
        using var aes = Aes.Create();
        aes.Key = key;
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.None;
        return aes.EncryptEcb(plaintext, PaddingMode.None);
    }

    private static string MaskAccount(string accountNumber)
    {
        if (accountNumber.Length <= 4)
            return "****";
        return string.Concat("****", accountNumber.AsSpan(accountNumber.Length - 4));
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        // Securely clear key material from memory
        foreach (var kvp in _keyStore)
        {
            CryptographicOperations.ZeroMemory(kvp.Value);
        }

        _keyStore.Clear();
        _tokenVault.Clear();
    }
}

/// <summary>
/// MAC algorithm selection, mirroring the proto MACAlgorithm enum values.
/// </summary>
public enum MacAlgorithm
{
    HmacSha256 = 1,
    CmacAes = 2
}
