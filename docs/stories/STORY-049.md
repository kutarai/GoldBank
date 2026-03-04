# STORY-049: Remote Terminal Software Updates

**Epic:** EPIC-009 Terminal Management & HSM
**Priority:** Should Have
**Story Points:** 3
**Status:** Not Started
**Assigned To:** Unassigned
**Created:** 2026-02-24
**Sprint:** 6

---

## User Story

As an **admin**
I want to **push software updates to terminals remotely**
So that **terminals stay current without requiring physical visits to merchant locations**

---

## Description

### Background

Maintaining a fleet of EFT POS terminals across Southern Africa presents a significant logistical challenge. Many terminals are deployed in remote locations — rural shops, informal markets, township businesses — where sending a technician for a firmware update is costly and time-consuming. Remote update capability transforms terminal fleet management from a field service operation to a centralized one.

UniBank's remote update system uses MQTT as the delivery notification channel. When an admin initiates an update, the system publishes an update manifest to the terminal's MQTT topic. The terminal downloads the update package from a secure URL, verifies its integrity via SHA-256 checksum, and applies it according to the specified policy (immediately, on next idle period, or at a scheduled time). The terminal reports update progress back through its status topic, giving the operations team real-time visibility into the rollout.

Three types of updates are supported:
- **Firmware updates:** Low-level device firmware from the terminal vendor. These typically require a terminal reboot.
- **Application updates:** The UniBank terminal application that handles payment processing, UI, and MQTT communication.
- **Configuration updates:** Runtime configuration changes that do not require a restart (e.g., transaction limits, supported payment types, UI themes).

**Functional Requirement:** FR-036

### Scope

**In scope:**
- Update manifest publication via MQTT topic `terminals/{id}/updates`
- Three update types: firmware, application, configuration
- Three apply policies: immediate, next_idle, scheduled
- SHA-256 checksum verification for update package integrity
- Update status tracking per terminal (downloading, applying, success, failed)
- Failed update retry mechanism (up to 3 attempts)
- Manual intervention flagging for persistent failures
- Rollback support for failed application and firmware updates
- Batch update rollout across terminal groups (by merchant, model, or tenant)
- Update history and audit trail

**Out of scope:**
- Update package building and signing (handled by CI/CD pipeline)
- Terminal-side update application logic (terminal vendor responsibility)
- Over-the-air (OTA) modem firmware updates (carrier responsibility)
- A/B partition management on the terminal (terminal firmware responsibility)
- Update package hosting infrastructure (uses existing object storage)

### User Flow

**Single terminal update:**
1. Admin selects a terminal (or group of terminals) in the admin portal
2. Admin chooses update type (firmware/app/config), selects the update package version, and sets the apply policy
3. System validates: target terminal(s) are active, update package exists, version is newer than current
4. System creates `terminal_updates` records and publishes update manifest to `terminals/{terminal_id}/updates` via MQTT
5. Terminal receives manifest, begins downloading from the `download_url`
6. Terminal verifies SHA-256 checksum of downloaded package
7. Terminal applies update according to policy (immediate: apply now; next_idle: wait for no active transactions; scheduled: apply at specified time)
8. Terminal reports progress on `terminals/{terminal_id}/status`: `downloading` -> `applying` -> `success` or `failed`
9. Terminal Manager updates `terminal_updates` record with each status change
10. On success: terminal's firmware/app version is updated in the `terminals` table
11. On failure: retry counter incremented. After 3 failures, terminal is flagged for manual intervention

**Batch rollout:**
1. Admin selects a group filter (all terminals of model X, all terminals for merchant Y, all terminals in tenant Z)
2. System stages the update for all matching terminals
3. Rollout proceeds in configurable waves (e.g., 10% of terminals first, then 50%, then 100%) with a configurable delay between waves
4. If failure rate exceeds threshold (default 20%) during a wave, rollout pauses and alerts the admin

---

## Acceptance Criteria

- [ ] Updates are pushed to terminals via MQTT topic `terminals/{terminal_id}/updates` with a JSON manifest containing: update_type, version, download_url, checksum_sha256, and apply_policy
- [ ] Three update types are supported: `firmware`, `app`, and `config`
- [ ] Three apply policies are supported: `immediate`, `next_idle`, and `scheduled`
- [ ] Terminal downloads the update package and verifies its SHA-256 checksum before applying
- [ ] Update status is tracked per terminal with states: `pending`, `downloading`, `applying`, `success`, `failed`, `rolled_back`
- [ ] Failed updates are retried automatically up to 3 times with exponential backoff
- [ ] After 3 consecutive failures, the terminal is flagged for manual intervention and an alert is raised
- [ ] Config changes can be applied without physical access to the terminal or terminal restart
- [ ] Batch updates can be initiated for groups of terminals filtered by model, merchant, or tenant
- [ ] Update history is maintained in the `terminal_updates` table with full audit trail
- [ ] Rollout can be paused if failure rate exceeds configurable threshold

---

## Technical Notes

### Components

| Component | Location | Purpose |
|-----------|----------|---------|
| `TerminalUpdateService.cs` | `src/Satellites/UniBank.TerminalManager/Services/` | gRPC service for update management |
| `UpdateDistributionHandler.cs` | `src/Satellites/UniBank.TerminalManager/Handlers/` | Wolverine handler for MQTT update publication |
| `UpdateStatusHandler.cs` | `src/Satellites/UniBank.TerminalManager/Handlers/` | Processes update status reports from terminals |
| `BatchRolloutOrchestrator.cs` | `src/Satellites/UniBank.TerminalManager/Jobs/` | Manages staged batch rollouts |
| `UpdateRetryHandler.cs` | `src/Satellites/UniBank.TerminalManager/Handlers/` | Retry logic for failed updates |
| `TerminalUpdateFailedAlert.cs` | `src/Shared/UniBank.Events/Terminal/` | Wolverine event for update failure alerting |
| `BatchRolloutPausedAlert.cs` | `src/Shared/UniBank.Events/Terminal/` | Wolverine event for paused rollout |

### API / gRPC Endpoints

**Additions to `terminal_service.proto`:**

```protobuf
service TerminalUpdateService {
  rpc PushUpdate (PushUpdateRequest) returns (PushUpdateResponse);
  rpc PushBatchUpdate (PushBatchUpdateRequest) returns (PushBatchUpdateResponse);
  rpc GetUpdateStatus (GetUpdateStatusRequest) returns (GetUpdateStatusResponse);
  rpc GetUpdateHistory (GetUpdateHistoryRequest) returns (GetUpdateHistoryResponse);
  rpc RetryUpdate (RetryUpdateRequest) returns (RetryUpdateResponse);
  rpc PauseRollout (PauseRolloutRequest) returns (PauseRolloutResponse);
  rpc ResumeRollout (ResumeRolloutRequest) returns (ResumeRolloutResponse);
  rpc CancelUpdate (CancelUpdateRequest) returns (CancelUpdateResponse);
}

message PushUpdateRequest {
  int32 terminal_id = 1;
  string update_type = 2;          // "firmware", "app", "config"
  string version = 3;
  string download_url = 4;         // Signed URL to update package
  string checksum_sha256 = 5;
  string apply_policy = 6;         // "immediate", "next_idle", "scheduled"
  int64 scheduled_at_unix = 7;     // Only used with "scheduled" policy
  string tenant_id = 8;
  string release_notes = 9;        // Optional description of changes
}

message PushUpdateResponse {
  int32 update_id = 1;
  string status = 2;               // "pending"
  bool success = 3;
  string error_message = 4;
}

message PushBatchUpdateRequest {
  string update_type = 1;
  string version = 2;
  string download_url = 3;
  string checksum_sha256 = 4;
  string apply_policy = 5;
  int64 scheduled_at_unix = 6;
  string tenant_id = 7;
  string release_notes = 8;
  // Targeting criteria (at least one required)
  string model_filter = 9;         // Target specific terminal model
  string merchant_id_filter = 10;  // Target specific merchant's terminals
  bool all_active_terminals = 11;  // Target all active terminals in tenant
  // Rollout strategy
  int32 wave_size_percent = 12;    // Percentage of terminals per wave (default: 100)
  int32 wave_delay_minutes = 13;   // Delay between waves (default: 0)
  int32 failure_threshold_pct = 14;// Pause rollout if failure rate exceeds this (default: 20)
}

message PushBatchUpdateResponse {
  int32 rollout_id = 1;
  int32 target_terminal_count = 2;
  int32 wave_count = 3;
  bool success = 4;
  string error_message = 5;
}

message GetUpdateStatusRequest {
  int32 update_id = 1;
  string tenant_id = 2;
}

message GetUpdateStatusResponse {
  int32 update_id = 1;
  int32 terminal_id = 2;
  string update_type = 3;
  string version = 4;
  string status = 5;
  int32 retry_count = 6;
  int64 initiated_at_unix = 7;
  int64 completed_at_unix = 8;
  string failure_reason = 9;
}

message GetUpdateHistoryRequest {
  int32 terminal_id = 1;
  string tenant_id = 2;
  int32 page = 3;
  int32 page_size = 4;
}

message GetUpdateHistoryResponse {
  repeated GetUpdateStatusResponse updates = 1;
  int32 total_count = 2;
}
```

### MQTT Update Protocol

**Update manifest** (published to `terminals/{terminal_id}/updates`):

```json
{
  "update_id": 5001,
  "update_type": "firmware",
  "version": "3.3.0",
  "download_url": "https://updates.unibank.internal/packages/pax-a920/firmware-3.3.0.bin?sig=SIGNED_TOKEN",
  "checksum_sha256": "a1b2c3d4e5f6...64_hex_chars",
  "apply_policy": "next_idle",
  "scheduled_at": null,
  "release_notes": "Security patch for NFC stack, battery optimization",
  "max_retry_count": 3,
  "timestamp": "2026-02-24T14:00:00Z"
}
```

**Update status report** (terminal publishes to `terminals/{terminal_id}/status`):

```json
{
  "message_type": "update_status",
  "update_id": 5001,
  "update_type": "firmware",
  "version": "3.3.0",
  "status": "downloading",
  "progress_percent": 45,
  "timestamp": "2026-02-24T14:02:00Z"
}
```

```json
{
  "message_type": "update_status",
  "update_id": 5001,
  "update_type": "firmware",
  "version": "3.3.0",
  "status": "success",
  "progress_percent": 100,
  "timestamp": "2026-02-24T14:05:00Z"
}
```

```json
{
  "message_type": "update_status",
  "update_id": 5001,
  "update_type": "firmware",
  "version": "3.3.0",
  "status": "failed",
  "error_message": "Checksum verification failed",
  "retry_count": 1,
  "timestamp": "2026-02-24T14:03:00Z"
}
```

### Database Changes

**terminal_updates table** (in `terminal_manager` schema):

```sql
CREATE TABLE terminal_manager.terminal_updates (
    id              SERIAL PRIMARY KEY,
    tenant_id       VARCHAR(50) NOT NULL,
    terminal_id     INT NOT NULL REFERENCES terminal_manager.terminals(id),
    rollout_id      INT,                    -- NULL for single updates, references batch rollout
    update_type     VARCHAR(20) NOT NULL CHECK (update_type IN ('firmware', 'app', 'config')),
    version         VARCHAR(50) NOT NULL,
    download_url    TEXT NOT NULL,
    checksum_sha256 VARCHAR(64) NOT NULL,
    apply_policy    VARCHAR(20) NOT NULL CHECK (apply_policy IN ('immediate', 'next_idle', 'scheduled')),
    scheduled_at    TIMESTAMPTZ,
    status          VARCHAR(20) NOT NULL DEFAULT 'pending'
                    CHECK (status IN ('pending', 'downloading', 'applying', 'success',
                                      'failed', 'rolled_back', 'cancelled')),
    retry_count     SMALLINT NOT NULL DEFAULT 0,
    max_retries     SMALLINT NOT NULL DEFAULT 3,
    failure_reason  TEXT,
    release_notes   TEXT,
    initiated_at    TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    completed_at    TIMESTAMPTZ,
    flagged_manual  BOOLEAN NOT NULL DEFAULT FALSE
);

CREATE INDEX idx_terminal_updates_terminal ON terminal_manager.terminal_updates (terminal_id, initiated_at DESC);
CREATE INDEX idx_terminal_updates_status ON terminal_manager.terminal_updates (status)
    WHERE status IN ('pending', 'downloading', 'applying');
CREATE INDEX idx_terminal_updates_rollout ON terminal_manager.terminal_updates (rollout_id)
    WHERE rollout_id IS NOT NULL;
CREATE INDEX idx_terminal_updates_flagged ON terminal_manager.terminal_updates (tenant_id)
    WHERE flagged_manual = TRUE AND status = 'failed';
```

**batch_rollouts table:**

```sql
CREATE TABLE terminal_manager.batch_rollouts (
    id                      SERIAL PRIMARY KEY,
    tenant_id               VARCHAR(50) NOT NULL,
    update_type             VARCHAR(20) NOT NULL,
    version                 VARCHAR(50) NOT NULL,
    target_filter           JSONB NOT NULL,          -- Serialized filter criteria
    total_terminals         INT NOT NULL,
    wave_size_percent       SMALLINT NOT NULL DEFAULT 100,
    wave_delay_minutes      SMALLINT NOT NULL DEFAULT 0,
    failure_threshold_pct   SMALLINT NOT NULL DEFAULT 20,
    current_wave            SMALLINT NOT NULL DEFAULT 1,
    total_waves             SMALLINT NOT NULL,
    status                  VARCHAR(20) NOT NULL DEFAULT 'active'
                            CHECK (status IN ('active', 'paused', 'completed', 'cancelled')),
    success_count           INT NOT NULL DEFAULT 0,
    failure_count           INT NOT NULL DEFAULT 0,
    initiated_at            TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    completed_at            TIMESTAMPTZ,
    initiated_by            VARCHAR(100) NOT NULL    -- Admin who started the rollout
);

CREATE INDEX idx_batch_rollouts_status ON terminal_manager.batch_rollouts (status)
    WHERE status IN ('active', 'paused');
```

### Security Considerations

- **Download URL Signing:** Update package URLs are time-limited signed URLs (expire after 1 hour). This prevents unauthorized package downloads even if the URL is intercepted.
- **Checksum Verification:** The SHA-256 checksum in the manifest is computed server-side from the original package. The terminal must verify the downloaded package's checksum matches before applying. Checksum mismatch aborts the update.
- **Package Signing (Future):** In a future enhancement, update packages should be code-signed with a platform key. The terminal would verify the signature in addition to the checksum, providing authenticity in addition to integrity.
- **MQTT TLS:** All update manifests are transmitted over TLS-encrypted MQTT. The download URL itself uses HTTPS.
- **Tenant Isolation:** Admins can only push updates to terminals within their tenant. The `PushUpdate` and `PushBatchUpdate` endpoints validate the terminal's tenant against the caller's tenant context.
- **Rollback Safety:** For firmware and app updates, the terminal should maintain a rollback partition. If the update fails to boot, the terminal reverts to the previous version and reports `rolled_back` status.
- **Audit Trail:** All update operations are logged: who initiated, what version, which terminals, outcome. This provides a complete chain of custody for every software change in the fleet.

### Edge Cases

- **Terminal offline when update is pushed:** The MQTT message is published with QoS 1 (at least once delivery). The MQTT broker retains the message until the terminal reconnects and receives it. The `terminal_updates` record remains in `pending` state until the terminal reports download start.
- **Update download interrupted:** If the terminal loses connectivity during download, it resumes from where it left off on reconnection (if the terminal firmware supports HTTP range requests). The `downloading` status is maintained, and the retry counter is not incremented for connection-related interruptions.
- **Checksum mismatch:** The terminal reports `failed` with reason "checksum_verification_failed". The system retries by re-publishing the manifest. If the checksum fails 3 times, the package itself may be corrupt — manual investigation is required.
- **Terminal reboots during firmware update:** This is a critical failure scenario. Terminals should use A/B partition schemes to protect against bricked devices. If the terminal does not come back online after a firmware update, it is flagged as `fault` with the update marked `failed`.
- **Concurrent updates:** Only one update per terminal can be in progress at a time. If a new update is pushed while one is in progress, the new update is queued in `pending` state until the current update completes or is cancelled.
- **Batch rollout failure cascade:** If the failure rate in a wave exceeds the threshold, the rollout pauses automatically. Terminals that already received the manifest continue their update; terminals in subsequent waves do not receive the manifest until the rollout is resumed.
- **Version downgrade:** By default, the system warns if the target version is older than the current version (potential downgrade). Admins can force a downgrade by setting an explicit override flag, which is logged in the audit trail.

---

## Dependencies

**Prerequisite Stories:**
- STORY-046: Terminal Registration & Provisioning — terminal records and MQTT topic structure must exist

**Blocked Stories:**
- None directly — remote updates are an operational capability that enhances existing terminal management

**External Dependencies:**
- Object storage (S3-compatible or Azure Blob) for hosting update packages
- MQTT broker with QoS 1 support and message persistence
- Terminal firmware must implement the update protocol (download, checksum verify, apply, report status)

---

## Definition of Done

- [ ] Code implemented and committed
- [ ] Unit tests written and passing (>=80% coverage) — update initiation, status processing, retry logic, batch rollout orchestration
- [ ] Integration tests passing — full update flow with MQTT broker and mock terminal
- [ ] Retry mechanism tested — failed update retries up to 3 times, then flags for manual intervention
- [ ] Batch rollout tested — wave-based rollout with pause on failure threshold
- [ ] Checksum verification tested — valid checksum succeeds, invalid checksum triggers failure
- [ ] Concurrent update prevention tested — second update queued while first is in progress
- [ ] Code reviewed and approved
- [ ] Documentation updated (update protocol specification, rollout procedures)
- [ ] Acceptance criteria validated
- [ ] Deployed to staging

---

## Progress Tracking

**Status History:**
- 2026-02-24: Created

**Actual Effort:** TBD

---

**This story was created using BMAD Method v6 - Phase 4 (Implementation)**
