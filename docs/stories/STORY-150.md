# STORY-150: Customer Card Endpoint

**Epic:** EPIC-021 Bank Teller & Vault Client Application
**Priority:** Must Have
**Story Points:** 3
**Status:** Not Started
**Created:** 2026-04-08
**Sprint:** 25

---

## User Story

As a **bank teller about to process a cash withdrawal**
I want **a single endpoint that returns the customer's photo, ID document image, signature, and key profile data**
So that **I can visually verify the person at the counter is the legitimate account holder before paying out cash**

---

## Description

### Background
FR-005 makes identity verification mandatory for every withdrawal. The teller needs to see the customer's selfie (from KYC), national-ID image (from KYC), signature (from `accounts.signature_image`), and the key profile fields all at once. A single endpoint avoids round-trips and gives the front-end a clean payload to render in the Customer Card screen (STORY-154).

### Scope
**In scope:**
- New endpoint `GET /api/teller/customers/{accountId}/card`
- Aggregates from `accounts`, `kyc_documents` (latest national_id + selfie), and signature columns
- Returns base64 data URLs for the three images so the front-end can render them as `<img>` tags directly
- Includes status flags (frozen, suspended, kyc_level, signature_verified)
- Audit-logs every access (PII access)

**Out of scope:**
- Customer search (covered by STORY-149's `/api/teller/customers/search` listed in the controller — extend it there)
- UI rendering (STORY-154)
- Editing customer data

### Response shape
```json
{
  "accountId": "ACC-000005",
  "fullName": "Willie Mapundu",
  "phone": "+263772123456",
  "email": "wmapundu@example.com",
  "dateOfBirth": "1985-04-12",
  "nationalId": "63-1234567-A-12",
  "kycLevel": 2,
  "status": "Active",
  "balanceZwg": 1234.56,
  "balanceUsd": 89.10,
  "flags": { "frozen": false, "suspended": false, "signatureVerified": true },
  "idImageUrl": "data:image/jpeg;base64,...",
  "selfieImageUrl": "data:image/jpeg;base64,...",
  "signatureImageUrl": "data:image/png;base64,...",
  "signatureVerifiedBy": "supervisor1",
  "signatureVerifiedAt": "2026-03-15T09:22:11Z"
}
```

---

## Acceptance Criteria

- [ ] `GET /api/teller/customers/{accountId}/card` returns 200 with the full payload above for a valid account ID (short ID format `ACC-NNNNNN` accepted)
- [ ] Returns 404 if the account doesn't exist or is in a different tenant
- [ ] Returns 403 if the JWT role isn't in (`teller`, `branch_manager`, `super_admin`)
- [ ] `idImageUrl` is the latest `kyc_documents.file_data` row where `document_type IN ('national_id','passport','drivers_license')`
- [ ] `selfieImageUrl` is the latest `kyc_documents.file_data` row where `document_type = 'selfie'`
- [ ] `signatureImageUrl` is built from `accounts.signature_image` (uses `image/png` MIME — signature uploads will be normalised to PNG by a future story)
- [ ] If any of the three images are missing, the corresponding URL is `null` (not an error)
- [ ] `flags.signatureVerified` = `accounts.signature_verified_at IS NOT NULL`
- [ ] Each access is recorded in `audit_logs` as `customer.card.viewed` with the teller ID, account ID, IP address
- [ ] Cache-Control header set to `no-store` (PII must not be cached by the browser)
- [ ] Endpoint returns within 500 ms p95 for accounts with images (load test with a synthetic 2 MB ID + 1 MB selfie)

---

## Technical Notes

### Query strategy
One DB hit, projecting only what's needed:
```csharp
var card = await _db.Accounts
    .Where(a => a.Id == accountId && a.TenantId == tenantId)
    .Select(a => new {
        a.Id, a.FirstName, a.LastName, ...,
        a.SignatureImage, a.SignatureVerifiedBy, a.SignatureVerifiedAt,
        IdDoc = _db.KycDocuments
            .Where(k => k.AccountId == a.Id && idTypes.Contains(k.DocumentType))
            .OrderByDescending(k => k.CreatedAt)
            .Select(k => new { k.FileData, k.ContentType }).FirstOrDefault(),
        Selfie = _db.KycDocuments
            .Where(k => k.AccountId == a.Id && k.DocumentType == "selfie")
            .OrderByDescending(k => k.CreatedAt)
            .Select(k => new { k.FileData, k.ContentType }).FirstOrDefault(),
    })
    .FirstOrDefaultAsync();
```

### Audit log
```csharp
_db.AuditLogs.Add(new AuditLog {
    AdminUserId = tellerId,
    Action = "customer.card.viewed",
    EntityType = "Account",
    EntityId = accountId.ToString(),
    Details = $"viewed by {tellerName}",
    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
    CreatedAt = DateTime.UtcNow,
});
```

### Headers
Set `Cache-Control: no-store, no-cache, must-revalidate` and `Pragma: no-cache` so the browser never caches PII.

---

## Dependencies

**Prerequisite Stories:**
- STORY-148 (drawer/cash schema for the teller controller to exist)
- KYC bytea storage (already deployed: migration `20260408070000_AddKycDocumentFileData`)
- Account signature columns (already deployed: migration `20260408080000_AddAccountSignature`)

**Blocked Stories:** STORY-154, 156

---

## Definition of Done

- [ ] Endpoint implemented and projecting only the required columns
- [ ] Audit log written on every call
- [ ] No-cache headers in place
- [ ] Integration test for: present account with all three images; missing selfie; missing signature; cross-tenant 404; non-teller 403
- [ ] Performance test: p95 < 500 ms with 2 MB ID + 1 MB selfie
- [ ] Code reviewed
- [ ] Merged to main

---

**This story was created using BMAD Method v6 - Phase 4 (Implementation)**
