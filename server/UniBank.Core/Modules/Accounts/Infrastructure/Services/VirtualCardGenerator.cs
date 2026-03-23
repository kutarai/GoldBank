using System.Security.Cryptography;

namespace UniBank.Core.Modules.Accounts.Infrastructure.Services;

/// <summary>
/// Generates Luhn-valid virtual card PANs for bank accounts.
/// Format: 6275 XXXX XXXX XXXX (16 digits)
/// BIN prefix 6275 = UniBank virtual cards.
/// </summary>
public static class VirtualCardGenerator
{
    private const string BinPrefix = "6275";
    private const int PanLength = 16;

    /// <summary>
    /// Generate a unique 16-digit Luhn-valid virtual card PAN.
    /// </summary>
    public static string GeneratePan()
    {
        // 6275 (BIN) + 11 random digits + 1 Luhn check digit = 16
        var randomDigits = RandomNumberGenerator.GetInt32(0, 99_999_999).ToString("D8")
                         + RandomNumberGenerator.GetInt32(0, 1000).ToString("D3");

        var partial = $"{BinPrefix}{randomDigits}"; // 15 digits
        var checkDigit = CalculateLuhnCheckDigit(partial);
        return $"{partial}{checkDigit}";
    }

    /// <summary>
    /// Validates a PAN using the Luhn algorithm.
    /// </summary>
    public static bool IsValidLuhn(string pan)
    {
        if (string.IsNullOrWhiteSpace(pan) || pan.Length < 13 || !pan.All(char.IsDigit))
            return false;

        var sum = 0;
        var alternate = false;
        for (var i = pan.Length - 1; i >= 0; i--)
        {
            var digit = pan[i] - '0';
            if (alternate)
            {
                digit *= 2;
                if (digit > 9) digit -= 9;
            }
            sum += digit;
            alternate = !alternate;
        }
        return sum % 10 == 0;
    }

    private static int CalculateLuhnCheckDigit(string partial)
    {
        var sum = 0;
        var alternate = true; // starts true because check digit position is even from right
        for (var i = partial.Length - 1; i >= 0; i--)
        {
            var digit = partial[i] - '0';
            if (alternate)
            {
                digit *= 2;
                if (digit > 9) digit -= 9;
            }
            sum += digit;
            alternate = !alternate;
        }
        return (10 - (sum % 10)) % 10;
    }
}
