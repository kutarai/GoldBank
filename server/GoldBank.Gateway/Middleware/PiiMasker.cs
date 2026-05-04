using System.Text.RegularExpressions;

namespace GoldBank.Gateway.Middleware;

/// <summary>
/// Utility for masking Personally Identifiable Information (PII) in log output.
/// Handles phone numbers (E.164), national IDs, email addresses, PINs, and account numbers.
/// </summary>
public static partial class PiiMasker
{
    // Phone: E.164 format like +27812345678 -> +27****5678
    [GeneratedRegex(@"\+\d{1,3}\d{4,}", RegexOptions.Compiled)]
    private static partial Regex PhoneRegex();

    // National/Government ID: 13-digit South African ID or similar patterns
    [GeneratedRegex(@"\b\d{13}\b", RegexOptions.Compiled)]
    private static partial Regex NationalIdRegex();

    // Email: user@domain.com -> u***@domain.com
    [GeneratedRegex(@"[a-zA-Z0-9._%+\-]+@[a-zA-Z0-9.\-]+\.[a-zA-Z]{2,}", RegexOptions.Compiled)]
    private static partial Regex EmailRegex();

    // PIN: 4-6 digit sequences labelled as pin in structured data
    [GeneratedRegex(@"(?<=""pin""\s*:\s*"")[0-9]{4,6}(?="")", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex PinRegex();

    // Account number: UUID-like patterns
    [GeneratedRegex(@"(?<=""account_id""\s*:\s*"")[0-9a-fA-F\-]{36}(?="")", RegexOptions.Compiled)]
    private static partial Regex AccountIdRegex();

    /// <summary>
    /// Masks all recognized PII patterns in the input string.
    /// Returns the original string if null or empty.
    /// </summary>
    public static string Mask(string? input)
    {
        if (string.IsNullOrEmpty(input))
            return input ?? string.Empty;

        var result = input;

        // Mask phone numbers: keep country code + last 4 digits
        result = PhoneRegex().Replace(result, match =>
        {
            var value = match.Value;
            if (value.Length <= 5) return value;
            var countryCodeEnd = value.StartsWith('+') ? Math.Min(4, value.Length - 4) : 0;
            var prefix = value[..countryCodeEnd];
            var suffix = value[^4..];
            var masked = new string('*', value.Length - countryCodeEnd - 4);
            return $"{prefix}{masked}{suffix}";
        });

        // Mask national IDs: show first 2 and last 2 digits
        result = NationalIdRegex().Replace(result, match =>
        {
            var value = match.Value;
            return $"{value[..2]}{new string('*', value.Length - 4)}{value[^2..]}";
        });

        // Mask emails: show first char + *** + @domain
        result = EmailRegex().Replace(result, match =>
        {
            var value = match.Value;
            var atIndex = value.IndexOf('@');
            if (atIndex <= 1) return value;
            return $"{value[0]}{new string('*', 3)}{value[atIndex..]}";
        });

        // Mask PINs entirely
        result = PinRegex().Replace(result, match => new string('*', match.Value.Length));

        // Mask account IDs: show first 8 chars
        result = AccountIdRegex().Replace(result, match =>
        {
            var value = match.Value;
            if (value.Length <= 8) return new string('*', value.Length);
            return $"{value[..8]}{new string('*', value.Length - 8)}";
        });

        return result;
    }

    /// <summary>
    /// Masks a single phone number value (not embedded in larger text).
    /// </summary>
    public static string MaskPhone(string? phone)
    {
        if (string.IsNullOrEmpty(phone) || phone.Length < 6)
            return phone ?? string.Empty;

        // Keep country code prefix and last 4 digits
        var countryCodeEnd = phone.StartsWith('+') ? Math.Min(4, phone.Length - 4) : 0;
        var prefix = phone[..countryCodeEnd];
        var suffix = phone[^4..];
        var masked = new string('*', phone.Length - countryCodeEnd - 4);
        return $"{prefix}{masked}{suffix}";
    }

    /// <summary>
    /// Masks a national ID: shows first 2 and last 2 characters.
    /// </summary>
    public static string MaskNationalId(string? id)
    {
        if (string.IsNullOrEmpty(id) || id.Length < 5)
            return id ?? string.Empty;

        return $"{id[..2]}{new string('*', id.Length - 4)}{id[^2..]}";
    }
}
