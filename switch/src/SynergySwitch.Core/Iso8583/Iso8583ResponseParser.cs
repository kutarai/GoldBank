using NetCore8583;
using SynergySwitch.Core.Iso20022;
using SynergySwitch.Core.Models;

namespace SynergySwitch.Core.Iso8583;

/// <summary>
/// Parses ISO 8583 0210 (Financial Transaction Response) into the domain AuthorisationResponse.
/// </summary>
public static class Iso8583ResponseParser
{
    /// <summary>
    /// Parse a 0210 response message into an AuthorisationResponse.
    /// </summary>
    public static AuthorisationResponse Parse(IsoMessage response, AuthorisationRequest originalRequest)
    {
        // Field 39: Response code (2 chars — "00" = approved)
        var responseCode39 = response.GetObjectValue(39)?.ToString()?.Trim() ?? "96";

        // Field 38: Authorization identification response (auth code, 6 chars)
        var authCode = response.GetObjectValue(38)?.ToString()?.Trim();

        // Map the 2-char ISO 8583 response code to our domain model
        var (domainResponseCode, reasonCode4Digit) = MapResponseCode(responseCode39);

        // Map to EMV tag 8A
        var emvTag8A = ResponseCodeMapper.ToEmvTag8A(reasonCode4Digit);

        // User-friendly display message
        var displayMessage = ResponseCodeMapper.GetDisplayMessage(reasonCode4Digit);

        return new AuthorisationResponse
        {
            ExchangeId = originalRequest.ExchangeId,
            TransactionReference = originalRequest.TransactionReference,
            ResponseCode = domainResponseCode,
            ResponseReason = reasonCode4Digit,
            AuthorisationCode = domainResponseCode == AuthorisationResponseCode.Approved ? authCode : null,
            EmvResponseCode = emvTag8A,
            DisplayMessage = displayMessage
        };
    }

    /// <summary>
    /// Map ISO 8583 2-character response code (field 39) to domain response code
    /// and a 4-digit reason code. Covers the full Zimswitch response code set.
    /// </summary>
    private static (AuthorisationResponseCode, string) MapResponseCode(string code39)
    {
        return code39 switch
        {
            "00" => (AuthorisationResponseCode.Approved, "0000"),
            "10" => (AuthorisationResponseCode.Partial, "0010"),
            "01" => (AuthorisationResponseCode.Declined, "0001"),
            "02" => (AuthorisationResponseCode.Declined, "0002"),
            "03" => (AuthorisationResponseCode.Declined, "0003"),
            "04" => (AuthorisationResponseCode.Declined, "0004"),
            "05" => (AuthorisationResponseCode.Declined, "0005"),
            "06" => (AuthorisationResponseCode.Declined, "0006"),
            "07" => (AuthorisationResponseCode.Declined, "0007"),
            "12" => (AuthorisationResponseCode.Declined, "0012"),
            "13" => (AuthorisationResponseCode.Declined, "0013"),
            "14" => (AuthorisationResponseCode.Declined, "0014"),
            "15" => (AuthorisationResponseCode.Declined, "0015"),
            "19" => (AuthorisationResponseCode.Declined, "0019"),
            "20" => (AuthorisationResponseCode.Declined, "0020"),
            "21" => (AuthorisationResponseCode.Declined, "0021"),
            "25" => (AuthorisationResponseCode.Declined, "0025"),
            "28" => (AuthorisationResponseCode.TechnicalError, "0028"),
            "30" => (AuthorisationResponseCode.Declined, "0030"),
            "33" => (AuthorisationResponseCode.Declined, "0033"),
            "34" => (AuthorisationResponseCode.Declined, "0034"),
            "36" => (AuthorisationResponseCode.Declined, "0036"),
            "38" => (AuthorisationResponseCode.Declined, "0038"),
            "39" => (AuthorisationResponseCode.Declined, "0039"),
            "40" => (AuthorisationResponseCode.Declined, "0040"),
            "41" => (AuthorisationResponseCode.Declined, "0041"),
            "43" => (AuthorisationResponseCode.Declined, "0043"),
            "51" => (AuthorisationResponseCode.Declined, "0051"),
            "52" => (AuthorisationResponseCode.Declined, "0052"),
            "53" => (AuthorisationResponseCode.Declined, "0053"),
            "54" => (AuthorisationResponseCode.Declined, "0054"),
            "55" => (AuthorisationResponseCode.Declined, "0055"),
            "56" => (AuthorisationResponseCode.Declined, "0056"),
            "57" => (AuthorisationResponseCode.Declined, "0057"),
            "58" => (AuthorisationResponseCode.Declined, "0058"),
            "59" => (AuthorisationResponseCode.Declined, "0059"),
            "61" => (AuthorisationResponseCode.Declined, "0061"),
            "62" => (AuthorisationResponseCode.Declined, "0062"),
            "63" => (AuthorisationResponseCode.Declined, "0063"),
            "65" => (AuthorisationResponseCode.Declined, "0065"),
            "66" => (AuthorisationResponseCode.Declined, "0066"),
            "68" => (AuthorisationResponseCode.TechnicalError, "0068"),
            "75" => (AuthorisationResponseCode.Declined, "0075"),
            "76" => (AuthorisationResponseCode.TechnicalError, "0076"),
            "77" => (AuthorisationResponseCode.Declined, "0077"),
            "78" => (AuthorisationResponseCode.Declined, "0078"),
            "80" => (AuthorisationResponseCode.Declined, "0080"),
            "81" => (AuthorisationResponseCode.TechnicalError, "0081"),
            "82" => (AuthorisationResponseCode.Declined, "0082"),
            "83" => (AuthorisationResponseCode.TechnicalError, "0083"),
            "84" => (AuthorisationResponseCode.Declined, "0084"),
            "85" => (AuthorisationResponseCode.Approved, "0085"),   // No reason to decline
            "86" => (AuthorisationResponseCode.TechnicalError, "0086"),
            "87" => (AuthorisationResponseCode.Declined, "0087"),
            "88" => (AuthorisationResponseCode.TechnicalError, "0088"),
            "89" => (AuthorisationResponseCode.Declined, "0089"),
            "90" => (AuthorisationResponseCode.TechnicalError, "0090"),
            "91" => (AuthorisationResponseCode.TechnicalError, "0091"),
            "92" => (AuthorisationResponseCode.TechnicalError, "0092"),
            "93" => (AuthorisationResponseCode.Declined, "0093"),
            "94" => (AuthorisationResponseCode.Declined, "0094"),
            "95" => (AuthorisationResponseCode.TechnicalError, "0095"),
            "96" => (AuthorisationResponseCode.TechnicalError, "0096"),
            "97" => (AuthorisationResponseCode.TechnicalError, "0097"),
            "98" => (AuthorisationResponseCode.TechnicalError, "0098"),
            "99" => (AuthorisationResponseCode.TechnicalError, "0099"),
            _ => (AuthorisationResponseCode.Declined, $"00{code39}".PadRight(4)[..4])
        };
    }
}
