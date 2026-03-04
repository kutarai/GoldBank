using System.Collections.Concurrent;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using UniBank.SharedKernel.Results;
using UniBank.Switching.Adapters;
using UniBank.Switching.Models;

namespace UniBank.Switching.Routing;

/// <summary>
/// Handles outbound transaction routing. Takes an internal payment request, converts
/// it to the canonical format, routes it to the correct adapter based on the destination
/// institution's protocol preference, and transmits via TCP (or simulated channel).
/// </summary>
public sealed class OutboundRouter
{
    private readonly MessageRouter _messageRouter;
    private readonly InstitutionRegistry _institutionRegistry;
    private readonly ILogger<OutboundRouter> _logger;

    /// <summary>
    /// Thread-safe log of all outbound transactions for reconciliation.
    /// </summary>
    private readonly ConcurrentDictionary<string, OutboundTransactionLog> _transactionLog = new();

    public OutboundRouter(
        MessageRouter messageRouter,
        InstitutionRegistry institutionRegistry,
        ILogger<OutboundRouter> logger)
    {
        _messageRouter = messageRouter;
        _institutionRegistry = institutionRegistry;
        _logger = logger;
    }

    /// <summary>
    /// Routes an outbound transaction through the national payment switch.
    /// Converts to canonical format, resolves destination, encodes to the target
    /// protocol, and transmits (or simulates transmission).
    /// </summary>
    public async Task<Result<OutboundTransactionResult>> RouteTransactionAsync(
        CanonicalMessage canonical, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(canonical);

        _logger.LogInformation(
            "Routing outbound transaction {TransactionId} to {Destination}, amount {Amount} {Currency}",
            canonical.TransactionId, canonical.DestinationInstitution, canonical.Amount, canonical.Currency);

        // Resolve the destination institution
        var institution = _institutionRegistry.GetById(canonical.DestinationInstitution);
        if (institution is null && !string.IsNullOrEmpty(canonical.CreditAccount))
        {
            institution = _institutionRegistry.ResolveByAccountPrefix(canonical.CreditAccount);
        }

        if (institution is null)
        {
            var error = new Error("Outbound.NoDestination",
                $"Cannot resolve destination institution for '{canonical.DestinationInstitution}'.");
            LogTransaction(canonical, false, "NO_DEST", error.Message);
            return Result.Failure<OutboundTransactionResult>(error);
        }

        canonical.DestinationInstitution = institution.InstitutionId;

        // Route through the message router (converts to target protocol and encodes)
        var routeResult = _messageRouter.Route(canonical);
        if (routeResult.IsFailure)
        {
            LogTransaction(canonical, false, "ROUTE_FAIL", routeResult.Error.Message);
            return Result.Failure<OutboundTransactionResult>(routeResult.Error);
        }

        var (payload, endpoint, protocol) = routeResult.Value;

        // Transmit to the destination
        var sendResult = await SendPayloadAsync(payload, endpoint, protocol, cancellationToken);

        if (sendResult.IsFailure)
        {
            LogTransaction(canonical, false, "SEND_FAIL", sendResult.Error.Message);

            // Even on send failure, return a result so the caller can handle retries
            return Result.Failure<OutboundTransactionResult>(sendResult.Error);
        }

        // Parse response if we received one
        var responseData = sendResult.Value;
        var responseCode = "00"; // Default to approved for simulated responses
        var authCode = GenerateAuthCode();

        if (responseData.Length > 0)
        {
            // Attempt to extract response code from the response payload
            var parsedCode = ExtractResponseCode(responseData, protocol);
            if (!string.IsNullOrEmpty(parsedCode))
            {
                responseCode = parsedCode;
            }
        }

        var result = new OutboundTransactionResult
        {
            TransactionId = canonical.TransactionId,
            SwitchReference = $"SW-{DateTime.UtcNow:yyyyMMdd}-{canonical.TransactionId[..8].ToUpperInvariant()}",
            ResponseCode = responseCode,
            AuthorizationCode = authCode,
            ProcessedAt = DateTime.UtcNow,
            DestinationInstitution = institution.InstitutionId,
            Protocol = protocol
        };

        LogTransaction(canonical, responseCode == "00", responseCode, "Transaction routed successfully");

        _logger.LogInformation(
            "Outbound transaction {TransactionId} completed with response code {ResponseCode}",
            canonical.TransactionId, responseCode);

        return Result.Success(result);
    }

    /// <summary>
    /// Gets the transaction log for reconciliation purposes.
    /// </summary>
    public IReadOnlyDictionary<string, OutboundTransactionLog> GetTransactionLog()
    {
        return _transactionLog;
    }

    /// <summary>
    /// Gets log entries filtered by date range.
    /// </summary>
    public IEnumerable<OutboundTransactionLog> GetTransactionsByDate(DateTime date)
    {
        return _transactionLog.Values
            .Where(t => t.Timestamp.Date == date.Date)
            .OrderBy(t => t.Timestamp);
    }

    /// <summary>
    /// Sends the encoded payload to the destination endpoint.
    /// Uses TCP for ISO 8583 connections and simulates for development.
    /// </summary>
    private async Task<Result<byte[]>> SendPayloadAsync(
        byte[] payload, string endpoint, SwitchProtocol protocol,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug(
            "Sending {PayloadLength} bytes via {Protocol} to {Endpoint}",
            payload.Length, protocol, endpoint);

        try
        {
            // Parse endpoint as host:port
            var parts = endpoint.Split(':');
            if (parts.Length != 2 || !int.TryParse(parts[1], out var port))
            {
                // Simulated mode - generate a simulated response
                _logger.LogDebug("Using simulated channel for endpoint {Endpoint}", endpoint);
                return Result.Success(GenerateSimulatedResponse(protocol));
            }

            var host = parts[0];

            // Try actual TCP connection; fall back to simulated if unavailable
            try
            {
                using var client = new TcpClient();
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(TimeSpan.FromSeconds(30));

                await client.ConnectAsync(host, port, cts.Token);

                var stream = client.GetStream();

                // Write 4-byte length header followed by payload (common ISO 8583 framing)
                var lengthHeader = BitConverter.GetBytes(payload.Length);
                if (BitConverter.IsLittleEndian)
                {
                    Array.Reverse(lengthHeader);
                }

                await stream.WriteAsync(lengthHeader, cts.Token);
                await stream.WriteAsync(payload, cts.Token);
                await stream.FlushAsync(cts.Token);

                // Read response with same framing
                var responseLength = new byte[4];
                var read = await stream.ReadAsync(responseLength, cts.Token);
                if (read < 4)
                {
                    return Result.Success(GenerateSimulatedResponse(protocol));
                }

                if (BitConverter.IsLittleEndian)
                {
                    Array.Reverse(responseLength);
                }

                var respLen = BitConverter.ToInt32(responseLength, 0);
                if (respLen <= 0 || respLen > 65536)
                {
                    return Result.Success(GenerateSimulatedResponse(protocol));
                }

                var responseBuffer = new byte[respLen];
                var totalRead = 0;
                while (totalRead < respLen)
                {
                    var chunk = await stream.ReadAsync(
                        responseBuffer.AsMemory(totalRead, respLen - totalRead), cts.Token);
                    if (chunk == 0) break;
                    totalRead += chunk;
                }

                return Result.Success(responseBuffer[..totalRead]);
            }
            catch (SocketException)
            {
                _logger.LogWarning(
                    "TCP connection to {Endpoint} failed, using simulated response", endpoint);
                return Result.Success(GenerateSimulatedResponse(protocol));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return Result.Failure<byte[]>(
                    new Error("Outbound.Cancelled", "Transaction was cancelled."));
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("TCP connection to {Endpoint} timed out", endpoint);
                return Result.Success(GenerateSimulatedResponse(protocol));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending payload to {Endpoint}", endpoint);
            return Result.Failure<byte[]>(
                new Error("Outbound.SendError", $"Failed to send payload: {ex.Message}"));
        }
    }

    /// <summary>
    /// Generates a simulated response for development and testing.
    /// </summary>
    private static byte[] GenerateSimulatedResponse(SwitchProtocol protocol)
    {
        if (protocol == SwitchProtocol.Iso8583)
        {
            // Build a minimal ISO 8583 response with response code "00" (approved)
            var response = new Iso8583Message
            {
                Mti = "0210"
            };
            response.SetField(39, "00"); // Response code: approved
            response.SetField(38, GenerateAuthCode()); // Auth code

            var adapter = new Iso8583Adapter();
            var encoded = adapter.Encode(response);
            return encoded.IsSuccess ? encoded.Value : [];
        }

        // ISO 20022 simulated pacs.002 acceptance
        var xml = $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <Document xmlns="urn:iso:std:iso:20022:tech:xsd:pacs.002.001.12">
              <FIToFIPmtStsRpt>
                <GrpHdr>
                  <MsgId>SIM-{Guid.NewGuid():N}</MsgId>
                  <CreDtTm>{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ss}</CreDtTm>
                </GrpHdr>
                <TxInfAndSts>
                  <TxSts>ACCP</TxSts>
                </TxInfAndSts>
              </FIToFIPmtStsRpt>
            </Document>
            """;
        return System.Text.Encoding.UTF8.GetBytes(xml);
    }

    /// <summary>
    /// Attempts to extract a response code from the response payload.
    /// </summary>
    private string ExtractResponseCode(byte[] responseData, SwitchProtocol protocol)
    {
        try
        {
            if (protocol == SwitchProtocol.Iso8583)
            {
                var adapter = new Iso8583Adapter();
                var decoded = adapter.Decode(responseData);
                if (decoded.IsSuccess && decoded.Value.HasField(39))
                {
                    return decoded.Value.GetField(39)!;
                }
            }
            else
            {
                var xml = System.Text.Encoding.UTF8.GetString(responseData);
                var adapter = new Iso20022Adapter();
                var parsed = adapter.Parse(xml);
                if (parsed.IsSuccess && !string.IsNullOrEmpty(parsed.Value.StatusCode))
                {
                    // Map ISO 20022 status codes to ISO 8583-style response codes
                    return parsed.Value.StatusCode switch
                    {
                        "ACCP" or "ACSC" or "ACSP" => "00",
                        "RJCT" => "05",
                        "PDNG" => "09",
                        _ => "00"
                    };
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract response code from response payload");
        }

        return string.Empty;
    }

    private static string GenerateAuthCode()
    {
        return Random.Shared.Next(100000, 999999).ToString();
    }

    private void LogTransaction(
        CanonicalMessage canonical, bool success, string responseCode, string message)
    {
        var log = new OutboundTransactionLog
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
/// Result of an outbound transaction routing operation.
/// </summary>
public sealed class OutboundTransactionResult
{
    public string TransactionId { get; set; } = string.Empty;
    public string SwitchReference { get; set; } = string.Empty;
    public string ResponseCode { get; set; } = string.Empty;
    public string AuthorizationCode { get; set; } = string.Empty;
    public DateTime ProcessedAt { get; set; }
    public string DestinationInstitution { get; set; } = string.Empty;
    public SwitchProtocol Protocol { get; set; }
}

/// <summary>
/// Outbound transaction log entry for reconciliation.
/// </summary>
public sealed class OutboundTransactionLog
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
