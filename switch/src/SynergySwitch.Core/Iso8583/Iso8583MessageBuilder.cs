using NetCore8583;
using SynergySwitch.Core.Models;

namespace SynergySwitch.Core.Iso8583;

/// <summary>
/// Builds ISO 8583 messages from domain models.
/// Field mapping follows the Zimswitch ISO 8587 ASCII specification.
/// </summary>
public static class Iso8583MessageBuilder
{
    private static long _stanCounter = Random.Shared.Next(1, 999999);

    /// <summary>
    /// Build a 0200 (Financial Transaction Request) from an AuthorisationRequest.
    /// </summary>
    public static IsoMessage BuildAuthorisationRequest(
        AuthorisationRequest request,
        BankConnectionSettings settings)
    {
        var msg = new IsoMessage
        {
            Type = 0x0200,
            Encoding = System.Text.Encoding.ASCII,
            Binary = false
        };

        var now = DateTime.UtcNow;
        var stan = Interlocked.Increment(ref _stanCounter) % 1000000;

        // Field 2: PAN (LLVAR, max 19)
        msg.SetValue(2, request.Pan, IsoType.LLVAR, request.Pan.Length);

        // Field 3: Processing code — "00" purchase, "00" default account, "00"
        msg.SetValue(3, "000000", IsoType.NUMERIC, 6);

        // Field 4: Transaction amount (12 digits, minor units)
        msg.SetValue(4, request.Amount.ToString("D12"), IsoType.NUMERIC, 12);

        // Field 7: Transmission date and time (MMddHHmmss)
        msg.SetValue(7, now.ToString("MMddHHmmss"), IsoType.NUMERIC, 10);

        // Field 11: System trace audit number (6 digits)
        msg.SetValue(11, stan.ToString("D6"), IsoType.NUMERIC, 6);

        // Field 12: Local transaction time (HHmmss)
        msg.SetValue(12, now.ToString("HHmmss"), IsoType.NUMERIC, 6);

        // Field 13: Local transaction date (MMdd)
        msg.SetValue(13, now.ToString("MMdd"), IsoType.NUMERIC, 4);

        // Field 14: Expiration date (YYMM)
        if (!string.IsNullOrEmpty(request.ExpiryDate) && request.ExpiryDate.Length >= 4)
        {
            msg.SetValue(14, request.ExpiryDate[..4], IsoType.NUMERIC, 4);
        }

        // Field 18: Merchant category code (4 digits)
        var mcc = request.MerchantCategoryCode ?? "5999";
        msg.SetValue(18, mcc.PadLeft(4, '0'), IsoType.NUMERIC, 4);

        // Field 22: Point of service entry mode (3 digits)
        msg.SetValue(22, MapEntryMode(request.CardEntryMode), IsoType.NUMERIC, 3);

        // Field 23: Card sequence number (3 digits)
        if (!string.IsNullOrEmpty(request.CardSequenceNumber))
        {
            msg.SetValue(23, request.CardSequenceNumber.PadLeft(3, '0'), IsoType.NUMERIC, 3);
        }

        // Field 24: Network international identifier
        msg.SetValue(24, settings.NetworkId.PadLeft(3, '0'), IsoType.NUMERIC, 3);

        // Field 25: POS condition code — "00" normal
        msg.SetValue(25, "00", IsoType.NUMERIC, 2);

        // Field 26: PIN capture code (if PIN present)
        if (request.EncryptedPinBlock is { Length: > 0 })
        {
            msg.SetValue(26, "12", IsoType.NUMERIC, 2);
        }

        // Field 32: Acquiring institution identification code (LLVAR, max 11)
        msg.SetValue(32, settings.AcquiringInstitutionId,
            IsoType.LLVAR, settings.AcquiringInstitutionId.Length);

        // Field 35: Track 2 equivalent data (LLVAR, max 37)
        if (!string.IsNullOrEmpty(request.Track2EquivalentData))
        {
            msg.SetValue(35, request.Track2EquivalentData,
                IsoType.LLVAR, request.Track2EquivalentData.Length);
        }

        // Field 37: Retrieval reference number (12 chars, fixed)
        var rrn = (request.TransactionReference ?? stan.ToString("D6")).PadRight(12)[..12];
        msg.SetValue(37, rrn, IsoType.ALPHA, 12);

        // Field 41: Card acceptor terminal ID (8 chars, fixed)
        msg.SetValue(41, (request.TerminalId ?? "").PadRight(8)[..8], IsoType.ALPHA, 8);

        // Field 42: Card acceptor identification code (15 chars, fixed)
        msg.SetValue(42, (request.MerchantId ?? "").PadRight(15)[..15], IsoType.ALPHA, 15);

        // Field 43: Card acceptor name/location (40 chars, fixed)
        var nameLocation = (request.MerchantName ?? "Synergy Merchant").PadRight(40)[..40];
        msg.SetValue(43, nameLocation, IsoType.ALPHA, 40);

        // Field 49: Currency code, transaction (3 chars — ISO 4217 numeric)
        msg.SetValue(49, MapCurrencyToNumeric(request.Currency), IsoType.ALPHA, 3);

        // Field 52: PIN data (8 bytes binary)
        if (request.EncryptedPinBlock is { Length: > 0 })
        {
            msg.SetValue(52, request.EncryptedPinBlock, IsoType.BINARY, 8);
        }

        // Field 55: ICC/EMV related data (LLLVAR)
        if (request.IccRelatedData is { Length: > 0 })
        {
            var emvHex = Convert.ToHexString(request.IccRelatedData);
            msg.SetValue(55, emvHex, IsoType.LLLVAR, emvHex.Length);
        }

        return msg;
    }

    /// <summary>
    /// Build a 0800 (Network Management Request) for sign-on / echo test.
    /// </summary>
    public static IsoMessage BuildNetworkManagementRequest()
    {
        var msg = new IsoMessage
        {
            Type = 0x0800,
            Encoding = System.Text.Encoding.ASCII,
            Binary = false
        };

        var now = DateTime.UtcNow;
        var stan = Interlocked.Increment(ref _stanCounter) % 1000000;

        msg.SetValue(7, now.ToString("MMddHHmmss"), IsoType.NUMERIC, 10);
        msg.SetValue(11, stan.ToString("D6"), IsoType.NUMERIC, 6);
        msg.SetValue(70, "001", IsoType.NUMERIC, 3); // 001 = sign-on

        return msg;
    }

    private static string MapEntryMode(string? cardEntryMode) => cardEntryMode?.ToUpperInvariant() switch
    {
        "ICC" or "CHIP" or "CICC" => "051",         // ICC, PIN capable
        "NFC" or "CONTACTLESS" or "ECTL_ENTRY" => "071", // Contactless chip
        "MCR" or "SWIPE" or "MGST_ENTRY" => "021",  // Magnetic stripe
        "MANUAL" or "MANU" => "011",                 // Manual/key entry
        _ => "051"
    };

    private static string MapCurrencyToNumeric(string? currency) => currency?.ToUpperInvariant() switch
    {
        "USD" => "840",
        "ZWL" => "932",
        "ZWG" => "924",
        "ZAR" => "710",
        "EUR" => "978",
        "GBP" => "826",
        _ => "840"
    };
}
