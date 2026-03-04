# STORY-065: Revenue & Fee Reports

**Epic:** EPIC-012 Reporting & Analytics
**Priority:** Must Have
**Story Points:** 3
**Status:** Not Started
**Assigned To:** Unassigned
**Created:** 2026-02-24
**Sprint:** 7

---

## User Story

As a finance admin
I want revenue and fee reports
So that I can track financial performance and ensure fee collection is accurate

---

## Description

### Background
Revenue tracking is essential for financial management and business viability. UniBank's revenue comes primarily from transaction fees (charged to customers or merchants), interchange fees (from switch transactions between banks), and terminal fees (monthly charges per deployed terminal). Finance staff need detailed breakdowns of revenue by source, by tenant, and by time period to monitor financial performance, prepare management reports, and ensure accurate fee collection. Month-over-month comparisons help identify trends and anomalies that require investigation.

In the multi-tenant model, each tenant contributes differently to overall platform revenue. Understanding per-tenant revenue allows for accurate revenue sharing, pricing negotiations, and financial forecasting.

### Scope
**In scope:**
- Revenue breakdown by fee type (NFC, QR, P2P, bill pay, cash-in, cash-out)
- Interchange fee revenue from switch transactions
- Terminal fee revenue (monthly per terminal)
- Per-tenant revenue breakdown
- Period comparison: month-over-month (MoM), quarter-over-quarter
- Revenue trend visualisation
- Summary statistics with growth percentages

**Out of scope:**
- Cost/expense tracking (revenue only)
- Profit and loss (P&L) statements
- Tax calculations and reporting
- Revenue forecasting and projections
- Invoicing and billing

### User Flow
1. Admin navigates to "Reports" -> "Revenue & Fees" in the sidebar
2. Report loads with default view: current month vs previous month, all tenants
3. Summary cards display: total revenue (current period), MoM change percentage, revenue by category totals
4. Stacked bar chart shows revenue by type over time (monthly granularity)
5. Table shows per-tenant revenue breakdown with totals per fee type
6. Admin can select different comparison periods: MoM, QoQ, YoY
7. Admin can change date range and granularity
8. Admin can filter by tenant (super_admin only) or view all tenants
9. Clicking on a revenue category or tenant row shows detailed breakdown
10. MoM growth percentages are displayed alongside each metric (green for positive, red for negative)
11. Admin can export the report (STORY-067)

---

## Acceptance Criteria

- [ ] Report displays total revenue for the selected period in platform currency
- [ ] Report displays revenue breakdown by fee type: NFC, QR, P2P, bill pay, cash-in, cash-out
- [ ] Report displays interchange fee revenue from switch transactions
- [ ] Report displays terminal fee revenue (monthly per terminal)
- [ ] Report displays a stacked bar chart showing revenue by type over time
- [ ] Report displays a per-tenant revenue breakdown table with columns for each fee type and total
- [ ] MoM comparison shows current period vs previous period with growth/decline percentage
- [ ] MoM growth percentage is colour-coded: green for positive, red for negative
- [ ] Comparison period options: month-over-month, quarter-over-quarter
- [ ] Date range filter allows custom period selection
- [ ] Granularity options: daily, weekly, monthly
- [ ] Tenant filter available for `super_admin` (all tenants or specific tenant)
- [ ] `tenant_admin` sees only their tenant's revenue data
- [ ] `finance` and `super_admin` roles have full access; other roles see summary only
- [ ] Revenue amounts are displayed in the appropriate currency with proper formatting (e.g., ZAR 1,234.56)
- [ ] Report loads within 3 seconds for up to 12 months of data

---

## Technical Notes

### Components
- **Blazor Pages:**
  - `Pages/Reports/RevenueReport.razor` -- main revenue report page
- **Components:**
  - `Components/Reports/ReportHeader.razor` -- shared header with date range and filters (reused)
  - `Components/Reports/RevenueSummaryCards.razor` -- total revenue, MoM change, category summaries
  - `Components/Reports/RevenueByTypeChart.razor` -- stacked bar chart of revenue by fee type over time
  - `Components/Reports/TenantRevenueTable.razor` -- per-tenant breakdown table with sortable columns
  - `Components/Reports/MoMComparisonBar.razor` -- horizontal comparison bars showing current vs previous period
  - `Components/Reports/RevenueGrowthIndicator.razor` -- percentage change with up/down arrow and colour
  - `Components/Reports/InterchangeFeePanel.razor` -- interchange fee detail panel
  - `Components/Reports/TerminalFeePanel.razor` -- terminal fee detail panel

### API / gRPC Endpoints
```protobuf
service ReportingService {
  rpc GetRevenueReport (GetRevenueReportRequest) returns (RevenueReportResponse);
}

message GetRevenueReportRequest {
  string tenant_id = 1;           // optional, auto-set for tenant_admin
  google.protobuf.Timestamp date_from = 2;
  google.protobuf.Timestamp date_to = 3;
  string granularity = 4;         // daily, weekly, monthly
  string comparison_period = 5;   // mom (month-over-month), qoq (quarter-over-quarter)
}

message RevenueReportResponse {
  // Summary
  int64 total_revenue = 1;                    // minor units
  int64 previous_period_revenue = 2;          // minor units
  double revenue_growth_rate = 3;             // percentage
  string currency = 4;

  // Revenue by fee type
  repeated FeeTypeRevenue fee_type_breakdown = 5;

  // Interchange and terminal fees
  int64 interchange_fee_revenue = 6;          // minor units
  int64 interchange_previous_period = 7;
  int64 terminal_fee_revenue = 8;             // minor units
  int64 terminal_previous_period = 9;
  int32 active_terminal_count = 10;

  // Time series (for stacked bar chart)
  repeated RevenuePeriodPoint revenue_series = 11;

  // Per-tenant breakdown
  repeated TenantRevenue tenant_breakdown = 12;

  // Period metadata
  google.protobuf.Timestamp date_from = 13;
  google.protobuf.Timestamp date_to = 14;
  string granularity = 15;
}

message FeeTypeRevenue {
  string fee_type = 1;                // nfc, qr, p2p, bill_pay, cash_in, cash_out
  string display_name = 2;
  int64 revenue = 3;                  // minor units, current period
  int64 previous_period_revenue = 4;  // minor units
  double growth_rate = 5;            // percentage
  int64 transaction_count = 6;
  int64 avg_fee_per_transaction = 7;  // minor units
}

message RevenuePeriodPoint {
  google.protobuf.Timestamp period_start = 1;
  string period_label = 2;
  int64 nfc_revenue = 3;
  int64 qr_revenue = 4;
  int64 p2p_revenue = 5;
  int64 bill_pay_revenue = 6;
  int64 cash_in_revenue = 7;
  int64 cash_out_revenue = 8;
  int64 interchange_revenue = 9;
  int64 terminal_revenue = 10;
  int64 total_revenue = 11;
}

message TenantRevenue {
  string tenant_id = 1;
  string tenant_name = 2;
  int64 total_revenue = 3;
  int64 nfc_revenue = 4;
  int64 qr_revenue = 5;
  int64 p2p_revenue = 6;
  int64 bill_pay_revenue = 7;
  int64 cash_in_revenue = 8;
  int64 cash_out_revenue = 9;
  int64 interchange_revenue = 10;
  int64 terminal_revenue = 11;
  double growth_rate = 12;          // MoM growth percentage
}
```

### Database Changes
No new tables required. Revenue queries aggregate from existing tables:

```sql
-- Revenue by fee type for a period
SELECT
    t.transaction_type AS fee_type,
    COUNT(*) AS transaction_count,
    COALESCE(SUM(tf.fee_amount), 0) AS total_revenue,
    COALESCE(AVG(tf.fee_amount), 0) AS avg_fee
FROM {tenant_schema}.transactions t
JOIN {tenant_schema}.transaction_fees tf ON tf.transaction_id = t.id
WHERE t.created_at BETWEEN :date_from AND :date_to
    AND t.status = 'completed'
GROUP BY t.transaction_type;

-- Monthly revenue time series (for stacked bar chart)
SELECT
    date_trunc('month', t.created_at) AS period,
    t.transaction_type,
    COALESCE(SUM(tf.fee_amount), 0) AS revenue
FROM {tenant_schema}.transactions t
JOIN {tenant_schema}.transaction_fees tf ON tf.transaction_id = t.id
WHERE t.created_at BETWEEN :date_from AND :date_to
    AND t.status = 'completed'
GROUP BY date_trunc('month', t.created_at), t.transaction_type
ORDER BY period;

-- Per-tenant revenue breakdown (super_admin cross-tenant query)
-- This query runs against each tenant schema and aggregates results
-- Implemented in application code iterating over tenant schemas

-- Interchange fee revenue
SELECT
    COALESCE(SUM(interchange_fee), 0) AS interchange_revenue
FROM {tenant_schema}.switch_transactions
WHERE created_at BETWEEN :date_from AND :date_to
    AND status = 'completed';

-- Terminal fee revenue
SELECT
    COUNT(*) AS active_terminals,
    COUNT(*) * :monthly_terminal_fee AS terminal_revenue
FROM {tenant_schema}.terminals
WHERE status = 'active'
    AND provisioned_at <= :date_to;

-- MoM comparison
-- Application code calculates by running the same query for current and previous period
```

Consider a pre-aggregated revenue summary for performance:

```sql
-- Optional: daily revenue aggregates (refreshed nightly)
CREATE TABLE admin.revenue_daily_summary (
    tenant_id       UUID NOT NULL,
    summary_date    DATE NOT NULL,
    fee_type        VARCHAR(30) NOT NULL,
    transaction_count INT NOT NULL DEFAULT 0,
    fee_revenue     BIGINT NOT NULL DEFAULT 0,    -- minor units
    interchange_revenue BIGINT NOT NULL DEFAULT 0,
    terminal_revenue BIGINT NOT NULL DEFAULT 0,
    PRIMARY KEY (tenant_id, summary_date, fee_type)
);

CREATE INDEX idx_revenue_daily_date ON admin.revenue_daily_summary(summary_date);
CREATE INDEX idx_revenue_daily_tenant ON admin.revenue_daily_summary(tenant_id);
```

### Security Considerations
- `finance` and `super_admin` roles have full access to detailed revenue data
- `operations` role can view summary revenue cards but not detailed per-tenant breakdowns
- `tenant_admin` sees only their own tenant's revenue
- `support` and `compliance` roles cannot access revenue reports
- Revenue figures are sensitive business data -- ensure no caching in browser (Cache-Control: no-store on report responses)
- All revenue amounts use integer minor units to avoid floating-point rounding issues
- Cross-tenant revenue queries (super_admin view) must be permission-gated at the service layer

### Edge Cases
- Tenant with no transactions in the period: display with zero revenue, not excluded from the table
- New tenant provisioned during the period: show data from provisioning date; MoM comparison shows "N/A" for previous period
- Terminal fee calculation for mid-month provisioning: prorate based on days active in the month
- Refunded transactions: subtract refund fees from revenue (net revenue, not gross)
- Currency mismatch: if tenants operate in different currencies, cross-tenant totals are displayed per currency (not converted)
- Very large dataset (millions of transactions): use pre-aggregated daily summary table; fall back to live query only for current day
- Zero revenue in comparison period: growth rate displayed as "N/A" rather than infinity
- Negative growth: ensure percentage is correctly displayed as negative with red colour indicator

---

## Dependencies

**Prerequisite Stories:** STORY-055 (Admin Portal Foundation & RBAC)
**Blocked Stories:** STORY-067 (Exportable Reports -- revenue report as export source)
**External Dependencies:**
- ApexCharts.Blazor or Radzen Charts for stacked bar charts and comparison visualisations

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
