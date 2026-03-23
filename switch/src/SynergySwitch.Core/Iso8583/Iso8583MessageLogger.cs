using Microsoft.Extensions.Logging;
using NetCore8583;

namespace SynergySwitch.Core.Iso8583;

/// <summary>
/// Logs ISO 8583 messages in human-readable form with timestamps,
/// field-by-field detail, and hex dumps for troubleshooting.
/// </summary>
public class Iso8583MessageLogger
{
    private readonly ILogger<Iso8583MessageLogger> _logger;

    public Iso8583MessageLogger(ILogger<Iso8583MessageLogger> logger)
    {
        _logger = logger;
    }

    /// <summary>Log an outbound message to the bank.</summary>
    public void LogOutbound(IsoMessage message, string description = "")
    {
        var timestamp = DateTime.UtcNow;
        var msgType = $"0x{message.Type:X4}";

        _logger.LogInformation(
            "══════ ISO 8583 OUTBOUND ══════ [{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] Type={MsgType} {Description}",
            timestamp, msgType, description);

        LogFields(message, ">>>");

        // Log raw bytes for wire-level debugging
        try
        {
            var raw = message.WriteData();
            var rawBytes = (byte[])(Array)raw;
            _logger.LogDebug(">>> RAW ({Length} bytes): {Hex}",
                rawBytes.Length, Convert.ToHexString(rawBytes));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not serialize message for hex dump");
        }

        SwitchMetrics.RecordMessageSent(msgType);
    }

    /// <summary>Log an inbound message from the bank.</summary>
    public void LogInbound(IsoMessage message, double elapsedMs, string description = "")
    {
        var timestamp = DateTime.UtcNow;
        var msgType = $"0x{message.Type:X4}";

        _logger.LogInformation(
            "══════ ISO 8583 INBOUND ═══════ [{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] Type={MsgType} Elapsed={Elapsed:F1}ms {Description}",
            timestamp, msgType, elapsedMs, description);

        LogFields(message, "<<<");

        SwitchMetrics.RecordMessageReceived(msgType);
    }

    /// <summary>Log a connection event.</summary>
    public void LogConnectionEvent(string eventType, string details)
    {
        _logger.LogInformation(
            "══════ BANK CONNECTION ════════ [{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] {Event}: {Details}",
            DateTime.UtcNow, eventType, details);
    }

    /// <summary>Log a connection error.</summary>
    public void LogConnectionError(string eventType, Exception ex)
    {
        _logger.LogError(ex,
            "══════ BANK CONN ERROR ════════ [{Timestamp:yyyy-MM-dd HH:mm:ss.fff}] {Event}",
            DateTime.UtcNow, eventType);
        SwitchMetrics.RecordConnectionError(eventType);
    }

    private void LogFields(IsoMessage message, string prefix)
    {
        // Log key fields that are present
        var fieldsToLog = new[]
        {
            (2, "PAN"), (3, "Processing Code"), (4, "Amount"),
            (7, "Date/Time"), (11, "STAN"), (12, "Local Time"),
            (13, "Local Date"), (14, "Expiry"), (18, "MCC"),
            (22, "Entry Mode"), (23, "Card Seq"), (24, "Network ID"),
            (25, "POS Condition"), (32, "Acq Inst"), (35, "Track 2"),
            (37, "RRN"), (38, "Auth Code"), (39, "Response Code"),
            (41, "Terminal ID"), (42, "Merchant ID"), (43, "Name/Loc"),
            (44, "Add'l Response"), (49, "Currency"), (52, "PIN Data"),
            (55, "ICC Data"), (70, "Network Mgmt")
        };

        foreach (var (field, name) in fieldsToLog)
        {
            if (message.HasField(field))
            {
                var value = message.GetObjectValue(field);
                var display = field == 2 ? MaskPan(value?.ToString()) :
                              field == 35 ? MaskTrack2(value?.ToString()) :
                              field == 52 ? "[ENCRYPTED]" :
                              value?.ToString() ?? "(null)";

                _logger.LogInformation("{Prefix} F{Field:D3} {Name}: {Value}",
                    prefix, field, name, display);
            }
        }
    }

    private static string MaskPan(string? pan)
    {
        if (string.IsNullOrEmpty(pan) || pan.Length < 8) return "****";
        return pan[..6] + new string('*', pan.Length - 10) + pan[^4..];
    }

    private static string MaskTrack2(string? track2)
    {
        if (string.IsNullOrEmpty(track2) || track2.Length < 10) return "****";
        return track2[..6] + "****" + track2[^4..];
    }
}
