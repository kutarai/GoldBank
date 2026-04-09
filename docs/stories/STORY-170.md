# STORY-170: Signed Document Capture Workflow

**Epic:** EPIC-021 Bank Teller & Vault Client Application
**Priority:** Must Have
**Story Points:** 5
**Status:** Not Started
**Created:** 2026-04-08
**Sprint:** 28

---

## User Story

As a **bank teller and vault manager**
I want **to upload the scanned, physically-signed receipt against every cash transaction, with the system blocking my end-of-day until they're all in**
So that **the bank has a legally binding paper trail proving every cash movement was authorised by both parties**

---

## Description

### Background
FR-019 makes signed-document capture mandatory for every cash-handling transaction (deposits, withdrawals, reversals, vault float-outs, surrenders, injections, withdrawals to HQ). The system stores the scan as bytea on the originating ledger row, with a hard 2 MB cap enforced by a server-side compression pipeline. End-of-day flows are blocked while any transaction is still `pending_signature`.

### Scope
**In scope:**
- New columns on `branch_cash_transactions` and `vault_movements`:
  - `signed_document_image` (bytea, nullable)
  - `signed_document_content_type` (varchar 50, nullable)
  - `signed_document_uploaded_by` (uuid, nullable, FK admin_users)
  - `signed_document_uploaded_at` (timestamptz, nullable)
  - `document_status` enum (`PendingSignature`, `Completed`, `NotRequired`) DEFAULT `PendingSignature`
- Migration that adds these columns to both tables
- Server-side compression pipeline (`SignedDocumentCompressor`):
  - Image input: downscale longest edge to ≤ 2000 px, re-encode JPEG q80
  - PDF input: rasterize at 200 DPI, then JPEG q80
  - Iterate q80 → q75 → q70 → … until ≤ 2 MB
  - Reject only if at q50 it still exceeds 2 MB
- New upload endpoints (multipart):
  - `POST /api/teller/cash-transactions/{id}/signed-document`
  - `POST /api/vault/movements/{id}/signed-document`
- Both endpoints validate the row belongs to the requester's tenant and is in `PendingSignature` state, run the compressor, store the result, set the metadata, transition status to `Completed`, write an audit log entry
- Re-uploads allowed; previous version moved to a JSONB version-history field (`signed_document_history_json`)
- Bank-client admin endpoint `GET /api/admin/cash-transactions/{id}/signed-document` returns the bytes for back-office review
- UI: "Upload signed receipt" button on every cash transaction in the day's list, with a pending counter on the dashboard
- UI: drawer close + vault EOD blocked until zero `pending_signature` rows
- UI: file picker accepts PNG, JPEG, PDF; max original size 50 MB (server compresses to ≤ 2 MB)

**Out of scope:**
- Hot/cold tiering (STORY-171)

---

## Acceptance Criteria

- [ ] Migration adds the new columns to both ledger tables
- [ ] On successful cash transaction creation, `document_status` defaults to `PendingSignature`
- [ ] Spot check adjustment movements default to `NotRequired` (have their own report flow)
- [ ] Upload endpoint accepts multipart with PNG, JPEG, PDF; rejects other types
- [ ] Compression pipeline produces a JPEG payload ≤ 2 MB for normal scans
- [ ] If even q50 still exceeds 2 MB, returns 422 with a clear error
- [ ] On success, row transitions to `Completed`, metadata fields populated, audit log written
- [ ] Re-upload appends the previous version to `signed_document_history_json` (each version: bytes_md5, content_type, uploaded_by, uploaded_at)
- [ ] Drawer close (`POST /api/teller/drawer/close`) returns 422 if any of today's `branch_cash_transactions` for this drawer are still `PendingSignature`
- [ ] Vault EOD endpoint blocks similarly
- [ ] UI shows a "pending signatures" counter in the drawer header
- [ ] Bank-client admin endpoint streams the image with the correct content type
- [ ] Integration tests cover: PNG upload, JPEG upload, PDF upload, oversize rejection, re-upload version history, EOD block

---

## Technical Notes

### Compression service
Use `SkiaSharp` for image processing and `Docnet.Core` (or `PdfPig`) for PDF rasterization.

### Endpoint signature
```csharp
[HttpPost("cash-transactions/{id}/signed-document")]
[RequestSizeLimit(50 * 1024 * 1024)] // 50 MB before compression
public async Task<IActionResult> UploadSignedDoc(Guid id, IFormFile file) { ... }
```

### EOD block check
```csharp
var pendingCount = await _db.BranchCashTransactions
    .CountAsync(t => t.DrawerSessionId == drawerId && t.DocumentStatus == DocumentStatus.PendingSignature);
if (pendingCount > 0) return UnprocessableEntity($"{pendingCount} transactions still missing signed documents.");
```

---

## Dependencies

**Prerequisite Stories:** STORY-148, 149, 164, 166

**Blocked Stories:** STORY-171 (tiering builds on these columns)

---

## Definition of Done

- [ ] Migration applied
- [ ] Compression pipeline tested with real scans
- [ ] Upload endpoints work for both ledgers
- [ ] EOD blocks enforced
- [ ] UI flow complete
- [ ] Re-upload version history working
- [ ] Code reviewed
- [ ] Merged

---

**This story was created using BMAD Method v6 - Phase 4 (Implementation)**
