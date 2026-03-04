# STORY-012: KYC - Selfie Capture & Photo Match

**Epic:** EPIC-001
**Priority:** Must Have
**Story Points:** 8
**Status:** Not Started
**Assigned To:** Unassigned
**Created:** 2026-02-24
**Sprint:** 2

---

## User Story

As a new user
I want to take a selfie matched against my ID photo
So that my identity is confirmed

---

## Description

### Background
After uploading their national ID document (STORY-011), users must complete a selfie-based identity verification step. This is a critical security measure that confirms the person registering is the same individual depicted on the national ID document. The system must perform liveness detection to prevent spoofing attacks (e.g., holding up a printed photo or showing a screen) and then compare the selfie against the photo extracted from the ID document.

Given UniBank's target market of unbanked users across Southern Africa, the solution must work reliably on low-end Android devices with varying camera quality and in diverse lighting conditions. The photo matching system should support a configurable confidence threshold per tenant, allowing operators in different countries to balance security against user experience based on local conditions and regulatory requirements.

This is the highest-pointed story in Sprint 2 (8 points) due to the complexity of integrating liveness detection, photo comparison logic, and the multiple possible outcomes (auto-approve, manual review, rejection).

### Scope
**In scope:**
- Live selfie capture with liveness detection (blink detection, head movement, or challenge-response)
- Selfie image encrypted storage (AES-256, consistent with STORY-011)
- Photo comparison between selfie and ID document photo
- Configurable confidence threshold per tenant
- Auto-approve when confidence score exceeds tenant threshold
- Flag for manual review when score is between lower and upper threshold
- Auto-reject when score falls below minimum threshold
- KYC status update and Wolverine event publishing (KYCApproved, KYCRejected, KYCPendingReview)
- Selfie retake option (up to 3 attempts)

**Out of scope:**
- Building a proprietary facial recognition ML model (use third-party provider or pre-trained model)
- Manual KYC review UI (future story)
- Handling KYC rejection appeal process
- Video-based liveness detection
- Multi-face detection and rejection (future enhancement)

### User Flow
1. User has completed national ID upload (STORY-011) and is prompted for selfie
2. App opens front-facing camera with face alignment guide overlay
3. Liveness check begins: user prompted to perform action (e.g., "Blink your eyes", "Turn head slightly left")
4. App captures multiple frames during liveness check
5. App selects the best frame based on clarity and alignment
6. App displays selfie preview with option to retake or confirm
7. On confirm, app streams selfie image to `KYCService.SubmitSelfie` via gRPC
8. Server receives and encrypts the selfie image (same as ID document encryption)
9. Server extracts the face region from the previously uploaded national ID document
10. Server performs photo comparison (via third-party API or local ML model)
11. Server evaluates confidence score against tenant-configured thresholds
12. Based on score:
    - **Above upper threshold (e.g., >= 85%):** Auto-approve KYC, publish `KYCApproved` event
    - **Between thresholds (e.g., 60-84%):** Flag for manual review, publish `KYCPendingReview` event
    - **Below lower threshold (e.g., < 60%):** Auto-reject, publish `KYCRejected` event
13. Server returns result to app
14. If approved: app congratulates user and proceeds to account activation
15. If pending review: app informs user their application is under review (typically 24-48 hours)
16. If rejected: app informs user and offers retry (up to 3 attempts per registration)

---

## Acceptance Criteria

- [ ] Live selfie capture is available using the device's front-facing camera
- [ ] Liveness detection prevents photo-of-photo and screen replay attacks
- [ ] Selfie image is stored encrypted using AES-256 consistent with ID document encryption
- [ ] Photo comparison produces a confidence score between 0 and 100
- [ ] Confidence thresholds are configurable per tenant (upper threshold for auto-approve, lower threshold for auto-reject)
- [ ] Score above upper threshold automatically approves KYC and publishes `KYCApproved` Wolverine event
- [ ] Score between thresholds flags KYC for manual review and publishes `KYCPendingReview` Wolverine event
- [ ] Score below lower threshold automatically rejects KYC and publishes `KYCRejected` Wolverine event
- [ ] KYC status is updated in the database to `approved`, `pending_review`, or `rejected` accordingly
- [ ] User is allowed up to 3 selfie attempts before the registration is locked for manual intervention
- [ ] Each selfie attempt is logged with the confidence score in the audit trail
- [ ] Liveness detection works on low-end Android devices (minimum Android 8.0)
- [ ] The entire selfie capture and verification flow completes within 30 seconds on a stable connection

---

## Technical Notes

### Components
- **KYCModule** (`src/Modules/KYC/`): Extended from STORY-011
  - `KYCService.cs`: Add `SubmitSelfie` endpoint
  - `PhotoComparisonService.cs`: Orchestrates face extraction and comparison
  - `LivenessDetectionService.cs`: Server-side liveness validation (or delegates to client-side SDK)
  - `KYCThresholdConfiguration.cs`: Tenant-specific threshold configuration
  - `KYCStateMachine.cs`: Manages KYC status transitions
- **KYC Events** (`src/Modules/KYC/Events/`):
  - `KYCApproved.cs`: Published when KYC passes auto-approval
  - `KYCRejected.cs`: Published when KYC fails auto-rejection
  - `KYCPendingReview.cs`: Published when KYC requires manual review
- **SharedKernel** (`src/SharedKernel/`): Face comparison abstractions for provider swapping
- **Wolverine Handlers** (`src/Modules/KYC/Handlers/`): Event handlers for KYC status changes

### API / gRPC Endpoints

**Service:** `KYCService`

```protobuf
service KYCService {
  rpc UploadDocument(stream UploadDocumentRequest) returns (UploadDocumentResponse);  // From STORY-011
  rpc SubmitSelfie(stream SubmitSelfieRequest) returns (SubmitSelfieResponse);
  rpc GetKYCStatus(GetKYCStatusRequest) returns (GetKYCStatusResponse);
}

message SubmitSelfieRequest {
  oneof payload {
    SelfieMetadata metadata = 1;
    bytes chunk = 2;
  }
}

message SelfieMetadata {
  string account_id = 1;
  string content_type = 2;               // "image/jpeg" or "image/png"
  int64 file_size = 3;
  LivenessData liveness_data = 4;        // Client-side liveness signals
}

message LivenessData {
  repeated LivenessFrame frames = 1;     // Multiple frames from liveness check
  string challenge_type = 2;             // "blink", "head_turn", "smile"
  bool challenge_completed = 3;
}

message LivenessFrame {
  bytes frame_data = 1;
  float confidence = 2;                  // Client-side liveness confidence
  google.protobuf.Timestamp captured_at = 3;
}

message SubmitSelfieResponse {
  string selfie_id = 1;
  string kyc_status = 2;                 // "approved", "pending_review", "rejected"
  float confidence_score = 3;            // 0-100
  string message = 4;
  int32 remaining_attempts = 5;
  google.protobuf.Timestamp processed_at = 6;
}

message GetKYCStatusRequest {
  string account_id = 1;
}

message GetKYCStatusResponse {
  string account_id = 1;
  string kyc_status = 2;
  bool document_uploaded = 3;
  bool selfie_submitted = 4;
  string rejection_reason = 5;           // If rejected
  int32 remaining_attempts = 6;
  google.protobuf.Timestamp last_updated = 7;
}
```

### Database Changes

**Table:** `kyc_selfies` (schema: `{tenant_schema}`)

```sql
CREATE TABLE kyc_selfies (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    account_id UUID NOT NULL REFERENCES accounts(id),
    document_id UUID NOT NULL REFERENCES kyc_documents(id),
    file_path VARCHAR(500) NOT NULL,
    encryption_key_ref VARCHAR(255) NOT NULL,
    confidence_score DECIMAL(5,2),
    liveness_score DECIMAL(5,2),
    liveness_challenge_type VARCHAR(50),
    attempt_number INT NOT NULL DEFAULT 1,
    status VARCHAR(30) NOT NULL DEFAULT 'processing',
    rejection_reason VARCHAR(500),
    processed_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT fk_kyc_selfies_account FOREIGN KEY (account_id) REFERENCES accounts(id),
    CONSTRAINT fk_kyc_selfies_document FOREIGN KEY (document_id) REFERENCES kyc_documents(id)
);

CREATE INDEX idx_kyc_selfies_account_id ON kyc_selfies(account_id);
CREATE INDEX idx_kyc_selfies_status ON kyc_selfies(status);
```

**Table Update:** `kyc_documents` (add KYC status tracking)

```sql
ALTER TABLE kyc_documents ADD COLUMN kyc_overall_status VARCHAR(30) DEFAULT 'pending';
-- Values: pending, approved, pending_review, rejected, locked
```

**Table:** `tenant_kyc_config` (schema: `{tenant_schema}`)

```sql
CREATE TABLE tenant_kyc_config (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL,
    auto_approve_threshold DECIMAL(5,2) NOT NULL DEFAULT 85.00,
    manual_review_threshold DECIMAL(5,2) NOT NULL DEFAULT 60.00,
    max_selfie_attempts INT NOT NULL DEFAULT 3,
    liveness_required BOOLEAN NOT NULL DEFAULT TRUE,
    provider VARCHAR(100) NOT NULL DEFAULT 'internal',
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_tenant_kyc_config UNIQUE (tenant_id)
);
```

### Wolverine Events

```csharp
public record KYCApproved(
    Guid AccountId,
    Guid DocumentId,
    Guid SelfieId,
    decimal ConfidenceScore,
    string TenantId,
    DateTimeOffset ApprovedAt
);

public record KYCRejected(
    Guid AccountId,
    Guid DocumentId,
    Guid SelfieId,
    decimal ConfidenceScore,
    string RejectionReason,
    int AttemptNumber,
    string TenantId,
    DateTimeOffset RejectedAt
);

public record KYCPendingReview(
    Guid AccountId,
    Guid DocumentId,
    Guid SelfieId,
    decimal ConfidenceScore,
    string TenantId,
    DateTimeOffset FlaggedAt
);
```

### Security Considerations
- **Liveness Detection:** Must resist common spoofing vectors: printed photos, screen replays, 3D masks. Server validates liveness signals in addition to client-side checks.
- **Biometric Data Protection:** Selfie images and extracted facial feature vectors are classified as biometric data under most data protection laws. Must be encrypted, access-logged, and subject to strict retention policies.
- **Photo Comparison Privacy:** If using a third-party provider, ensure the provider does not retain images or biometric templates after comparison. Prefer on-premise or self-hosted solutions where possible.
- **Rate Limiting:** Maximum 3 selfie attempts per KYC process. After exhaustion, account is locked for manual review to prevent brute-force matching attempts.
- **Audit Trail:** Every comparison attempt logged with confidence score, liveness score, outcome, and timestamp. Logs retained per regulatory requirements.
- **Encryption:** Selfie images encrypted with the same AES-256-GCM scheme as ID documents (STORY-011). Separate encryption key per file.

### Edge Cases
- **Poor lighting conditions:** Return a descriptive error suggesting the user move to better lighting. Client-side image quality check before upload.
- **Multiple faces detected in selfie:** Reject the selfie and prompt user to ensure only their face is visible.
- **ID document photo too low quality for comparison:** If the face cannot be extracted from the ID document, flag for manual review rather than auto-rejecting.
- **Third-party provider timeout:** If the comparison service times out, retry up to 2 times with exponential backoff. If still failing, queue for asynchronous processing and notify user of delay.
- **Threshold configuration missing for tenant:** Fall back to system-wide defaults (85/60) and log a warning.
- **User closes app during processing:** Selfie already uploaded should still be processed. User can check status via `GetKYCStatus` on next app open.
- **All 3 attempts exhausted:** Lock the KYC process for this account. Require manual intervention by a KYC reviewer to unlock.

---

## Dependencies

**Prerequisite Stories:**
- STORY-011: KYC - National ID Document Upload (ID document must be uploaded first)

**Blocked Stories:**
- STORY-013: Account Activation on KYC Approval (requires KYC status resolution)

**External Dependencies:**
- Third-party facial recognition / photo comparison provider (e.g., AWS Rekognition, Azure Face API, or self-hosted open-source model like InsightFace)
- HSM or Key Management Service (shared with STORY-011)

---

## Definition of Done

- [ ] Code implemented and committed
- [ ] Unit tests written and passing (>=80% coverage)
- [ ] Integration tests passing
- [ ] Code reviewed and approved
- [ ] Documentation updated
- [ ] Acceptance criteria validated
- [ ] Deployed to staging
- [ ] Liveness detection verified against at least 3 spoofing vectors (printed photo, screen replay, static image upload)
- [ ] Photo comparison accuracy validated with test dataset (>= 95% true positive rate at configured threshold)
- [ ] Wolverine events verified: KYCApproved, KYCRejected, KYCPendingReview all published correctly
- [ ] Tenant-specific threshold configuration verified with at least 2 different tenant configurations
- [ ] Performance: end-to-end selfie verification completes within 30 seconds

---

## Progress Tracking

**Status History:**
- 2026-02-24: Created

**Actual Effort:** TBD

---

**This story was created using BMAD Method v6 - Phase 4 (Implementation)**
