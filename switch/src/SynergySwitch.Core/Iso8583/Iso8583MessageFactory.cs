using NetCore8583;
using NetCore8583.Parse;
using System.Text;

namespace SynergySwitch.Core.Iso8583;

/// <summary>
/// Configures the Netcore8583 MessageFactory with the Zimswitch ISO 8587 ASCII field definitions.
/// </summary>
public static class Iso8583MessageFactory
{
    /// <summary>
    /// Create a MessageFactory configured for Zimswitch ISO 8583:1987 ASCII.
    /// </summary>
    public static MessageFactory<IsoMessage> Create()
    {
        var factory = new MessageFactory<IsoMessage>
        {
            Encoding = Encoding.ASCII,
            UseBinaryMessages = false
        };

        // Configure parse guides for 0210 (Financial Transaction Response)
        var parseMap0210 = new Dictionary<int, FieldParseInfo>
        {
            [2]  = FieldParseInfo.GetInstance(IsoType.LLVAR,   19, Encoding.ASCII),   // PAN
            [3]  = FieldParseInfo.GetInstance(IsoType.NUMERIC,  6, Encoding.ASCII),   // Processing code
            [4]  = FieldParseInfo.GetInstance(IsoType.NUMERIC, 12, Encoding.ASCII),   // Amount
            [7]  = FieldParseInfo.GetInstance(IsoType.NUMERIC, 10, Encoding.ASCII),   // Transmission date/time
            [11] = FieldParseInfo.GetInstance(IsoType.NUMERIC,  6, Encoding.ASCII),   // STAN
            [12] = FieldParseInfo.GetInstance(IsoType.NUMERIC,  6, Encoding.ASCII),   // Local time
            [13] = FieldParseInfo.GetInstance(IsoType.NUMERIC,  4, Encoding.ASCII),   // Local date
            [14] = FieldParseInfo.GetInstance(IsoType.NUMERIC,  4, Encoding.ASCII),   // Expiry date
            [15] = FieldParseInfo.GetInstance(IsoType.NUMERIC,  4, Encoding.ASCII),   // Settlement date
            [18] = FieldParseInfo.GetInstance(IsoType.NUMERIC,  4, Encoding.ASCII),   // MCC
            [22] = FieldParseInfo.GetInstance(IsoType.NUMERIC,  3, Encoding.ASCII),   // POS entry mode
            [23] = FieldParseInfo.GetInstance(IsoType.NUMERIC,  3, Encoding.ASCII),   // Card sequence number
            [24] = FieldParseInfo.GetInstance(IsoType.NUMERIC,  3, Encoding.ASCII),   // Network int'l ID
            [25] = FieldParseInfo.GetInstance(IsoType.NUMERIC,  2, Encoding.ASCII),   // POS condition code
            [26] = FieldParseInfo.GetInstance(IsoType.NUMERIC,  2, Encoding.ASCII),   // PIN capture code
            [32] = FieldParseInfo.GetInstance(IsoType.LLVAR,   11, Encoding.ASCII),   // Acquiring inst ID
            [35] = FieldParseInfo.GetInstance(IsoType.LLVAR,   37, Encoding.ASCII),   // Track 2
            [37] = FieldParseInfo.GetInstance(IsoType.ALPHA,   12, Encoding.ASCII),   // RRN
            [38] = FieldParseInfo.GetInstance(IsoType.ALPHA,    6, Encoding.ASCII),   // Auth ID response
            [39] = FieldParseInfo.GetInstance(IsoType.ALPHA,    2, Encoding.ASCII),   // Response code
            [40] = FieldParseInfo.GetInstance(IsoType.ALPHA,    3, Encoding.ASCII),   // Service restriction
            [41] = FieldParseInfo.GetInstance(IsoType.ALPHA,    8, Encoding.ASCII),   // Terminal ID
            [42] = FieldParseInfo.GetInstance(IsoType.ALPHA,   15, Encoding.ASCII),   // Card acceptor ID
            [43] = FieldParseInfo.GetInstance(IsoType.ALPHA,   40, Encoding.ASCII),   // Name/location
            [44] = FieldParseInfo.GetInstance(IsoType.LLVAR,   25, Encoding.ASCII),   // Additional response
            [49] = FieldParseInfo.GetInstance(IsoType.ALPHA,    3, Encoding.ASCII),   // Currency code
            [54] = FieldParseInfo.GetInstance(IsoType.LLLVAR, 120, Encoding.ASCII),   // Additional amounts
            [55] = FieldParseInfo.GetInstance(IsoType.LLLVAR, 999, Encoding.ASCII),   // ICC data (EMV)
        };

        factory.SetParseMap(0x0210, parseMap0210);

        // Configure parse guide for 0810 (Network Management Response)
        var parseMap0810 = new Dictionary<int, FieldParseInfo>
        {
            [7]  = FieldParseInfo.GetInstance(IsoType.NUMERIC, 10, Encoding.ASCII),
            [11] = FieldParseInfo.GetInstance(IsoType.NUMERIC,  6, Encoding.ASCII),
            [39] = FieldParseInfo.GetInstance(IsoType.ALPHA,    2, Encoding.ASCII),
            [70] = FieldParseInfo.GetInstance(IsoType.NUMERIC,  3, Encoding.ASCII),
        };

        factory.SetParseMap(0x0810, parseMap0810);

        return factory;
    }
}
