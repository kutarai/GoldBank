namespace UniBank.Switching.Models;

/// <summary>
/// Represents an ISO 8583 financial transaction message. Contains the Message Type
/// Indicator (MTI), primary and secondary bitmaps indicating which data elements
/// are present, and a dictionary of data element values keyed by field number.
/// </summary>
public sealed class Iso8583Message
{
    /// <summary>
    /// The 4-digit Message Type Indicator (e.g. "0200" for financial request,
    /// "0210" for financial response, "0100" for authorization request).
    /// </summary>
    public string Mti { get; set; } = string.Empty;

    /// <summary>
    /// 64-bit primary bitmap indicating presence of data elements 1-64.
    /// Bit 1 (MSB) indicates whether a secondary bitmap is present.
    /// </summary>
    public long PrimaryBitmap { get; set; }

    /// <summary>
    /// 64-bit secondary bitmap indicating presence of data elements 65-128.
    /// Only present when bit 1 of the primary bitmap is set.
    /// </summary>
    public long SecondaryBitmap { get; set; }

    /// <summary>
    /// Dictionary of data element values keyed by field number (2-128).
    /// Values are stored as strings regardless of their wire encoding.
    /// </summary>
    public Dictionary<int, string> DataElements { get; set; } = new();

    /// <summary>
    /// Checks whether a specific data element is present in the message.
    /// </summary>
    public bool HasField(int fieldNumber)
    {
        return DataElements.ContainsKey(fieldNumber);
    }

    /// <summary>
    /// Gets the value of a data element, or null if the element is not present.
    /// </summary>
    public string? GetField(int fieldNumber)
    {
        return DataElements.TryGetValue(fieldNumber, out var value) ? value : null;
    }

    /// <summary>
    /// Sets a data element value and updates the appropriate bitmap.
    /// </summary>
    public void SetField(int fieldNumber, string value)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(fieldNumber, 2);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(fieldNumber, 128);

        DataElements[fieldNumber] = value;
        SetBitmapBit(fieldNumber);
    }

    /// <summary>
    /// Returns true if a secondary bitmap is present (bit 1 of the primary bitmap is set).
    /// </summary>
    public bool HasSecondaryBitmap => (PrimaryBitmap & unchecked((long)(1UL << 63))) != 0;

    /// <summary>
    /// Recalculates both bitmaps from the current set of data elements.
    /// </summary>
    public void RebuildBitmaps()
    {
        PrimaryBitmap = 0;
        SecondaryBitmap = 0;

        foreach (var fieldNumber in DataElements.Keys)
        {
            SetBitmapBit(fieldNumber);
        }
    }

    private void SetBitmapBit(int fieldNumber)
    {
        if (fieldNumber <= 64)
        {
            // Bits are numbered 1-64 from MSB to LSB
            PrimaryBitmap |= unchecked((long)(1UL << (64 - fieldNumber)));
        }
        else
        {
            // Set bit 1 of primary bitmap to indicate secondary bitmap is present
            PrimaryBitmap |= unchecked((long)(1UL << 63));
            // Secondary bitmap covers fields 65-128
            SecondaryBitmap |= unchecked((long)(1UL << (128 - fieldNumber)));
        }
    }
}
