# STORY-171: Hot/Cold Tiering for Signed Documents

**Epic:** EPIC-021 Bank Teller & Vault Client Application
**Priority:** Should Have
**Story Points:** 2
**Status:** Not Started
**Created:** 2026-04-08
**Sprint:** 28

---

## User Story

As a **platform engineer**
I want **signed documents older than 3 months to automatically migrate from PG bytea to encrypted file storage, with retrieval staying transparent to clients**
So that **the primary database stays small and fast while older documents remain available for audit and compliance**

---

## Description

### Background
With STORY-170 storing signed scans as bytea, the primary DB grows linearly. A 3-month hot window keeps PG fast (~9 GB / branch cap) and a nightly archival job moves older rows to encrypted file storage. The disable switch (`cash.signed_doc_hot_days = 0`) lets tenants opt out entirely.

### Scope
**In scope:**
- New columns on both `branch_cash_transactions` and `vault_movements`:
  - `signed_document_archive_path` (varchar 500, nullable)
  - `document_storage_tier` enum (`Hot`, `Cold`) DEFAULT `Hot`
- Migration adding the columns
- Nightly Wolverine background job `ArchiveSignedDocumentsJob`
  - Reads `cash.signed_doc_hot_days` from `system_config` (default 90)
  - **If 0, immediately returns (job is a no-op)**
  - Otherwise selects rows where `tier = Hot AND uploaded_at < now − cutoff`
  - For each row: streams bytea → `DocumentStorageService.StoreAsync()`, sets `archive_path`, NULLs `signed_document_image`, flips `tier` to `Cold`
  - Processes in batches of 200 to avoid long transactions
- Bank-client admin retrieval endpoint updated to be tier-aware:
  - If `tier = Hot` → return bytea inline
  - If `tier = Cold` → stream from archive
- Admin "Cash Transaction" detail screen displays the tier as a badge
- Configurable via `system_config` key `cash.signed_doc_hot_days` (default 90)
- Unit test for the disabled (`= 0`) case proving the job is a no-op

**Out of scope:**
- Cross-tier migration UI
- Manual cold→hot promotion (not needed)

---

## Acceptance Criteria

- [ ] Migration adds `signed_document_archive_path` and `document_storage_tier` to both ledger tables
- [ ] Default tier is `Hot`
- [ ] Nightly job runs at 02:00 (configurable cron)
- [ ] Job reads `cash.signed_doc_hot_days` from `system_config`
- [ ] **If `cash.signed_doc_hot_days = 0`, the job logs "Tiering disabled" and exits immediately without touching any rows**
- [ ] Otherwise, job processes eligible rows in batches of 200
- [ ] After successful archival of a row: bytea is NULL, archive_path is set, tier is `Cold`
- [ ] If file storage write fails, the row is left untouched and the error is logged (idempotent retry on next run)
- [ ] Admin retrieval endpoint streams from the correct tier transparently — clients can't tell the difference
- [ ] Performance: archiving 1000 rows < 30 s
- [ ] Unit test: job with `cash.signed_doc_hot_days = 0` makes zero DB writes
- [ ] Unit test: job with `cash.signed_doc_hot_days = 90` correctly archives a row from 100 days ago and skips one from 60 days ago
- [ ] Integration test: full round-trip — upload, age the row by manipulating `signed_document_uploaded_at`, run job, fetch via admin endpoint, get the same bytes back

---

## Technical Notes

### Job skeleton
```csharp
public class ArchiveSignedDocumentsJob {
    public async Task Handle(CancellationToken ct) {
        var cutoffDays = await _config.GetIntAsync("cash.signed_doc_hot_days", 90);
        if (cutoffDays == 0) {
            _logger.LogInformation("Tiering disabled (cash.signed_doc_hot_days = 0)");
            return;
        }
        var cutoffDate = DateTime.UtcNow.AddDays(-cutoffDays);
        // batch loop, archive each, save changes
    }
}
```

### Storage path
`{filestorage_root}/signed-receipts/{tenantId}/{yyyyMM}/{txnId}.jpg`

---

## Dependencies

**Prerequisite Stories:** STORY-170

**Blocked Stories:** None

---

## Definition of Done

- [ ] Migration applied
- [ ] Wolverine job registered and runs nightly
- [ ] Disable switch verified by unit test
- [ ] Round-trip integration test passes
- [ ] Admin endpoint serves both tiers transparently
- [ ] Code reviewed
- [ ] Merged

---

**This story was created using BMAD Method v6 - Phase 4 (Implementation)**
