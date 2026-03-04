using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using UniBank.Switching.Adapters;
using UniBank.Switching.Models;
using UniBank.Switching.Routing;

namespace UniBank.Switching.Services;

/// <summary>
/// Background service that listens for incoming ISO 8583 messages on a TCP port.
/// Simulates the network-facing side of the payment switch that receives transactions
/// from other financial institutions connected to the national payment switch.
/// </summary>
public sealed class TcpListenerService : BackgroundService
{
    private readonly InboundProcessor _inboundProcessor;
    private readonly ILogger<TcpListenerService> _logger;
    private readonly int _port;
    private TcpListener? _listener;

    public TcpListenerService(
        InboundProcessor inboundProcessor,
        ILogger<TcpListenerService> logger)
    {
        _inboundProcessor = inboundProcessor;
        _logger = logger;
        _port = 9100; // Default inbound switch port
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("TCP Listener Service starting on port {Port}", _port);

        try
        {
            _listener = new TcpListener(IPAddress.Any, _port);
            _listener.Start();
            _logger.LogInformation("TCP Listener Service listening on port {Port}", _port);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var client = await AcceptClientAsync(stoppingToken);
                    if (client is not null)
                    {
                        // Handle each connection in a separate task
                        _ = HandleClientAsync(client, stoppingToken);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (SocketException ex) when (stoppingToken.IsCancellationRequested)
                {
                    _logger.LogDebug(ex, "Socket exception during shutdown");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error accepting TCP connection");
                    await Task.Delay(1000, stoppingToken);
                }
            }
        }
        catch (SocketException ex)
        {
            _logger.LogError(ex,
                "Failed to start TCP Listener on port {Port}. Port may be in use.", _port);
        }
        finally
        {
            _listener?.Stop();
            _logger.LogInformation("TCP Listener Service stopped");
        }
    }

    /// <summary>
    /// Accepts a TCP client with cancellation support.
    /// </summary>
    private async Task<TcpClient?> AcceptClientAsync(CancellationToken cancellationToken)
    {
        if (_listener is null)
        {
            return null;
        }

        try
        {
            return await _listener.AcceptTcpClientAsync(cancellationToken);
        }
        catch (ObjectDisposedException)
        {
            return null;
        }
    }

    /// <summary>
    /// Handles an individual TCP client connection. Reads the ISO 8583 message,
    /// processes it through the inbound processor, and sends the response.
    /// </summary>
    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        var remoteEndpoint = client.Client.RemoteEndPoint?.ToString() ?? "unknown";
        _logger.LogInformation("Accepted connection from {RemoteEndpoint}", remoteEndpoint);

        try
        {
            using (client)
            {
                var stream = client.GetStream();
                stream.ReadTimeout = 30_000; // 30 second read timeout
                stream.WriteTimeout = 30_000;

                while (!cancellationToken.IsCancellationRequested && client.Connected)
                {
                    // Read message with 4-byte length header framing
                    var messageData = await ReadFramedMessageAsync(stream, cancellationToken);
                    if (messageData is null || messageData.Length == 0)
                    {
                        // Client disconnected or empty read
                        break;
                    }

                    _logger.LogDebug(
                        "Received {Length} byte message from {RemoteEndpoint}",
                        messageData.Length, remoteEndpoint);

                    // Process through inbound processor
                    var result = _inboundProcessor.ProcessIso8583(messageData);

                    byte[] response;
                    if (result.IsSuccess)
                    {
                        response = result.Value.Response;
                        _logger.LogInformation(
                            "Processed inbound transaction {TransactionId} from {RemoteEndpoint}",
                            result.Value.Canonical.TransactionId, remoteEndpoint);
                    }
                    else
                    {
                        _logger.LogWarning(
                            "Failed to process inbound message from {RemoteEndpoint}: {Error}",
                            remoteEndpoint, result.Error.Message);

                        // Send a minimal error response
                        response = BuildErrorResponse();
                    }

                    // Send response with length-prefixed framing
                    await WriteFramedMessageAsync(stream, response, cancellationToken);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Graceful shutdown
        }
        catch (IOException ex)
        {
            _logger.LogDebug(ex, "IO error on connection from {RemoteEndpoint}", remoteEndpoint);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling connection from {RemoteEndpoint}", remoteEndpoint);
        }

        _logger.LogDebug("Connection from {RemoteEndpoint} closed", remoteEndpoint);
    }

    /// <summary>
    /// Reads a length-prefixed (4-byte big-endian) message from the stream.
    /// </summary>
    private static async Task<byte[]?> ReadFramedMessageAsync(
        NetworkStream stream, CancellationToken cancellationToken)
    {
        // Read 4-byte length header
        var lengthBuffer = new byte[4];
        var bytesRead = 0;
        while (bytesRead < 4)
        {
            var read = await stream.ReadAsync(
                lengthBuffer.AsMemory(bytesRead, 4 - bytesRead), cancellationToken);
            if (read == 0)
            {
                return null; // Connection closed
            }
            bytesRead += read;
        }

        // Convert big-endian length
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(lengthBuffer);
        }
        var messageLength = BitConverter.ToInt32(lengthBuffer, 0);

        if (messageLength <= 0 || messageLength > 65536)
        {
            return null; // Invalid length
        }

        // Read the message body
        var messageBuffer = new byte[messageLength];
        bytesRead = 0;
        while (bytesRead < messageLength)
        {
            var read = await stream.ReadAsync(
                messageBuffer.AsMemory(bytesRead, messageLength - bytesRead), cancellationToken);
            if (read == 0)
            {
                return null; // Connection closed mid-message
            }
            bytesRead += read;
        }

        return messageBuffer;
    }

    /// <summary>
    /// Writes a length-prefixed (4-byte big-endian) message to the stream.
    /// </summary>
    private static async Task WriteFramedMessageAsync(
        NetworkStream stream, byte[] data, CancellationToken cancellationToken)
    {
        var lengthBytes = BitConverter.GetBytes(data.Length);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(lengthBytes);
        }

        await stream.WriteAsync(lengthBytes, cancellationToken);
        await stream.WriteAsync(data, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    /// <summary>
    /// Builds a minimal ISO 8583 error response when processing fails entirely.
    /// </summary>
    private static byte[] BuildErrorResponse()
    {
        var errorMsg = new Iso8583Message
        {
            Mti = "0210"
        };
        errorMsg.SetField(39, "96"); // System malfunction

        var adapter = new Iso8583Adapter();
        var encoded = adapter.Encode(errorMsg);
        return encoded.IsSuccess ? encoded.Value : [];
    }

    public override void Dispose()
    {
        _listener?.Stop();
        base.Dispose();
    }
}
