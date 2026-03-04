# STORY-063: User Growth & Registration Reports

**Epic:** EPIC-012 Reporting & Analytics
**Priority:** Must Have
**Story Points:** 3
**Status:** Not Started
**Assigned To:** Unassigned
**Created:** 2026-02-24
**Sprint:** 7

---

## User Story

As a business admin
I want user registration and growth reports
So that I can track adoption and measure the platform's growth trajectory

---

## Description

### Background
Tracking user growth is fundamental to understanding UniBank's market penetration and the effectiveness of customer acquisition strategies. For a platform targeting the unbanked in Southern Africa, registration numbers, KYC completion rates, and active user ratios are key business metrics. Business stakeholders need to see daily, weekly, and monthly trends, understand where users drop off in the onboarding funnel (registration -> KYC submission -> KYC approval), and measure churn (users who registered but stopped transacting). These metrics inform decisions about marketing spend, agent deployment, and product development.

### Scope
**In scope:**
- Registration counts by period (daily, weekly, monthly)
- KYC funnel analysis (registered -> KYC submitted -> KYC approved)
- KYC completion rate as a percentage
- Active vs inactive user ratio (active = transacted in last 30 days)
- Churn rate calculation
- Growth trend line charts
- Filterable by tenant and date range
- Summary statistics cards

**Out of scope:**
- Demographic breakdown (age, gender, region)
- Cohort retention analysis
- Attribution tracking (how users heard about UniBank)
- Predictive growth modelling

### User Flow
1. Admin navigates to "Reports" -> "User Growth" in the sidebar
2. Report page loads with default view: last 30 days, all tenants (or current tenant for `tenant_admin`)
3. Summary cards display: total registrations (period), KYC completion rate, active user rate, churn rate
4. Line chart shows daily registration count over the selected period
5. Bar chart shows KYC funnel: registered, KYC submitted, KYC approved (stacked or grouped)
6. Pie chart shows active vs inactive user distribution
7. Admin can change the date range using a date picker
8. Admin can change the period granularity: daily, weekly, monthly
9. Admin can filter by tenant (super_admin only)
10. Charts and cards update based on selected filters
11. Admin can export the report using the export function (STORY-067)

---

## Acceptance Criteria

- [ ] Report displays total registration count for the selected period
- [ ] Report displays daily/weekly/monthly registration counts as a line chart
- [ ] Report displays KYC funnel as a bar chart: registered count, KYC submitted count, KYC approved count
- [ ] Report displays KYC completion rate as a percentage (approved / total registered)
- [ ] Report displays active user rate (users who transacted in last 30 days / total registered)
- [ ] Report displays churn rate (users who registered > 30 days ago and have not transacted in last 30 days / total registered > 30 days ago)
- [ ] Report displays active vs inactive distribution as a pie chart
- [ ] Date range filter allows selection of custom date ranges (max 12 months)
- [ ] Period granularity can be switched: daily, weekly, monthly
- [ ] Tenant filter available for `super_admin` (all tenants or specific tenant)
- [ ] `tenant_admin` sees only their tenant's data (filter auto-applied, not visible)
- [ ] Summary cards show period-over-period comparison (e.g., +15% vs previous period)
- [ ] Report loads within 3 seconds for up to 12 months of data
- [ ] Empty state: if no registrations in selected period, display "No registrations in selected period"

---

## Technical Notes

### Components
- **Blazor Pages:**
  - `Pages/Reports/UserGrowthReport.razor` -- main report page
- **Components:**
  - `Components/Reports/ReportHeader.razor` -- date range picker, granularity selector, tenant filter
  - `Components/Reports/SummaryCard.razor` -- metric card with value, trend arrow, and percentage change
  - `Components/Reports/RegistrationChart.razor` -- line chart for registrations over time
  - `Components/Reports/KYCFunnelChart.razor` -- bar chart for KYC funnel stages
  - `Components/Reports/ActiveInactivePieChart.razor` -- pie chart for active/inactive distribution
  - `Components/Reports/GrowthSummaryCards.razor` -- container for summary metric cards

### API / gRPC Endpoints
```protobuf
service ReportingService {
  rpc GetUserGrowthReport (GetUserGrowthReportRequest) returns (UserGrowthReportResponse);
}

message GetUserGrowthReportRequest {
  string tenant_id = 1;           // optional, auto-set for tenant_admin
  google.protobuf.Timestamp date_from = 2;
  google.protobuf.Timestamp date_to = 3;
  string granularity = 4;         // daily, weekly, monthly
}

message UserGrowthReportResponse {
  // Summary metrics
  int64 total_registrations = 1;
  int64 previous_period_registrations = 2;  // for comparison
  double registration_growth_rate = 3;       // percentage change
  double kyc_completion_rate = 4;            // percentage
  double active_user_rate = 5;               // percentage
  double churn_rate = 6;                     // percentage
  int64 total_active_users = 7;
  int64 total_inactive_users = 8;

  // Time series data
  repeated RegistrationPoint registration_series = 9;

  // KYC funnel
  KYCFunnel kyc_funnel = 10;

  // Period metadata
  google.protobuf.Timestamp date_from = 11;
  google.protobuf.Timestamp date_to = 12;
  string granularity = 13;
}

message RegistrationPoint {
  google.protobuf.Timestamp period_start = 1;
  string period_label = 2;        // "2026-02-24", "Week 8", "Feb 2026"
  int64 registration_count = 3;
  int64 cumulative_total = 4;
}

message KYCFunnel {
  int64 registered = 1;           // total accounts created in period
  int64 kyc_submitted = 2;        // submitted KYC documents
  int64 kyc_approved = 3;         // KYC approved
  int64 kyc_rejected = 4;         // KYC rejected
  int64 kyc_pending = 5;          // awaiting review
  double submission_rate = 6;     // submitted / registered (%)
  double approval_rate = 7;       // approved / submitted (%)
}
```

### Database Changes
No new tables required. Report queries aggregate from existing tables:

```sql
-- Registration count by period (daily example)
SELECT
    date_trunc('day', created_at) AS period,
    COUNT(*) AS registration_count
FROM {tenant_schema}.accounts
WHERE created_at BETWEEN :date_from AND :date_to
GROUP BY date_trunc('day', created_at)
ORDER BY period;

-- KYC funnel
SELECT
    COUNT(*) AS registered,
    COUNT(*) FILTER (WHERE kyc_status IN ('submitted', 'approved', 'rejected')) AS kyc_submitted,
    COUNT(*) FILTER (WHERE kyc_status = 'approved') AS kyc_approved,
    COUNT(*) FILTER (WHERE kyc_status = 'rejected') AS kyc_rejected,
    COUNT(*) FILTER (WHERE kyc_status = 'submitted') AS kyc_pending
FROM {tenant_schema}.accounts
WHERE created_at BETWEEN :date_from AND :date_to;

-- Active user rate (transacted in last 30 days)
SELECT
    COUNT(DISTINCT a.id) FILTER (
        WHERE EXISTS (
            SELECT 1 FROM {tenant_schema}.transactions t
            WHERE t.account_id = a.id
            AND t.created_at >= now() - interval '30 days'
        )
    ) AS active_users,
    COUNT(a.id) AS total_users
FROM {tenant_schema}.accounts a
WHERE a.status = 'active';

-- Churn rate
SELECT
    COUNT(*) FILTER (
        WHERE NOT EXISTS (
            SELECT 1 FROM {tenant_schema}.transactions t
            WHERE t.account_id = a.id
            AND t.created_at >= now() - interval '30 days'
        )
    )::FLOAT / NULLIF(COUNT(*), 0) AS churn_rate
FROM {tenant_schema}.accounts a
WHERE a.created_at < now() - interval '30 days'
  AND a.status = 'active';
```

Consider creating a materialised view or pre-aggregated table for performance on large datasets:

```sql
-- Optional: materialised view for daily registration aggregates
CREATE MATERIALIZED VIEW {tenant_schema}.mv_daily_registrations AS
SELECT
    date_trunc('day', created_at) AS registration_date,
    COUNT(*) AS registration_count,
    COUNT(*) FILTER (WHERE kyc_status = 'approved') AS kyc_approved_count
FROM {tenant_schema}.accounts
GROUP BY date_trunc('day', created_at);

CREATE UNIQUE INDEX idx_mv_daily_reg ON {tenant_schema}.mv_daily_registrations(registration_date);

-- Refresh daily via scheduled job
-- REFRESH MATERIALIZED VIEW CONCURRENTLY {tenant_schema}.mv_daily_registrations;
```

### Security Considerations
- All admin roles can access the user growth report (business visibility is broadly useful)
- `tenant_admin` data is automatically scoped to their tenant
- `super_admin` can view cross-tenant aggregates or filter by specific tenant
- No PII is exposed in the report (aggregated counts only)
- Rate limiting on report generation to prevent abuse (max 10 report requests per minute per admin)

### Edge Cases
- New tenant with zero registrations: display "No data available" with suggestion to check date range
- Very large date range (> 6 months): warn admin that report may take longer; consider using materialised view
- Timezone boundary: registrations are counted based on UTC date; charts display labels in admin's local timezone
- Period granularity change: weekly periods start on Monday (ISO 8601); monthly periods use calendar months
- Active user calculation on a new platform: if platform is < 30 days old, adjust active user window proportionally
- Cross-midnight registrations: a registration at 23:59 UTC and one at 00:01 UTC are counted in different daily buckets

---

## Dependencies

**Prerequisite Stories:** STORY-055 (Admin Portal Foundation & RBAC)
**Blocked Stories:** STORY-067 (Exportable Reports -- user growth report as export source)
**External Dependencies:**
- ApexCharts.Blazor or Radzen Charts for chart rendering
- Optional: PostgreSQL materialised views for performance optimisation on large datasets

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
