# STORY-159: End-of-Day Teller Report

**Epic:** EPIC-021 Bank Teller & Vault Client Application
**Priority:** Must Have
**Story Points:** 3
**Status:** Not Started
**Created:** 2026-04-08
**Sprint:** 27

---

## User Story

As a **bank teller closing my drawer**
I want **a one-page PDF summarising my entire shift — opening float, every transaction, closing balance, variance, and signature blocks**
So that **I have a printable record to sign with my supervisor at the end of the day**

---

## Description

### Background
At end-of-shift the teller and supervisor sign off on the cash count. The system needs to produce an authoritative single-page PDF that lists every transaction and the variance (if any). This PDF is stored against the drawer session and can be reprinted on demand.

### Scope
**In scope:**
- New endpoint `GET /api/teller/drawer/{sessionId}/eod-report.pdf`
- Triggered automatically when a drawer is closed
- A4 portrait layout
- Header: tenant logo, branch, business date, teller name + ID
- Body: opening float per currency (with denomination breakdown), all transactions in chronological order, totals (deposits, withdrawals, reversals) per currency, expected closing balance, counted closing balance, variance per currency
- Footer: signature blocks for teller and closing supervisor
- Stored at `teller_drawer_sessions.eod_report_path`

**Out of scope:**
- Branch-level rollup (STORY-160 / 169)
- Email distribution

---

## Acceptance Criteria

- [ ] Endpoint returns 200 + PDF for a closed drawer; 404 for non-existent; 403 for non-owner
- [ ] PDF includes: tenant logo, branch name, business date, teller name + admin id
- [ ] Opening float section: per currency, denominations and total
- [ ] Transactions table: time, ref, type (Deposit/Withdrawal/Reversal), account, currency, amount, signed-status
- [ ] Totals section: per currency, total in, total out, net, expected closing
- [ ] Variance section: per currency, expected vs counted vs variance, highlighted red if non-zero
- [ ] Signature blocks at the bottom: Teller, Supervisor (closing), with name + signature line
- [ ] Generation triggered automatically on drawer close (Wolverine event handler)
- [ ] PDF cached on disk; subsequent fetches serve from disk
- [ ] Generation < 1 s for a typical 100-transaction shift

---

## Technical Notes

### Wolverine event handler
```csharp
public class DrawerClosedHandler : IWolverineHandler<DrawerClosedEvent> {
    public async Task Handle(DrawerClosedEvent evt, IEodReportService svc) {
        await svc.GenerateAsync(evt.DrawerSessionId);
    }
}
```

### PDF builder
QuestPDF, single A4 page; if a single shift exceeds the page (rare, > 200 txns), spill onto page 2.

---

## Dependencies

**Prerequisite Stories:** STORY-149, 158 (QuestPDF dependency added)

**Blocked Stories:** STORY-160 (branch dashboard reuses the report data)

---

## Definition of Done

- [ ] Endpoint and Wolverine event handler implemented
- [ ] PDF visually correct on a real shift
- [ ] Cached on disk
- [ ] Code reviewed
- [ ] Merged

---

**This story was created using BMAD Method v6 - Phase 4 (Implementation)**
