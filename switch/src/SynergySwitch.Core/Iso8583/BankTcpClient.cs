using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetCore8583;

namespace SynergySwitch.Core.Iso8583;

/// <summary>
/// High-throughput bank gateway that maintains a pool of TCP connections to the
/// acquiring bank / Zimswitch. Designed for 10,000+ concurrent transactions.
///
/// Architecture:
///  - Maintains a configurable pool of persistent TCP connections.
///  - Each connection has a dedicated read loop that dispatches responses by STAN (field 11).
///  - Requests are round-robined across healthy connections.
///  - Failed connections are automatically replaced by the health monitor.
///  - Full async/await — no thread blocking.
/// </summary>
public class BankTcpClient : IHostedService, IDisposable
{
    private readonly BankConnectionSettings _settings;
    private readonly MessageFactory<IsoMessage> _messageFactory;
    private readonly Iso8583MessageLogger _messageLogger;
    private readonly ILogger<BankTcpClient> _logger;
    private readonly BankConnection[] _pool;
    private int _roundRobinIndex;
    private CancellationTokenSource? _cts;
    private Task? _healthTask;

    // ── Aggregate status tracking ──
    private readonly object _statusLock = new();
    private DateTime? _firstConnectedAt;
    private DateTime? _lastDisconnectedAt;
    private int _totalConnectionDrops;
    private int _totalReconnectAttempts;
    private string _lastError = "";

    public BankTcpClient(
        IOptions<BankConnectionSettings> settings,
        Iso8583MessageLogger messageLogger,
        ILogger<BankTcpClient> logger)
    {
        _settings = settings.Value;
        _messageLogger = messageLogger;
        _logger = logger;
        _messageFactory = Iso8583MessageFactory.Create();

        var poolSize = Math.Max(1, _settings.PoolSize);
        _pool = new BankConnection[poolSize];
        for (int i = 0; i < poolSize; i++)
            _pool[i] = new BankConnection(i);
    }

    /// <summary>Number of connections currently signed on and healthy.</summary>
    public int ActiveConnectionCount => _pool.Count(c => c.IsHealthy);

    /// <summary>
    /// Returns the current bank connection status for dashboard display.
    /// </summary>
    public BankConnectionStatus GetConnectionStatus()
    {
        var active = ActiveConnectionCount;
        lock (_statusLock)
        {
            return new BankConnectionStatus
            {
                IsConnected = active > 0,
                IsOfflineMode = _settings.OfflineMode,
                Host = _settings.Host,
                Port = _settings.Port,
                ConnectedSince = _firstConnectedAt,
                LastDisconnectedAt = _lastDisconnectedAt,
                ConnectionDropCount = _totalConnectionDrops,
                ReconnectAttempts = _totalReconnectAttempts,
                LastError = _lastError,
                UptimeSeconds = _firstConnectedAt.HasValue && active > 0
                    ? (DateTime.UtcNow - _firstConnectedAt.Value).TotalSeconds
                    : 0,
                PoolSize = _pool.Length,
                ActiveConnections = active,
                PendingRequests = _pool.Sum(c => c.PendingCount)
            };
        }
    }

    // ── IHostedService ──

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_settings.OfflineMode)
        {
            _logger.LogInformation("Bank connection is in OFFLINE mode — not connecting");
            return Task.CompletedTask;
        }

        _logger.LogInformation(
            "Starting bank gateway: {PoolSize} connections to {Host}:{Port}",
            _pool.Length, _settings.Host, _settings.Port);

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _healthTask = RunHealthMonitorAsync(_cts.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping bank gateway");
        _cts?.Cancel();

        if (_healthTask != null)
        {
            try { await _healthTask; }
            catch (OperationCanceledException) { /* expected */ }
        }

        foreach (var conn in _pool)
            DisconnectOne(conn);
    }

    /// <summary>
    /// Send a 0200 authorisation request and return the 0210 response.
    /// Picks a healthy connection via round-robin. Fully concurrent — multiple
    /// in-flight requests per connection correlated by STAN.
    /// </summary>
    public async Task<IsoMessage> SendAuthorisationAsync(IsoMessage request, CancellationToken ct = default)
    {
        var conn = PickConnection();
        if (conn == null)
            throw new InvalidOperationException("No healthy bank connections available");

        // Use STAN (field 11) to correlate request and response
        var stan = request.GetObjectValue(11)?.ToString()?.Trim()
            ?? throw new InvalidOperationException("Request missing STAN (field 11)");

        var tcs = new TaskCompletionSource<IsoMessage>(TaskCreationOptions.RunContinuationsAsynchronously);

        // Register the pending request so the read loop can dispatch the response
        conn.PendingRequests[stan] = tcs;

        try
        {
            _messageLogger.LogOutbound(request, $"Auth Request [conn={conn.Index}, STAN={stan}]");
            var sw = Stopwatch.StartNew();

            await conn.SendAsync(request, _settings.SendLengthHeader, ct);

            // Wait for the response (dispatched by read loop), with timeout
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(_settings.TimeoutSeconds));

            var registration = timeoutCts.Token.Register(() =>
                tcs.TrySetException(new TimeoutException(
                    $"Bank response timeout after {_settings.TimeoutSeconds}s (STAN={stan})")));

            IsoMessage response;
            try
            {
                response = await tcs.Task;
            }
            finally
            {
                await registration.DisposeAsync();
            }

            sw.Stop();
            var responseCode = response.GetObjectValue(39)?.ToString()?.Trim() ?? "??";
            _messageLogger.LogInbound(response, sw.Elapsed.TotalMilliseconds,
                $"Auth Response [conn={conn.Index}, STAN={stan}]");
            SwitchMetrics.ObserveBankDuration(sw.Elapsed.TotalSeconds, responseCode);

            return response;
        }
        catch (Exception)
        {
            conn.PendingRequests.TryRemove(stan, out _);
            throw;
        }
    }

    // ── Connection pool management ──

    /// <summary>
    /// Pick the next healthy connection via round-robin.
    /// </summary>
    private BankConnection? PickConnection()
    {
        var poolLen = _pool.Length;
        for (int i = 0; i < poolLen; i++)
        {
            var idx = Interlocked.Increment(ref _roundRobinIndex) % poolLen;
            if (idx < 0) idx += poolLen;
            if (_pool[idx].IsHealthy)
                return _pool[idx];
        }
        return null;
    }

    /// <summary>
    /// Background health monitor: ensures all connections are up, replaces dead ones.
    /// </summary>
    private async Task RunHealthMonitorAsync(CancellationToken ct)
    {
        // Initial connection burst — connect all pool members
        var connectTasks = _pool.Select(c => ConnectOneAsync(c, ct)).ToArray();
        await Task.WhenAll(connectTasks);

        // Ongoing health checks every 3 seconds
        while (!ct.IsCancellationRequested)
        {
            try { await Task.Delay(3000, ct); }
            catch (OperationCanceledException) { break; }

            for (int i = 0; i < _pool.Length; i++)
            {
                var conn = _pool[i];
                if (!conn.IsHealthy)
                {
                    lock (_statusLock)
                        _totalReconnectAttempts++;

                    _logger.LogInformation("Reconnecting pool connection [{Index}]", i);
                    DisconnectOne(conn);
                    await ConnectOneAsync(conn, ct);
                }
            }

            // Update aggregate connected-since
            var active = ActiveConnectionCount;
            lock (_statusLock)
            {
                if (active > 0 && _firstConnectedAt == null)
                    _firstConnectedAt = DateTime.UtcNow;
                else if (active == 0)
                    _firstConnectedAt = null;
            }

            SwitchMetrics.SetActiveConnections(active);
        }
    }

    private async Task ConnectOneAsync(BankConnection conn, CancellationToken ct)
    {
        try
        {
            _messageLogger.LogConnectionEvent("CONNECT",
                $"[conn={conn.Index}] Connecting to {_settings.Host}:{_settings.Port}");

            var tcp = new TcpClient
            {
                ReceiveTimeout = _settings.TimeoutSeconds * 1000,
                SendTimeout = _settings.TimeoutSeconds * 1000
            };

            using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            connectCts.CancelAfter(TimeSpan.FromSeconds(_settings.TimeoutSeconds));

            await tcp.ConnectAsync(_settings.Host, _settings.Port, connectCts.Token);
            var stream = tcp.GetStream();

            _messageLogger.LogConnectionEvent("CONNECTED",
                $"[conn={conn.Index}] TCP established to {_settings.Host}:{_settings.Port}");

            // Sign-on
            var signOn = Iso8583MessageBuilder.BuildNetworkManagementRequest();
            await SendRawAsync(stream, signOn, ct);
            var signOnResponse = await ReceiveRawAsync(stream, ct);

            var rc = signOnResponse.GetObjectValue(39)?.ToString()?.Trim() ?? "96";
            if (rc != "00")
            {
                _messageLogger.LogConnectionEvent("SIGN-ON FAILED",
                    $"[conn={conn.Index}] Response code: {rc}");
                tcp.Dispose();
                return;
            }

            // Connection is live — activate it
            conn.Activate(tcp, stream);

            // Start dedicated read loop for this connection
            conn.ReadLoopTask = Task.Run(() => ReadLoopAsync(conn, ct), ct);

            lock (_statusLock)
            {
                _firstConnectedAt ??= DateTime.UtcNow;
                _lastError = "";
            }

            _messageLogger.LogConnectionEvent("SIGN-ON",
                $"[conn={conn.Index}] Bank sign-on successful");
            _logger.LogInformation("Pool connection [{Index}] established and signed on", conn.Index);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // shutting down
        }
        catch (Exception ex)
        {
            _messageLogger.LogConnectionError($"CONNECT_FAILED[{conn.Index}]", ex);
            lock (_statusLock)
                _lastError = ex.Message;
            SwitchMetrics.RecordConnectionError("connect_failed");
        }
    }

    /// <summary>
    /// Dedicated read loop for one connection. Reads responses and dispatches
    /// them to the waiting TaskCompletionSource by STAN.
    /// </summary>
    private async Task ReadLoopAsync(BankConnection conn, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested && conn.IsHealthy)
            {
                var response = await ReceiveRawAsync(conn.Stream!, ct);

                var stan = response.GetObjectValue(11)?.ToString()?.Trim();
                if (stan != null && conn.PendingRequests.TryRemove(stan, out var tcs))
                {
                    tcs.TrySetResult(response);
                }
                else
                {
                    _logger.LogWarning(
                        "[conn={Index}] Received response with unknown STAN={Stan}, discarding",
                        conn.Index, stan ?? "null");
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // shutting down
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[conn={Index}] Read loop terminated", conn.Index);

            // Fail all pending requests on this connection
            foreach (var kvp in conn.PendingRequests)
            {
                if (conn.PendingRequests.TryRemove(kvp.Key, out var tcs))
                    tcs.TrySetException(new IOException($"Bank connection [{conn.Index}] lost: {ex.Message}"));
            }

            // Mark as unhealthy so the health monitor reconnects
            lock (_statusLock)
            {
                _totalConnectionDrops++;
                _lastDisconnectedAt = DateTime.UtcNow;
                _lastError = ex.Message;
            }
            SwitchMetrics.RecordConnectionError("connection_drop");
            conn.MarkUnhealthy();
        }
    }

    private void DisconnectOne(BankConnection conn)
    {
        conn.Dispose();
    }

    // ── Raw send/receive (no correlation, used for sign-on and by read loop) ──

    private async Task SendRawAsync(NetworkStream stream, IsoMessage message, CancellationToken ct)
    {
        var sbyteData = message.WriteData();
        var messageBytes = new byte[sbyteData.Length];
        Buffer.BlockCopy(sbyteData, 0, messageBytes, 0, sbyteData.Length);

        if (_settings.SendLengthHeader)
        {
            var lengthHeader = new byte[2];
            BinaryPrimitives.WriteUInt16BigEndian(lengthHeader, (ushort)messageBytes.Length);
            await stream.WriteAsync(lengthHeader, ct);
        }

        await stream.WriteAsync(messageBytes, ct);
        await stream.FlushAsync(ct);
    }

    private async Task<IsoMessage> ReceiveRawAsync(NetworkStream stream, CancellationToken ct)
    {
        int messageLength;

        if (_settings.SendLengthHeader)
        {
            var lengthHeader = new byte[2];
            await ReadExactAsync(stream, lengthHeader, ct);
            messageLength = BinaryPrimitives.ReadUInt16BigEndian(lengthHeader);
        }
        else
        {
            messageLength = 4096;
        }

        var buffer = new byte[messageLength];

        if (_settings.SendLengthHeader)
        {
            await ReadExactAsync(stream, buffer, ct);
        }
        else
        {
            int bytesRead = await stream.ReadAsync(buffer, ct);
            if (bytesRead == 0)
                throw new IOException("Bank closed the connection");
            buffer = buffer[..bytesRead];
        }

        var sbyteBuffer = new sbyte[buffer.Length];
        Buffer.BlockCopy(buffer, 0, sbyteBuffer, 0, buffer.Length);
        return _messageFactory.ParseMessage(sbyteBuffer, 0);
    }

    private static async Task ReadExactAsync(NetworkStream stream, byte[] buffer, CancellationToken ct)
    {
        int totalRead = 0;
        while (totalRead < buffer.Length)
        {
            int read = await stream.ReadAsync(
                buffer.AsMemory(totalRead, buffer.Length - totalRead), ct);
            if (read == 0)
                throw new IOException("Bank closed the connection");
            totalRead += read;
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        foreach (var conn in _pool)
            conn.Dispose();
        _cts?.Dispose();
    }

    // ── Inner class: one pooled connection ──

    private class BankConnection : IDisposable
    {
        public readonly int Index;
        private TcpClient? _tcp;
        private NetworkStream? _stream;
        private readonly SemaphoreSlim _writeLock = new(1, 1);
        private volatile bool _healthy;

        public BankConnection(int index) => Index = index;

        public NetworkStream? Stream => _stream;
        public bool IsHealthy => _healthy && _tcp?.Connected == true;
        public Task? ReadLoopTask { get; set; }

        /// <summary>
        /// Pending requests waiting for responses, keyed by STAN.
        /// </summary>
        public ConcurrentDictionary<string, TaskCompletionSource<IsoMessage>> PendingRequests { get; } = new();

        public int PendingCount => PendingRequests.Count;

        public void Activate(TcpClient tcp, NetworkStream stream)
        {
            _tcp = tcp;
            _stream = stream;
            _healthy = true;
        }

        public void MarkUnhealthy() => _healthy = false;

        /// <summary>
        /// Serialized write to this connection's stream. Multiple callers can
        /// write concurrently but each individual message write is atomic.
        /// </summary>
        public async Task SendAsync(IsoMessage message, bool sendLengthHeader, CancellationToken ct)
        {
            if (_stream == null || !_healthy)
                throw new InvalidOperationException($"Connection [{Index}] is not healthy");

            var sbyteData = message.WriteData();
            var messageBytes = new byte[sbyteData.Length];
            Buffer.BlockCopy(sbyteData, 0, messageBytes, 0, sbyteData.Length);

            await _writeLock.WaitAsync(ct);
            try
            {
                if (sendLengthHeader)
                {
                    var lengthHeader = new byte[2];
                    BinaryPrimitives.WriteUInt16BigEndian(lengthHeader, (ushort)messageBytes.Length);
                    await _stream.WriteAsync(lengthHeader, ct);
                }
                await _stream.WriteAsync(messageBytes, ct);
                await _stream.FlushAsync(ct);
            }
            finally
            {
                _writeLock.Release();
            }
        }

        public void Dispose()
        {
            _healthy = false;
            // Fail all pending requests
            foreach (var kvp in PendingRequests)
            {
                if (PendingRequests.TryRemove(kvp.Key, out var tcs))
                    tcs.TrySetCanceled();
            }
            try { _stream?.Dispose(); } catch { /* ignore */ }
            try { _tcp?.Dispose(); } catch { /* ignore */ }
            _stream = null;
            _tcp = null;
            _writeLock.Dispose();
        }
    }
}

/// <summary>
/// Snapshot of the bank TCP connection pool status for dashboard display.
/// </summary>
public record BankConnectionStatus
{
    public bool IsConnected { get; init; }
    public bool IsOfflineMode { get; init; }
    public string Host { get; init; } = "";
    public int Port { get; init; }
    public DateTime? ConnectedSince { get; init; }
    public DateTime? LastDisconnectedAt { get; init; }
    public int ConnectionDropCount { get; init; }
    public int ReconnectAttempts { get; init; }
    public string LastError { get; init; } = "";
    public double UptimeSeconds { get; init; }
    public int PoolSize { get; init; }
    public int ActiveConnections { get; init; }
    public int PendingRequests { get; init; }

    public string StatusText => IsOfflineMode
        ? "Offline Mode"
        : IsConnected
            ? "Connected"
            : "Disconnected";

    public string UptimeFormatted
    {
        get
        {
            if (UptimeSeconds <= 0) return "-";
            var ts = TimeSpan.FromSeconds(UptimeSeconds);
            return ts.TotalHours >= 1
                ? $"{(int)ts.TotalHours}h {ts.Minutes}m"
                : ts.TotalMinutes >= 1
                    ? $"{(int)ts.TotalMinutes}m {ts.Seconds}s"
                    : $"{ts.Seconds}s";
        }
    }
}
