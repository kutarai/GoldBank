using GoldBank.SharedKernel.Results;

namespace GoldBank.Core.Modules.Accounts.Application.Validators;

/// <summary>
/// Validates PINs for account security. Rejects weak patterns including
/// sequential digits, repeated digits, and commonly guessed PINs.
/// PIN must be 4-6 ASCII digits only.
/// </summary>
public static class PINValidator
{
    /// <summary>
    /// Known weak PINs that are commonly guessed or trivially predictable.
    /// Includes repeated digits, sequential runs, and keypad patterns.
    /// </summary>
    private static readonly HashSet<string> WeakPINs = new(StringComparer.Ordinal)
    {
        // Repeated digits (4-digit)
        "0000", "1111", "2222", "3333", "4444",
        "5555", "6666", "7777", "8888", "9999",
        // Sequential ascending (4-digit)
        "0123", "1234", "2345", "3456", "4567", "5678", "6789",
        // Sequential descending (4-digit)
        "9876", "8765", "7654", "6543", "5432", "4321", "3210",
        // Keypad / common patterns (4-digit)
        "1357", "2468", "1470", "2580", "0852",
        // Repeated digits (5-digit)
        "00000", "11111", "22222", "33333", "44444",
        "55555", "66666", "77777", "88888", "99999",
        // Sequential (5-digit)
        "01234", "12345", "23456", "34567", "45678", "56789",
        "98765", "87654", "76543", "65432", "54321", "43210",
        // Repeated digits (6-digit)
        "000000", "111111", "222222", "333333", "444444",
        "555555", "666666", "777777", "888888", "999999",
        // Sequential (6-digit)
        "012345", "123456", "234567", "345678", "456789",
        "987654", "876543", "765432", "654321", "543210"
    };

    private static readonly Error PinRequired = new("PIN.Required", "PIN is required");
    private static readonly Error PinDigitsOnly = new("PIN.InvalidFormat", "PIN must contain only digits");
    private static readonly Error PinInvalidLength = new("PIN.InvalidLength", "PIN must be 4-6 digits");
    private static readonly Error PinMismatch = new("PIN.Mismatch", "PIN and confirmation do not match");
    private static readonly Error PinTooWeak = new("PIN.TooWeak", "PIN is too easy to guess. Avoid sequential or repeated numbers.");

    /// <summary>
    /// Validates a PIN and its confirmation value against all security rules.
    /// </summary>
    /// <param name="pin">The candidate PIN (4-6 ASCII digits).</param>
    /// <param name="confirmation">The confirmation PIN that must match exactly.</param>
    /// <returns>A success result or a failure result with a descriptive error.</returns>
    public static Result Validate(string pin, string confirmation)
    {
        // Check null/empty
        if (string.IsNullOrWhiteSpace(pin))
            return Result.Failure(PinRequired);

        // Check digits only (ASCII 0-9)
        if (!pin.All(static c => c is >= '0' and <= '9'))
            return Result.Failure(PinDigitsOnly);

        // Check length
        if (pin.Length < 4 || pin.Length > 6)
            return Result.Failure(PinInvalidLength);

        // Check confirmation match
        if (!string.Equals(pin, confirmation, StringComparison.Ordinal))
            return Result.Failure(PinMismatch);

        // Check against known weak PINs list
        if (WeakPINs.Contains(pin))
            return Result.Failure(PinTooWeak);

        // Check for dynamically detected sequential patterns (ascending or descending)
        if (IsSequential(pin))
            return Result.Failure(PinTooWeak);

        // Check for all same digits
        if (IsAllSameDigit(pin))
            return Result.Failure(PinTooWeak);

        return Result.Success();
    }

    /// <summary>
    /// Detects strictly ascending (each digit = previous + 1) or
    /// strictly descending (each digit = previous - 1) sequences.
    /// Examples: "3456" (ascending), "6543" (descending).
    /// </summary>
    private static bool IsSequential(string pin)
    {
        bool ascending = true;
        bool descending = true;

        for (int i = 1; i < pin.Length; i++)
        {
            if (pin[i] - pin[i - 1] != 1)
                ascending = false;
            if (pin[i - 1] - pin[i] != 1)
                descending = false;

            // Early exit: if neither pattern holds, no need to continue
            if (!ascending && !descending)
                return false;
        }

        return ascending || descending;
    }

    /// <summary>
    /// Detects PINs where every digit is the same (e.g., "1111", "55555").
    /// </summary>
    private static bool IsAllSameDigit(string pin)
    {
        char first = pin[0];
        for (int i = 1; i < pin.Length; i++)
        {
            if (pin[i] != first)
                return false;
        }

        return true;
    }
}
