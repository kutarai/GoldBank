# STORY-059: KYC Review & Approval Workflow (Admin)

**Epic:** EPIC-011 Admin / Back-Office Portal
**Priority:** Must Have
**Story Points:** 5
**Status:** Not Started
**Assigned To:** Unassigned
**Created:** 2026-02-24
**Sprint:** 7

---

## User Story

As a compliance admin
I want to review and approve KYC submissions
So that identity verification meets regulatory standards and only verified customers can transact

---

## Description

### Background
Know Your Customer (KYC) compliance is a regulatory requirement for all financial services in Southern Africa. While GoldBank uses automated identity verification (ID document scanning and selfie matching), a manual review workflow is essential for cases where automated checks are inconclusive, for escalated reviews, and to satisfy regulatory requirements for human oversight. Compliance staff need a streamlined queue-based workflow to efficiently review KYC submissions, compare ID documents against selfies, and make approval decisions. This story builds the admin-facing KYC review interface that connects to the KYC infrastructure established in STORY-012.

### Scope
**In scope:**
- Pending KYC review queue with FIFO ordering
- Side-by-side display of ID document image and selfie photograph
- Auto-match confidence score display from automated verification
- Approve, reject, and request-resubmission actions
- Mandatory notes/reason for rejection and resubmission requests
- Decision triggers Wolverine events (KYCApproved / KYCRejected)
- Review metrics: pending count, average review time, approval rate
- Queue assignment to prevent duplicate reviews
- Tenant-scoped queue for `tenant_admin`

**Out of scope:**
- Automated KYC verification logic (handled in STORY-012)
- External KYC provider integration
- Enhanced due diligence (EDD) workflows for high-risk customers
- Document forgery detection algorithms

### User Flow
1. Compliance admin navigates to "KYC Review" in the sidebar
2. Dashboard shows metrics: pending count, average review time (last 7 days), approval rate, rejection rate
3. Admin clicks "Start Review" to pick up the next item in the FIFO queue
4. The item is assigned to the admin (preventing other admins from reviewing the same submission)
5. Review page displays:
   - Left panel: ID document image (zoomable)
   - Right panel: Selfie photograph (zoomable)
   - Bottom: applicant details (name, ID number, date of birth), auto-match confidence score, any previous submission history
6. Admin compares the ID document photo with the selfie
7. Admin reviews the auto-match confidence score and applicant details
8. Admin selects one of three actions:
   - **Approve**: confirms identity verification is satisfactory
   - **Reject**: identity cannot be verified (mandatory reason required)
   - **Request Resubmission**: documents are unclear or insufficient (mandatory note describing what is needed)
9. System records the decision with reviewer ID, timestamp, and notes
10. Wolverine event is published: `KYCApproved` or `KYCRejected` or `KYCResubmissionRequested`
11. Customer is notified of the outcome via push notification and SMS
12. Admin is returned to the queue to pick up the next item

---

## Acceptance Criteria

- [ ] Compliance admin sees a KYC review queue sorted by submission date (oldest first, FIFO)
- [ ] Queue displays: applicant name, submission date, document type, auto-match confidence score, assigned reviewer (if any)
- [ ] "Start Review" assigns the next unassigned item to the current admin and opens the review page
- [ ] Review page shows ID document image and selfie side by side with zoom capability
- [ ] Review page shows applicant details: full name, ID number (partially masked), date of birth, phone number
- [ ] Review page shows auto-match confidence score as a percentage with colour indicator (green > 80%, yellow 50-80%, red < 50%)
- [ ] Review page shows previous submission history if the applicant has submitted before (previous decisions and reasons)
- [ ] Admin can approve the KYC submission (no reason required, optional notes)
- [ ] Admin can reject the KYC submission with a mandatory reason (minimum 20 characters)
- [ ] Admin can request resubmission with mandatory notes describing what is needed (minimum 20 characters)
- [ ] Approval triggers `KYCApproved` Wolverine event that updates the customer's KYC status
- [ ] Rejection triggers `KYCRejected` Wolverine event that updates the customer's KYC status
- [ ] Resubmission request triggers `KYCResubmissionRequested` Wolverine event and customer notification
- [ ] Decision is logged with: reviewer admin_user_id, action, reason/notes, timestamp, time spent on review
- [ ] KYC dashboard shows: pending count, average review time (last 7 days), approval rate (%), rejection rate (%), resubmission rate (%)
- [ ] `tenant_admin` sees only KYC submissions from their tenant
- [ ] Assigned items that are not actioned within 30 minutes are automatically unassigned and returned to the queue

---

## Technical Notes

### Components
- **Blazor Pages:**
  - `Pages/KYC/KYCDashboard.razor` -- metrics dashboard with pending count and review statistics
  - `Pages/KYC/KYCReviewQueue.razor` -- FIFO queue of pending KYC submissions
  - `Pages/KYC/KYCReviewDetail.razor` -- side-by-side document review with action buttons
- **Components:**
  - `Components/KYC/DocumentViewer.razor` -- zoomable image viewer for ID documents and selfies
  - `Components/KYC/ConfidenceScoreBadge.razor` -- colour-coded confidence score display
  - `Components/KYC/ReviewActionPanel.razor` -- approve/reject/resubmit buttons with notes input
  - `Components/KYC/SubmissionHistoryPanel.razor` -- previous submissions and decisions timeline
  - `Components/KYC/KYCMetricsCards.razor` -- dashboard metric cards (pending, avg time, rates)

### API / gRPC Endpoints
```protobuf
service AdminKYCService {
  rpc GetKYCDashboard (GetKYCDashboardRequest) returns (KYCDashboardResponse);
  rpc GetPendingKYC (GetPendingKYCRequest) returns (GetPendingKYCResponse);
  rpc AssignKYCReview (AssignKYCReviewRequest) returns (KYCReviewDetailResponse);
  rpc GetKYCReviewDetail (GetKYCReviewDetailRequest) returns (KYCReviewDetailResponse);
  rpc ReviewKYC (ReviewKYCRequest) returns (ReviewKYCResponse);
  rpc UnassignKYCReview (UnassignKYCReviewRequest) returns (UnassignKYCReviewResponse);
}

message GetKYCDashboardRequest {
  string tenant_id = 1;  // optional, auto-set for tenant_admin
}

message KYCDashboardResponse {
  int32 pending_count = 1;
  int32 assigned_count = 2;
  double avg_review_time_seconds = 3;  // last 7 days
  double approval_rate = 4;            // percentage
  double rejection_rate = 5;           // percentage
  double resubmission_rate = 6;        // percentage
  int32 reviewed_today = 7;
  int32 reviewed_this_week = 8;
}

message GetPendingKYCRequest {
  string tenant_id = 1;
  int32 page = 2;
  int32 page_size = 3;  // default 20
}

message GetPendingKYCResponse {
  repeated KYCSummary items = 1;
  int32 total_count = 2;
}

message KYCSummary {
  string kyc_submission_id = 1;
  string account_id = 2;
  string applicant_name = 3;
  string document_type = 4;       // national_id, passport, drivers_license
  double auto_match_confidence = 5;
  string assigned_to = 6;         // admin_user_id, empty if unassigned
  google.protobuf.Timestamp submitted_at = 7;
  int32 previous_submission_count = 8;
}

message AssignKYCReviewRequest {
  string admin_user_id = 1;
  string tenant_id = 2;           // optional
}

message KYCReviewDetailResponse {
  string kyc_submission_id = 1;
  string account_id = 2;
  string applicant_name = 3;
  string id_number_masked = 4;    // partially masked
  google.protobuf.Timestamp date_of_birth = 5;
  string phone = 6;
  string document_type = 7;
  string id_document_url = 8;     // pre-signed URL, expires in 15 min
  string selfie_url = 9;          // pre-signed URL, expires in 15 min
  double auto_match_confidence = 10;
  repeated PreviousSubmission previous_submissions = 11;
  google.protobuf.Timestamp submitted_at = 12;
  google.protobuf.Timestamp assigned_at = 13;
}

message PreviousSubmission {
  string submission_id = 1;
  string decision = 2;            // approved, rejected, resubmission_requested
  string reason = 3;
  string reviewer_name = 4;
  google.protobuf.Timestamp decided_at = 5;
}

message ReviewKYCRequest {
  string kyc_submission_id = 1;
  string action = 2;              // approve, reject, request_resubmission
  string notes = 3;               // mandatory for reject and request_resubmission
  string admin_user_id = 4;
}

message ReviewKYCResponse {
  bool success = 1;
  string new_kyc_status = 2;
  string message = 3;
}
```

### Database Changes
```sql
-- KYC review assignments and decisions (in tenant schema)
-- Extends kyc_documents table from STORY-012 with review fields
ALTER TABLE {tenant_schema}.kyc_documents
    ADD COLUMN IF NOT EXISTS assigned_to UUID,
    ADD COLUMN IF NOT EXISTS assigned_at TIMESTAMPTZ,
    ADD COLUMN IF NOT EXISTS reviewed_by UUID,
    ADD COLUMN IF NOT EXISTS reviewed_at TIMESTAMPTZ,
    ADD COLUMN IF NOT EXISTS review_notes TEXT,
    ADD COLUMN IF NOT EXISTS review_duration_seconds INT;

-- KYC review log for full audit trail
CREATE TABLE {tenant_schema}.kyc_review_log (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    kyc_submission_id   UUID NOT NULL REFERENCES {tenant_schema}.kyc_documents(id),
    account_id          UUID NOT NULL,
    action              VARCHAR(30) NOT NULL,  -- approve, reject, request_resubmission, assign, unassign
    notes               TEXT,
    admin_user_id       UUID NOT NULL,
    review_duration_seconds INT,               -- time from assignment to decision
    created_at          TIMESTAMPTZ NOT NULL DEFAULT now()
);

CREATE INDEX idx_kyc_review_log_submission ON {tenant_schema}.kyc_review_log(kyc_submission_id);
CREATE INDEX idx_kyc_review_log_admin ON {tenant_schema}.kyc_review_log(admin_user_id);
CREATE INDEX idx_kyc_review_log_created ON {tenant_schema}.kyc_review_log(created_at);

-- Index for pending queue query
CREATE INDEX idx_kyc_documents_pending ON {tenant_schema}.kyc_documents(submitted_at)
    WHERE status = 'pending' AND assigned_to IS NULL;
```

### Security Considerations
- Only `compliance` and `super_admin` roles can access the KYC review pages
- Document images served via pre-signed URLs that expire after 15 minutes to prevent unauthorized access
- ID numbers are partially masked in the UI; full ID number visible only during active review session
- All review decisions are immutably logged (no deletion or modification of review log entries)
- Review assignment prevents concurrent review of the same submission
- Auto-unassign after 30 minutes prevents items being stuck in limbo if a reviewer abandons a session
- KYC document images must not be cached in the browser (Cache-Control: no-store)
- PII access via the KYC review pages generates audit log entries for POPIA compliance

### Edge Cases
- Document images fail to load: show retry button and fallback "Image unavailable" message; allow reviewer to skip and unassign
- Very low confidence score (< 10%): display warning banner suggesting likely mismatch, but still allow manual approval
- Concurrent assignment: when two admins click "Start Review" simultaneously, only one gets assigned (database-level lock); the other gets the next item
- Customer resubmits while review is in progress: the current review should be on the original submission; new submission queues separately
- Reviewer browser crash during review: item remains assigned for up to 30 minutes before auto-unassign
- Extremely high queue backlog: display estimated wait time for each item and highlight items waiting > 24 hours as "overdue"
- Document in unsupported format: display "Unsupported format" message and allow rejection with "document unreadable" reason

---

## Dependencies

**Prerequisite Stories:**
- STORY-055 (Admin Portal Foundation & RBAC)
- STORY-012 (KYC Document Verification -- provides the KYC infrastructure, document storage, and auto-match scoring)

**Blocked Stories:** None directly
**External Dependencies:**
- Object storage (e.g., MinIO/S3-compatible) for document images with pre-signed URL generation
- Wolverine message bus for publishing KYCApproved/KYCRejected events

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
