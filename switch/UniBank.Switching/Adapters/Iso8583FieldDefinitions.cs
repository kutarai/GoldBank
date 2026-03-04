namespace UniBank.Switching.Adapters;

/// <summary>
/// Encoding type for an ISO 8583 data element.
/// </summary>
public enum FieldEncoding
{
    /// <summary>ASCII-encoded alphanumeric characters.</summary>
    Ascii,

    /// <summary>Binary-Coded Decimal (packed numeric).</summary>
    Bcd,

    /// <summary>Raw binary bytes.</summary>
    Binary
}

/// <summary>
/// Length type for an ISO 8583 data element.
/// </summary>
public enum FieldLengthType
{
    /// <summary>Fixed length - no length prefix.</summary>
    Fixed,

    /// <summary>Variable length with 2-digit (LL) length prefix.</summary>
    LlVar,

    /// <summary>Variable length with 3-digit (LLL) length prefix.</summary>
    LllVar
}

/// <summary>
/// Definition of a single ISO 8583 data element including its position, length,
/// encoding, and human-readable name.
/// </summary>
public sealed record Iso8583FieldDefinition(
    int FieldNumber,
    string Name,
    FieldLengthType LengthType,
    int MaxLength,
    FieldEncoding Encoding);

/// <summary>
/// Static registry of ISO 8583 field definitions for the common data elements
/// used in the national payment switch.
/// </summary>
public static class Iso8583FieldDefinitions
{
    private static readonly Dictionary<int, Iso8583FieldDefinition> Fields = new()
    {
        [2]  = new(2,  "Primary Account Number (PAN)",   FieldLengthType.LlVar,  19, FieldEncoding.Ascii),
        [3]  = new(3,  "Processing Code",                FieldLengthType.Fixed,   6, FieldEncoding.Ascii),
        [4]  = new(4,  "Transaction Amount",             FieldLengthType.Fixed,  12, FieldEncoding.Ascii),
        [7]  = new(7,  "Transmission Date & Time",       FieldLengthType.Fixed,  10, FieldEncoding.Ascii),
        [11] = new(11, "System Trace Audit Number",      FieldLengthType.Fixed,   6, FieldEncoding.Ascii),
        [12] = new(12, "Local Transaction Time",         FieldLengthType.Fixed,   6, FieldEncoding.Ascii),
        [13] = new(13, "Local Transaction Date",         FieldLengthType.Fixed,   4, FieldEncoding.Ascii),
        [22] = new(22, "POS Entry Mode",                 FieldLengthType.Fixed,   3, FieldEncoding.Ascii),
        [25] = new(25, "POS Condition Code",             FieldLengthType.Fixed,   2, FieldEncoding.Ascii),
        [32] = new(32, "Acquiring Institution ID",       FieldLengthType.LlVar,  11, FieldEncoding.Ascii),
        [37] = new(37, "Retrieval Reference Number",     FieldLengthType.Fixed,  12, FieldEncoding.Ascii),
        [38] = new(38, "Authorization ID Response",      FieldLengthType.Fixed,   6, FieldEncoding.Ascii),
        [39] = new(39, "Response Code",                  FieldLengthType.Fixed,   2, FieldEncoding.Ascii),
        [41] = new(41, "Card Acceptor Terminal ID",      FieldLengthType.Fixed,   8, FieldEncoding.Ascii),
        [42] = new(42, "Card Acceptor ID Code",          FieldLengthType.Fixed,  15, FieldEncoding.Ascii),
        [43] = new(43, "Card Acceptor Name/Location",    FieldLengthType.Fixed,  40, FieldEncoding.Ascii),
        [48] = new(48, "Additional Data",                FieldLengthType.LllVar, 999, FieldEncoding.Ascii),
        [49] = new(49, "Currency Code, Transaction",     FieldLengthType.Fixed,   3, FieldEncoding.Ascii),
        [54] = new(54, "Additional Amounts",             FieldLengthType.LllVar, 120, FieldEncoding.Ascii),
        [60] = new(60, "Private Use",                    FieldLengthType.LllVar, 999, FieldEncoding.Ascii),
    };

    /// <summary>
    /// Gets the field definition for the specified data element number.
    /// </summary>
    /// <param name="fieldNumber">The ISO 8583 data element number (2-128).</param>
    /// <returns>The field definition, or null if the field is not registered.</returns>
    public static Iso8583FieldDefinition? GetDefinition(int fieldNumber)
    {
        return Fields.TryGetValue(fieldNumber, out var definition) ? definition : null;
    }

    /// <summary>
    /// Returns all registered field definitions.
    /// </summary>
    public static IReadOnlyDictionary<int, Iso8583FieldDefinition> GetAll() => Fields;

    /// <summary>
    /// Checks whether a field definition exists for the given data element number.
    /// </summary>
    public static bool IsSupported(int fieldNumber) => Fields.ContainsKey(fieldNumber);
}
