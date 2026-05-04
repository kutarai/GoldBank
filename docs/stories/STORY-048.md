# STORY-048: Terminal Status Monitoring

**Epic:** EPIC-009 Terminal Management & HSM
**Priority:** Must Have
**Story Points:** 3
**Status:** Not Started
**Assigned To:** Unassigned
**Created:** 2026-02-24
**Sprint:** 6

---

## User Story

As an **operations team**
I want to **see the status of all POS terminals**
So that **I can identify offline or faulty terminals and ensure merchant payment acceptance is not disrupted**

---

## Description

### Background

In GoldBank's target market — Southern Africa — terminal connectivity is often unreliable. Power outages, network instability, and remote merchant locations mean that terminals can go offline without warning. For a deploying institution, an offline terminal is lost revenue: merchants cannot accept digital payments, and customers revert to cash. The operations team needs real-time visibility into the health of their terminal fleet to proactively address issues before merchants call support.

Terminal status monitoring uses MQTT as the heartbeat channel. Each active terminal publishes a heartbeat message every 60 seconds to its status topic. The Terminal Manager satellite service subscribes to all terminal status topics via a wildcard subscription, processes incoming heartbeats, and maintains the current state of every terminal. When a terminal goes silent beyond a configurable threshold, the system raises an alert.

This monitoring data feeds into a Grafana dashboard providing a fleet-wide overview: total terminals, online/offline/fault counts, geographic distribution, battery levels, signal strength, and firmware version distribution. This dashboard is the operations team's primary tool for terminal fleet management.

**Functional Requirement:** FR-035

### Scope

**In scope:**
- MQTT heartbeat subscription and processing (wildcard `terminals/+/status`)
- Terminal state tracking: online, offline, fault
- Offline detection via background job comparing `last_heartbeat` against threshold
- Alerting via Wolverine event `TerminalOfflineAlert` when terminal exceeds offline threshold
- `TerminalService.GetTerminalStatus` and `GetAllTerminals` gRPC endpoints for dashboard queries
- Terminal status history for trend analysis
- Grafana dashboard configuration for terminal fleet overview
- Configurable heartbeat interval and offline thresholds per tenant

**Out of scope:**
- Terminal registration and provisioning (STORY-046)
- Key management monitoring (STORY-047)
- Remote software updates (STORY-049)
- Automated terminal restart or recovery actions (future enhancement)
- SMS/email notification delivery (handled by notification service)

### User Flow

**Real-time monitoring flow:**
1. Active terminal publishes heartbeat to `terminals/{terminal_id}/status` every 60 seconds (configurable)
2. Terminal Manager receives heartbeat via wildcard MQTT subscription
3. Heartbeat payload is parsed: terminal ID, status, battery level, signal strength, firmware version, last transaction time
4. Terminal Manager updates the `terminals` table: `last_heartbeat`, `status`, and stores diagnostic data
5. If terminal was previously offline and now sends a heartbeat, status transitions to `online` and a `TerminalBackOnlineEvent` is published

**Offline detection flow:**
1. Background job runs every 60 seconds (configurable)
2. Queries `terminals` table for active terminals where `last_heartbeat` < `NOW() - offline_threshold` (default 5 minutes)
3. Terminals exceeding the threshold are marked `offline`
4. If offline duration exceeds alert threshold (default 15 minutes), a `TerminalOfflineAlert` Wolverine event is published
5. Alert is routed to operations team via configured notification channel

**Dashboard flow:**
1. Operations user opens Grafana terminal fleet dashboard
2. Dashboard queries `GetAllTerminals` gRPC endpoint (or directly queries PostgreSQL via Grafana datasource)
3. Dashboard displays: terminal count by status, geographic map, battery level distribution, signal strength heatmap, firmware version breakdown, offline terminal list with duration

---

## Acceptance Criteria

- [ ] Terminal heartbeat messages are received via MQTT topic `terminals/{terminal_id}/status` and processed by the Terminal Manager
- [ ] Terminal status is tracked with three states: `online`, `offline`, `fault`
- [ ] Heartbeat updates the `last_heartbeat` timestamp and diagnostic data in the `terminals` table
- [ ] Terminal is marked `offline` when no heartbeat is received for more than 5 minutes (configurable per tenant)
- [ ] `TerminalOfflineAlert` Wolverine event is published when a terminal has been offline for more than 15 minutes (configurable per tenant)
- [ ] `TerminalBackOnlineEvent` is published when a previously offline terminal resumes heartbeats
- [ ] `TerminalService.GetTerminalStatus` returns current status, last heartbeat time, battery level, signal strength, and firmware version for a single terminal
- [ ] `TerminalService.GetAllTerminals` returns a paginated list of all terminals for a tenant with filtering by status, merchant, and model
- [ ] Last communication timestamp is displayed for each terminal
- [ ] Grafana dashboard shows: total terminals, online count, offline count, fault count, and a list of offline terminals with offline duration
- [ ] Heartbeat interval and offline thresholds are configurable per tenant

---

## Technical Notes

### Components

| Component | Location | Purpose |
|-----------|----------|---------|
| `HeartbeatProcessor.cs` | `src/Satellites/GoldBank.TerminalManager/Mqtt/` | Processes incoming MQTT heartbeat messages |
| `OfflineDetectionJob.cs` | `src/Satellites/GoldBank.TerminalManager/Jobs/` | Background job that detects offline terminals |
| `TerminalOfflineAlert.cs` | `src/Shared/GoldBank.Events/Terminal/` | Wolverine event for offline alert |
| `TerminalBackOnlineEvent.cs` | `src/Shared/GoldBank.Events/Terminal/` | Wolverine event for terminal recovery |
| `TerminalStatusHandler.cs` | `src/Satellites/GoldBank.TerminalManager/Handlers/` | Wolverine handler for alert routing |
| `TerminalDashboardQueries.cs` | `src/Satellites/GoldBank.TerminalManager/Queries/` | Read-model queries for dashboard data |
| `terminal-fleet-dashboard.json` | `infrastructure/grafana/dashboards/` | Grafana dashboard definition |

### API / gRPC Endpoints

**Additions to `terminal_service.proto`:**

```protobuf
message GetTerminalStatusRequest {
  int32 terminal_id = 1;
  string tenant_id = 2;
}

message GetTerminalStatusResponse {
  int32 terminal_id = 1;
  string terminal_device_id = 2;
  string merchant_id = 3;
  string status = 4;               // "online", "offline", "fault"
  int64 last_heartbeat_unix = 5;
  float battery_level = 6;         // 0.0 - 1.0
  float signal_strength = 7;       // 0.0 - 1.0
  string firmware_version = 8;
  int64 last_transaction_time_unix = 9;
  int64 offline_since_unix = 10;   // 0 if online
}

message GetAllTerminalsRequest {
  string tenant_id = 1;
  string status_filter = 2;        // Optional: "online", "offline", "fault", "" for all
  string merchant_id_filter = 3;   // Optional: filter by merchant
  string model_filter = 4;         // Optional: filter by model
  int32 page = 5;                  // Pagination: 1-indexed
  int32 page_size = 6;             // Default: 50, max: 200
  string sort_by = 7;              // "last_heartbeat", "status", "merchant_id"
  string sort_order = 8;           // "asc" or "desc"
}

message GetAllTerminalsResponse {
  repeated TerminalSummary terminals = 1;
  int32 total_count = 2;
  int32 page = 3;
  int32 page_size = 4;
}

message TerminalSummary {
  int32 terminal_id = 1;
  string terminal_device_id = 2;
  string merchant_id = 3;
  string merchant_name = 4;
  string model = 5;
  string status = 6;
  int64 last_heartbeat_unix = 7;
  float battery_level = 8;
  float signal_strength = 9;
  string firmware_version = 10;
}

message GetTerminalFleetSummaryRequest {
  string tenant_id = 1;
}

message GetTerminalFleetSummaryResponse {
  int32 total_terminals = 1;
  int32 online_count = 2;
  int32 offline_count = 3;
  int32 fault_count = 4;
  int32 registered_count = 5;      // Provisioned but not yet active
  int32 suspended_count = 6;
  float average_battery_level = 7;
  map<string, int32> model_distribution = 8;
  map<string, int32> firmware_distribution = 9;
}
```

### MQTT Heartbeat Protocol

**Heartbeat payload** (terminal publishes to `terminals/{terminal_id}/status`):

```json
{
  "message_type": "heartbeat",
  "terminal_id": "PAX-A920-SN00012345",
  "status": "online",
  "battery_level": 0.85,
  "signal_strength": 0.72,
  "firmware_version": "3.2.1",
  "app_version": "1.0.4",
  "last_transaction_time": "2026-02-24T09:45:00Z",
  "memory_usage_percent": 45,
  "storage_usage_percent": 22,
  "uptime_seconds": 86400,
  "error_codes": [],
  "timestamp": "2026-02-24T10:00:00Z"
}
```

**Fault heartbeat** (terminal reports error condition):

```json
{
  "message_type": "heartbeat",
  "terminal_id": "PAX-A920-SN00012345",
  "status": "fault",
  "battery_level": 0.15,
  "signal_strength": 0.30,
  "firmware_version": "3.2.1",
  "error_codes": ["PRINTER_FAULT", "LOW_BATTERY"],
  "timestamp": "2026-02-24T10:01:00Z"
}
```

### Database Changes

**terminal_status_history table** (for trend analysis):

```sql
CREATE TABLE terminal_manager.terminal_status_history (
    id              BIGSERIAL PRIMARY KEY,
    tenant_id       VARCHAR(50) NOT NULL,
    terminal_id     INT NOT NULL REFERENCES terminal_manager.terminals(id),
    status          VARCHAR(20) NOT NULL,
    battery_level   REAL,
    signal_strength REAL,
    error_codes     TEXT[],
    recorded_at     TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Partition by month for efficient data management
CREATE INDEX idx_status_history_terminal ON terminal_manager.terminal_status_history (terminal_id, recorded_at DESC);
CREATE INDEX idx_status_history_tenant ON terminal_manager.terminal_status_history (tenant_id, recorded_at DESC);

-- Retention: status history older than 90 days is automatically purged
-- Implemented via pg_cron or application-level cleanup job
```

**Additional columns on terminals table** (extending STORY-046 schema):

```sql
ALTER TABLE terminal_manager.terminals
    ADD COLUMN battery_level       REAL,
    ADD COLUMN signal_strength     REAL,
    ADD COLUMN app_version         VARCHAR(50),
    ADD COLUMN memory_usage_pct    SMALLINT,
    ADD COLUMN storage_usage_pct   SMALLINT,
    ADD COLUMN uptime_seconds      INT,
    ADD COLUMN error_codes         TEXT[],
    ADD COLUMN offline_since       TIMESTAMPTZ;
```

### Offline Detection Configuration

```csharp
public class TerminalMonitoringOptions
{
    /// <summary>Interval for the offline detection background job.</summary>
    public TimeSpan DetectionInterval { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>Time since last heartbeat before terminal is marked offline.</summary>
    public TimeSpan OfflineThreshold { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>Time offline before an alert is fired.</summary>
    public TimeSpan AlertThreshold { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>Expected heartbeat interval from terminals.</summary>
    public TimeSpan HeartbeatInterval { get; set; } = TimeSpan.FromSeconds(60);
}
```

### Security Considerations

- **Heartbeat Authenticity:** Heartbeat messages are only accepted from terminals that have an `active` status in the database. Heartbeats from unknown terminal IDs are logged and discarded.
- **Status Data Integrity:** While heartbeat data is diagnostic (not transactional), tampered status data could mask a compromised terminal. MQTT TLS ensures transport integrity. Future enhancement: HMAC on heartbeat payloads using TMK-MAC.
- **Tenant Isolation:** Dashboard queries enforce tenant context. Operations team for Tenant A cannot see Tenant B's terminal fleet. The `GetAllTerminals` endpoint requires `tenant_id` and validates it against the caller's JWT.
- **Rate Limiting:** The heartbeat processor enforces a minimum interval between heartbeat updates per terminal (default 30 seconds) to prevent a misbehaving terminal from overwhelming the system with status messages.
- **Data Retention:** Status history is retained for 90 days. Older data is purged to comply with data minimization principles and manage storage.

### Edge Cases

- **Burst of heartbeats after network recovery:** When a terminal regains connectivity after a network outage, it may send multiple buffered heartbeats. The processor deduplicates by accepting only the most recent heartbeat per terminal within a 30-second window.
- **Clock skew on terminal:** If a terminal's clock is significantly skewed, the `timestamp` in the heartbeat may be inaccurate. The server uses its own receive time for `last_heartbeat`, not the terminal's reported timestamp. The terminal timestamp is stored separately for diagnostics.
- **Mass offline event:** If many terminals go offline simultaneously (e.g., network outage affecting a region), the system batches alerts rather than firing individual alerts for each terminal. A `MassOfflineAlert` is raised when >10% of a tenant's fleet goes offline within a 5-minute window.
- **Terminal in fault state:** A terminal reporting `fault` status is treated differently from `offline`. Fault terminals are still communicating but have hardware issues. They appear in a separate section of the dashboard with their error codes.
- **Heartbeat from decommissioned terminal:** Heartbeats from terminals with status `decommissioned` are discarded. A decommission command is re-published to the terminal's command topic in case the original was not received.
- **Database write pressure:** With hundreds of terminals sending heartbeats every 60 seconds, the status history table grows rapidly. Write batching (buffer heartbeats for 10 seconds, bulk insert) reduces database pressure. The primary `terminals` table update uses `ON CONFLICT DO UPDATE` for idempotency.

---

## Dependencies

**Prerequisite Stories:**
- STORY-046: Terminal Registration & Provisioning — terminal records and MQTT topic structure must exist

**Blocked Stories:**
- Operational dashboard stories (terminal fleet visibility)
- SLA monitoring and reporting for terminal uptime

**External Dependencies:**
- MQTT broker operational with wildcard subscription support
- Grafana instance deployed and accessible to operations team
- PostgreSQL with sufficient write throughput for heartbeat volume

---

## Definition of Done

- [ ] Code implemented and committed
- [ ] Unit tests written and passing (>=80% coverage) — heartbeat processing, offline detection logic, alert thresholds
- [ ] Integration tests passing — MQTT heartbeat subscription, database updates, Wolverine event publishing
- [ ] Offline detection tested — terminal goes offline, is detected within threshold, alert fires
- [ ] Back-online detection tested — previously offline terminal resumes, event fires
- [ ] Fleet summary endpoint returns accurate counts
- [ ] Grafana dashboard configured and displaying terminal fleet data
- [ ] Pagination and filtering on `GetAllTerminals` verified
- [ ] Code reviewed and approved
- [ ] Documentation updated
- [ ] Acceptance criteria validated
- [ ] Deployed to staging

---

## Progress Tracking

**Status History:**
- 2026-02-24: Created

**Actual Effort:** TBD

---

**This story was created using BMAD Method v6 - Phase 4 (Implementation)**
