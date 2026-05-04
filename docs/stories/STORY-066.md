# STORY-066: Reconciliation Reports

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
I want reconciliation reports
So that I can verify transactions are accounted for and identify discrepancies between our records and external systems

---

## Description

### Background
Reconciliation is a fundamental financial control process. Every day, GoldBank must verify that its internal transaction records match those of external payment networks and switch partners. The daily reconciliation job (implemented in STORY-045) compares GoldBank's transaction records with settlement files from external switches and produces match/mismatch results. This story provides the admin-facing reporting interface that allows finance staff to view reconciliation summaries, drill down into mismatched transactions, investigate discrepancies, and track the overall health of the reconciliation process.

Unresolved mismatches represent financial risk -- either GoldBank has charged a customer without the external network processing the payment, or the external network has processed a payment that GoldBank has not recorded. Timely identification and resolution of mismatches is critical for financial integrity.

### Scope
**In scope:**
- Daily reconciliation summary view
- Matched vs unmatched transaction counts
- Settlement amounts and discrepancies
- Drill-down into mismatched transactions with full detail
- Mismatch categorisation (amount mismatch, status mismatch, unmatched local, unmatched remote)
- Export capability for mismatch reports
- Historical reconciliation trend
- Tenant-scoped views

**Out of scope:**
- Automated mismatch resolution
- External switch file upload/ingestion (handled in STORY-045)
- Settlement processing and fund transfers
- Inter-bank reconciliation

### User Flow
1. Finance admin navigates to "Reports" -> "Reconciliation" in the sidebar
2. Report page shows a calendar/date selector defaulting to the most recent completed reconciliation date
3. Summary cards display: total transactions, matched count, matched percentage, mismatched count, unmatched local count, unmatched remote count, net settlement amount
4. A trend chart shows reconciliation match rate over the last 30 days
5. Below the summary, a mismatch table lists all discrepancies for the selected date
6. Each mismatch row shows: transaction ID, mismatch type, our amount, switch amount, our status, switch status, difference
7. Admin can click on a mismatch to view full transaction details alongside the switch record
8. Admin can filter mismatches by type (amount_mismatch, status_mismatch, unmatched_local, unmatched_remote)
9. Admin can sort mismatches by amount difference (largest first) to prioritise investigation
10. Admin can add investigation notes to a mismatch record
11. Admin can mark a mismatch as "investigated" or "resolved" with notes
12. Admin can export the mismatch list (STORY-067)

---

## Acceptance Criteria

- [ ] Report displays daily reconciliation summary: total transactions, matched count, mismatched count
- [ ] Report displays unmatched local count (transactions in our system but not in switch file)
- [ ] Report displays unmatched remote count (transactions in switch file but not in our system)
- [ ] Report displays net settlement amount for the selected date
- [ ] Report displays match rate as a percentage (matched / total * 100)
- [ ] Report displays a trend chart showing match rate over the last 30 days
- [ ] Mismatch table lists all discrepancies with: transaction ID, mismatch type, our amount, switch amount, our status, switch status, amount difference
- [ ] Mismatch table is filterable by mismatch type
- [ ] Mismatch table is sortable by date, amount difference, and mismatch type
- [ ] Clicking a mismatch row shows full detail: our transaction record vs switch record side by side
- [ ] Admin can add investigation notes to a mismatch record
- [ ] Admin can mark a mismatch as "investigated" or "resolved" with mandatory notes
- [ ] Date selector allows viewing any past reconciliation date
- [ ] If reconciliation has not yet run for the selected date, display "Reconciliation pending" message
- [ ] `finance` and `super_admin` roles have full access including mismatch management
- [ ] `operations` role has read-only access to reconciliation summaries
- [ ] `tenant_admin` sees only their tenant's reconciliation data
- [ ] Export button available on the mismatch table for CSV/PDF export (via STORY-067)

---

## Technical Notes

### Components
- **Blazor Pages:**
  - `Pages/Reports/ReconciliationReport.razor` -- main reconciliation report page
  - `Pages/Reports/MismatchDetail.razor` -- detailed side-by-side comparison for a mismatch
- **Components:**
  - `Components/Reports/ReconSummaryCards.razor` -- summary metric cards (matched, mismatched, unmatched)
  - `Components/Reports/ReconTrendChart.razor` -- line chart of match rate over time
  - `Components/Reports/MismatchTable.razor` -- filterable, sortable mismatch list
  - `Components/Reports/MismatchTypeFilter.razor` -- dropdown filter for mismatch categories
  - `Components/Reports/SideBySideComparison.razor` -- our record vs switch record comparison panel
  - `Components/Reports/InvestigationNotesPanel.razor` -- add/view investigation notes on mismatch
  - `Components/Reports/ReconDateSelector.razor` -- date picker for selecting reconciliation date
  - `Components/Reports/SettlementAmountCard.razor` -- net settlement display with discrepancy highlight

### API / gRPC Endpoints
```protobuf
service ReportingService {
  rpc GetReconReport (GetReconReportRequest) returns (ReconReportResponse);
  rpc GetReconTrend (GetReconTrendRequest) returns (ReconTrendResponse);
  rpc GetMismatchDetail (GetMismatchDetailRequest) returns (MismatchDetailResponse);
  rpc UpdateMismatch (UpdateMismatchRequest) returns (UpdateMismatchResponse);
}

message GetReconReportRequest {
  string tenant_id = 1;
  google.protobuf.Timestamp recon_date = 2;   // specific date
  string mismatch_type_filter = 3;             // optional: amount_mismatch, status_mismatch, unmatched_local, unmatched_remote
  string sort_by = 4;                          // amount_difference, mismatch_type, created_at
  string sort_direction = 5;                   // asc, desc
  int32 page = 6;
  int32 page_size = 7;                         // default 50
}

message ReconReportResponse {
  // Summary
  google.protobuf.Timestamp recon_date = 1;
  string recon_status = 2;                     // completed, pending, failed
  int64 total_transactions = 3;
  int64 matched_count = 4;
  int64 mismatched_count = 5;
  int64 unmatched_local_count = 6;
  int64 unmatched_remote_count = 7;
  double match_rate = 8;                       // percentage
  int64 our_settlement_amount = 9;             // minor units
  int64 switch_settlement_amount = 10;         // minor units
  int64 settlement_discrepancy = 11;           // minor units (our - switch)
  string currency = 12;

  // Mismatch list
  repeated MismatchSummary mismatches = 13;
  int32 total_mismatch_count = 14;
  int32 page = 15;
}

message MismatchSummary {
  string mismatch_id = 1;
  string transaction_id = 2;
  string mismatch_type = 3;                    // amount_mismatch, status_mismatch, unmatched_local, unmatched_remote
  int64 our_amount = 4;                        // minor units (0 if unmatched_remote)
  int64 switch_amount = 5;                     // minor units (0 if unmatched_local)
  string our_status = 6;
  string switch_status = 7;
  int64 amount_difference = 8;                 // absolute difference in minor units
  string investigation_status = 9;             // open, investigating, resolved
  google.protobuf.Timestamp created_at = 10;
}

message GetReconTrendRequest {
  string tenant_id = 1;
  int32 days = 2;                              // default 30
}

message ReconTrendResponse {
  repeated ReconTrendPoint points = 1;
}

message ReconTrendPoint {
  google.protobuf.Timestamp date = 1;
  double match_rate = 2;
  int64 total_transactions = 3;
  int64 mismatch_count = 4;
}

message GetMismatchDetailRequest {
  string mismatch_id = 1;
}

message MismatchDetailResponse {
  string mismatch_id = 1;
  string mismatch_type = 2;
  string investigation_status = 3;

  // Our record
  TransactionRecord our_record = 4;

  // Switch record
  SwitchRecord switch_record = 5;

  // Differences highlighted
  repeated FieldDifference differences = 6;

  // Investigation history
  repeated InvestigationNote notes = 7;
}

message TransactionRecord {
  string transaction_id = 1;
  string transaction_type = 2;
  int64 amount = 3;
  string currency = 4;
  string status = 5;
  string source_account = 6;
  string destination_account = 7;
  string reference = 8;
  google.protobuf.Timestamp created_at = 9;
  google.protobuf.Timestamp completed_at = 10;
}

message SwitchRecord {
  string switch_reference = 1;
  string message_type = 2;
  int64 amount = 3;
  string currency = 4;
  string response_code = 5;
  string authorization_code = 6;
  google.protobuf.Timestamp timestamp = 7;
}

message FieldDifference {
  string field_name = 1;
  string our_value = 2;
  string switch_value = 3;
}

message InvestigationNote {
  string note = 1;
  string admin_name = 2;
  google.protobuf.Timestamp created_at = 3;
}

message UpdateMismatchRequest {
  string mismatch_id = 1;
  string new_status = 2;          // investigating, resolved
  string notes = 3;               // mandatory
  string admin_user_id = 4;
}
```

### Database Changes
The reconciliation tables are created by STORY-045. This story adds investigation tracking:

```sql
-- Add investigation fields to reconciliation mismatches table
ALTER TABLE {tenant_schema}.reconciliation_mismatches
    ADD COLUMN IF NOT EXISTS investigation_status VARCHAR(20) NOT NULL DEFAULT 'open'
        CHECK (investigation_status IN ('open', 'investigating', 'resolved')),
    ADD COLUMN IF NOT EXISTS resolved_by UUID,
    ADD COLUMN IF NOT EXISTS resolved_at TIMESTAMPTZ;

-- Investigation notes table
CREATE TABLE {tenant_schema}.recon_investigation_notes (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    mismatch_id     UUID NOT NULL REFERENCES {tenant_schema}.reconciliation_mismatches(id),
    note            TEXT NOT NULL,
    admin_user_id   UUID NOT NULL,
    admin_name      VARCHAR(200) NOT NULL,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX idx_recon_notes_mismatch ON {tenant_schema}.recon_investigation_notes(mismatch_id);

-- Index for filtering mismatches by investigation status
CREATE INDEX idx_recon_mismatches_investigation
    ON {tenant_schema}.reconciliation_mismatches(investigation_status)
    WHERE investigation_status != 'resolved';

-- Summary view for trend chart
CREATE OR REPLACE VIEW {tenant_schema}.v_recon_daily_summary AS
SELECT
    recon_date,
    total_transactions,
    matched_count,
    mismatched_count,
    unmatched_local_count,
    unmatched_remote_count,
    CASE WHEN total_transactions > 0
        THEN (matched_count::FLOAT / total_transactions * 100)
        ELSE 100.0
    END AS match_rate,
    our_settlement_amount,
    switch_settlement_amount,
    (our_settlement_amount - switch_settlement_amount) AS settlement_discrepancy
FROM {tenant_schema}.reconciliation_summaries
ORDER BY recon_date DESC;
```

### Security Considerations
- `finance` and `super_admin` roles have full access including mismatch investigation and resolution
- `operations` role has read-only access to summaries (cannot add notes or resolve mismatches)
- `tenant_admin` sees only their tenant's reconciliation data
- `support` and `compliance` roles cannot access reconciliation reports
- Switch record details may contain sensitive network information -- display only to `finance` and `super_admin`
- Investigation notes are immutable (cannot be edited or deleted once created)
- All mismatch status changes are audit-logged
- Settlement amounts are financial data -- use integer minor units throughout, no floating-point

### Edge Cases
- Reconciliation not yet run for selected date: display "Reconciliation is pending for this date" with last completed date
- Reconciliation job failed: display "Reconciliation failed for this date" with error summary and retry option (super_admin only)
- Zero transactions on a date (e.g., public holiday): display 100% match rate with "No transactions" note
- Very large number of mismatches (> 1,000): pagination essential; summary statistics still computed across full dataset
- Mismatch amount difference of zero but status mismatch: correctly categorise as status_mismatch, not amount_mismatch
- Cross-date transactions: transactions that span midnight (started before, completed after) are reconciled based on creation date
- Multiple switch partners: reconciliation report should indicate which switch partner each mismatch relates to
- Stale data warning: if viewing a date older than 30 days, display "Historical data -- some details may have been archived"

---

## Dependencies

**Prerequisite Stories:**
- STORY-045 (Daily Reconciliation Job -- provides the reconciliation data that this report displays)
- STORY-055 (Admin Portal Foundation & RBAC)

**Blocked Stories:** STORY-067 (Exportable Reports -- reconciliation mismatch list as export source)
**External Dependencies:** None beyond existing reconciliation infrastructure from STORY-045

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
