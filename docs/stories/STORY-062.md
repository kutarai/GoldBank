# STORY-062: Real-Time Transaction Dashboard

**Epic:** EPIC-012 Reporting & Analytics
**Priority:** Must Have
**Story Points:** 5
**Status:** Not Started
**Assigned To:** Unassigned
**Created:** 2026-02-24
**Sprint:** 7

---

## User Story

As an admin
I want a real-time transaction dashboard
So that I can monitor platform activity at a glance and quickly identify anomalies

---

## Description

### Background
A real-time operational dashboard is essential for monitoring the health and activity of the GoldBank platform. Operations staff need an at-a-glance view of transaction throughput, success rates, active users, and top-performing merchants. The dashboard provides both real-time metrics (updated every few seconds) and rolling historical context (last 24 hours). This serves as the primary "heartbeat" view for the platform, enabling rapid identification of issues such as elevated failure rates, throughput drops, or unusual patterns.

The dashboard leverages Blazor Server's built-in SignalR connectivity for real-time updates, combined with Redis counters for low-latency metric reads and PostgreSQL aggregates for historical data.

### Scope
**In scope:**
- Live TPS (transactions per second) gauge
- Transaction volume line chart (last 24 hours, per minute granularity)
- Transaction value bar chart (by type, last 24 hours)
- Success/failure ratio donut chart
- Active users count
- Top merchants widget
- Configurable auto-refresh interval (5s, 15s, 30s, 60s)
- Tenant-scoped dashboard for `tenant_admin`

**Out of scope:**
- Alerting and threshold notifications (separate alerting system)
- Historical trend analysis beyond 24 hours (covered by other reporting stories)
- Custom dashboard builder / drag-and-drop widgets
- Mobile-optimised dashboard layout

### User Flow
1. Admin logs in and lands on the Dashboard page (default landing page)
2. Dashboard immediately begins loading current metrics
3. Real-time widgets display: TPS gauge (animated), transaction volume chart, value by type chart, success/failure donut, active users count, top 10 merchants
4. Data updates automatically based on configured refresh interval (default 15 seconds)
5. Admin can change the auto-refresh interval using a dropdown in the dashboard header
6. Admin can click on any widget to drill down (e.g., clicking the failure segment of the donut chart navigates to Transaction Search filtered by failed status)
7. If the admin is a `tenant_admin`, all metrics are scoped to their tenant
8. `super_admin` sees platform-wide metrics with an optional tenant filter dropdown

---

## Acceptance Criteria

- [ ] Dashboard displays a TPS (transactions per second) gauge showing current throughput
- [ ] Dashboard displays a line chart of transaction volume over the last 24 hours (per-minute granularity)
- [ ] Dashboard displays a bar chart of transaction value grouped by type (NFC, QR, P2P, bill pay, cash-in, cash-out)
- [ ] Dashboard displays a donut chart showing success/failure/pending transaction ratio
- [ ] Dashboard displays the count of currently active users (users who transacted in the last 30 minutes)
- [ ] Dashboard displays a top 10 merchants widget ranked by transaction volume (last 24 hours)
- [ ] Dashboard auto-refreshes at a configurable interval: 5s, 15s, 30s, 60s (default 15s)
- [ ] Auto-refresh interval persists across page navigation within the session
- [ ] Real-time updates use Blazor Server SignalR for push-based metric delivery
- [ ] Clicking on chart segments navigates to the relevant filtered view (e.g., failed transactions)
- [ ] `tenant_admin` sees only their tenant's metrics
- [ ] `super_admin` sees platform-wide metrics with an optional tenant filter
- [ ] Dashboard loads initial data within 2 seconds
- [ ] Dashboard gracefully handles SignalR disconnection (shows "Reconnecting..." indicator)

---

## Technical Notes

### Components
- **Blazor Pages:**
  - `Pages/Dashboard/Dashboard.razor` -- main dashboard page composing all widgets
- **Components:**
  - `Components/Dashboard/TPSGauge.razor` -- animated gauge showing current TPS
  - `Components/Dashboard/TransactionVolumeChart.razor` -- line chart (last 24h, per-minute)
  - `Components/Dashboard/TransactionValueChart.razor` -- bar chart grouped by transaction type
  - `Components/Dashboard/SuccessRatioDonut.razor` -- donut chart for success/failure/pending ratios
  - `Components/Dashboard/ActiveUsersCard.razor` -- card displaying active user count
  - `Components/Dashboard/TopMerchantsTable.razor` -- top 10 merchants by volume
  - `Components/Dashboard/RefreshIntervalSelector.razor` -- dropdown for auto-refresh interval
  - `Components/Dashboard/DashboardHeader.razor` -- title, tenant filter (super_admin), refresh controls
  - `Components/Dashboard/ReconnectingOverlay.razor` -- shown during SignalR reconnection
- **Services:**
  - `Services/DashboardMetricsService.cs` -- fetches metrics from Redis counters and PostgreSQL aggregates
  - `Services/RealTimeFeedService.cs` -- manages SignalR subscription for real-time metric push
  - `Hubs/DashboardHub.cs` -- SignalR hub broadcasting metric updates
- **Background Workers:**
  - `Workers/MetricsAggregatorWorker.cs` -- aggregates transaction data into Redis counters every minute

### API / gRPC Endpoints
```protobuf
service ReportingService {
  rpc GetDashboard (GetDashboardRequest) returns (DashboardResponse);
  rpc StreamDashboardMetrics (StreamDashboardMetricsRequest) returns (stream DashboardMetricUpdate);
}

message GetDashboardRequest {
  string tenant_id = 1;           // optional, auto-set for tenant_admin
}

message DashboardResponse {
  double current_tps = 1;
  int64 transactions_last_24h = 2;
  int64 total_value_last_24h = 3;  // in minor units
  string currency = 4;
  int32 active_users = 5;
  SuccessRatio success_ratio = 6;
  repeated TimeSeriesPoint volume_series = 7;       // last 24h, per-minute
  repeated TypeValueBreakdown value_by_type = 8;
  repeated TopMerchant top_merchants = 9;           // top 10
}

message SuccessRatio {
  int64 success_count = 1;
  int64 failure_count = 2;
  int64 pending_count = 3;
  double success_percentage = 4;
  double failure_percentage = 5;
}

message TimeSeriesPoint {
  google.protobuf.Timestamp timestamp = 1;
  int64 count = 2;
  int64 value_minor_units = 3;
}

message TypeValueBreakdown {
  string transaction_type = 1;     // nfc, qr, p2p, bill_pay, cash_in, cash_out
  int64 count = 2;
  int64 total_value_minor_units = 3;
}

message TopMerchant {
  string merchant_id = 1;
  string merchant_name = 2;
  int64 transaction_count = 3;
  int64 total_value_minor_units = 4;
}

message StreamDashboardMetricsRequest {
  string tenant_id = 1;
  int32 interval_seconds = 2;     // 5, 15, 30, 60
}

message DashboardMetricUpdate {
  double current_tps = 1;
  int32 active_users = 2;
  SuccessRatio success_ratio = 3;
  TimeSeriesPoint latest_volume_point = 4;
  google.protobuf.Timestamp timestamp = 5;
}
```

### Database Changes
No new tables required. The dashboard reads from existing tables:
- `{tenant_schema}.transactions` -- for historical aggregation
- `{tenant_schema}.merchants` -- for top merchants

Redis key structure for real-time metrics:
```
metrics:{tenant_id}:tx_count:{YYYYMMDD}:{HHmm}     -> INT (transaction count per minute)
metrics:{tenant_id}:tx_value:{YYYYMMDD}:{HHmm}      -> INT (total value per minute, minor units)
metrics:{tenant_id}:tx_count_by_type:{type}:{YYYYMMDD}:{HHmm} -> INT
metrics:{tenant_id}:tx_status:{status}:{YYYYMMDD}:{HH}        -> INT (hourly status counts)
metrics:{tenant_id}:active_users                     -> SET (account IDs active in last 30 min)
metrics:global:tx_count:{YYYYMMDD}:{HHmm}           -> INT (platform-wide)
metrics:global:tx_value:{YYYYMMDD}:{HHmm}           -> INT (platform-wide)
```

Redis keys use TTL of 26 hours (24h window + 2h buffer) and are auto-expired.

The `MetricsAggregatorWorker` runs every 60 seconds and:
1. Reads the current minute's transaction data from the database
2. Updates Redis counters for the current minute
3. Updates the active users set (adds recent transactors, removes those older than 30 minutes)
4. Publishes a metric update to the `DashboardHub` SignalR hub

### Security Considerations
- Dashboard is accessible to all authenticated admin roles (all roles need operational visibility)
- Tenant isolation: `tenant_admin` SignalR subscriptions are filtered to their tenant's metrics only
- `super_admin` tenant filter selection does not persist beyond the session
- Redis keys are namespaced by tenant_id to prevent cross-tenant data leakage
- No PII is displayed on the dashboard (merchant names are business names, not personal)
- Rate of SignalR updates is server-controlled (not client-configurable beyond allowed intervals)

### Edge Cases
- No transactions in the last 24 hours: display zero values with "No activity" message, charts show flat line at zero
- Very high TPS (> 1000): gauge scale adjusts dynamically; chart aggregation switches from per-minute to per-5-minute
- Redis unavailable: fall back to direct PostgreSQL queries with 60-second cache TTL in memory; show "Real-time metrics temporarily unavailable" warning
- SignalR disconnection: Blazor Server auto-reconnects; overlay message shown during disconnection; metrics catch up on reconnection
- Timezone handling: all timestamps in UTC; charts display in the admin user's configured timezone (default: Africa/Johannesburg, SAST UTC+2)
- Dashboard with tenant filter change: all widgets re-render with new tenant's data; loading indicator shown during transition
- Multiple browser tabs: each tab maintains its own SignalR connection and refresh state independently

---

## Dependencies

**Prerequisite Stories:** STORY-055 (Admin Portal Foundation & RBAC)
**Blocked Stories:** STORY-067 (Exportable Reports -- dashboard can be exported)
**External Dependencies:**
- Blazor charting library: ApexCharts.Blazor (recommended) or Radzen Charts for line, bar, donut charts and gauge
- Redis for real-time metric counters and active user tracking

---

## Definition of Done

- [ ] Code implemented and committed
- [ ] Unit tests written and passing (>=80% coverage)
- [ ] Integration tests passing
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
