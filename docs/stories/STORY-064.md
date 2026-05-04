# STORY-064: Merchant/Agent Performance Reports

**Epic:** EPIC-012 Reporting & Analytics
**Priority:** Must Have
**Story Points:** 3
**Status:** Not Started
**Assigned To:** Unassigned
**Created:** 2026-02-24
**Sprint:** 7

---

## User Story

As an operations admin
I want merchant and agent performance reports
So that I can identify top and underperforming agents and make informed decisions about the network

---

## Description

### Background
The merchant and agent network is GoldBank's primary distribution channel for reaching the unbanked population. Understanding which merchants and agents are performing well (high transaction volume, reliable service) versus underperforming (low activity, high failure rates) is critical for network management. Operations staff use these reports to identify agents who need additional training or support, recognise top performers for incentive programs, and make strategic decisions about agent deployment and coverage. Agent commission tracking is also essential for financial reconciliation and ensuring agents are compensated correctly.

### Scope
**In scope:**
- Transaction volume and value per merchant/agent
- Cash-in/cash-out volume tracking for agents
- Commission earned per agent
- Merchant/agent rankings (top 10, bottom 10)
- Performance trends over time
- Filterable by period, tenant, merchant type
- Sortable data tables

**Out of scope:**
- Agent territory/geographic performance analysis
- Predictive performance modelling
- Automated performance-based alerts
- Agent incentive program management

### User Flow
1. Admin navigates to "Reports" -> "Merchant Performance" in the sidebar
2. Report loads with default view: last 30 days, all merchants, current tenant
3. Summary cards display: total merchants active, total transaction volume, total transaction value, average transaction size, total commissions paid
4. Sortable table shows all merchants with: name, type, transaction count, transaction value, average size, failure rate
5. Admin can click on a merchant row to see a detailed breakdown
6. Merchant detail shows: daily transaction trend, transaction type breakdown, commission earned (if agent)
7. Rankings section shows: top 10 by volume, top 10 by value, bottom 10 by activity
8. Bar charts visualise top performer comparisons
9. For agents specifically: cash-in count/value, cash-out count/value, net float change, commission earned
10. Admin can change date range, filter by merchant type (merchant/agent), and filter by tenant
11. Admin can export the report (STORY-067)

---

## Acceptance Criteria

- [ ] Report displays total active merchant count for the selected period
- [ ] Report displays per-merchant metrics: transaction count, transaction value, average transaction size
- [ ] Report displays per-merchant failure rate (failed / total transactions as percentage)
- [ ] Report displays agent-specific metrics: cash-in count, cash-in value, cash-out count, cash-out value
- [ ] Report displays commission earned per agent for the selected period
- [ ] Report shows top 10 merchants by transaction volume with bar chart visualisation
- [ ] Report shows top 10 merchants by transaction value with bar chart visualisation
- [ ] Report shows bottom 10 merchants by activity (lowest transaction count among active merchants)
- [ ] Merchant table is sortable by any column (name, count, value, average, failure rate)
- [ ] Merchant table is paginated (default 25 per page)
- [ ] Clicking a merchant row shows detailed breakdown: daily trend chart and transaction type distribution
- [ ] Date range filter allows custom range selection (max 12 months)
- [ ] Merchant type filter: all, merchants only, agents only
- [ ] Tenant filter available for `super_admin`
- [ ] `tenant_admin` sees only their tenant's merchant data
- [ ] Report loads within 3 seconds for datasets up to 10,000 merchants

---

## Technical Notes

### Components
- **Blazor Pages:**
  - `Pages/Reports/MerchantPerformanceReport.razor` -- main report page
  - `Pages/Reports/MerchantPerformanceDetail.razor` -- individual merchant detail breakdown
- **Components:**
  - `Components/Reports/ReportHeader.razor` -- shared date range picker, filters (reused from STORY-063)
  - `Components/Reports/MerchantSummaryCards.razor` -- summary metric cards
  - `Components/Reports/MerchantPerformanceTable.razor` -- sortable, paginated merchant table
  - `Components/Reports/TopPerformersChart.razor` -- horizontal bar chart for top 10 rankings
  - `Components/Reports/BottomPerformersChart.razor` -- horizontal bar chart for bottom 10
  - `Components/Reports/AgentMetricsPanel.razor` -- agent-specific cash-in/cash-out/commission display
  - `Components/Reports/MerchantTrendChart.razor` -- daily transaction trend for individual merchant
  - `Components/Reports/TransactionTypeBreakdown.razor` -- pie/donut chart of transaction types

### API / gRPC Endpoints
```protobuf
service ReportingService {
  rpc GetMerchantPerformanceReport (GetMerchantReportRequest) returns (MerchantPerformanceReportResponse);
  rpc GetMerchantPerformanceDetail (GetMerchantDetailReportRequest) returns (MerchantPerformanceDetailResponse);
}

message GetMerchantReportRequest {
  string tenant_id = 1;
  google.protobuf.Timestamp date_from = 2;
  google.protobuf.Timestamp date_to = 3;
  string merchant_type = 4;       // all, merchant, agent
  string sort_by = 5;             // transaction_count, transaction_value, avg_size, failure_rate, commission
  string sort_direction = 6;      // asc, desc
  int32 page = 7;
  int32 page_size = 8;            // default 25
}

message MerchantPerformanceReportResponse {
  // Summary
  int32 active_merchant_count = 1;
  int64 total_transaction_count = 2;
  int64 total_transaction_value = 3;  // minor units
  int64 avg_transaction_size = 4;     // minor units
  int64 total_commissions_paid = 5;   // minor units
  string currency = 6;

  // Merchant table
  repeated MerchantPerformanceRow merchants = 7;
  int32 total_count = 8;
  int32 page = 9;

  // Rankings
  repeated MerchantRanking top_by_volume = 10;      // top 10
  repeated MerchantRanking top_by_value = 11;        // top 10
  repeated MerchantRanking bottom_by_activity = 12;  // bottom 10
}

message MerchantPerformanceRow {
  string merchant_id = 1;
  string merchant_name = 2;
  string merchant_type = 3;       // merchant or agent
  int64 transaction_count = 4;
  int64 transaction_value = 5;    // minor units
  int64 avg_transaction_size = 6; // minor units
  double failure_rate = 7;        // percentage
  // Agent-specific fields (populated only for agents)
  int64 cash_in_count = 8;
  int64 cash_in_value = 9;
  int64 cash_out_count = 10;
  int64 cash_out_value = 11;
  int64 commission_earned = 12;   // minor units
}

message MerchantRanking {
  int32 rank = 1;
  string merchant_id = 2;
  string merchant_name = 3;
  int64 metric_value = 4;         // the ranking metric (count or value)
  string metric_label = 5;        // human-readable label
}

message GetMerchantDetailReportRequest {
  string merchant_id = 1;
  google.protobuf.Timestamp date_from = 2;
  google.protobuf.Timestamp date_to = 3;
}

message MerchantPerformanceDetailResponse {
  string merchant_id = 1;
  string merchant_name = 2;
  string merchant_type = 3;

  // Daily trend
  repeated DailyMerchantMetric daily_trend = 4;

  // Transaction type breakdown
  repeated TypeBreakdown type_breakdown = 5;

  // Agent specifics (if agent)
  AgentPerformanceDetail agent_detail = 6;
}

message DailyMerchantMetric {
  google.protobuf.Timestamp date = 1;
  int64 transaction_count = 2;
  int64 transaction_value = 3;
}

message TypeBreakdown {
  string transaction_type = 1;
  int64 count = 2;
  int64 value = 3;
  double percentage = 4;
}

message AgentPerformanceDetail {
  int64 total_cash_in_count = 1;
  int64 total_cash_in_value = 2;
  int64 total_cash_out_count = 3;
  int64 total_cash_out_value = 4;
  int64 net_float_change = 5;
  int64 total_commission = 6;
  repeated DailyCommission daily_commission = 7;
}

message DailyCommission {
  google.protobuf.Timestamp date = 1;
  int64 commission_amount = 2;
}
```

### Database Changes
No new tables required. Report queries aggregate from existing tables:

```sql
-- Per-merchant performance summary
SELECT
    m.id AS merchant_id,
    m.business_name AS merchant_name,
    m.merchant_type,
    COUNT(t.id) AS transaction_count,
    COALESCE(SUM(t.amount), 0) AS transaction_value,
    COALESCE(AVG(t.amount), 0) AS avg_transaction_size,
    COALESCE(
        COUNT(*) FILTER (WHERE t.status = 'failed')::FLOAT /
        NULLIF(COUNT(*), 0) * 100, 0
    ) AS failure_rate
FROM {tenant_schema}.merchants m
LEFT JOIN {tenant_schema}.transactions t
    ON t.merchant_id = m.id
    AND t.created_at BETWEEN :date_from AND :date_to
WHERE m.status = 'active'
GROUP BY m.id, m.business_name, m.merchant_type
ORDER BY transaction_count DESC;

-- Agent cash-in/cash-out breakdown
SELECT
    t.merchant_id,
    COUNT(*) FILTER (WHERE t.transaction_type = 'cash_in') AS cash_in_count,
    COALESCE(SUM(t.amount) FILTER (WHERE t.transaction_type = 'cash_in'), 0) AS cash_in_value,
    COUNT(*) FILTER (WHERE t.transaction_type = 'cash_out') AS cash_out_count,
    COALESCE(SUM(t.amount) FILTER (WHERE t.transaction_type = 'cash_out'), 0) AS cash_out_value,
    COALESCE(SUM(t.commission_amount), 0) AS commission_earned
FROM {tenant_schema}.transactions t
JOIN {tenant_schema}.merchants m ON m.id = t.merchant_id
WHERE m.merchant_type = 'agent'
    AND t.created_at BETWEEN :date_from AND :date_to
    AND t.status = 'completed'
GROUP BY t.merchant_id;

-- Top 10 by volume
SELECT m.id, m.business_name, COUNT(t.id) AS tx_count
FROM {tenant_schema}.merchants m
JOIN {tenant_schema}.transactions t ON t.merchant_id = m.id
    AND t.created_at BETWEEN :date_from AND :date_to
    AND t.status = 'completed'
WHERE m.status = 'active'
GROUP BY m.id, m.business_name
ORDER BY tx_count DESC
LIMIT 10;
```

Consider a pre-aggregated daily summary table for performance on large datasets:

```sql
-- Optional: daily merchant aggregates (refreshed nightly)
CREATE TABLE {tenant_schema}.merchant_daily_summary (
    merchant_id     UUID NOT NULL REFERENCES {tenant_schema}.merchants(id),
    summary_date    DATE NOT NULL,
    transaction_count INT NOT NULL DEFAULT 0,
    transaction_value BIGINT NOT NULL DEFAULT 0,
    failed_count    INT NOT NULL DEFAULT 0,
    cash_in_count   INT NOT NULL DEFAULT 0,
    cash_in_value   BIGINT NOT NULL DEFAULT 0,
    cash_out_count  INT NOT NULL DEFAULT 0,
    cash_out_value  BIGINT NOT NULL DEFAULT 0,
    commission_earned BIGINT NOT NULL DEFAULT 0,
    PRIMARY KEY (merchant_id, summary_date)
);

CREATE INDEX idx_merchant_daily_date ON {tenant_schema}.merchant_daily_summary(summary_date);
```

### Security Considerations
- `operations` and `super_admin` roles have full access to merchant performance reports
- `tenant_admin` sees only their tenant's merchants
- `finance` role can access the report (commission data is relevant for reconciliation)
- `support` role has read-only access
- Commission amounts are financial data -- ensure no rounding errors (use integer minor units throughout)
- Merchant business names are not PII, but owner names are -- ensure owner names are not exposed in this report

### Edge Cases
- Merchant with no transactions in the period: display with zero counts; do not exclude from the table (helps identify inactive merchants)
- Agent without commission configuration: display "N/A" for commission fields rather than zero
- Very large merchant network (> 5,000): pagination is essential; rankings still computed across full dataset, not just current page
- New merchant approved during the report period: include from approval date; partial period data is expected
- Deactivated merchant with historical transactions: include in report with "Deactivated" badge; historical data still valuable
- Merchant appearing in multiple tenants (franchise model): each tenant sees their own merchant record independently
- Division by zero in failure rate: handle with NULLIF to prevent SQL errors; display "0%" for merchants with no transactions

---

## Dependencies

**Prerequisite Stories:** STORY-055 (Admin Portal Foundation & RBAC)
**Blocked Stories:** STORY-067 (Exportable Reports -- merchant report as export source)
**External Dependencies:**
- ApexCharts.Blazor or Radzen Charts for bar charts, trend lines, and pie charts

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
