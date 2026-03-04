using Prometheus;

namespace UniBank.SharedKernel.Metrics;

/// <summary>
/// Centralised Prometheus metric definitions for all UniBank services.
/// Consumers call the static properties to record observations; the underlying
/// collectors are created once and shared for the lifetime of the process.
/// </summary>
public static class MetricReporter
{
    // ── Transactions ────────────────────────────────────────────────────
    /// <summary>
    /// Total number of banking transactions processed, labelled by
    /// transaction type (purchase, withdrawal, balance, reversal, etc.)
    /// and outcome (approved / declined / error).
    /// </summary>
    public static readonly Counter TransactionsTotal = Prometheus.Metrics
        .CreateCounter(
            "unibank_transactions_total",
            "Total banking transactions processed.",
            new CounterConfiguration
            {
                LabelNames = new[] { "type", "status" }
            });

    // ── gRPC ────────────────────────────────────────────────────────────
    /// <summary>
    /// Histogram of gRPC call durations in seconds, labelled by service,
    /// method and gRPC status code.
    /// </summary>
    public static readonly Histogram GrpcDurationSeconds = Prometheus.Metrics
        .CreateHistogram(
            "unibank_grpc_duration_seconds",
            "Duration of gRPC calls in seconds.",
            new HistogramConfiguration
            {
                LabelNames = new[] { "grpc_service", "grpc_method", "grpc_status" },
                Buckets = new[] { 0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1.0, 2.5, 5.0, 10.0 }
            });

    // ── Redis Cache ─────────────────────────────────────────────────────
    /// <summary>Total Redis cache hits.</summary>
    public static readonly Counter RedisCacheHitsTotal = Prometheus.Metrics
        .CreateCounter(
            "unibank_redis_cache_hits_total",
            "Total number of Redis cache hits.",
            new CounterConfiguration
            {
                LabelNames = new[] { "cache_name" }
            });

    /// <summary>Total Redis cache misses.</summary>
    public static readonly Counter RedisCacheMissesTotal = Prometheus.Metrics
        .CreateCounter(
            "unibank_redis_cache_misses_total",
            "Total number of Redis cache misses.",
            new CounterConfiguration
            {
                LabelNames = new[] { "cache_name" }
            });

    // ── Connections ─────────────────────────────────────────────────────
    /// <summary>
    /// Current number of active connections (HTTP, gRPC, WebSocket, etc.).
    /// </summary>
    public static readonly Gauge ActiveConnections = Prometheus.Metrics
        .CreateGauge(
            "unibank_active_connections",
            "Number of currently active connections.",
            new GaugeConfiguration
            {
                LabelNames = new[] { "connection_type" }
            });

    // ── Message Queue ───────────────────────────────────────────────────
    /// <summary>
    /// Current depth of internal message / event queues.
    /// </summary>
    public static readonly Gauge MessageQueueDepth = Prometheus.Metrics
        .CreateGauge(
            "unibank_message_queue_depth",
            "Current depth of the message queue.",
            new GaugeConfiguration
            {
                LabelNames = new[] { "queue_name" }
            });

    // ── MQTT / Terminal Manager ─────────────────────────────────────────
    /// <summary>
    /// Number of payment terminals currently connected via MQTT.
    /// </summary>
    public static readonly Gauge MqttConnectedTerminals = Prometheus.Metrics
        .CreateGauge(
            "unibank_mqtt_connected_terminals",
            "Number of POS terminals currently connected via MQTT.",
            new GaugeConfiguration
            {
                LabelNames = new[] { "region" }
            });
}
