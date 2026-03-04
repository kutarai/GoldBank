using System.Globalization;
using System.Text;

namespace UniBank.Core.Modules.Payments.Infrastructure.Services;

/// <summary>
/// EMV QR code generation following QRCPS (QR Code Payment Specification) merchant-presented mode (STORY-026).
/// Implements TLV (Tag-Length-Value) encoding for EMV data objects and CRC-16/CCITT-FALSE checksum.
///
/// EMV QR code structure:
/// - 00: Payload Format Indicator ("01")
/// - 01: Point of Initiation Method ("12" for dynamic QR)
/// - 26-51: Merchant Account Information (sub-TLV with merchant ID and payment reference)
/// - 52: Merchant Category Code
/// - 53: Transaction Currency (ISO 4217 numeric code)
/// - 54: Transaction Amount
/// - 58: Country Code
/// - 59: Merchant Name
/// - 62: Additional Data Field Template (sub-TLV with reference label)
/// - 63: CRC (CRC-16/CCITT-FALSE)
/// </summary>
public sealed class EmvQrCodeService
{
    private const string PayloadFormatIndicator = "01";
    private const string DynamicQrInitiation = "12";
    private const string DefaultMerchantCategoryCode = "5411"; // Grocery stores
    private const string DefaultCountryCode = "ZA";

    /// <summary>
    /// Generates an EMV-compliant QR code data string for merchant-presented payment.
    /// </summary>
    /// <param name="merchantId">The merchant identifier.</param>
    /// <param name="merchantName">The merchant business name.</param>
    /// <param name="amount">The transaction amount.</param>
    /// <param name="currencyNumeric">ISO 4217 numeric currency code (e.g., "710" for ZAR).</param>
    /// <param name="countryCode">ISO 3166-1 alpha-2 country code.</param>
    /// <param name="paymentReference">Unique payment reference for this QR code.</param>
    /// <param name="categoryCode">Merchant Category Code (MCC).</param>
    /// <returns>EMV QR code data string with CRC-16 checksum.</returns>
    public string Generate(
        string merchantId,
        string merchantName,
        decimal amount,
        string currencyNumeric,
        string? countryCode,
        string paymentReference,
        string? categoryCode)
    {
        var qr = new StringBuilder();

        // 00 - Payload Format Indicator
        qr.Append(EncodeTlv("00", PayloadFormatIndicator));

        // 01 - Point of Initiation Method (12 = dynamic QR)
        qr.Append(EncodeTlv("01", DynamicQrInitiation));

        // 26 - Merchant Account Information (sub-TLV)
        var merchantAccountInfo = new StringBuilder();
        merchantAccountInfo.Append(EncodeTlv("00", "com.unibank")); // Globally unique identifier
        merchantAccountInfo.Append(EncodeTlv("01", merchantId));     // Merchant ID
        merchantAccountInfo.Append(EncodeTlv("02", paymentReference)); // Payment reference
        qr.Append(EncodeTlv("26", merchantAccountInfo.ToString()));

        // 52 - Merchant Category Code
        qr.Append(EncodeTlv("52", categoryCode ?? DefaultMerchantCategoryCode));

        // 53 - Transaction Currency (ISO 4217 numeric)
        qr.Append(EncodeTlv("53", currencyNumeric));

        // 54 - Transaction Amount
        qr.Append(EncodeTlv("54", amount.ToString("F2", CultureInfo.InvariantCulture)));

        // 58 - Country Code
        qr.Append(EncodeTlv("58", countryCode ?? DefaultCountryCode));

        // 59 - Merchant Name (truncate to 25 chars per EMV spec)
        var truncatedName = merchantName.Length > 25 ? merchantName[..25] : merchantName;
        qr.Append(EncodeTlv("59", truncatedName));

        // 62 - Additional Data Field Template (sub-TLV)
        var additionalData = new StringBuilder();
        additionalData.Append(EncodeTlv("05", paymentReference)); // Reference Label
        qr.Append(EncodeTlv("62", additionalData.ToString()));

        // 63 - CRC: Calculate over entire string including "6304" prefix
        qr.Append("6304");
        var crc = CalculateCrc16(qr.ToString());
        qr.Append(crc.ToString("X4"));

        return qr.ToString();
    }

    /// <summary>
    /// Parses an EMV QR code data string and extracts key payment fields.
    /// Returns null if the QR code is invalid or CRC check fails.
    /// </summary>
    public EmvQrParseResult? Parse(string qrData)
    {
        if (string.IsNullOrWhiteSpace(qrData) || qrData.Length < 8)
            return null;

        // Verify CRC
        if (!VerifyCrc(qrData))
            return null;

        var fields = ParseTlvFields(qrData);

        // Validate Payload Format Indicator
        if (!fields.TryGetValue("00", out var pfi) || pfi != PayloadFormatIndicator)
            return null;

        string? merchantId = null;
        string? paymentReference = null;

        // Parse Merchant Account Information (tag 26)
        if (fields.TryGetValue("26", out var merchantInfo))
        {
            var subFields = ParseTlvFields(merchantInfo);
            subFields.TryGetValue("01", out merchantId);
            subFields.TryGetValue("02", out paymentReference);
        }

        fields.TryGetValue("54", out var amountStr);
        fields.TryGetValue("53", out var currencyNumeric);
        fields.TryGetValue("59", out var merchantName);

        decimal amount = 0;
        if (!string.IsNullOrEmpty(amountStr))
            decimal.TryParse(amountStr, CultureInfo.InvariantCulture, out amount);

        if (string.IsNullOrEmpty(merchantId) || string.IsNullOrEmpty(paymentReference))
            return null;

        return new EmvQrParseResult(
            MerchantId: merchantId,
            MerchantName: merchantName ?? string.Empty,
            Amount: amount,
            CurrencyNumeric: currencyNumeric ?? string.Empty,
            PaymentReference: paymentReference);
    }

    /// <summary>
    /// Maps ISO 4217 alpha currency code to its numeric counterpart.
    /// </summary>
    public static string GetCurrencyNumeric(string currencyAlpha)
    {
        return currencyAlpha.ToUpperInvariant() switch
        {
            "ZWG" => "924",
            "USD" => "840",
            "EUR" => "978",
            "GBP" => "826",
            "KES" => "404",
            "NGN" => "566",
            "GHS" => "936",
            "TZS" => "834",
            "UGX" => "800",
            "BWP" => "072",
            "MWK" => "454",
            "ZMW" => "967",
            _ => "924" // Default to ZWG
        };
    }

    /// <summary>
    /// Maps ISO 4217 numeric currency code to its alpha counterpart.
    /// </summary>
    public static string GetCurrencyAlpha(string currencyNumeric)
    {
        return currencyNumeric switch
        {
            "924" => "ZWG",
            "840" => "USD",
            "978" => "EUR",
            "826" => "GBP",
            "404" => "KES",
            "566" => "NGN",
            "936" => "GHS",
            "834" => "TZS",
            "800" => "UGX",
            "072" => "BWP",
            "454" => "MWK",
            "967" => "ZMW",
            _ => "ZWG" // Default to ZWG
        };
    }

    /// <summary>
    /// Encodes a TLV (Tag-Length-Value) data object per EMV QR specification.
    /// Length is encoded as a 2-digit zero-padded decimal string.
    /// </summary>
    private static string EncodeTlv(string tag, string value)
    {
        var length = value.Length.ToString("D2");
        return $"{tag}{length}{value}";
    }

    /// <summary>
    /// Parses TLV-encoded data into a dictionary of tag-value pairs.
    /// </summary>
    private static Dictionary<string, string> ParseTlvFields(string data)
    {
        var fields = new Dictionary<string, string>();
        var pos = 0;

        while (pos + 4 <= data.Length)
        {
            var tag = data.Substring(pos, 2);
            pos += 2;

            if (pos + 2 > data.Length) break;

            var lengthStr = data.Substring(pos, 2);
            pos += 2;

            if (!int.TryParse(lengthStr, out var length)) break;
            if (pos + length > data.Length) break;

            var value = data.Substring(pos, length);
            pos += length;

            fields[tag] = value;
        }

        return fields;
    }

    /// <summary>
    /// Calculates CRC-16/CCITT-FALSE checksum used in EMV QR codes.
    /// Polynomial: 0x1021, Initial value: 0xFFFF
    /// </summary>
    private static ushort CalculateCrc16(string data)
    {
        ushort crc = 0xFFFF;
        var bytes = Encoding.ASCII.GetBytes(data);

        foreach (var b in bytes)
        {
            crc ^= (ushort)(b << 8);
            for (var i = 0; i < 8; i++)
            {
                if ((crc & 0x8000) != 0)
                    crc = (ushort)((crc << 1) ^ 0x1021);
                else
                    crc = (ushort)(crc << 1);
            }
        }

        return crc;
    }

    /// <summary>
    /// Verifies the CRC-16 checksum of an EMV QR code data string.
    /// The last 4 characters are the hex-encoded CRC.
    /// </summary>
    private static bool VerifyCrc(string qrData)
    {
        if (qrData.Length < 8) return false;

        var crcHex = qrData[^4..];
        var payload = qrData[..^4];

        if (!ushort.TryParse(crcHex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var expectedCrc))
            return false;

        var actualCrc = CalculateCrc16(payload);
        return actualCrc == expectedCrc;
    }
}

/// <summary>
/// Result of parsing an EMV QR code.
/// </summary>
public sealed record EmvQrParseResult(
    string MerchantId,
    string MerchantName,
    decimal Amount,
    string CurrencyNumeric,
    string PaymentReference);
