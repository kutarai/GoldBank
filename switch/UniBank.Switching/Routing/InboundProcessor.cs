using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using UniBank.SharedKernel.Results;
using UniBank.Switching.Adapters;
using UniBank.Switching.Models;

namespace UniBank.Switching.Routing;

/// <summary>
/// Processes inbound transactions received from external institutions through
/// the national payment switch. Converts incoming ISO 8583 or ISO 20022 messages
/// to canonical format, validates them, and prepares responses.
/// </summary>
public sealed class InboundProcessor
{
    private readonly MessageRouter _messageRouter;
    private readonly InstitutionRegistry _institutionRegistry;
    private readonly Iso8583Adapter _iso8583Adapter;
    private readonly Iso20022Adapter _iso20022Adapter;
    private readonly ILogger<InboundProcessor> _logger;

    /// <summary>
    /// Thread-safe log of all inbound transactions for reconciliation.
    /// </summary>
    private readonly ConcurrentDictionary<string, InboundTransactionLog> _transactionLog = new();

    public InboundProcessor(
        MessageRouter messageRouter,
        InstitutionRegistry institutionRegistry,
        Iso8583Adapter iso8583Adapter,
        Iso20022Adapter iso20022Adapter,
        ILogger<InboundProcessor> logger)
    {
        _messageRouter = messageRouter;
        _institutionRegistry = institutionRegistry;
        _iso8583Adapter = iso8583Adapter;
        _iso20022Adapter = iso20022Adapter;
        _logger = logger;
    }

    /// <summary>
    /// Processes an inbound ISO 8583 binary message from an external institution.
    /// Decodes, translates to canonical, validates, and generates a response.
    /// </summary>
    public Result<(CanonicalMessage Canonical, byte[] Response)> ProcessIso8583(byte[] rawData)
    {
        _logger.LogInformation("Processing inbound ISO 8583 message ({Length} bytes)", rawData.Length);

        // Decode the raw binary message
        var decodeResult = _iso8583Adapter.Decode(rawData);
        if (decodeResult.IsFailure)
        {
            _logger.LogWarning("Failed to decode inbound ISO 8583 message: {Error}", decodeResult.Error.Message);
            return Result.Failure<(CanonicalMessage, byte[])>(decodeResult.Error);
        }

        var inboundMessage = decodeResult.Value;

        // Translate to canonical format
        var canonicalResult = _messageRouter.FromIso8583(inboundMessage);
        if (canonicalResult.IsFailure)
        {
            return Result.Failure<(CanonicalMessage, byte[])>(canonicalResult.Error);
        }

        var canonical = canonicalResult.Value;

        // Validate the transaction
        var validationResult = ValidateInboundTransaction(canonical);
        if (validationResult.IsFailure)
        {
            // Build a decline response
            canonical.ResponseCode = "05"; // Do not honour
            var declineResult = BuildIso8583Response(canonical, "05");
            LogInboundTransaction(canonical, false, "05", validationResult.Error.Message);
            return declineResult.IsSuccess
                ? Result.Success<(CanonicalMessage, byte[])>((canonical, declineResult.Value))
                : Result.Failure<(CanonicalMessage, byte[])>(declineResult.Error);
        }

        // Process the transaction (simulate approval for the switching layer)
        canonical.ResponseCode = "00";
        canonical.AuthorizationCode = Random.Shared.Next(100000, 999999).ToString();

        // Build approval response
        var responseResult = BuildIso8583Response(canonical, "00");
        if (responseResult.IsFailure)
        {
            return Result.Failure<(CanonicalMessage, byte[])>(responseResult.Error);
        }

        LogInboundTransaction(canonical, true, "00", "Transaction approved");

        _logger.LogInformation(
            "Inbound ISO 8583 transaction {TransactionId} approved, auth code {AuthCode}",
            canonical.TransactionId, canonical.AuthorizationCode);

        return Result.Success<(CanonicalMessage, byte[])>((canonical, responseResult.Value));
    }

    /// <summary>
    /// Processes an inbound ISO 20022 XML message from an external institution.
    /// Parses, translates to canonical, validates, and generates a status response.
    /// </summary>
    public Result<(CanonicalMessage Canonical, string ResponseXml)> ProcessIso20022(string xml)
    {
        _logger.LogInformation("Processing inbound ISO 20022 message ({Length} chars)", xml.Length);

        // Parse the XML message
        var parseResult = _iso20022Adapter.Parse(xml);
        if (parseResult.IsFailure)
        {
            _logger.LogWarning("Failed to parse inbound ISO 20022 message: {Error}", parseResult.Error.Message);
            return Result.Failure<(CanonicalMessage, string)>(parseResult.Error);
        }

        var inboundMessage = parseResult.Value;

        // Translate to canonical format
        var canonicalResult = _messageRouter.FromIso20022(inboundMessage);
        if (canonicalResult.IsFailure)
        {
            return Result.Failure<(CanonicalMessage, string)>(canonicalResult.Error);
        }

        var canonical = canonicalResult.Value;

        // Validate the transaction
        var validationResult = ValidateInboundTransaction(canonical);
        if (validationResult.IsFailure)
        {
            canonical.ResponseCode = "RJCT";
            var rejectResponse = BuildIso20022StatusResponse(canonical, "RJCT", "AM04");
            LogInboundTransaction(canonical, false, "RJCT", validationResult.Error.Message);
            return rejectResponse.IsSuccess
                ? Result.Success<(CanonicalMessage, string)>((canonical, rejectResponse.Value))
                : Result.Failure<(CanonicalMessage, string)>(rejectResponse.Error);
        }

        // Approve the transaction
        canonical.ResponseCode = "ACCP";
        canonical.AuthorizationCode = Random.Shared.Next(100000, 999999).ToString();

        var responseResult = BuildIso20022StatusResponse(canonical, "ACCP", string.Empty);
        if (responseResult.IsFailure)
        {
            return Result.Failure<(CanonicalMessage, string)>(responseResult.Error);
        }

        LogInboundTransaction(canonical, true, "ACCP", "Transaction accepted");

        _logger.LogInformation(
            "Inbound ISO 20022 transaction {TransactionId} accepted",
            canonical.TransactionId);

        return Result.Success<(CanonicalMessage, string)>((canonical, responseResult.Value));
    }

    /// <summary>
    /// Gets all inbound transaction logs for reconciliation.
    /// </summary>
    public IReadOnlyDictionary<string, InboundTransactionLog> GetTransactionLog()
    {
        return _transactionLog;
    }

    /// <summary>
    /// Gets inbound log entries filtered by date.
    /// </summary>
    public IEnumerable<InboundTransactionLog> GetTransactionsByDate(DateTime date)
    {
        return _transactionLog.Values
            .Where(t => t.Timestamp.Date == date.Date)
            .OrderBy(t => t.Timestamp);
    }

    /// <summary>
    /// Validates an inbound transaction against basic business rules.
    /// </summary>
    private Result ValidateInboundTransaction(CanonicalMessage canonical)
    {
        if (canonical.Amount <= 0)
        {
            return Result.Failure(new Error("Inbound.InvalidAmount", "Transaction amount must be positive."));
        }

        if (string.IsNullOrEmpty(canonical.Currency))
        {
            return Result.Failure(new Error("Inbound.NoCurrency", "Currency code is required."));
        }

        if (string.IsNullOrEmpty(canonical.DebitAccount) && string.IsNullOrEmpty(canonical.CreditAccount))
        {
            return Result.Failure(new Error("Inbound.NoAccount", "At least one account must be specified."));
        }

        // Verify the destination institution is registered
        if (!string.IsNullOrEmpty(canonical.DestinationInstitution) &&
            !_institutionRegistry.Contains(canonical.DestinationInstitution))
        {
            // Also try resolving by credit account prefix
            if (string.IsNullOrEmpty(canonical.CreditAccount) ||
                _institutionRegistry.ResolveByAccountPrefix(canonical.CreditAccount) is null)
            {
                return Result.Failure(new Error("Inbound.UnknownDestination",
                    $"Destination institution '{canonical.DestinationInstitution}' is not registered."));
            }
        }

        // Amount limit check (configurable in production, hardcoded for now)
        const decimal maxAmount = 10_000_000m;
        if (canonical.Amount > maxAmount)
        {
            return Result.Failure(new Error("Inbound.AmountExceeded",
                $"Transaction amount {canonical.Amount} exceeds the maximum allowed {maxAmount}."));
        }

        return Result.Success();
    }

    /// <summary>
    /// Builds an ISO 8583 response message for the inbound transaction.
    /// </summary>
    private Result<byte[]> BuildIso8583Response(CanonicalMessage canonical, string responseCode)
    {
        // Set message type to response
        canonical.ResponseCode = responseCode;
        canonical.MessageType = canonical.MessageType switch
        {
            CanonicalMessageType.FinancialRequest => CanonicalMessageType.FinancialResponse,
            CanonicalMessageType.AuthorizationRequest => CanonicalMessageType.AuthorizationResponse,
            CanonicalMessageType.ReversalRequest => CanonicalMessageType.ReversalResponse,
            _ => CanonicalMessageType.FinancialResponse
        };

        var msgResult = _messageRouter.ToIso8583(canonical);
        if (msgResult.IsFailure)
        {
            return Result.Failure<byte[]>(msgResult.Error);
        }

        return _iso8583Adapter.Encode(msgResult.Value);
    }

    /// <summary>
    /// Builds an ISO 20022 pacs.002 status response for the inbound transaction.
    /// </summary>
    private Result<string> BuildIso20022StatusResponse(
        CanonicalMessage canonical, string statusCode, string reasonCode)
    {
        var statusMessage = new Iso20022Message
        {
            MessageType = Iso20022MessageType.Pacs002,
            MessageId = $"RSP-{canonical.TransactionId}",
            CreationDateTime = DateTime.UtcNow,
            SendingInstitution = canonical.DestinationInstitution,
            ReceivingInstitution = canonical.SourceInstitution,
            TransactionId = canonical.TransactionId,
            EndToEndId = canonical.RetrievalReference,
            StatusCode = statusCode,
            ReasonCode = reasonCode
        };

        return _iso20022Adapter.Generate(statusMessage);
    }

    private void LogInboundTransaction(
        CanonicalMessage canonical, bool success, string responseCode, string message)
    {
        var log = new InboundTransactionLog
        {
            TransactionId = canonical.TransactionId,
            SourceInstitution = canonical.SourceInstitution,
            DestinationInstitution = canonical.DestinationInstitution,
            Amount = canonical.Amount,
            Currency = canonical.Currency,
            ResponseCode = responseCode,
            Success = success,
            Message = message,
            Timestamp = DateTime.UtcNow
        };

        _transactionLog.TryAdd(canonical.TransactionId, log);
    }
}

/// <summary>
/// Inbound transaction log entry for reconciliation.
/// </summary>
public sealed class InboundTransactionLog
{
    public string TransactionId { get; set; } = string.Empty;
    public string SourceInstitution { get; set; } = string.Empty;
    public string DestinationInstitution { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string ResponseCode { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}
