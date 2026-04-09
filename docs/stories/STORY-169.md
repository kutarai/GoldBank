# STORY-169: Branch Vault Report (PDF)

**Epic:** EPIC-021 Bank Teller & Vault Client Application
**Priority:** Should Have
**Story Points:** 2
**Status:** Not Started
**Created:** 2026-04-08
**Sprint:** 28

---

## User Story

As a **branch vault manager closing the day**
I want **a one-page PDF showing opening stock, every movement, closing stock, and any variance — broken down by currency and denomination**
So that **I have a single signed document for the branch's daily cash reconciliation**

---

## Description

### Background
The Branch Vault Report is the daily counterpart to the Teller End-of-Day report (STORY-159). It captures the entire vault's day in one page that the vault manager and branch supervisor sign and file.

### Scope
**In scope:**
- New endpoint `GET /api/vault/{vaultId}/eod-report.pdf?date=YYYY-MM-DD`
- Triggered automatically at end-of-day or on demand
- A4 portrait layout
- Sections:
  - Header: tenant logo, branch name, vault name, business date
  - Opening stock table (per currency × denomination)
  - Movements table (chronological: time, type, currency, amount, teller/counterparty, breakdown summary)
  - Closing stock table (per currency × denomination)
  - Variance summary (any spot-check adjustments during the day)
  - Signature blocks: vault manager + branch supervisor
- PDF stored at `vaults.eod_report_path` (latest only — historical reports retrieved via the endpoint with explicit date)
- Spot check reports (sub-PDFs) embedded as appendices if any spot check happened that day

**Out of scope:**
- Email distribution
- Cross-day rollup reports

---

## Acceptance Criteria

- [ ] `GET /api/vault/{vaultId}/eod-report.pdf?date=2026-04-08` returns 200 + PDF for any date with vault activity; 404 if no movements
- [ ] PDF visually correct: header, opening stock, movements table, closing stock, variance, signatures
- [ ] Opening stock = closing stock from the previous business date
- [ ] Movements list every `vault_movements` row for the date in chronological order
- [ ] Closing stock = current `vault_denomination_stock` snapshot at end-of-day
- [ ] Variance summary lists any `SpotCheckAdjustment` movements with their notes
- [ ] Two signature blocks at the bottom
- [ ] If a spot check happened that day, the spot check PDF is embedded as an appendix page
- [ ] Endpoint role-protected: vault manager / branch manager / super_admin
- [ ] Generation < 1.5 s for typical day

---

## Technical Notes

### Library
QuestPDF (already added in STORY-158).

### Spot check appendix
QuestPDF supports embedding existing PDF pages via PdfSharp/PDFsharp — or simply concatenate using a separate post-processing step.

---

## Dependencies

**Prerequisite Stories:** STORY-158 (QuestPDF), 164, 168 (spot check rows referenced)

**Blocked Stories:** None

---

## Definition of Done

- [ ] Endpoint and PDF generator implemented
- [ ] Daily report visually correct
- [ ] Spot check appendix works
- [ ] Manual test on dev branch with sample data
- [ ] Code reviewed
- [ ] Merged

---

**This story was created using BMAD Method v6 - Phase 4 (Implementation)**
