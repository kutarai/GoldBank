# STORY-158: Receipt PDF Generation + Printing

**Epic:** EPIC-021 Bank Teller & Vault Client Application
**Priority:** Must Have
**Story Points:** 3
**Status:** Not Started
**Created:** 2026-04-08
**Sprint:** 27

---

## User Story

As a **bank teller**
I want **the system to generate a printable A6 PDF receipt for every cash transaction with branch logo, denominations, and signature blocks**
So that **the customer has an immediate physical record and the bank has a signed paper trail**

---

## Description

### Background
Each cash transaction (deposit / withdrawal / vault movement) needs a receipt with both parties' signature blocks. The receipt is generated server-side as a PDF that the front-end can open in a new tab for printing on a thermal printer.

### Scope
**In scope:**
- New `IReceiptPdfService` with implementations for deposit, withdrawal, vault movement
- QuestPDF library for PDF generation
- A6 layout (105 × 148 mm) suitable for 80 mm thermal printers
- Includes: tenant logo, branch name, transaction reference, date/time, teller name, customer name, depositor name, currency, amount, denomination breakdown, QR code with the transaction reference, signature blocks
- New endpoint `GET /api/teller/transactions/{id}/receipt.pdf` returns the PDF stream
- Reprint endpoint reuses the same generator
- PDF stored at `branch_cash_transactions.receipt_pdf_path` (file storage) on first generation; subsequent fetches read from disk

**Out of scope:**
- Browser-side PDF generation
- Receipt language localisation (open question in epic)
- Pre-printed templates / merge

---

## Acceptance Criteria

- [ ] `GET /api/teller/transactions/{id}/receipt.pdf` returns 200 with `Content-Type: application/pdf`
- [ ] PDF is A6 portrait, fits within an 80 mm thermal printer
- [ ] Header section: tenant logo (left), branch name + address (right)
- [ ] Body: type label (DEPOSIT / WITHDRAWAL), reference, date, teller, account, customer name, depositor name (deposits only), currency, formatted amount
- [ ] Denomination breakdown table: face value, count, sub-total, grand total
- [ ] Footer: QR code with the reference + thank-you line + two signature blocks (`Customer:` / `Teller:`)
- [ ] First call generates and stores the PDF; subsequent calls serve from disk
- [ ] Returns 404 for non-existent transaction
- [ ] Returns 403 if the requester isn't the originating teller, branch_manager, or super_admin
- [ ] Generation is fast: p95 < 200 ms for the first call, < 50 ms for cached calls
- [ ] Front-end "Print" button on receipt preview opens the PDF in a new tab via `window.open(...)`
- [ ] Reprint endpoint accessible from a `Reprint` button on the day's transaction list

---

## Technical Notes

### Library
QuestPDF — `dotnet add package QuestPDF` — Apache 2.0 license, no runtime cost.

### Service
```csharp
public interface IReceiptPdfService {
    Task<byte[]> GenerateAsync(BranchCashTransaction txn, CancellationToken ct = default);
}
```

### Storage path
`{filestorage_root}/receipts/{tenantId}/{yyyyMM}/{txnId}.pdf`

---

## Dependencies

**Prerequisite Stories:** STORY-149

**Blocked Stories:** None

---

## Definition of Done

- [ ] Service generates valid A6 PDF
- [ ] Endpoint serves PDF
- [ ] Caching to disk works
- [ ] Print button on UI opens PDF
- [ ] Code reviewed
- [ ] Merged

---

**This story was created using BMAD Method v6 - Phase 4 (Implementation)**
