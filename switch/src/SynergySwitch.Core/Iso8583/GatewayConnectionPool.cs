using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net.Sockets;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NetCore8583;
using SynergySwitch.Core.Gateway;

namespace SynergySwitch.Core.Iso8583;

/// <summary>
/// Manages TCP connection pools for ALL configured gateways.
/// On startup and on cache refresh, spins up/down pools per gateway.
/// Each pool has N connections with STAN-correlated multiplexing.
/// </summary>
public class GatewayConnectionPool : IHostedService, IDisposable
{
    private readonly Iso8583MessageLogger _messageLogger;
    private readonly ILogger<GatewayConnectionPool> _logger;
    private readonly MessageFactory<IsoMessage> _messageFactory;
    private readonly ConcurrentDictionary<int, SingleGatewayPool> _pools = new();
    private readonly Iso20022Bank.Iso20022GrpcClient? _iso20022Client;
    private CancellationTokenSource? _cts;
    private Task? _healthTask;

    public GatewayConnectionPool(
        Iso8583MessageLogger messageLogger,
        Iso20022Bank.Iso20022GrpcClient iso20022Client,
        ILogger<GatewayConnectionPool> logger)
    {
        _messageLogger = messageLogger;
        _iso20022Client = iso20022Client;
        _logger = logger;
        _messageFactory = Iso8583MessageFactory.Create();
    }

    // ── IHostedService ──

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting multi-gateway connection pool manager");
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _healthTask = RunHealthLoopAsync(_cts.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping multi-gateway connection pool manager");
        _cts?.Cancel();
        if (_healthTask != null)
        {
            try { await _healthTask; }
            catch (OperationCanceledException) { /* expected */ }
        }
        foreach (var pool in _pools.Values)
            pool.Dispose();
        _pools.Clear();
    }

    /// <summary>
    /// Send a 0200 to a specific gateway (identified by CachedGateway from routing).
    /// </summary>
    public async Task<IsoMessage> SendAuthorisationAsync(
        CachedGateway gateway, IsoMessage request, CancellationToken ct = default)
    {
        if (!_pools.TryGetValue(gateway.Id, out var pool) || !pool.HasHealthyConnection)
        {
            throw new InvalidOperationException(
                $"No healthy connections for gateway '{gateway.Name}' ({gateway.Host}:{gateway.Port})");
        }

        return await pool.SendAsync(request, ct);
    }

    /// <summary>
    /// Get status of all gateway pools for dashboard display.
    /// Includes both ISO 8583 TCP pools and ISO 20022 gRPC channels.
    /// </summary>
    public List<GatewayPoolStatus> GetAllPoolStatuses()
    {
        var statuses = _pools.Values.Select(p => p.GetStatus()).ToList();

        // Add ISO 20022 gRPC gateways from the cache (skip any already in TCP pools)
        var existingIds = statuses.Select(s => s.GatewayId).ToHashSet();
        foreach (var gw in GatewayManager.GetCachedGateways())
        {
            if (gw.Protocol != Data.Entities.GatewayProtocol.Iso20022Grpc || !gw.IsEnabled)
                continue;
            if (existingIds.Contains(gw.Id))
                continue;

            var isActive = _iso20022Client?.IsChannelActive(gw.Id) ?? false;
            statuses.Add(new GatewayPoolStatus
            {
                GatewayId = gw.Id,
                GatewayName = gw.Name,
                Host = gw.Host,
                Port = gw.Port,
                PoolSize = 1, // gRPC uses HTTP/2 multiplexing — one channel
                ActiveConnections = isActive ? 1 : 0,
                PendingRequests = 0,
                TotalDrops = 0,
                ReconnectAttempts = 0,
                ConnectedSince = null,
                IsEnabled = true,
                Protocol = "ISO20022"
            });
        }

        return statuses;
    }

    /// <summary>
    /// Get status of a specific gateway pool.
    /// </summary>
    public GatewayPoolStatus? GetPoolStatus(int gatewayId)
    {
        return _pools.TryGetValue(gatewayId, out var pool) ? pool.GetStatus() : null;
    }

    // ── Health monitor: reconciles pools with cached gateway config ──

    private async Task RunHealthLoopAsync(CancellationToken ct)
    {
        // Brief initial delay to let DB + cache initialise
        try { await Task.Delay(2000, ct); } catch (OperationCanceledException) { return; }

        while (!ct.IsCancellationRequested)
        {
            try
            {
                ReconcilePools(ct);

                // Health-check all gateways in parallel:
                // - ISO 8583 TCP pools maintain connections
                // - ISO 20022 gRPC channels proactively connect
                var healthTasks = new List<Task>();

                foreach (var pool in _pools.Values)
                    healthTasks.Add(pool.MaintainConnectionsAsync(ct));

                if (_iso20022Client != null)
                {
                    foreach (var gw in GatewayManager.GetCachedGateways())
                    {
                        if (gw.IsEnabled && gw.Protocol == Data.Entities.GatewayProtocol.Iso20022Grpc)
                        {
                            if (!_iso20022Client.IsChannelActive(gw.Id))
                            {
                                _logger.LogInformation("[ISO20022] Initiating connect to '{Name}' ({Host}:{Port})",
                                    gw.Name, gw.Host, gw.Port);
                                healthTasks.Add(_iso20022Client.ConnectAsync(gw, ct));
                            }
                        }
                    }
                }

                await Task.WhenAll(healthTasks);

                // Emit Prometheus metrics
                foreach (var pool in _pools.Values)
                {
                    var s = pool.GetStatus();
                    SwitchMetrics.UpdateGatewayPoolMetrics(
                        s.GatewayName, s.Host, s.ActiveConnections, s.PoolSize, s.PendingRequests);
                }
                SwitchMetrics.UpdateRuntimeMetrics();
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex) { _logger.LogError(ex, "Health loop error"); }

            try { await Task.Delay(3000, ct); } catch (OperationCanceledException) { break; }
        }
    }

    /// <summary>
    /// Sync pools with the cached gateway list: add new, remove deleted, disable/enable as needed.
    /// </summary>
    private void ReconcilePools(CancellationToken ct)
    {
        var cachedGateways = GatewayManager.GetCachedGateways();
        var cachedIds = cachedGateways.Select(g => g.Id).ToHashSet();

        // Remove pools for deleted gateways
        foreach (var id in _pools.Keys)
        {
            if (!cachedIds.Contains(id))
            {
                if (_pools.TryRemove(id, out var removed))
                {
                    _logger.LogInformation("Removing pool for deleted gateway {Id}", id);
                    removed.Dispose();
                }
            }
        }

        // Add/update pools (ISO 8583 only — ISO 20022 uses gRPC channels, not TCP pools)
        foreach (var gw in cachedGateways)
        {
            if (!gw.IsEnabled || gw.Protocol == Data.Entities.GatewayProtocol.Iso20022Grpc)
            {
                // Disabled or ISO 20022 gateway — tear down TCP pool if exists
                if (_pools.TryRemove(gw.Id, out var removed))
                {
                    _logger.LogInformation("Removing TCP pool for gateway '{Name}' (enabled={Enabled}, protocol={Protocol})",
                        gw.Name, gw.IsEnabled, gw.Protocol);
                    removed.Dispose();
                }
                continue;
            }

            if (!_pools.ContainsKey(gw.Id))
            {
                _logger.LogInformation(
                    "Creating pool for gateway '{Name}' ({Host}:{Port}, pool={PoolSize})",
                    gw.Name, gw.Host, gw.Port, gw.PoolSize);

                var pool = new SingleGatewayPool(gw, _messageFactory, _messageLogger, _logger);
                _pools[gw.Id] = pool;
            }
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        foreach (var pool in _pools.Values)
            pool.Dispose();
        _pools.Clear();
        _cts?.Dispose();
    }
}

/// <summary>
/// Connection pool for a single gateway. N persistent TCP connections,
/// STAN-correlated multiplexed request/response, round-robin send.
/// </summary>
internal class SingleGatewayPool : IDisposable
{
    private readonly CachedGateway _gw;
    private readonly MessageFactory<IsoMessage> _messageFactory;
    private readonly Iso8583MessageLogger _messageLogger;
    private readonly Microsoft.Extensions.Logging.ILogger _logger;
    private readonly GatewayConnection[] _connections;
    private int _roundRobin;
    private int _totalDrops;
    private int _reconnectAttempts;
    private DateTime? _connectedSince;
    private string _lastReportedState = "Idle";

    public SingleGatewayPool(
        CachedGateway gw,
        MessageFactory<IsoMessage> messageFactory,
        Iso8583MessageLogger messageLogger,
        Microsoft.Extensions.Logging.ILogger logger)
    {
        _gw = gw;
        _messageFactory = messageFactory;
        _messageLogger = messageLogger;
        _logger = logger;
        _connections = new GatewayConnection[Math.Max(1, gw.PoolSize)];
        for (int i = 0; i < _connections.Length; i++)
            _connections[i] = new GatewayConnection(i);
    }

    public bool HasHealthyConnection => _connections.Any(c => c.IsHealthy);

    public async Task<IsoMessage> SendAsync(IsoMessage request, CancellationToken ct)
    {
        var conn = PickConnection()
            ?? throw new InvalidOperationException($"No healthy connections for gateway '{_gw.Name}'");

        var stan = request.GetObjectValue(11)?.ToString()?.Trim()
            ?? throw new InvalidOperationException("Missing STAN (field 11)");

        var tcs = new TaskCompletionSource<IsoMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        conn.Pending[stan] = tcs;

        try
        {
            await conn.SendAsync(request, _gw.SendLengthHeader, ct);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(_gw.TimeoutSeconds));

            var reg = timeoutCts.Token.Register(() =>
                tcs.TrySetException(new TimeoutException($"Timeout waiting for response (STAN={stan})")));

            try { return await tcs.Task; }
            finally { await reg.DisposeAsync(); }
        }
        catch
        {
            conn.Pending.TryRemove(stan, out _);
            throw;
        }
    }

    public async Task MaintainConnectionsAsync(CancellationToken ct)
    {
        var hadHealthy = _connections.Any(c => c.IsHealthy);

        for (int i = 0; i < _connections.Length; i++)
        {
            if (ct.IsCancellationRequested) break;
            var conn = _connections[i];
            if (!conn.IsHealthy)
            {
                Interlocked.Increment(ref _reconnectAttempts);
                EmitState("Connecting");
                await ConnectOneAsync(conn, ct);
            }
        }

        var hasHealthy = _connections.Any(c => c.IsHealthy);
        var activeCount = _connections.Count(c => c.IsHealthy);

        if (hasHealthy)
        {
            if (_connectedSince == null) _connectedSince = DateTime.UtcNow;
            EmitState("Ready");
        }
        else
        {
            _connectedSince = null;
            EmitState("Disconnected");
        }
    }

    /// <summary>
    /// Emit a state transition to Prometheus if the state has changed.
    /// </summary>
    private void EmitState(string newState)
    {
        var oldState = _lastReportedState;
        if (oldState == newState) return;

        _lastReportedState = newState;
        _logger.LogInformation("[ISO8583] '{Gateway}' state changed: {OldState} → {NewState}",
            _gw.Name, oldState, newState);

        SwitchMetrics.RecordStateTransition(_gw.Name, oldState, newState);

        var connectivityValue = newState switch
        {
            "Ready" => 1.0,
            "Connecting" => 0.5,
            _ => 0.0
        };
        SwitchMetrics.SetGatewayConnectivity(_gw.Name, "ISO8583", connectivityValue);

        // Detect drops: was Ready, now down
        if (oldState == "Ready" && newState == "Disconnected")
        {
            _logger.LogWarning("[ISO8583] '{Gateway}' connection DROPPED", _gw.Name);
            SwitchMetrics.RecordConnectionDropDetected(_gw.Name, "ISO8583");
        }

        // Detect reconnects: was not Ready, now Ready
        if (oldState != "Ready" && newState == "Ready")
        {
            _logger.LogInformation("[ISO8583] '{Gateway}' RECONNECTED", _gw.Name);
            SwitchMetrics.RecordReconnectDetected(_gw.Name, "ISO8583");
        }
    }

    public GatewayPoolStatus GetStatus() => new()
    {
        GatewayId = _gw.Id,
        GatewayName = _gw.Name,
        Host = _gw.Host,
        Port = _gw.Port,
        PoolSize = _connections.Length,
        ActiveConnections = _connections.Count(c => c.IsHealthy),
        PendingRequests = _connections.Sum(c => c.Pending.Count),
        TotalDrops = _totalDrops,
        ReconnectAttempts = _reconnectAttempts,
        ConnectedSince = _connectedSince,
        IsEnabled = true
    };

    private GatewayConnection? PickConnection()
    {
        for (int i = 0; i < _connections.Length; i++)
        {
            var idx = Interlocked.Increment(ref _roundRobin) % _connections.Length;
            if (idx < 0) idx += _connections.Length;
            if (_connections[idx].IsHealthy) return _connections[idx];
        }
        return null;
    }

    private async Task ConnectOneAsync(GatewayConnection conn, CancellationToken ct)
    {
        try
        {
            conn.Dispose();

            var tcp = new TcpClient
            {
                ReceiveTimeout = _gw.TimeoutSeconds * 1000,
                SendTimeout = _gw.TimeoutSeconds * 1000
            };

            using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            connectCts.CancelAfter(TimeSpan.FromSeconds(_gw.TimeoutSeconds));

            await tcp.ConnectAsync(_gw.Host, _gw.Port, connectCts.Token);
            var stream = tcp.GetStream();

            // Sign-on
            var signOn = Iso8583MessageBuilder.BuildNetworkManagementRequest();
            await SendRawAsync(stream, signOn, _gw.SendLengthHeader, ct);
            var resp = await ReceiveRawAsync(stream, _gw.SendLengthHeader, ct);

            var rc = resp.GetObjectValue(39)?.ToString()?.Trim() ?? "96";
            if (rc != "00")
            {
                _logger.LogWarning("[{Gw}][conn={I}] Sign-on failed: {Rc}", _gw.Name, conn.Index, rc);
                tcp.Dispose();
                return;
            }

            conn.Activate(tcp, stream);
            conn.ReadTask = Task.Run(() => ReadLoopAsync(conn, ct), ct);

            _logger.LogInformation("[{Gw}][conn={I}] Connected and signed on", _gw.Name, conn.Index);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { /* shutting down */ }
        catch (Exception ex)
        {
            SwitchMetrics.RecordConnectionError("connect_failed");
            _logger.LogWarning(ex, "[{Gw}][conn={I}] Connection failed", _gw.Name, conn.Index);
        }
    }

    private async Task ReadLoopAsync(GatewayConnection conn, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested && conn.IsHealthy)
            {
                var response = await ReceiveRawAsync(conn.Stream!, _gw.SendLengthHeader, ct);
                var stan = response.GetObjectValue(11)?.ToString()?.Trim();
                if (stan != null && conn.Pending.TryRemove(stan, out var tcs))
                    tcs.TrySetResult(response);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { /* ok */ }
        catch (Exception ex)
        {
            foreach (var kvp in conn.Pending)
                if (conn.Pending.TryRemove(kvp.Key, out var t))
                    t.TrySetException(new IOException($"Gateway '{_gw.Name}' connection lost: {ex.Message}"));

            Interlocked.Increment(ref _totalDrops);
            SwitchMetrics.RecordConnectionError("connection_drop");
            conn.MarkUnhealthy();
            // State transition will be emitted by MaintainConnectionsAsync on next health cycle
        }
    }

    // ── Raw IO ──

    private static async Task SendRawAsync(NetworkStream stream, IsoMessage msg, bool lengthHeader, CancellationToken ct)
    {
        var sbyteData = msg.WriteData();
        var bytes = new byte[sbyteData.Length];
        Buffer.BlockCopy(sbyteData, 0, bytes, 0, sbyteData.Length);

        if (lengthHeader)
        {
            var hdr = new byte[2];
            BinaryPrimitives.WriteUInt16BigEndian(hdr, (ushort)bytes.Length);
            await stream.WriteAsync(hdr, ct);
        }
        await stream.WriteAsync(bytes, ct);
        await stream.FlushAsync(ct);
    }

    private async Task<IsoMessage> ReceiveRawAsync(NetworkStream stream, bool lengthHeader, CancellationToken ct)
    {
        int len;
        if (lengthHeader)
        {
            var hdr = new byte[2];
            await ReadExactAsync(stream, hdr, ct);
            len = BinaryPrimitives.ReadUInt16BigEndian(hdr);
        }
        else { len = 4096; }

        var buf = new byte[len];
        if (lengthHeader) { await ReadExactAsync(stream, buf, ct); }
        else
        {
            int n = await stream.ReadAsync(buf, ct);
            if (n == 0) throw new IOException("Connection closed");
            buf = buf[..n];
        }

        var sb = new sbyte[buf.Length];
        Buffer.BlockCopy(buf, 0, sb, 0, buf.Length);
        return _messageFactory.ParseMessage(sb, 0);
    }

    private static async Task ReadExactAsync(NetworkStream stream, byte[] buf, CancellationToken ct)
    {
        int total = 0;
        while (total < buf.Length)
        {
            int n = await stream.ReadAsync(buf.AsMemory(total, buf.Length - total), ct);
            if (n == 0) throw new IOException("Connection closed");
            total += n;
        }
    }

    public void Dispose()
    {
        foreach (var c in _connections) c.Dispose();
    }
}

/// <summary>One TCP connection within a gateway pool.</summary>
internal class GatewayConnection : IDisposable
{
    public readonly int Index;
    private TcpClient? _tcp;
    private NetworkStream? _stream;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private volatile bool _healthy;

    public GatewayConnection(int index) => Index = index;
    public NetworkStream? Stream => _stream;
    public bool IsHealthy => _healthy && _tcp?.Connected == true;
    public Task? ReadTask { get; set; }
    public ConcurrentDictionary<string, TaskCompletionSource<IsoMessage>> Pending { get; } = new();

    public void Activate(TcpClient tcp, NetworkStream stream) { _tcp = tcp; _stream = stream; _healthy = true; }
    public void MarkUnhealthy() => _healthy = false;

    public async Task SendAsync(IsoMessage msg, bool lengthHeader, CancellationToken ct)
    {
        if (_stream == null || !_healthy)
            throw new InvalidOperationException($"Connection [{Index}] not healthy");

        var sbyteData = msg.WriteData();
        var bytes = new byte[sbyteData.Length];
        Buffer.BlockCopy(sbyteData, 0, bytes, 0, sbyteData.Length);

        await _writeLock.WaitAsync(ct);
        try
        {
            if (lengthHeader)
            {
                var hdr = new byte[2];
                BinaryPrimitives.WriteUInt16BigEndian(hdr, (ushort)bytes.Length);
                await _stream.WriteAsync(hdr, ct);
            }
            await _stream.WriteAsync(bytes, ct);
            await _stream.FlushAsync(ct);
        }
        finally { _writeLock.Release(); }
    }

    public void Dispose()
    {
        _healthy = false;
        foreach (var kvp in Pending)
            if (Pending.TryRemove(kvp.Key, out var t)) t.TrySetCanceled();
        try { _stream?.Dispose(); } catch { }
        try { _tcp?.Dispose(); } catch { }
        _stream = null; _tcp = null;
        _writeLock.Dispose();
    }
}

/// <summary>Status snapshot for one gateway's connection pool.</summary>
public record GatewayPoolStatus
{
    public int GatewayId { get; init; }
    public string GatewayName { get; init; } = "";
    public string Host { get; init; } = "";
    public int Port { get; init; }
    public int PoolSize { get; init; }
    public int ActiveConnections { get; init; }
    public int PendingRequests { get; init; }
    public int TotalDrops { get; init; }
    public int ReconnectAttempts { get; init; }
    public DateTime? ConnectedSince { get; init; }
    public bool IsEnabled { get; init; }
    public string Protocol { get; init; } = "ISO8583";
}
