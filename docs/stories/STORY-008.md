# STORY-008: Monitoring & Logging Stack

**Epic:** EPIC-000 Infrastructure
**Priority:** Must Have
**Story Points:** 5
**Status:** Not Started
**Assigned To:** Unassigned
**Created:** 2026-02-24
**Sprint:** 1

---

## User Story

As a developer,
I want Prometheus, Grafana, and ELK configured,
So that we can monitor system health and search logs.

---

## Description

### Background
Operating a financial platform serving the unbanked population in Southern Africa demands comprehensive observability. Service outages, slow transactions, and security incidents must be detected and resolved rapidly. This story establishes the three pillars of observability for GoldBank:

1. **Metrics (Prometheus + Grafana):** Collects and visualizes quantitative data -- request rates, error rates, latency percentiles, active connections, Redis cache hit rates, transaction throughput.
2. **Logging (Serilog + Elasticsearch + Kibana):** Structured JSON logs from all services, centrally aggregated and searchable. Enables root cause analysis across services.
3. **Alerting (Prometheus Alertmanager):** Automated alerts for critical conditions such as high error rates, elevated latency, and service unavailability.

All monitoring infrastructure runs as Docker containers alongside the application services (configured in STORY-002). This story adds the application-side instrumentation and monitoring configuration.

### Scope

**In scope:**
- Prometheus configuration (`prometheus.yml`) with scrape targets for all .NET services
- Grafana dashboard provisioning with pre-built dashboards for service health, gRPC metrics, and system overview
- Serilog configuration in all .NET services: structured JSON output, Elasticsearch sink
- `prometheus-net` integration in all ASP.NET Core services for .NET runtime metrics and custom metrics
- Elasticsearch index templates for log data
- Kibana index patterns and saved searches
- Prometheus alert rules for critical conditions
- Custom metrics: transaction throughput, gRPC latency histograms, Redis cache hit rates
- Health check dashboard

**Out of scope:**
- Distributed tracing (OpenTelemetry/Jaeger -- future story)
- APM (Application Performance Monitoring) tools
- Log rotation and retention policies for production
- PagerDuty, Slack, or email alert integration (future story)
- Custom Grafana plugins
- Log-based anomaly detection

### User Flow

**Developer Monitoring Flow:**
1. Developer starts the full stack via `docker compose up`
2. Developer opens Grafana at `http://localhost:3000` (admin/admin)
3. Developer views the "GoldBank Service Health" dashboard showing all services
4. Developer views the "gRPC Metrics" dashboard showing request rates and latencies
5. Developer opens Kibana at `http://localhost:5601`
6. Developer searches logs by service name, tenant ID, or correlation ID
7. Developer investigates an error by filtering logs to the specific request

**Alert Flow:**
1. A service's error rate exceeds 1% for 5 minutes
2. Prometheus evaluates the alert rule and fires the alert
3. Alert appears in Grafana's alert panel
4. Developer investigates using Grafana dashboards and Kibana logs

---

## Acceptance Criteria

- [ ] Prometheus scrapes metrics from all .NET services at 15-second intervals
- [ ] All .NET services expose Prometheus metrics endpoint at `/metrics`
- [ ] `prometheus-net` is integrated in all ASP.NET Core services exposing: process metrics, .NET runtime metrics, HTTP/gRPC request metrics
- [ ] Custom metrics are defined: `goldbank_transactions_total` (counter), `goldbank_grpc_duration_seconds` (histogram), `goldbank_redis_cache_hits_total` (counter), `goldbank_redis_cache_misses_total` (counter), `goldbank_active_connections` (gauge)
- [ ] Grafana is provisioned with at least 3 pre-built dashboards: Service Health, gRPC Metrics, Transaction Overview
- [ ] Grafana datasource for Prometheus is pre-configured via provisioning
- [ ] Serilog is configured in all .NET services with structured JSON format
- [ ] Serilog writes to Console sink (development) and Elasticsearch sink
- [ ] All log entries include: `Timestamp`, `Level`, `MessageTemplate`, `ServiceName`, `TenantId`, `CorrelationId`, `MachineName`
- [ ] Elasticsearch receives logs from all services within 30 seconds of emission
- [ ] Kibana index pattern `goldbank-logs-*` is pre-configured
- [ ] Prometheus alert rules fire when: error rate > 1% for 5min, p95 latency > 2s for 5min, service health check fails for 1min
- [ ] Grafana displays active alerts from Prometheus
- [ ] All monitoring containers are healthy in Docker Compose

---

## Technical Notes

### Components

**Affected Projects (Application-Side Instrumentation):**
- All .NET service projects: GoldBank.Gateway, GoldBank.Core, GoldBank.Switching, GoldBank.TerminalManager, GoldBank.HSM, GoldBank.Admin, GoldBank.Reporting, GoldBank.Notifications

**Configuration Files:**
```
monitoring/
  prometheus/
    prometheus.yml
    alert-rules.yml
  grafana/
    provisioning/
      datasources/
        prometheus.yml
      dashboards/
        dashboard.yml
    dashboards/
      service-health.json
      grpc-metrics.json
      transaction-overview.json
  elasticsearch/
    index-template.json
  kibana/
    saved-objects.ndjson
```

### Prometheus Configuration

**prometheus.yml:**
```yaml
global:
  scrape_interval: 15s
  evaluation_interval: 15s

rule_files:
  - "alert-rules.yml"

scrape_configs:
  - job_name: 'goldbank-gateway'
    static_configs:
      - targets: ['gateway:5000']
    metrics_path: '/metrics'

  - job_name: 'goldbank-core'
    static_configs:
      - targets: ['core:5002']
    metrics_path: '/metrics'

  - job_name: 'goldbank-switching'
    static_configs:
      - targets: ['switching:5003']
    metrics_path: '/metrics'

  - job_name: 'goldbank-terminal-manager'
    static_configs:
      - targets: ['terminal-manager:5004']
    metrics_path: '/metrics'

  - job_name: 'goldbank-hsm'
    static_configs:
      - targets: ['hsm:5005']
    metrics_path: '/metrics'

  - job_name: 'goldbank-admin'
    static_configs:
      - targets: ['admin:5010']
    metrics_path: '/metrics'

  - job_name: 'goldbank-reporting'
    static_configs:
      - targets: ['reporting:5006']
    metrics_path: '/metrics'

  - job_name: 'goldbank-notifications'
    static_configs:
      - targets: ['notifications:5007']
    metrics_path: '/metrics'

  - job_name: 'postgres'
    static_configs:
      - targets: ['postgres-exporter:9187']

  - job_name: 'redis'
    static_configs:
      - targets: ['redis-exporter:9121']
```

### Prometheus Alert Rules

**alert-rules.yml:**
```yaml
groups:
  - name: goldbank-service-alerts
    rules:
      - alert: HighErrorRate
        expr: |
          sum(rate(grpc_server_handled_total{grpc_code!="OK"}[5m])) by (grpc_service)
          /
          sum(rate(grpc_server_handled_total[5m])) by (grpc_service)
          > 0.01
        for: 5m
        labels:
          severity: critical
        annotations:
          summary: "High error rate on {{ $labels.grpc_service }}"
          description: "Error rate is {{ $value | humanizePercentage }} (threshold: 1%)"

      - alert: HighLatency
        expr: |
          histogram_quantile(0.95, sum(rate(grpc_server_handling_seconds_bucket[5m])) by (le, grpc_service))
          > 2
        for: 5m
        labels:
          severity: warning
        annotations:
          summary: "High p95 latency on {{ $labels.grpc_service }}"
          description: "p95 latency is {{ $value }}s (threshold: 2s)"

      - alert: ServiceDown
        expr: up == 0
        for: 1m
        labels:
          severity: critical
        annotations:
          summary: "Service {{ $labels.job }} is down"
          description: "{{ $labels.instance }} has been unreachable for 1 minute"

      - alert: HighMemoryUsage
        expr: |
          process_resident_memory_bytes / 1024 / 1024 > 512
        for: 10m
        labels:
          severity: warning
        annotations:
          summary: "High memory usage on {{ $labels.job }}"
          description: "Memory usage is {{ $value | humanize }}MB"

      - alert: PostgreSQLConnectionPoolExhausted
        expr: |
          pg_stat_activity_count > 90
        for: 5m
        labels:
          severity: critical
        annotations:
          summary: "PostgreSQL connection pool near exhaustion"
          description: "Active connections: {{ $value }}"

      - alert: RedisHighMemory
        expr: |
          redis_memory_used_bytes / redis_memory_max_bytes > 0.8
        for: 5m
        labels:
          severity: warning
        annotations:
          summary: "Redis memory usage above 80%"
```

### .NET Application Instrumentation

**Shared Serilog Configuration (via extension method):**
```csharp
// GoldBank.SharedKernel/Logging/SerilogExtensions.cs
public static class SerilogExtensions
{
    public static IHostBuilder UseGoldBankSerilog(
        this IHostBuilder hostBuilder, string serviceName)
    {
        return hostBuilder.UseSerilog((context, services, config) =>
        {
            config
                .ReadFrom.Configuration(context.Configuration)
                .Enrich.FromLogContext()
                .Enrich.WithMachineName()
                .Enrich.WithEnvironmentName()
                .Enrich.WithProperty("ServiceName", serviceName)
                .Enrich.WithProperty("ServiceVersion",
                    Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "0.0.0")
                .WriteTo.Console(new JsonFormatter())
                .WriteTo.Elasticsearch(new ElasticsearchSinkOptions(
                    new Uri(context.Configuration["Elasticsearch:Url"]
                            ?? "http://elasticsearch:9200"))
                {
                    AutoRegisterTemplate = true,
                    AutoRegisterTemplateVersion = AutoRegisterTemplateVersion.ESv8,
                    IndexFormat = $"goldbank-logs-{serviceName.ToLower()}-{{0:yyyy.MM.dd}}",
                    NumberOfShards = 1,
                    NumberOfReplicas = 0, // Dev setting
                    BatchPostingLimit = 50,
                    Period = TimeSpan.FromSeconds(5)
                });
        });
    }
}
```

**Prometheus Metrics Integration:**
```csharp
// Program.cs for each service
var builder = WebApplication.CreateBuilder(args);

// Add Prometheus metrics
builder.Services.AddSingleton<MetricReporter>(); // Custom metrics
builder.Host.UseGoldBankSerilog("GoldBank.Gateway");

var app = builder.Build();

// Prometheus metrics endpoint
app.UseHttpMetrics(); // HTTP request metrics
app.MapMetrics();     // Expose /metrics endpoint

app.Run();
```

**Custom Metrics Reporter:**
```csharp
// GoldBank.SharedKernel/Metrics/MetricReporter.cs
public class MetricReporter
{
    // Transaction counters
    public static readonly Counter TransactionsTotal = Metrics.CreateCounter(
        "goldbank_transactions_total",
        "Total number of transactions processed",
        new CounterConfiguration
        {
            LabelNames = new[] { "type", "status", "tenant_id" }
        });

    // gRPC request duration
    public static readonly Histogram GrpcDuration = Metrics.CreateHistogram(
        "goldbank_grpc_duration_seconds",
        "Duration of gRPC calls in seconds",
        new HistogramConfiguration
        {
            LabelNames = new[] { "service", "method", "status" },
            Buckets = new[] { 0.01, 0.05, 0.1, 0.25, 0.5, 1.0, 2.0, 5.0, 10.0 }
        });

    // Active connections
    public static readonly Gauge ActiveConnections = Metrics.CreateGauge(
        "goldbank_active_connections",
        "Number of active connections",
        new GaugeConfiguration
        {
            LabelNames = new[] { "service" }
        });

    // Redis cache metrics
    public static readonly Counter RedisCacheHits = Metrics.CreateCounter(
        "goldbank_redis_cache_hits_total",
        "Total Redis cache hits",
        new CounterConfiguration
        {
            LabelNames = new[] { "cache_name" }
        });

    public static readonly Counter RedisCacheMisses = Metrics.CreateCounter(
        "goldbank_redis_cache_misses_total",
        "Total Redis cache misses",
        new CounterConfiguration
        {
            LabelNames = new[] { "cache_name" }
        });

    // Wolverine message queue depth
    public static readonly Gauge MessageQueueDepth = Metrics.CreateGauge(
        "goldbank_message_queue_depth",
        "Current message queue depth",
        new GaugeConfiguration
        {
            LabelNames = new[] { "queue_name" }
        });

    // MQTT connected terminals
    public static readonly Gauge MqttConnectedTerminals = Metrics.CreateGauge(
        "goldbank_mqtt_connected_terminals",
        "Number of MQTT-connected terminals");
}
```

### Grafana Dashboard Provisioning

**provisioning/datasources/prometheus.yml:**
```yaml
apiVersion: 1
datasources:
  - name: Prometheus
    type: prometheus
    access: proxy
    url: http://prometheus:9090
    isDefault: true
    editable: true
  - name: Elasticsearch
    type: elasticsearch
    access: proxy
    url: http://elasticsearch:9200
    database: "goldbank-logs-*"
    jsonData:
      timeField: "@timestamp"
      esVersion: "8.0.0"
    editable: true
```

**provisioning/dashboards/dashboard.yml:**
```yaml
apiVersion: 1
providers:
  - name: 'GoldBank Dashboards'
    orgId: 1
    folder: 'GoldBank'
    type: file
    disableDeletion: false
    editable: true
    updateIntervalSeconds: 10
    allowUiUpdates: true
    options:
      path: /var/lib/grafana/dashboards
      foldersFromFilesStructure: true
```

### Grafana Dashboard Panels (Service Health)

Key panels for the Service Health dashboard:
- **Service Status Grid:** UP/DOWN status for all services (based on `up` metric)
- **Request Rate:** `sum(rate(grpc_server_started_total[5m])) by (grpc_service)`
- **Error Rate:** `sum(rate(grpc_server_handled_total{grpc_code!="OK"}[5m])) by (grpc_service)`
- **p50/p95/p99 Latency:** `histogram_quantile(0.95, sum(rate(grpc_server_handling_seconds_bucket[5m])) by (le))`
- **Active gRPC Connections:** `grpc_server_started_total - grpc_server_handled_total`
- **Redis Memory Usage:** `redis_memory_used_bytes`
- **PostgreSQL Active Connections:** `pg_stat_activity_count`
- **CPU & Memory per Service:** `process_cpu_seconds_total`, `process_resident_memory_bytes`

Key panels for the Transaction Overview dashboard:
- **Transactions Per Second:** `rate(goldbank_transactions_total[1m])`
- **Transaction Volume by Type:** `goldbank_transactions_total` grouped by `type`
- **Failed Transactions:** `goldbank_transactions_total{status="failed"}`
- **Average Transaction Duration:** `goldbank_grpc_duration_seconds` for payment methods
- **Cache Hit Rate:** `goldbank_redis_cache_hits_total / (goldbank_redis_cache_hits_total + goldbank_redis_cache_misses_total)`
- **Message Queue Depth:** `goldbank_message_queue_depth`

### Elasticsearch Index Template

```json
{
  "index_patterns": ["goldbank-logs-*"],
  "template": {
    "settings": {
      "number_of_shards": 1,
      "number_of_replicas": 0,
      "index.lifecycle.name": "goldbank-log-retention",
      "index.lifecycle.rollover_alias": "goldbank-logs"
    },
    "mappings": {
      "properties": {
        "@timestamp": { "type": "date" },
        "level": { "type": "keyword" },
        "messageTemplate": { "type": "text" },
        "message": { "type": "text" },
        "ServiceName": { "type": "keyword" },
        "TenantId": { "type": "keyword" },
        "CorrelationId": { "type": "keyword" },
        "MachineName": { "type": "keyword" },
        "EnvironmentName": { "type": "keyword" },
        "exception": { "type": "text" },
        "properties": { "type": "object", "dynamic": true }
      }
    }
  }
}
```

### Serilog appsettings.json Configuration

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "Microsoft.Hosting.Lifetime": "Information",
        "Grpc": "Information",
        "System.Net.Http": "Warning"
      }
    },
    "Enrich": ["FromLogContext", "WithMachineName", "WithEnvironmentName"],
    "Properties": {
      "Application": "GoldBank"
    }
  },
  "Elasticsearch": {
    "Url": "http://elasticsearch:9200"
  }
}
```

### API / gRPC Endpoints

**Metrics Endpoint (all services):**
- Path: `/metrics`
- Method: HTTP GET
- No authentication (internal network only)
- Returns: Prometheus text exposition format

**Health Endpoint (all services):**
- Path: `/health`
- Method: HTTP GET or gRPC Health Check
- Returns: Service health status

### Database Changes
None directly. Elasticsearch creates its own indexes based on log data.

### Security Considerations
- `/metrics` endpoint must not be exposed externally; only accessible from Prometheus within the Docker network
- Grafana default credentials (admin/admin) must be changed in staging/production
- Elasticsearch security (xpack) should be enabled in production with authentication
- Logs may contain sensitive information despite PII masking -- Elasticsearch access should be restricted
- Kibana access should require authentication in staging/production
- Alert notifications should be sent over secure channels
- Monitoring data retention should comply with data protection regulations

### Edge Cases
- Elasticsearch unavailable: Serilog should not block the application; use `BufferBaseFilename` for local buffering
- Prometheus scrape timeout: If a service is slow to respond, Prometheus records a scrape failure (visible in `up` metric)
- Grafana provisioning errors: If dashboard JSON is malformed, Grafana starts but the dashboard is unavailable; validate JSON before deployment
- High log volume: During load spikes, Elasticsearch may fall behind; use bulk indexing and appropriate buffer sizes
- Metric cardinality explosion: Avoid using high-cardinality labels (e.g., user IDs) in Prometheus metrics
- Clock synchronization: All services must use NTP to ensure log timestamps are consistent for cross-service correlation
- Docker resource limits: Elasticsearch is memory-hungry (minimum 512 MB heap); document Docker memory allocation requirements

---

## Dependencies

**Prerequisite Stories:**
- STORY-002: Docker Compose Development Environment (Prometheus, Grafana, ELK containers must be defined)

**Blocked Stories:**
- No stories are directly blocked, but all subsequent stories benefit from monitoring and logging

**External Dependencies:**
- Docker images: `prom/prometheus`, `grafana/grafana`, `elasticsearch:8.x`, `kibana:8.x`
- NuGet packages: `prometheus-net.AspNetCore`, `Serilog.AspNetCore`, `Serilog.Sinks.Elasticsearch`, `Serilog.Enrichers.Environment`
- Optional: `postgres_exporter` and `redis_exporter` Docker images for database/cache metrics

---

## Definition of Done

- [ ] Code implemented and committed
- [ ] Unit tests written and passing (>=80% coverage) -- for custom metric reporter utility
- [ ] Integration tests passing -- Prometheus scrapes metrics, logs appear in Elasticsearch
- [ ] Code reviewed and approved
- [ ] Documentation updated (monitoring runbook, dashboard descriptions, alert escalation guide)
- [ ] Acceptance criteria validated
- [ ] Deployed to staging

---

## Progress Tracking

**Status History:**
- 2026-02-24: Created

**Actual Effort:** TBD

---

**This story was created using BMAD Method v6 - Phase 4 (Implementation)**
