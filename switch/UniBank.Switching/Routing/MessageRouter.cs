using System.Globalization;
using Microsoft.Extensions.Logging;
using UniBank.SharedKernel.Results;
using UniBank.Switching.Adapters;
using UniBank.Switching.Models;

namespace UniBank.Switching.Routing;

/// <summary>
/// Routes messages between ISO 8583 and ISO 20022 via the internal canonical format.
/// Translates inbound messages to canonical, determines the destination institution's
/// preferred protocol, and translates the canonical message to the outbound format.
/// </summary>
public sealed class MessageRouter
{
    private readonly Iso8583Adapter _iso8583Adapter;
    private readonly Iso20022Adapter _iso20022Adapter;
    private readonly InstitutionRegistry _institutionRegistry;
    private readonly ILogger<MessageRouter> _logger;

    public MessageRouter(
        Iso8583Adapter iso8583Adapter,
        Iso20022Adapter iso20022Adapter,
        InstitutionRegistry institutionRegistry,
        ILogger<MessageRouter> logger)
    {
        _iso8583Adapter = iso8583Adapter;
        _iso20022Adapter = iso20022Adapter;
        _institutionRegistry = institutionRegistry;
        _logger = logger;
    }

    #region ISO 8583 <-> Canonical

    /// <summary>
    /// Translates an ISO 8583 message to the canonical format.
    /// </summary>
    public Result<CanonicalMessage> FromIso8583(Iso8583Message message)
    {
        ArgumentNullException.ThrowIfNull(message);

        try
        {
            var canonical = new CanonicalMessage
            {
                MessageType = MapMtiToCanonicalType(message.Mti),
                Timestamp = DateTime.UtcNow
            };

            // DE2 - Primary Account Number (used as debit account)
            if (message.HasField(2))
            {
                canonical.DebitAccount = message.GetField(2)!;
            }

            // DE3 - Processing Code
            if (message.HasField(3))
            {
                canonical.ProcessingCode = message.GetField(3)!;
            }

            // DE4 - Transaction Amount (12 digits, last 2 are decimal)
            if (message.HasField(4))
            {
                var amountStr = message.GetField(4)!.TrimStart('0');
                if (amountStr.Length == 0) amountStr = "0";
                if (decimal.TryParse(amountStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var amount))
                {
                    canonical.Amount = amount / 100m; // Last 2 digits are cents
                }
            }

            // DE7 - Transmission Date and Time (MMDDhhmmss)
            if (message.HasField(7))
            {
                var dt = message.GetField(7)!;
                if (dt.Length == 10)
                {
                    var now = DateTime.UtcNow;
                    if (int.TryParse(dt[..2], out var month) &&
                        int.TryParse(dt[2..4], out var day) &&
                        int.TryParse(dt[4..6], out var hour) &&
                        int.TryParse(dt[6..8], out var minute) &&
                        int.TryParse(dt[8..10], out var second))
                    {
                        try
                        {
                            canonical.Timestamp = new DateTime(now.Year, month, day, hour, minute, second, DateTimeKind.Utc);
                        }
                        catch (ArgumentOutOfRangeException)
                        {
                            canonical.Timestamp = DateTime.UtcNow;
                        }
                    }
                }
            }

            // DE11 - STAN
            if (message.HasField(11))
            {
                canonical.Stan = message.GetField(11)!;
            }

            // DE32 - Acquiring Institution (source)
            if (message.HasField(32))
            {
                canonical.SourceInstitution = message.GetField(32)!;
            }

            // DE37 - Retrieval Reference Number
            if (message.HasField(37))
            {
                canonical.RetrievalReference = message.GetField(37)!;
                canonical.Reference = message.GetField(37)!;
            }

            // DE38 - Authorization Code
            if (message.HasField(38))
            {
                canonical.AuthorizationCode = message.GetField(38)!;
            }

            // DE39 - Response Code
            if (message.HasField(39))
            {
                canonical.ResponseCode = message.GetField(39)!;
            }

            // DE41 - Terminal ID
            if (message.HasField(41))
            {
                canonical.TerminalId = message.GetField(41)!;
            }

            // DE42 - Merchant ID
            if (message.HasField(42))
            {
                canonical.MerchantId = message.GetField(42)!;
            }

            // DE43 - Merchant Name
            if (message.HasField(43))
            {
                canonical.MerchantName = message.GetField(43)!.Trim();
            }

            // DE49 - Currency Code
            if (message.HasField(49))
            {
                canonical.Currency = message.GetField(49)!;
            }

            // Resolve destination from the credit account prefix or additional data
            if (message.HasField(48))
            {
                canonical.CreditAccount = message.GetField(48)!;
                var dest = _institutionRegistry.ResolveByAccountPrefix(canonical.CreditAccount);
                if (dest is not null)
                {
                    canonical.DestinationInstitution = dest.InstitutionId;
                }
            }

            // Map remaining fields into AdditionalData
            int[] mappedFields = [2, 3, 4, 7, 11, 12, 13, 32, 37, 38, 39, 41, 42, 43, 48, 49];
            foreach (var (field, value) in message.DataElements)
            {
                if (!mappedFields.Contains(field))
                {
                    canonical.AdditionalData[$"DE{field}"] = value;
                }
            }

            _logger.LogInformation(
                "Translated ISO 8583 MTI {Mti} to canonical message {TransactionId}",
                message.Mti, canonical.TransactionId);

            return Result.Success(canonical);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to translate ISO 8583 to canonical");
            return Result.Failure<CanonicalMessage>(
                new Error("Router.Iso8583ToCanonical", $"Translation failed: {ex.Message}"));
        }
    }

    /// <summary>
    /// Translates a canonical message back to ISO 8583 format.
    /// </summary>
    public Result<Iso8583Message> ToIso8583(CanonicalMessage canonical)
    {
        ArgumentNullException.ThrowIfNull(canonical);

        try
        {
            var message = new Iso8583Message
            {
                Mti = MapCanonicalTypeToMti(canonical.MessageType)
            };

            if (!string.IsNullOrEmpty(canonical.DebitAccount))
            {
                message.SetField(2, canonical.DebitAccount);
            }

            if (!string.IsNullOrEmpty(canonical.ProcessingCode))
            {
                message.SetField(3, canonical.ProcessingCode);
            }

            // Amount: multiply by 100 and format as 12-digit string
            var amountCents = (long)(canonical.Amount * 100m);
            message.SetField(4, amountCents.ToString("D12"));

            // Transmission date/time
            message.SetField(7, canonical.Timestamp.ToString("MMddHHmmss"));

            if (!string.IsNullOrEmpty(canonical.Stan))
            {
                message.SetField(11, canonical.Stan);
            }

            // Local time and date
            message.SetField(12, canonical.Timestamp.ToString("HHmmss"));
            message.SetField(13, canonical.Timestamp.ToString("MMdd"));

            if (!string.IsNullOrEmpty(canonical.SourceInstitution))
            {
                message.SetField(32, canonical.SourceInstitution);
            }

            if (!string.IsNullOrEmpty(canonical.RetrievalReference))
            {
                message.SetField(37, canonical.RetrievalReference);
            }

            if (!string.IsNullOrEmpty(canonical.AuthorizationCode))
            {
                message.SetField(38, canonical.AuthorizationCode);
            }

            if (!string.IsNullOrEmpty(canonical.ResponseCode))
            {
                message.SetField(39, canonical.ResponseCode);
            }

            if (!string.IsNullOrEmpty(canonical.TerminalId))
            {
                message.SetField(41, canonical.TerminalId);
            }

            if (!string.IsNullOrEmpty(canonical.MerchantId))
            {
                message.SetField(42, canonical.MerchantId);
            }

            if (!string.IsNullOrEmpty(canonical.MerchantName))
            {
                message.SetField(43, canonical.MerchantName);
            }

            if (!string.IsNullOrEmpty(canonical.CreditAccount))
            {
                message.SetField(48, canonical.CreditAccount);
            }

            if (!string.IsNullOrEmpty(canonical.Currency))
            {
                message.SetField(49, canonical.Currency);
            }

            // Restore additional data elements
            foreach (var (key, value) in canonical.AdditionalData)
            {
                if (key.StartsWith("DE", StringComparison.Ordinal) &&
                    int.TryParse(key[2..], out var fieldNum) &&
                    fieldNum >= 2 && fieldNum <= 128)
                {
                    if (!message.HasField(fieldNum))
                    {
                        message.SetField(fieldNum, value);
                    }
                }
            }

            _logger.LogInformation(
                "Translated canonical message {TransactionId} to ISO 8583 MTI {Mti}",
                canonical.TransactionId, message.Mti);

            return Result.Success(message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to translate canonical to ISO 8583");
            return Result.Failure<Iso8583Message>(
                new Error("Router.CanonicalToIso8583", $"Translation failed: {ex.Message}"));
        }
    }

    #endregion

    #region ISO 20022 <-> Canonical

    /// <summary>
    /// Translates an ISO 20022 message to the canonical format.
    /// </summary>
    public Result<CanonicalMessage> FromIso20022(Iso20022Message message)
    {
        ArgumentNullException.ThrowIfNull(message);

        try
        {
            var canonical = new CanonicalMessage
            {
                TransactionId = !string.IsNullOrEmpty(message.TransactionId)
                    ? message.TransactionId
                    : Guid.NewGuid().ToString("N"),
                MessageType = MapIso20022TypeToCanonical(message.MessageType, message.StatusCode),
                SourceInstitution = message.SendingInstitution,
                DestinationInstitution = message.ReceivingInstitution,
                Amount = message.Amount,
                Currency = message.Currency,
                DebitAccount = message.DebtorAccount,
                CreditAccount = message.CreditorAccount,
                Reference = message.RemittanceInformation,
                RetrievalReference = message.EndToEndId,
                Timestamp = message.CreationDateTime,
                ResponseCode = message.StatusCode,
                MerchantName = message.CreditorName
            };

            // If destination institution is empty, try to resolve from creditor agent
            if (string.IsNullOrEmpty(canonical.DestinationInstitution) &&
                !string.IsNullOrEmpty(message.CreditorAgent))
            {
                var dest = _institutionRegistry.GetById(message.CreditorAgent);
                if (dest is not null)
                {
                    canonical.DestinationInstitution = dest.InstitutionId;
                }
            }

            _logger.LogInformation(
                "Translated ISO 20022 {MessageType} to canonical message {TransactionId}",
                message.MessageType, canonical.TransactionId);

            return Result.Success(canonical);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to translate ISO 20022 to canonical");
            return Result.Failure<CanonicalMessage>(
                new Error("Router.Iso20022ToCanonical", $"Translation failed: {ex.Message}"));
        }
    }

    /// <summary>
    /// Translates a canonical message to ISO 20022 format.
    /// </summary>
    public Result<Iso20022Message> ToIso20022(CanonicalMessage canonical)
    {
        ArgumentNullException.ThrowIfNull(canonical);

        try
        {
            var messageType = MapCanonicalToIso20022Type(canonical.MessageType);

            var message = new Iso20022Message
            {
                MessageType = messageType,
                MessageId = canonical.TransactionId,
                CreationDateTime = canonical.Timestamp,
                SendingInstitution = canonical.SourceInstitution,
                ReceivingInstitution = canonical.DestinationInstitution,
                EndToEndId = !string.IsNullOrEmpty(canonical.RetrievalReference)
                    ? canonical.RetrievalReference
                    : canonical.TransactionId,
                TransactionId = canonical.TransactionId,
                Amount = canonical.Amount,
                Currency = canonical.Currency,
                DebtorAccount = canonical.DebitAccount,
                DebtorName = canonical.MerchantName,
                DebtorAgent = canonical.SourceInstitution,
                CreditorAccount = canonical.CreditAccount,
                CreditorName = !string.IsNullOrEmpty(canonical.MerchantName)
                    ? canonical.MerchantName
                    : "Beneficiary",
                CreditorAgent = canonical.DestinationInstitution,
                RemittanceInformation = canonical.Reference,
                StatusCode = canonical.ResponseCode
            };

            _logger.LogInformation(
                "Translated canonical message {TransactionId} to ISO 20022 {MessageType}",
                canonical.TransactionId, messageType);

            return Result.Success(message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to translate canonical to ISO 20022");
            return Result.Failure<Iso20022Message>(
                new Error("Router.CanonicalToIso20022", $"Translation failed: {ex.Message}"));
        }
    }

    #endregion

    #region Routing

    /// <summary>
    /// Routes a canonical message to the destination institution by converting it to
    /// the institution's preferred protocol format. Returns the encoded bytes ready
    /// for transmission along with the target endpoint.
    /// </summary>
    public Result<(byte[] Payload, string Endpoint, SwitchProtocol Protocol)> Route(CanonicalMessage canonical)
    {
        ArgumentNullException.ThrowIfNull(canonical);

        // Resolve destination institution
        var institution = _institutionRegistry.GetById(canonical.DestinationInstitution);
        if (institution is null && !string.IsNullOrEmpty(canonical.CreditAccount))
        {
            institution = _institutionRegistry.ResolveByAccountPrefix(canonical.CreditAccount);
        }

        if (institution is null)
        {
            _logger.LogWarning(
                "No institution found for destination {Destination} or account {Account}",
                canonical.DestinationInstitution, canonical.CreditAccount);

            return Result.Failure<(byte[], string, SwitchProtocol)>(
                new Error("Router.NoDestination",
                    $"Unable to resolve destination institution for '{canonical.DestinationInstitution}'."));
        }

        canonical.DestinationInstitution = institution.InstitutionId;

        switch (institution.Protocol)
        {
            case SwitchProtocol.Iso8583:
            {
                var msgResult = ToIso8583(canonical);
                if (msgResult.IsFailure)
                {
                    return Result.Failure<(byte[], string, SwitchProtocol)>(msgResult.Error);
                }

                var encodeResult = _iso8583Adapter.Encode(msgResult.Value);
                if (encodeResult.IsFailure)
                {
                    return Result.Failure<(byte[], string, SwitchProtocol)>(encodeResult.Error);
                }

                _logger.LogInformation(
                    "Routed transaction {TxId} to {Institution} via ISO 8583 at {Endpoint}",
                    canonical.TransactionId, institution.InstitutionId, institution.Endpoint);

                return Result.Success<(byte[], string, SwitchProtocol)>(
                    (encodeResult.Value, institution.Endpoint, SwitchProtocol.Iso8583));
            }

            case SwitchProtocol.Iso20022:
            {
                var msgResult = ToIso20022(canonical);
                if (msgResult.IsFailure)
                {
                    return Result.Failure<(byte[], string, SwitchProtocol)>(msgResult.Error);
                }

                var xmlResult = _iso20022Adapter.Generate(msgResult.Value);
                if (xmlResult.IsFailure)
                {
                    return Result.Failure<(byte[], string, SwitchProtocol)>(xmlResult.Error);
                }

                var payload = System.Text.Encoding.UTF8.GetBytes(xmlResult.Value);

                _logger.LogInformation(
                    "Routed transaction {TxId} to {Institution} via ISO 20022 at {Endpoint}",
                    canonical.TransactionId, institution.InstitutionId, institution.Endpoint);

                return Result.Success<(byte[], string, SwitchProtocol)>(
                    (payload, institution.Endpoint, SwitchProtocol.Iso20022));
            }

            default:
                return Result.Failure<(byte[], string, SwitchProtocol)>(
                    new Error("Router.UnsupportedProtocol",
                        $"Protocol {institution.Protocol} is not supported."));
        }
    }

    #endregion

    #region Mapping Helpers

    private static CanonicalMessageType MapMtiToCanonicalType(string mti)
    {
        return mti switch
        {
            "0100" => CanonicalMessageType.AuthorizationRequest,
            "0110" => CanonicalMessageType.AuthorizationResponse,
            "0200" => CanonicalMessageType.FinancialRequest,
            "0210" => CanonicalMessageType.FinancialResponse,
            "0400" => CanonicalMessageType.ReversalRequest,
            "0410" => CanonicalMessageType.ReversalResponse,
            _ => CanonicalMessageType.FinancialRequest
        };
    }

    private static string MapCanonicalTypeToMti(CanonicalMessageType type)
    {
        return type switch
        {
            CanonicalMessageType.AuthorizationRequest => "0100",
            CanonicalMessageType.AuthorizationResponse => "0110",
            CanonicalMessageType.FinancialRequest => "0200",
            CanonicalMessageType.FinancialResponse => "0210",
            CanonicalMessageType.ReversalRequest => "0400",
            CanonicalMessageType.ReversalResponse => "0410",
            CanonicalMessageType.StatusReport => "0210",
            _ => "0200"
        };
    }

    private static CanonicalMessageType MapIso20022TypeToCanonical(
        Iso20022MessageType type, string statusCode)
    {
        return type switch
        {
            Iso20022MessageType.Pacs008 => CanonicalMessageType.FinancialRequest,
            Iso20022MessageType.Pain001 => CanonicalMessageType.FinancialRequest,
            Iso20022MessageType.Pacs002 => string.IsNullOrEmpty(statusCode)
                ? CanonicalMessageType.StatusReport
                : CanonicalMessageType.FinancialResponse,
            _ => CanonicalMessageType.FinancialRequest
        };
    }

    private static Iso20022MessageType MapCanonicalToIso20022Type(CanonicalMessageType type)
    {
        return type switch
        {
            CanonicalMessageType.FinancialResponse or
            CanonicalMessageType.AuthorizationResponse or
            CanonicalMessageType.ReversalResponse or
            CanonicalMessageType.StatusReport => Iso20022MessageType.Pacs002,
            _ => Iso20022MessageType.Pacs008
        };
    }

    #endregion
}
