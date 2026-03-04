using System.Buffers.Binary;
using System.Text;
using UniBank.SharedKernel.Results;
using UniBank.Switching.Models;

namespace UniBank.Switching.Adapters;

/// <summary>
/// Encodes and decodes ISO 8583 binary messages for national payment switch communication.
/// Handles the MTI, primary/secondary bitmaps, and all supported data elements with their
/// respective field encoding rules (fixed-length, LLVAR, LLLVAR).
/// </summary>
public sealed class Iso8583Adapter
{
    private static readonly Encoding AsciiEncoding = Encoding.ASCII;

    /// <summary>
    /// Decodes a raw binary ISO 8583 message into an <see cref="Iso8583Message"/> model.
    /// </summary>
    /// <param name="data">The raw binary message bytes.</param>
    /// <returns>A Result containing the decoded message or an error.</returns>
    public Result<Iso8583Message> Decode(byte[] data)
    {
        if (data is null || data.Length < 12)
        {
            return Result.Failure<Iso8583Message>(
                new Error("ISO8583.InvalidLength", "Message data is null or too short to contain MTI and bitmap."));
        }

        try
        {
            var message = new Iso8583Message();
            var offset = 0;

            // Read 4-byte ASCII MTI (e.g. "0200")
            message.Mti = AsciiEncoding.GetString(data, offset, 4);
            offset += 4;

            // Read 8-byte primary bitmap
            message.PrimaryBitmap = BinaryPrimitives.ReadInt64BigEndian(data.AsSpan(offset, 8));
            offset += 8;

            // Check for secondary bitmap (bit 1 set = MSB of primary bitmap)
            if (message.HasSecondaryBitmap)
            {
                if (data.Length < offset + 8)
                {
                    return Result.Failure<Iso8583Message>(
                        new Error("ISO8583.InvalidLength", "Message too short for secondary bitmap."));
                }

                message.SecondaryBitmap = BinaryPrimitives.ReadInt64BigEndian(data.AsSpan(offset, 8));
                offset += 8;
            }

            // Parse each data element indicated by the bitmaps
            for (var field = 2; field <= 128; field++)
            {
                if (!IsBitSet(message, field))
                {
                    continue;
                }

                var definition = Iso8583FieldDefinitions.GetDefinition(field);
                if (definition is null)
                {
                    // Skip unknown fields - attempt to move past by treating as fixed 0 length
                    continue;
                }

                var fieldResult = ReadField(data, offset, definition);
                if (fieldResult.IsFailure)
                {
                    return Result.Failure<Iso8583Message>(fieldResult.Error);
                }

                var (value, bytesConsumed) = fieldResult.Value;
                message.DataElements[field] = value;
                offset += bytesConsumed;
            }

            return Result.Success(message);
        }
        catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentOutOfRangeException)
        {
            return Result.Failure<Iso8583Message>(
                new Error("ISO8583.ParseError", $"Failed to parse ISO 8583 message: {ex.Message}"));
        }
    }

    /// <summary>
    /// Encodes an <see cref="Iso8583Message"/> into its binary wire format.
    /// </summary>
    /// <param name="message">The message to encode.</param>
    /// <returns>A Result containing the encoded byte array or an error.</returns>
    public Result<byte[]> Encode(Iso8583Message message)
    {
        if (message is null)
        {
            return Result.Failure<byte[]>(
                new Error("ISO8583.NullMessage", "Message cannot be null."));
        }

        if (string.IsNullOrEmpty(message.Mti) || message.Mti.Length != 4)
        {
            return Result.Failure<byte[]>(
                new Error("ISO8583.InvalidMTI", "MTI must be exactly 4 characters."));
        }

        try
        {
            message.RebuildBitmaps();

            using var stream = new MemoryStream(512);

            // Write 4-byte ASCII MTI
            stream.Write(AsciiEncoding.GetBytes(message.Mti));

            // Write 8-byte primary bitmap
            Span<byte> bitmapBytes = stackalloc byte[8];
            BinaryPrimitives.WriteInt64BigEndian(bitmapBytes, message.PrimaryBitmap);
            stream.Write(bitmapBytes);

            // Write secondary bitmap if needed
            if (message.HasSecondaryBitmap)
            {
                BinaryPrimitives.WriteInt64BigEndian(bitmapBytes, message.SecondaryBitmap);
                stream.Write(bitmapBytes);
            }

            // Write each data element in field-number order
            foreach (var field in message.DataElements.Keys.Order())
            {
                var definition = Iso8583FieldDefinitions.GetDefinition(field);
                if (definition is null)
                {
                    return Result.Failure<byte[]>(
                        new Error("ISO8583.UnsupportedField", $"No field definition for data element {field}."));
                }

                var fieldResult = WriteField(message.DataElements[field], definition);
                if (fieldResult.IsFailure)
                {
                    return Result.Failure<byte[]>(fieldResult.Error);
                }

                stream.Write(fieldResult.Value);
            }

            return Result.Success(stream.ToArray());
        }
        catch (Exception ex)
        {
            return Result.Failure<byte[]>(
                new Error("ISO8583.EncodeError", $"Failed to encode ISO 8583 message: {ex.Message}"));
        }
    }

    /// <summary>
    /// Reads a single data element from the binary message at the given offset.
    /// Returns the string value and number of bytes consumed.
    /// </summary>
    private static Result<(string Value, int BytesConsumed)> ReadField(
        byte[] data, int offset, Iso8583FieldDefinition definition)
    {
        var remaining = data.Length - offset;

        switch (definition.LengthType)
        {
            case FieldLengthType.Fixed:
            {
                var fieldLength = GetFixedFieldByteLength(definition);
                if (remaining < fieldLength)
                {
                    return Result.Failure<(string, int)>(
                        new Error("ISO8583.FieldTruncated",
                            $"DE{definition.FieldNumber}: expected {fieldLength} bytes, only {remaining} available."));
                }

                var value = ReadFieldValue(data, offset, fieldLength, definition.Encoding);
                return Result.Success<(string, int)>((value, fieldLength));
            }

            case FieldLengthType.LlVar:
            {
                if (remaining < 2)
                {
                    return Result.Failure<(string, int)>(
                        new Error("ISO8583.FieldTruncated",
                            $"DE{definition.FieldNumber}: not enough bytes for LLVAR length prefix."));
                }

                var lengthStr = AsciiEncoding.GetString(data, offset, 2);
                if (!int.TryParse(lengthStr, out var fieldLength) || fieldLength < 0 || fieldLength > definition.MaxLength)
                {
                    return Result.Failure<(string, int)>(
                        new Error("ISO8583.InvalidFieldLength",
                            $"DE{definition.FieldNumber}: invalid LLVAR length '{lengthStr}'."));
                }

                if (remaining < 2 + fieldLength)
                {
                    return Result.Failure<(string, int)>(
                        new Error("ISO8583.FieldTruncated",
                            $"DE{definition.FieldNumber}: LLVAR data truncated."));
                }

                var value = ReadFieldValue(data, offset + 2, fieldLength, definition.Encoding);
                return Result.Success<(string, int)>((value, 2 + fieldLength));
            }

            case FieldLengthType.LllVar:
            {
                if (remaining < 3)
                {
                    return Result.Failure<(string, int)>(
                        new Error("ISO8583.FieldTruncated",
                            $"DE{definition.FieldNumber}: not enough bytes for LLLVAR length prefix."));
                }

                var lengthStr = AsciiEncoding.GetString(data, offset, 3);
                if (!int.TryParse(lengthStr, out var fieldLength) || fieldLength < 0 || fieldLength > definition.MaxLength)
                {
                    return Result.Failure<(string, int)>(
                        new Error("ISO8583.InvalidFieldLength",
                            $"DE{definition.FieldNumber}: invalid LLLVAR length '{lengthStr}'."));
                }

                if (remaining < 3 + fieldLength)
                {
                    return Result.Failure<(string, int)>(
                        new Error("ISO8583.FieldTruncated",
                            $"DE{definition.FieldNumber}: LLLVAR data truncated."));
                }

                var value = ReadFieldValue(data, offset + 3, fieldLength, definition.Encoding);
                return Result.Success<(string, int)>((value, 3 + fieldLength));
            }

            default:
                return Result.Failure<(string, int)>(
                    new Error("ISO8583.UnsupportedLengthType",
                        $"DE{definition.FieldNumber}: unsupported length type {definition.LengthType}."));
        }
    }

    /// <summary>
    /// Writes a single data element to its binary representation.
    /// </summary>
    private static Result<byte[]> WriteField(string value, Iso8583FieldDefinition definition)
    {
        switch (definition.LengthType)
        {
            case FieldLengthType.Fixed:
            {
                var fieldLength = GetFixedFieldByteLength(definition);
                var padded = PadFieldValue(value, fieldLength, definition);
                return Result.Success(WriteFieldValue(padded, definition.Encoding));
            }

            case FieldLengthType.LlVar:
            {
                if (value.Length > definition.MaxLength)
                {
                    return Result.Failure<byte[]>(
                        new Error("ISO8583.FieldTooLong",
                            $"DE{definition.FieldNumber}: value length {value.Length} exceeds max {definition.MaxLength}."));
                }

                var lengthPrefix = AsciiEncoding.GetBytes(value.Length.ToString("D2"));
                var fieldBytes = WriteFieldValue(value, definition.Encoding);
                var result = new byte[lengthPrefix.Length + fieldBytes.Length];
                Buffer.BlockCopy(lengthPrefix, 0, result, 0, lengthPrefix.Length);
                Buffer.BlockCopy(fieldBytes, 0, result, lengthPrefix.Length, fieldBytes.Length);
                return Result.Success(result);
            }

            case FieldLengthType.LllVar:
            {
                if (value.Length > definition.MaxLength)
                {
                    return Result.Failure<byte[]>(
                        new Error("ISO8583.FieldTooLong",
                            $"DE{definition.FieldNumber}: value length {value.Length} exceeds max {definition.MaxLength}."));
                }

                var lengthPrefix = AsciiEncoding.GetBytes(value.Length.ToString("D3"));
                var fieldBytes = WriteFieldValue(value, definition.Encoding);
                var result = new byte[lengthPrefix.Length + fieldBytes.Length];
                Buffer.BlockCopy(lengthPrefix, 0, result, 0, lengthPrefix.Length);
                Buffer.BlockCopy(fieldBytes, 0, result, lengthPrefix.Length, fieldBytes.Length);
                return Result.Success(result);
            }

            default:
                return Result.Failure<byte[]>(
                    new Error("ISO8583.UnsupportedLengthType",
                        $"DE{definition.FieldNumber}: unsupported length type {definition.LengthType}."));
        }
    }

    /// <summary>
    /// Returns the byte length for a fixed-length field based on its encoding.
    /// BCD fields pack two digits per byte; ASCII and binary use the max length directly.
    /// </summary>
    private static int GetFixedFieldByteLength(Iso8583FieldDefinition definition)
    {
        return definition.Encoding switch
        {
            FieldEncoding.Bcd => (definition.MaxLength + 1) / 2,
            _ => definition.MaxLength
        };
    }

    /// <summary>
    /// Reads a field value from raw bytes based on its encoding.
    /// </summary>
    private static string ReadFieldValue(byte[] data, int offset, int length, FieldEncoding encoding)
    {
        return encoding switch
        {
            FieldEncoding.Bcd => DecodeBcd(data, offset, length),
            FieldEncoding.Binary => Convert.ToHexString(data, offset, length),
            _ => AsciiEncoding.GetString(data, offset, length)
        };
    }

    /// <summary>
    /// Writes a field value to bytes based on its encoding.
    /// </summary>
    private static byte[] WriteFieldValue(string value, FieldEncoding encoding)
    {
        return encoding switch
        {
            FieldEncoding.Bcd => EncodeBcd(value),
            FieldEncoding.Binary => Convert.FromHexString(value),
            _ => AsciiEncoding.GetBytes(value)
        };
    }

    /// <summary>
    /// Pads a fixed-length field value to the required length.
    /// Numeric fields are left-padded with zeros; alpha fields are right-padded with spaces.
    /// </summary>
    private static string PadFieldValue(string value, int targetLength, Iso8583FieldDefinition definition)
    {
        if (definition.Encoding == FieldEncoding.Bcd || definition.Encoding == FieldEncoding.Binary)
        {
            return value.PadLeft(definition.MaxLength, '0');
        }

        // For ASCII: numeric fields pad left with '0', alpha fields pad right with ' '
        if (value.Length >= targetLength)
        {
            return value[..targetLength];
        }

        var isNumeric = value.All(char.IsDigit);
        return isNumeric
            ? value.PadLeft(targetLength, '0')
            : value.PadRight(targetLength, ' ');
    }

    /// <summary>
    /// Decodes BCD (Binary-Coded Decimal) bytes to a numeric string.
    /// </summary>
    private static string DecodeBcd(byte[] data, int offset, int length)
    {
        var sb = new StringBuilder(length * 2);
        for (var i = 0; i < length; i++)
        {
            sb.Append((data[offset + i] >> 4).ToString("X"));
            sb.Append((data[offset + i] & 0x0F).ToString("X"));
        }
        return sb.ToString();
    }

    /// <summary>
    /// Encodes a numeric string to BCD (Binary-Coded Decimal) bytes.
    /// </summary>
    private static byte[] EncodeBcd(string value)
    {
        // Pad to even length
        if (value.Length % 2 != 0)
        {
            value = "0" + value;
        }

        var bytes = new byte[value.Length / 2];
        for (var i = 0; i < bytes.Length; i++)
        {
            var high = Convert.ToByte(value.Substring(i * 2, 1), 16);
            var low = Convert.ToByte(value.Substring(i * 2 + 1, 1), 16);
            bytes[i] = (byte)((high << 4) | low);
        }
        return bytes;
    }

    /// <summary>
    /// Checks whether a specific field bit is set in the message bitmaps.
    /// </summary>
    private static bool IsBitSet(Iso8583Message message, int fieldNumber)
    {
        if (fieldNumber < 2 || fieldNumber > 128)
        {
            return false;
        }

        if (fieldNumber <= 64)
        {
            var mask = unchecked((long)(1UL << (64 - fieldNumber)));
            return (message.PrimaryBitmap & mask) != 0;
        }
        else
        {
            var mask = unchecked((long)(1UL << (128 - fieldNumber)));
            return (message.SecondaryBitmap & mask) != 0;
        }
    }
}
