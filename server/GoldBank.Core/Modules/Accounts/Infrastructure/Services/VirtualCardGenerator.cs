using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using GoldBank.Core.Common.Persistence;

namespace GoldBank.Core.Modules.Accounts.Infrastructure.Services;

/// <summary>
/// Generates Luhn-valid virtual card PANs for bank accounts.
/// BIN prefix is read from SystemConfig (key: "card.bin_prefix").
/// Falls back to "6275" if not configured.
/// </summary>
public sealed class VirtualCardGenerator
{
    public const string ConfigKey = "card.bin_prefix";
    public const string DefaultBinPrefix = "6275";
    private const int PanLength = 16;

    private readonly GoldBankDbContext _dbContext;

    public VirtualCardGenerator(GoldBankDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// Generate a unique 16-digit Luhn-valid virtual card PAN using the configured BIN prefix.
    /// </summary>
    public async Task<string> GeneratePanAsync(string? tenantId = null, CancellationToken ct = default)
    {
        var bin = await GetBinPrefixAsync(tenantId, ct);
        return GenerateWithBin(bin);
    }

    /// <summary>
    /// Generate a PAN with an explicit BIN prefix (for static/test usage).
    /// </summary>
    public static string GeneratePan(string binPrefix = DefaultBinPrefix)
    {
        return GenerateWithBin(binPrefix);
    }

    private async Task<string> GetBinPrefixAsync(string? tenantId, CancellationToken ct)
    {
        // Try tenant-specific config first, then global
        var config = await _dbContext.SystemConfigs
            .Where(c => c.Key == ConfigKey)
            .Where(c => c.TenantId == tenantId || c.TenantId == null)
            .OrderByDescending(c => c.TenantId) // tenant-specific takes priority over global
            .FirstOrDefaultAsync(ct);

        if (config is not null)
        {
            var value = config.ValueJson.Trim().Trim('"');
            if (value.Length >= 4 && value.Length <= 6 && value.All(char.IsDigit))
                return value;
        }

        return DefaultBinPrefix;
    }

    private static string GenerateWithBin(string binPrefix)
    {
        var randomLength = PanLength - binPrefix.Length - 1; // -1 for Luhn check digit
        var randomPart = "";
        for (var i = 0; i < randomLength; i++)
            randomPart += RandomNumberGenerator.GetInt32(0, 10).ToString();

        var partial = $"{binPrefix}{randomPart}";
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
        var alternate = true;
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
