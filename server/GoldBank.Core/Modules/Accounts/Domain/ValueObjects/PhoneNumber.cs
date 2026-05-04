using System.Text.RegularExpressions;
using GoldBank.SharedKernel.Domain;
using GoldBank.SharedKernel.Results;

namespace GoldBank.Core.Modules.Accounts.Domain.ValueObjects;

/// <summary>
/// Value object representing a validated E.164 phone number for Southern African countries.
/// Supported country codes: +27 (ZAF), +263 (ZWE), +260 (ZMB), +258 (MOZ),
/// +267 (BWA), +266 (LSO), +268 (SWZ).
/// </summary>
public sealed class PhoneNumber : ValueObject
{
    private static readonly Regex E164Regex = new(
        @"^\+(?:27|263|260|258|267|266|268)\d{8,9}$",
        RegexOptions.Compiled);

    private static readonly Dictionary<string, string> CountryCodes = new()
    {
        ["+27"] = "ZAF",
        ["+263"] = "ZWE",
        ["+260"] = "ZMB",
        ["+258"] = "MOZ",
        ["+267"] = "BWA",
        ["+266"] = "LSO",
        ["+268"] = "SWZ",
    };

    public string Value { get; }
    public string CountryCode { get; }

    private PhoneNumber(string value, string countryCode)
    {
        Value = value;
        CountryCode = countryCode;
    }

    public static Result<PhoneNumber> Create(string? phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
            return Result.Failure<PhoneNumber>(PhoneNumberErrors.Required);

        var normalized = phoneNumber.Trim().Replace(" ", "");

        if (!E164Regex.IsMatch(normalized))
            return Result.Failure<PhoneNumber>(PhoneNumberErrors.InvalidFormat);

        var countryCode = CountryCodes.Keys.First(cc => normalized.StartsWith(cc, StringComparison.Ordinal));
        return Result.Success(new PhoneNumber(normalized, CountryCodes[countryCode]));
    }

    /// <summary>
    /// Returns a masked representation suitable for logging (e.g., "+27****4567").
    /// </summary>
    public string ToMasked()
    {
        if (Value.Length <= 4)
            return Value;

        var visibleSuffix = Value[^4..];
        var prefix = Value[..^(Value.Length - Value.IndexOf(CountryCodes.Keys.First(
            cc => Value.StartsWith(cc, StringComparison.Ordinal)), StringComparison.Ordinal) -
            CountryCodes.Keys.First(cc => Value.StartsWith(cc, StringComparison.Ordinal)).Length)];

        // Simpler approach: show country code prefix + **** + last 4 digits
        var cc = CountryCodes.Keys.First(cc => Value.StartsWith(cc, StringComparison.Ordinal));
        return $"{cc}****{visibleSuffix}";
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}

public static class PhoneNumberErrors
{
    public static readonly Error Required = new("PhoneNumber.Required", "Phone number is required.");
    public static readonly Error InvalidFormat = new(
        "PhoneNumber.InvalidFormat",
        "Invalid phone number format. Expected E.164 with Southern African country code (+27, +263, +260, +258, +267, +266, +268).");
}
