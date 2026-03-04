using System.Text.RegularExpressions;
using Serilog.Events;
using Serilog.Formatting;

namespace UniBank.Core.Modules.Security.Infrastructure;

/// <summary>
/// Serilog formatter that masks PII (phone numbers, account numbers, PINs) in log output (STORY-075).
/// Ensures sensitive data is never written to log storage in cleartext.
/// </summary>
public sealed partial class PiiMaskingFormatter : ITextFormatter
{
    public void Format(LogEvent logEvent, TextWriter output)
    {
        var message = logEvent.RenderMessage();
        var maskedMessage = MaskPii(message);

        output.Write($"[{logEvent.Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}] ");
        output.Write($"[{logEvent.Level}] ");
        output.Write(maskedMessage);

        if (logEvent.Exception is not null)
        {
            output.Write(" ");
            output.Write(MaskPii(logEvent.Exception.ToString()));
        }

        output.WriteLine();
    }

    /// <summary>
    /// Masks PII patterns in the given text.
    /// </summary>
    public static string MaskPii(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        // Mask phone numbers (international format: +263XXXXXXXXX or similar)
        text = PhonePattern().Replace(text, match =>
        {
            var phone = match.Value;
            if (phone.Length <= 4)
                return phone;
            return phone[..3] + new string('*', phone.Length - 5) + phone[^2..];
        });

        // Mask account numbers (UUID format: keep first 8 chars, mask the rest)
        text = UuidPattern().Replace(text, match =>
        {
            var uuid = match.Value;
            return uuid[..8] + "-****-****-****-************";
        });

        // Mask PIN values (4-6 digit sequences preceded by "pin" keyword)
        text = PinPattern().Replace(text, match =>
            match.Groups[1].Value + "****");

        // Mask national ID numbers
        text = NationalIdPattern().Replace(text, match =>
            match.Groups[1].Value + "***" + match.Groups[2].Value[^2..]);

        return text;
    }

    [GeneratedRegex(@"\+?\d{10,15}", RegexOptions.Compiled)]
    private static partial Regex PhonePattern();

    [GeneratedRegex(@"[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex UuidPattern();

    [GeneratedRegex(@"(pin[:\s=]+)\d{4,6}", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex PinPattern();

    [GeneratedRegex(@"(national[_\s]?id[:\s=]+)(\w{3,20})", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex NationalIdPattern();
}
