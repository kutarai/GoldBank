using Prometheus;

namespace SynergySwitch.Core.Iso8583;

/// <summary>
/// Prometheus metrics for SynergySwitch performance monitoring.
/// </summary>
public static class SwitchMetrics
{
    private static readonly Counter TransactionsTotal = Metrics.CreateCounter(
        "switch_transactions_total",
        "Total number of transactions processed",
        new CounterConfiguration { LabelNames = ["response_code", "entry_mode", "currency"] });

    private static readonly Histogram TransactionDuration = Metrics.CreateHistogram(
        "switch_transaction_duration_seconds",
        "Time from request received to response sent",
        new HistogramConfiguration
        {
            LabelNames = ["response_code"],
            Buckets = [0.1, 0.25, 0.5, 1, 2, 5, 10, 20, 30]
        });

    private static readonly Histogram BankResponseDuration = Metrics.CreateHistogram(
        "switch_bank_response_duration_seconds",
        "Time for bank ISO 8583 round-trip",
        new HistogramConfiguration
        {
            LabelNames = ["response_code"],
            Buckets = [0.1, 0.25, 0.5, 1, 2, 5, 10, 20]
        });

    private static readonly Gauge ActiveConnections = Metrics.CreateGauge(
        "switch_bank_connections_active",
        "Number of active TCP connections to bank");

    private static readonly Counter BankConnectionErrors = Metrics.CreateCounter(
        "switch_bank_connection_errors_total",
        "Total bank connection errors",
        new CounterConfiguration { LabelNames = ["error_type"] });

    private static readonly Counter MessagesSent = Metrics.CreateCounter(
        "switch_iso8583_messages_sent_total",
        "Total ISO 8583 messages sent to bank",
        new CounterConfiguration { LabelNames = ["message_type"] });

    private static readonly Counter MessagesReceived = Metrics.CreateCounter(
        "switch_iso8583_messages_received_total",
        "Total ISO 8583 messages received from bank",
        new CounterConfiguration { LabelNames = ["message_type"] });

    private static readonly Counter Iso20022MessagesSent = Metrics.CreateCounter(
        "switch_iso20022_messages_sent_total",
        "Total ISO 20022 gRPC messages sent to bank",
        new CounterConfiguration { LabelNames = ["gateway_name", "method"] });

    private static readonly Counter Iso20022MessagesReceived = Metrics.CreateCounter(
        "switch_iso20022_messages_received_total",
        "Total ISO 20022 gRPC responses received from bank",
        new CounterConfiguration { LabelNames = ["gateway_name", "response_code"] });

    public static void RecordTransaction(string responseCode, string entryMode, string currency)
        => TransactionsTotal.WithLabels(responseCode, entryMode, currency).Inc();

    public static Prometheus.ITimer StartTransactionTimer(string responseCode)
        => TransactionDuration.WithLabels(responseCode).NewTimer();

    public static IDisposable MeasureTransactionDuration()
        => TransactionDuration.WithLabels("pending").NewTimer();

    public static void ObserveTransactionDuration(double seconds, string responseCode)
        => TransactionDuration.WithLabels(responseCode).Observe(seconds);

    public static void ObserveBankDuration(double seconds, string responseCode)
        => BankResponseDuration.WithLabels(responseCode).Observe(seconds);

    public static void IncrementActiveConnections() => ActiveConnections.Inc();
    public static void DecrementActiveConnections() => ActiveConnections.Dec();
    public static void SetActiveConnections(int count) => ActiveConnections.Set(count);

    public static void RecordConnectionError(string errorType)
        => BankConnectionErrors.WithLabels(errorType).Inc();

    public static void RecordMessageSent(string messageType)
        => MessagesSent.WithLabels(messageType).Inc();

    public static void RecordMessageReceived(string messageType)
        => MessagesReceived.WithLabels(messageType).Inc();

    // ── Gateway connectivity state metrics ──

    private static readonly Counter GatewayStateTransitions = Metrics.CreateCounter(
        "switch_gateway_state_transitions_total",
        "Total gRPC/TCP state transitions per gateway",
        new CounterConfiguration { LabelNames = ["gateway_name", "from_state", "to_state"] });

    private static readonly Gauge GatewayConnectivityState = Metrics.CreateGauge(
        "switch_gateway_connectivity_state",
        "Current connectivity state per gateway (1=Ready, 0.5=Connecting, 0=Down)",
        new GaugeConfiguration { LabelNames = ["gateway_name", "protocol"] });

    private static readonly Counter GatewayConnectionDropsDetected = Metrics.CreateCounter(
        "switch_gateway_connection_drops_detected_total",
        "Connection drops detected by state watcher (alertable)",
        new CounterConfiguration { LabelNames = ["gateway_name", "protocol"] });

    private static readonly Counter GatewayReconnectsDetected = Metrics.CreateCounter(
        "switch_gateway_reconnects_detected_total",
        "Successful reconnects detected by state watcher",
        new CounterConfiguration { LabelNames = ["gateway_name", "protocol"] });

    public static void RecordStateTransition(string gatewayName, string fromState, string toState)
        => GatewayStateTransitions.WithLabels(gatewayName, fromState, toState).Inc();

    public static void SetGatewayConnectivity(string gatewayName, string protocol, double value)
        => GatewayConnectivityState.WithLabels(gatewayName, protocol).Set(value);

    public static void RecordConnectionDropDetected(string gatewayName, string protocol)
        => GatewayConnectionDropsDetected.WithLabels(gatewayName, protocol).Inc();

    public static void RecordReconnectDetected(string gatewayName, string protocol)
        => GatewayReconnectsDetected.WithLabels(gatewayName, protocol).Inc();

    public static void RecordIso20022Sent(string gatewayName, string method)
        => Iso20022MessagesSent.WithLabels(gatewayName, method).Inc();

    public static void RecordIso20022Received(string gatewayName, string responseCode)
        => Iso20022MessagesReceived.WithLabels(gatewayName, responseCode).Inc();

    // ── Per-gateway pool metrics ──

    private static readonly Gauge GatewayPoolActiveConnections = Metrics.CreateGauge(
        "switch_gateway_pool_active_connections",
        "Active TCP connections per gateway",
        new GaugeConfiguration { LabelNames = ["gateway_name", "gateway_host"] });

    private static readonly Gauge GatewayPoolSize = Metrics.CreateGauge(
        "switch_gateway_pool_size",
        "Configured pool size per gateway",
        new GaugeConfiguration { LabelNames = ["gateway_name", "gateway_host"] });

    private static readonly Gauge GatewayPendingRequests = Metrics.CreateGauge(
        "switch_gateway_pending_requests",
        "In-flight requests per gateway",
        new GaugeConfiguration { LabelNames = ["gateway_name", "gateway_host"] });

    private static readonly Counter GatewayConnectionDrops = Metrics.CreateCounter(
        "switch_gateway_connection_drops_total",
        "Total connection drops per gateway",
        new CounterConfiguration { LabelNames = ["gateway_name", "gateway_host"] });

    private static readonly Counter GatewayReconnectAttempts = Metrics.CreateCounter(
        "switch_gateway_reconnect_attempts_total",
        "Total reconnect attempts per gateway",
        new CounterConfiguration { LabelNames = ["gateway_name", "gateway_host"] });

    private static readonly Counter GatewayTransactionsRouted = Metrics.CreateCounter(
        "switch_gateway_transactions_routed_total",
        "Transactions routed to each gateway",
        new CounterConfiguration { LabelNames = ["gateway_name", "response_code"] });

    private static readonly Histogram GatewayRoundTrip = Metrics.CreateHistogram(
        "switch_gateway_roundtrip_seconds",
        "ISO 8583 round-trip time per gateway",
        new HistogramConfiguration
        {
            LabelNames = ["gateway_name"],
            Buckets = [0.05, 0.1, 0.25, 0.5, 1, 2, 5, 10, 20]
        });

    // ── Thread pool metrics ──

    private static readonly Gauge ThreadPoolWorkerThreads = Metrics.CreateGauge(
        "switch_threadpool_worker_threads_available",
        "Available worker threads in .NET ThreadPool");

    private static readonly Gauge ThreadPoolCompletionPortThreads = Metrics.CreateGauge(
        "switch_threadpool_completion_port_threads_available",
        "Available I/O completion port threads");

    private static readonly Gauge ThreadPoolWorkerThreadsMax = Metrics.CreateGauge(
        "switch_threadpool_worker_threads_max",
        "Max worker threads in .NET ThreadPool");

    private static readonly Gauge ThreadPoolPendingWorkItems = Metrics.CreateGauge(
        "switch_threadpool_pending_work_items",
        "Pending work items in ThreadPool queue");

    private static readonly Gauge ProcessThreadCount = Metrics.CreateGauge(
        "switch_process_thread_count",
        "Total threads in the process");

    private static readonly Gauge GcGen0Collections = Metrics.CreateGauge(
        "switch_gc_gen0_collections",
        "Gen 0 garbage collections");

    private static readonly Gauge GcGen1Collections = Metrics.CreateGauge(
        "switch_gc_gen1_collections",
        "Gen 1 garbage collections");

    private static readonly Gauge GcGen2Collections = Metrics.CreateGauge(
        "switch_gc_gen2_collections",
        "Gen 2 garbage collections");

    private static readonly Gauge GcTotalMemory = Metrics.CreateGauge(
        "switch_gc_total_memory_bytes",
        "Total managed memory bytes");

    /// <summary>Update per-gateway pool gauges (call periodically from health loop).</summary>
    public static void UpdateGatewayPoolMetrics(string gatewayName, string host,
        int activeConnections, int poolSize, int pendingRequests)
    {
        GatewayPoolActiveConnections.WithLabels(gatewayName, host).Set(activeConnections);
        GatewayPoolSize.WithLabels(gatewayName, host).Set(poolSize);
        GatewayPendingRequests.WithLabels(gatewayName, host).Set(pendingRequests);
    }

    public static void RecordGatewayDrop(string gatewayName, string host)
        => GatewayConnectionDrops.WithLabels(gatewayName, host).Inc();

    public static void RecordGatewayReconnect(string gatewayName, string host)
        => GatewayReconnectAttempts.WithLabels(gatewayName, host).Inc();

    public static void RecordGatewayTransaction(string gatewayName, string responseCode)
        => GatewayTransactionsRouted.WithLabels(gatewayName, responseCode).Inc();

    public static void ObserveGatewayRoundTrip(string gatewayName, double seconds)
        => GatewayRoundTrip.WithLabels(gatewayName).Observe(seconds);

    /// <summary>Snapshot .NET ThreadPool and GC stats into Prometheus gauges.</summary>
    public static void UpdateRuntimeMetrics()
    {
        ThreadPool.GetAvailableThreads(out int workerAvail, out int ioAvail);
        ThreadPool.GetMaxThreads(out int workerMax, out int ioMax);

        ThreadPoolWorkerThreads.Set(workerAvail);
        ThreadPoolCompletionPortThreads.Set(ioAvail);
        ThreadPoolWorkerThreadsMax.Set(workerMax);
        ThreadPoolPendingWorkItems.Set(ThreadPool.PendingWorkItemCount);
        ProcessThreadCount.Set(System.Diagnostics.Process.GetCurrentProcess().Threads.Count);
        GcGen0Collections.Set(GC.CollectionCount(0));
        GcGen1Collections.Set(GC.CollectionCount(1));
        GcGen2Collections.Set(GC.CollectionCount(2));
        GcTotalMemory.Set(GC.GetTotalMemory(false));
    }
}
