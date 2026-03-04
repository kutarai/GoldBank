# STORY-061: Dispute/Chargeback Management (Admin)

**Epic:** EPIC-011 Admin / Back-Office Portal
**Priority:** Should Have
**Story Points:** 5
**Status:** Not Started
**Assigned To:** Unassigned
**Created:** 2026-02-24
**Sprint:** 7

---

## User Story

As a support admin
I want to manage transaction disputes
So that customer issues are resolved fairly and in compliance with regulations

---

## Description

### Background
Transaction disputes are an inevitable part of financial services. Customers may report unauthorized transactions, duplicate charges, services not received, or incorrect amounts. UniBank needs a structured dispute management workflow that allows support staff to open, investigate, and resolve disputes fairly. Resolutions may include full refunds, partial refunds, or denial. Each resolution that involves a refund must trigger a reversal of the original transaction and its associated fees. The dispute lifecycle must be fully tracked for regulatory compliance and customer trust.

In Southern Africa, regulators expect financial institutions to have documented dispute resolution processes with defined timeframes. This system provides the operational tooling to meet those requirements.

### Scope
**In scope:**
- Dispute creation (by customer request via admin, or by admin directly)
- Dispute lifecycle: open -> investigating -> resolved -> closed
- Dispute types: unauthorized, duplicate, service_not_received, amount_error
- Resolution options: refund_full, refund_partial, denied
- Refund processing via Wolverine saga (reverse transaction and fees)
- Dispute detail with full timeline
- Dispute list with filters and search
- SLA tracking (time to resolve)

**Out of scope:**
- Customer-facing dispute submission portal (customers contact support)
- Automated dispute detection
- Card network chargeback processing (Visa/Mastercard arbitration)
- Regulatory reporting of dispute statistics (separate reporting story)

### User Flow
1. Customer contacts support about a problematic transaction
2. Support admin searches for the transaction using STORY-058 Transaction Search
3. Admin clicks "Raise Dispute" on the transaction detail page
4. Dispute creation form appears with: dispute type (dropdown), description (text), supporting evidence (file upload optional)
5. Admin selects type and enters description, then submits
6. Dispute is created in "open" status and appears in the disputes queue
7. A support or operations admin assigns the dispute to themselves for investigation
8. Admin investigates: reviews transaction details, contacts merchant if needed, reviews evidence
9. Admin updates the dispute status to "investigating" with notes
10. After investigation, admin resolves the dispute:
    - **Refund Full**: entire transaction amount + fees returned to customer
    - **Refund Partial**: specified amount returned to customer
    - **Denied**: dispute is closed with denial reason
11. If refund is selected, the system triggers a Wolverine saga to: reverse original transaction fees, credit the customer account
12. Dispute moves to "resolved" status
13. Customer is notified of the resolution
14. After a cooling period (7 days), dispute auto-closes to "closed" status

---

## Acceptance Criteria

- [ ] Support admin can create a dispute from a transaction detail page
- [ ] Admin can create a dispute independently by specifying the transaction ID
- [ ] Dispute types supported: unauthorized, duplicate, service_not_received, amount_error
- [ ] Dispute is created in "open" status with a unique dispute reference number
- [ ] Dispute list page shows all disputes with filters: status, type, date range, amount range
- [ ] Admin can assign a dispute to themselves for investigation
- [ ] Admin can update dispute status to "investigating" with mandatory notes
- [ ] Admin can resolve a dispute with "refund_full" -- credits full transaction amount + fees to customer
- [ ] Admin can resolve a dispute with "refund_partial" -- credits specified amount to customer
- [ ] Admin can resolve a dispute with "denied" -- closes dispute with mandatory denial reason
- [ ] Full refund triggers Wolverine saga that reverses the original transaction fees and credits the customer account
- [ ] Partial refund triggers Wolverine saga that credits the specified amount to the customer account
- [ ] Dispute detail page shows full timeline: creation, assignment, status changes, resolution, notes
- [ ] All dispute actions are audit-logged with admin_user_id, action, timestamp, notes
- [ ] SLA indicator shows time elapsed since dispute creation (green < 24h, yellow 24-48h, red > 48h)
- [ ] Resolved disputes auto-close after 7-day cooling period
- [ ] `tenant_admin` sees only disputes within their tenant
- [ ] Duplicate dispute prevention: cannot raise a dispute for a transaction that already has an active dispute

---

## Technical Notes

### Components
- **Blazor Pages:**
  - `Pages/Disputes/DisputeList.razor` -- filterable list of all disputes
  - `Pages/Disputes/DisputeDetail.razor` -- full dispute detail with timeline and action buttons
  - `Pages/Disputes/CreateDispute.razor` -- dispute creation form
- **Components:**
  - `Components/Disputes/DisputeTimeline.razor` -- visual timeline of dispute lifecycle events
  - `Components/Disputes/DisputeStatusBadge.razor` -- colour-coded status indicator
  - `Components/Disputes/SLAIndicator.razor` -- time elapsed with colour coding
  - `Components/Disputes/ResolutionForm.razor` -- refund/denial form with amount input for partial refunds
  - `Components/Disputes/DisputeFilterPanel.razor` -- filter controls for dispute list
  - `Components/Disputes/EvidenceUpload.razor` -- file upload for supporting evidence
- **Wolverine Handlers:**
  - `Handlers/ProcessFullRefundHandler.cs` -- saga for full refund (reverse fees + credit account)
  - `Handlers/ProcessPartialRefundHandler.cs` -- saga for partial refund (credit specified amount)
  - `Handlers/AutoCloseDisputeHandler.cs` -- scheduled handler to auto-close resolved disputes after cooling period

### API / gRPC Endpoints
```protobuf
service AdminDisputeService {
  rpc CreateDispute (CreateDisputeRequest) returns (DisputeResponse);
  rpc GetDisputes (GetDisputesRequest) returns (GetDisputesResponse);
  rpc GetDisputeDetail (GetDisputeDetailRequest) returns (DisputeDetailResponse);
  rpc AssignDispute (AssignDisputeRequest) returns (DisputeResponse);
  rpc UpdateDisputeStatus (UpdateDisputeStatusRequest) returns (DisputeResponse);
  rpc ResolveDispute (ResolveDisputeRequest) returns (ResolveDisputeResponse);
}

message CreateDisputeRequest {
  string transaction_id = 1;
  string raised_by_type = 2;     // customer, admin
  string raised_by_id = 3;       // account_id or admin_user_id
  string dispute_type = 4;       // unauthorized, duplicate, service_not_received, amount_error
  string description = 5;
  string admin_user_id = 6;      // creating admin
}

message DisputeResponse {
  string dispute_id = 1;
  string reference_number = 2;   // human-readable reference (e.g., DSP-2026-000123)
  string transaction_id = 3;
  string dispute_type = 4;
  string status = 5;
  string assigned_to = 6;
  google.protobuf.Timestamp created_at = 7;
}

message GetDisputesRequest {
  string status = 1;              // optional filter
  string dispute_type = 2;       // optional filter
  google.protobuf.Timestamp date_from = 3;
  google.protobuf.Timestamp date_to = 4;
  int64 amount_min = 5;
  int64 amount_max = 6;
  string tenant_id = 7;
  string assigned_to = 8;        // optional, filter by assigned admin
  int32 page = 9;
  int32 page_size = 10;          // default 25
}

message GetDisputesResponse {
  repeated DisputeSummary disputes = 1;
  int32 total_count = 2;
  int32 open_count = 3;
  int32 investigating_count = 4;
}

message DisputeSummary {
  string dispute_id = 1;
  string reference_number = 2;
  string transaction_id = 3;
  string dispute_type = 4;
  string status = 5;
  int64 transaction_amount = 6;
  string currency = 7;
  string customer_name = 8;
  string assigned_to_name = 9;
  google.protobuf.Timestamp created_at = 10;
  int32 hours_elapsed = 11;     // for SLA indicator
}

message DisputeDetailResponse {
  string dispute_id = 1;
  string reference_number = 2;
  string transaction_id = 3;
  string dispute_type = 4;
  string status = 5;
  string description = 6;
  string raised_by_type = 7;
  string raised_by_name = 8;
  string assigned_to = 9;
  string assigned_to_name = 10;
  string resolution = 11;        // refund_full, refund_partial, denied, empty if unresolved
  int64 resolution_amount = 12;  // for partial refunds
  string resolution_notes = 13;
  TransactionDetailResponse original_transaction = 14;  // full transaction details
  repeated DisputeEvent timeline = 15;
  google.protobuf.Timestamp created_at = 16;
  google.protobuf.Timestamp resolved_at = 17;
}

message DisputeEvent {
  string event_type = 1;          // created, assigned, status_changed, note_added, resolved, closed
  string description = 2;
  string actor_name = 3;
  string notes = 4;
  google.protobuf.Timestamp timestamp = 5;
}

message ResolveDisputeRequest {
  string dispute_id = 1;
  string resolution = 2;          // refund_full, refund_partial, denied
  int64 resolution_amount = 3;    // required for refund_partial (in minor units)
  string notes = 4;               // mandatory for denied
  string admin_user_id = 5;
}

message ResolveDisputeResponse {
  bool success = 1;
  string message = 2;
  string refund_transaction_id = 3; // if refund was issued
}
```

### Database Changes
```sql
-- Disputes table (in tenant schema)
CREATE TABLE {tenant_schema}.disputes (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    reference_number    VARCHAR(20) NOT NULL UNIQUE,  -- DSP-YYYY-NNNNNN
    transaction_id      UUID NOT NULL REFERENCES {tenant_schema}.transactions(id),
    raised_by_type      VARCHAR(10) NOT NULL CHECK (raised_by_type IN ('customer', 'admin')),
    raised_by_id        UUID NOT NULL,               -- account_id or admin_user_id
    dispute_type        VARCHAR(30) NOT NULL CHECK (dispute_type IN (
                            'unauthorized', 'duplicate', 'service_not_received', 'amount_error'
                        )),
    status              VARCHAR(20) NOT NULL DEFAULT 'open' CHECK (status IN (
                            'open', 'investigating', 'resolved', 'closed'
                        )),
    description         TEXT NOT NULL,
    assigned_to         UUID,                         -- admin_user_id
    resolution          VARCHAR(20) CHECK (resolution IN (
                            'refund_full', 'refund_partial', 'denied'
                        )),
    resolution_amount   BIGINT,                       -- in minor units, for partial refund
    resolution_notes    TEXT,
    refund_transaction_id UUID,                       -- reference to the refund transaction
    created_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
    assigned_at         TIMESTAMPTZ,
    resolved_at         TIMESTAMPTZ,
    closed_at           TIMESTAMPTZ,
    CONSTRAINT chk_partial_has_amount
        CHECK (resolution != 'refund_partial' OR resolution_amount IS NOT NULL AND resolution_amount > 0),
    CONSTRAINT chk_denied_has_notes
        CHECK (resolution != 'denied' OR resolution_notes IS NOT NULL)
);

CREATE INDEX idx_disputes_status ON {tenant_schema}.disputes(status);
CREATE INDEX idx_disputes_transaction ON {tenant_schema}.disputes(transaction_id);
CREATE INDEX idx_disputes_assigned ON {tenant_schema}.disputes(assigned_to) WHERE assigned_to IS NOT NULL;
CREATE INDEX idx_disputes_created ON {tenant_schema}.disputes(created_at);

-- Prevent duplicate active disputes on the same transaction
CREATE UNIQUE INDEX idx_disputes_active_per_tx ON {tenant_schema}.disputes(transaction_id)
    WHERE status IN ('open', 'investigating');

-- Dispute timeline events (in tenant schema)
CREATE TABLE {tenant_schema}.dispute_events (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    dispute_id      UUID NOT NULL REFERENCES {tenant_schema}.disputes(id),
    event_type      VARCHAR(30) NOT NULL,   -- created, assigned, status_changed, note_added, resolved, closed
    description     TEXT NOT NULL,
    actor_id        UUID NOT NULL,          -- admin_user_id
    actor_name      VARCHAR(200) NOT NULL,
    notes           TEXT,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX idx_dispute_events_dispute ON {tenant_schema}.dispute_events(dispute_id);

-- Sequence for reference numbers
CREATE SEQUENCE {tenant_schema}.dispute_ref_seq START 1;
```

### Security Considerations
- `support` and `operations` roles can create and manage disputes
- `super_admin` has full access to all disputes across tenants
- `tenant_admin` sees only disputes within their tenant
- `finance` role has read-only access to dispute details (for reconciliation)
- Refund amount for partial refunds cannot exceed the original transaction amount
- Full refund amount is calculated server-side (not from client input) to prevent manipulation
- Refund transactions are marked as "dispute_refund" type and linked to the original transaction and dispute
- All dispute actions are immutably logged in the dispute_events table

### Edge Cases
- Dispute on an already-reversed transaction: reject with "Transaction has already been reversed"
- Dispute on a pending transaction: allow creation but block resolution until transaction reaches terminal state
- Partial refund exceeding original amount: server-side validation prevents this
- Refund when customer account is frozen/closed: queue the refund and alert admin; hold funds in suspense account
- Concurrent dispute resolution: optimistic locking prevents two admins from resolving the same dispute
- Merchant-initiated dispute (merchant claims customer was served): handle as note on existing dispute, not separate dispute
- Dispute on a cross-tenant transaction: dispute created in the customer's tenant schema; merchant tenant notified
- Auto-close failure: if the scheduled auto-close job fails, disputes remain in "resolved" state; background job retries with exponential backoff
- High volume of disputes for a single merchant: trigger alert for potential systematic issue

---

## Dependencies

**Prerequisite Stories:** STORY-058 (Transaction Monitoring & Search -- provides transaction lookup for dispute creation)
**Blocked Stories:** None directly
**External Dependencies:**
- Wolverine for refund saga orchestration
- Background scheduler for auto-close after cooling period

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
