namespace SynergySwitch.Core.Iso20022;

/// <summary>
/// Parses BER-TLV encoded EMV ICC data from terminal transactions.
/// </summary>
public static class EmvTlvParser
{
    /// <summary>
    /// Parse raw BER-TLV bytes into a tag→value dictionary.
    /// Tag keys are uppercase hex strings (e.g. "9F26").
    /// Values are hex-encoded byte strings.
    /// </summary>
    public static Dictionary<string, string> Parse(byte[] data)
    {
        var result = new Dictionary<string, string>();
        int offset = 0;

        while (offset < data.Length)
        {
            if (offset + 1 >= data.Length) break;

            // Parse tag
            var (tag, tagLen) = ReadTag(data, offset);
            offset += tagLen;
            if (offset >= data.Length) break;

            // Parse length
            var (length, lenBytes) = ReadLength(data, offset);
            offset += lenBytes;
            if (offset + length > data.Length) break;

            // Parse value
            var value = new byte[length];
            Array.Copy(data, offset, value, 0, length);
            offset += length;

            result[tag] = Convert.ToHexString(value);
        }

        return result;
    }

    /// <summary>
    /// Extract a specific tag value from raw BER-TLV data.
    /// Returns null if tag not found.
    /// </summary>
    public static string? GetTag(byte[] data, string tagHex)
    {
        var tags = Parse(data);
        return tags.TryGetValue(tagHex.ToUpper(), out var value) ? value : null;
    }

    private static (string tag, int bytesConsumed) ReadTag(byte[] data, int offset)
    {
        byte first = data[offset];

        // If lower 5 bits are all 1, tag is multi-byte
        if ((first & 0x1F) == 0x1F)
        {
            // Two-byte tag (could be more but EMV uses max 2)
            if (offset + 1 < data.Length)
            {
                string tag = Convert.ToHexString(data, offset, 2).ToUpper();
                return (tag, 2);
            }
        }

        string singleTag = Convert.ToHexString(data, offset, 1).ToUpper();
        return (singleTag, 1);
    }

    private static (int length, int bytesConsumed) ReadLength(byte[] data, int offset)
    {
        byte first = data[offset];

        if ((first & 0x80) == 0)
        {
            // Short form: single byte length
            return (first, 1);
        }

        // Long form
        int numBytes = first & 0x7F;
        int length = 0;
        for (int i = 1; i <= numBytes && offset + i < data.Length; i++)
        {
            length = (length << 8) | data[offset + i];
        }

        return (length, numBytes + 1);
    }
}
