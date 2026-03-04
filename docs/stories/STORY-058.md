# STORY-058: Transaction Monitoring & Search (Admin)

**Epic:** EPIC-011 Admin / Back-Office Portal
**Priority:** Must Have
**Story Points:** 5
**Status:** Not Started
**Assigned To:** Unassigned
**Created:** 2026-02-24
**Sprint:** 7

---

## User Story

As an operations admin
I want to monitor and search all transactions
So that I can investigate issues and ensure the platform is operating correctly

---

## Description

### Background
Transaction monitoring is a critical operational capability for any financial platform. Operations and support staff need the ability to view transactions in real-time as they flow through the system, search historical transactions using various criteria, and drill into the full processing trail of any individual transaction. This supports both day-to-day operational oversight and specific issue investigation (e.g., a customer reporting a failed transaction). The ability to flag suspicious transactions for further review also provides a first line of defence for fraud detection and compliance requirements.

### Scope
**In scope:**
- Real-time transaction feed using Blazor Server SignalR
- Transaction search with multi-criteria filtering
- Full transaction detail view with processing trail (saga steps)
- Ability to flag transactions for review
- Transaction statistics summary
- Tenant-scoped views for `tenant_admin`

**Out of scope:**
- Automated fraud detection rules (separate system)
- Transaction reversal/refund (handled via STORY-061 Disputes)
- Settlement processing views
- Real-time alerting/notifications for thresholds

### User Flow
1. Operations admin navigates to "Transactions" in the sidebar
2. The Transaction Monitor page shows a live feed of transactions as they occur
3. Transactions appear in real-time via SignalR, colour-coded by status (green=success, red=failed, yellow=pending)
4. Admin can pause the live feed to examine entries without scrolling
5. Admin switches to "Search" tab to perform historical searches
6. Admin enters search criteria: transaction ID, account ID, merchant ID, date range, amount range, transaction type, status
7. Results are displayed in a paginated table with sortable columns
8. Admin clicks on a transaction to view full details
9. Detail page shows: transaction summary, full processing trail (each saga step with timestamp), switch messages (if applicable), related transactions
10. Admin can click "Flag for Review" to mark a suspicious transaction
11. Flagged transactions appear in a separate "Flagged" queue for compliance review

---

## Acceptance Criteria

- [ ] Real-time transaction feed displays transactions as they are processed (latency < 2 seconds)
- [ ] Live feed is colour-coded by status: success (green), failed (red), pending (yellow), reversed (orange)
- [ ] Live feed can be paused and resumed without losing transactions
- [ ] Live feed shows: timestamp, transaction ID (truncated), type, amount, from/to, status
- [ ] Search supports filtering by: transaction_id, account_id, merchant_id, date_range, amount_range, type, status
- [ ] Search results are paginated (default 50 per page) with total count
- [ ] Search results are sortable by date, amount, and status
- [ ] Transaction detail page shows complete transaction summary with all fields
- [ ] Transaction detail page shows the full processing trail: each saga step with name, status, timestamp, duration
- [ ] Transaction detail page shows switch/network messages if the transaction involved external switch routing
- [ ] Admin can flag a transaction for review with a mandatory note
- [ ] Flagged transactions appear in a dedicated "Flagged for Review" queue
- [ ] Flagged transaction review shows the flag reason, flagged-by admin, and flag timestamp
- [ ] `tenant_admin` sees only transactions within their tenant
- [ ] Transaction feed respects tenant scope (tenant_admin sees only their tenant's transactions)
- [ ] All flag actions are audit-logged

---

## Technical Notes

### Components
- **Blazor Pages:**
  - `Pages/Transactions/TransactionMonitor.razor` -- real-time feed with SignalR
  - `Pages/Transactions/TransactionSearch.razor` -- multi-criteria search with results table
  - `Pages/Transactions/TransactionDetail.razor` -- full transaction detail with processing trail
  - `Pages/Transactions/FlaggedTransactions.razor` -- flagged transaction review queue
- **Components:**
  - `Components/Transactions/LiveTransactionFeed.razor` -- real-time scrolling feed component
  - `Components/Transactions/TransactionSearchForm.razor` -- search filter controls
  - `Components/Transactions/TransactionRow.razor` -- single transaction row with status colour coding
  - `Components/Transactions/ProcessingTrailTimeline.razor` -- visual timeline of saga steps
  - `Components/Transactions/SwitchMessagePanel.razor` -- raw switch message display
  - `Components/Transactions/FlagTransactionModal.razor` -- flag with notes modal
- **Services:**
  - `Services/TransactionFeedService.cs` -- manages SignalR subscription for live transaction data
  - `Hubs/TransactionHub.cs` -- SignalR hub that broadcasts new transactions

### API / gRPC Endpoints
```protobuf
service AdminTransactionService {
  rpc SearchTransactions (SearchTransactionsRequest) returns (SearchTransactionsResponse);
  rpc GetTransactionDetail (GetTransactionDetailRequest) returns (TransactionDetailResponse);
  rpc StreamTransactions (StreamTransactionsRequest) returns (stream TransactionEvent);
  rpc FlagTransaction (FlagTransactionRequest) returns (FlagTransactionResponse);
  rpc GetFlaggedTransactions (GetFlaggedTransactionsRequest) returns (GetFlaggedTransactionsResponse);
  rpc ResolveFlaggedTransaction (ResolveFlaggedTransactionRequest) returns (ResolveFlaggedTransactionResponse);
}

message SearchTransactionsRequest {
  string transaction_id = 1;
  string account_id = 2;
  string merchant_id = 3;
  google.protobuf.Timestamp date_from = 4;
  google.protobuf.Timestamp date_to = 5;
  int64 amount_min_minor_units = 6;
  int64 amount_max_minor_units = 7;
  string transaction_type = 8;     // nfc, qr, p2p, bill_pay, cash_in, cash_out, etc.
  string status = 9;               // pending, completed, failed, reversed
  string tenant_id = 10;
  string sort_by = 11;             // created_at, amount, status
  string sort_direction = 12;      // asc, desc
  int32 page = 13;
  int32 page_size = 14;            // default 50, max 200
}

message SearchTransactionsResponse {
  repeated TransactionSummary transactions = 1;
  int32 total_count = 2;
  int32 page = 3;
  int32 page_size = 4;
}

message TransactionDetailResponse {
  string transaction_id = 1;
  string transaction_type = 2;
  string status = 3;
  int64 amount_minor_units = 4;
  string currency = 5;
  string source_account_id = 6;
  string source_account_name = 7;
  string destination_account_id = 8;
  string destination_account_name = 9;
  string merchant_id = 10;
  string merchant_name = 11;
  int64 fee_minor_units = 12;
  string reference = 13;
  string tenant_id = 14;
  google.protobuf.Timestamp created_at = 15;
  google.protobuf.Timestamp completed_at = 16;
  repeated SagaStep processing_trail = 17;
  repeated SwitchMessage switch_messages = 18;
  FlagInfo flag_info = 19;         // null if not flagged
}

message SagaStep {
  string step_name = 1;            // e.g., "ValidateBalance", "DebitAccount", "CreditAccount"
  string status = 2;               // completed, failed, compensated
  google.protobuf.Timestamp started_at = 3;
  google.protobuf.Timestamp completed_at = 4;
  int32 duration_ms = 5;
  string error_message = 6;        // if failed
}

message SwitchMessage {
  string direction = 1;            // inbound, outbound
  string message_type = 2;         // 0100, 0110, 0200, 0210, etc.
  string raw_payload = 3;          // masked sensitive fields
  google.protobuf.Timestamp timestamp = 4;
  string response_code = 5;
}

message StreamTransactionsRequest {
  string tenant_id = 1;            // optional, for tenant_admin scoping
  repeated string transaction_types = 2; // optional filter
}

message TransactionEvent {
  string transaction_id = 1;
  string transaction_type = 2;
  string status = 3;
  int64 amount_minor_units = 4;
  string currency = 5;
  string source_display = 6;       // masked account info
  string destination_display = 7;
  google.protobuf.Timestamp timestamp = 8;
}

message FlagTransactionRequest {
  string transaction_id = 1;
  string reason = 2;               // mandatory
  string admin_user_id = 3;
}
```

### Database Changes
```sql
-- Flagged transactions table (in admin schema, references across tenants)
CREATE TABLE admin.flagged_transactions (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    transaction_id  UUID NOT NULL,
    tenant_id       UUID NOT NULL,
    reason          TEXT NOT NULL,
    flagged_by      UUID NOT NULL REFERENCES admin.admin_users(id),
    status          VARCHAR(20) NOT NULL DEFAULT 'open'
                        CHECK (status IN ('open', 'investigating', 'resolved', 'dismissed')),
    resolution_notes TEXT,
    resolved_by     UUID REFERENCES admin.admin_users(id),
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now(),
    resolved_at     TIMESTAMPTZ
);

CREATE INDEX idx_flagged_tx_status ON admin.flagged_transactions(status);
CREATE INDEX idx_flagged_tx_transaction ON admin.flagged_transactions(transaction_id);
CREATE INDEX idx_flagged_tx_tenant ON admin.flagged_transactions(tenant_id);
CREATE INDEX idx_flagged_tx_created ON admin.flagged_transactions(created_at);

-- Transaction saga steps log (in tenant schema, for processing trail)
-- This may already exist from transaction processing stories;
-- ensure it captures all fields needed for the admin detail view.
CREATE TABLE IF NOT EXISTS {tenant_schema}.transaction_saga_steps (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    transaction_id  UUID NOT NULL REFERENCES {tenant_schema}.transactions(id),
    step_name       VARCHAR(100) NOT NULL,
    step_order      INT NOT NULL,
    status          VARCHAR(20) NOT NULL,  -- completed, failed, compensated
    started_at      TIMESTAMPTZ NOT NULL,
    completed_at    TIMESTAMPTZ,
    duration_ms     INT,
    error_message   TEXT,
    metadata        JSONB
);

CREATE INDEX idx_saga_steps_tx ON {tenant_schema}.transaction_saga_steps(transaction_id);
```

### Security Considerations
- Real-time feed requires authentication; SignalR hub validates JWT on connection
- Tenant-scoped SignalR: `tenant_admin` connections are filtered to only receive their tenant's transactions
- Switch message raw payloads have sensitive fields masked (PAN, CVV, PIN block) before storage and display
- Transaction search is rate-limited (max 20 searches per minute per admin) to prevent data exfiltration
- Flag action requires `operations`, `compliance`, or `super_admin` role
- `support` role can view transactions but cannot flag them

### Edge Cases
- High transaction volume: live feed should throttle display to max 100 TPS to prevent browser overload; buffer and batch-render
- Disconnected SignalR: auto-reconnect with exponential backoff; show "Reconnecting..." indicator and catch up on missed transactions
- Very old transactions: search for transactions older than retention period returns "Archived" indicator with limited detail
- Cross-tenant transactions (e.g., inter-tenant P2P): both tenant admins see their side; super_admin sees both sides linked
- Transaction with no saga steps (edge case bug): display "Processing trail unavailable" with raw transaction data
- Search with broad criteria returning massive result set: enforce maximum date range of 90 days, require at least one specific filter for unbounded searches

---

## Dependencies

**Prerequisite Stories:** STORY-055 (Admin Portal Foundation & RBAC)
**Blocked Stories:** STORY-061 (Dispute/Chargeback Management -- needs transaction search to identify disputed transactions)
**External Dependencies:** None beyond Blazor Server built-in SignalR

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
