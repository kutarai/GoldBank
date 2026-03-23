namespace SynergySwitch.Core.Iso20022;

/// <summary>
/// Maps between ISO 20022 response codes, ISO 8583 response codes,
/// and EMV tag 8A (Authorisation Response Code) values.
///
/// Display messages are user-friendly descriptions suitable for showing
/// on the terminal screen. These translate cryptic numeric ISO 8583 codes
/// into clear language the cashier/customer can understand.
/// </summary>
public static class ResponseCodeMapper
{
    /// <summary>
    /// Maps an ISO 8583-style reason code to an EMV tag 8A value.
    /// Tag 8A is 2 bytes of ASCII. "00" approved = hex "3030".
    /// </summary>
    public static string ToEmvTag8A(string reasonCode)
    {
        if (string.IsNullOrEmpty(reasonCode) || reasonCode.Length < 2)
            return "3030"; // Default to approved

        char c1 = reasonCode[0];
        char c2 = reasonCode[1];
        return $"{(int)c1:X2}{(int)c2:X2}";
    }

    /// <summary>
    /// Gets a user-friendly display message for a given 4-digit reason code.
    /// These messages are sent to the POS terminal for display to the cashier.
    /// </summary>
    public static string GetDisplayMessage(string reasonCode) => reasonCode switch
    {
        "0000" => "Approved",
        "0001" => "Please call your bank for authorisation",
        "0002" => "Please call your bank — special conditions",
        "0003" => "Invalid merchant — contact acquirer",
        "0004" => "Card restricted — please use another card",
        "0005" => "Transaction declined by bank",
        "0006" => "Error — please try again",
        "0007" => "Card restricted — please use another card",
        "0010" => "Partially approved",
        "0012" => "Invalid transaction — not supported",
        "0013" => "Invalid amount entered",
        "0014" => "Invalid card number",
        "0015" => "Card issuer not found",
        "0019" => "Please try the transaction again",
        "0020" => "Invalid response from bank — try again",
        "0021" => "No action taken — try again",
        "0025" => "Transaction not found",
        "0028" => "File temporarily unavailable — try later",
        "0030" => "Message format error — contact support",
        "0033" => "Card expired — please use another card",
        "0034" => "Suspected fraud — transaction declined",
        "0036" => "Card restricted — please use another card",
        "0038" => "Too many PIN attempts — card blocked",
        "0039" => "No credit account found",
        "0040" => "Function not supported",
        "0041" => "Card reported lost — transaction declined",
        "0043" => "Card reported stolen — transaction declined",
        "0051" => "Insufficient funds",
        "0052" => "No cheque account found",
        "0053" => "No savings account found",
        "0054" => "Card expired",
        "0055" => "Incorrect PIN",
        "0056" => "Card not recognised — please use another card",
        "0057" => "Transaction not allowed for this card",
        "0058" => "Transaction not permitted on this terminal",
        "0059" => "Suspected fraud — declined",
        "0061" => "Amount exceeds withdrawal limit",
        "0062" => "Card restricted — limited use",
        "0063" => "Security violation — declined",
        "0065" => "Withdrawal frequency exceeded — try later",
        "0066" => "Call your bank — acceptor call acquirer",
        "0068" => "Response received too late — try again",
        "0075" => "Too many incorrect PIN attempts — card blocked",
        "0076" => "Key sync error — contact support",
        "0077" => "Invalid transaction — data inconsistency",
        "0078" => "Account not found",
        "0080" => "Invalid date — check transaction details",
        "0081" => "PIN encryption error — try again",
        "0082" => "Incorrect CVV — check card details",
        "0083" => "Unable to verify PIN",
        "0084" => "Invalid authorisation lifecycle",
        "0085" => "No reason to decline (validation only)",
        "0086" => "Unable to verify PIN",
        "0087" => "Cashback not allowed",
        "0088" => "PIN encryption error — try again",
        "0089" => "Authentication failure — declined",
        "0090" => "Bank system cut-off in progress — try later",
        "0091" => "Bank unavailable — try again shortly",
        "0092" => "Transaction cannot be routed — contact support",
        "0093" => "Transaction cannot be completed — violation of law",
        "0094" => "Duplicate transaction detected",
        "0095" => "System reconciliation error — try later",
        "0096" => "System error — please try again",
        "0097" => "Security key synchronisation needed",
        "0098" => "Bank not available — try later",
        "0099" => "PIN/MAC verification failed",
        _ => $"Transaction declined (code {reasonCode})"
    };

    /// <summary>
    /// Gets a display message directly from a 2-char ISO 8583 field 39 response code.
    /// This is a convenience method that maps to the 4-digit format first.
    /// </summary>
    public static string GetDisplayMessageFromIso8583(string code39)
    {
        var fourDigit = code39.Length == 2 ? $"00{code39}" : code39;
        return GetDisplayMessage(fourDigit);
    }

    /// <summary>
    /// Generate a 6-character authorisation code.
    /// </summary>
    public static string GenerateAuthCode()
    {
        var random = Random.Shared;
        return random.Next(100000, 999999).ToString();
    }
}
