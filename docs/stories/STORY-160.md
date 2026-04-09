# STORY-160: Branch Supervisor Dashboard

**Epic:** EPIC-021 Bank Teller & Vault Client Application
**Priority:** Must Have
**Story Points:** 3
**Status:** Not Started
**Created:** 2026-04-08
**Sprint:** 27

---

## User Story

As a **branch supervisor**
I want **a live dashboard showing my branch's active tellers, drawer balances, pending high-value approvals, today's volume, and variance alerts**
So that **I can spot problems and approve high-value transactions in seconds**

---

## Description

### Background
The branch supervisor is responsible for everything happening at the counter. They need an at-a-glance view of all active tellers and their drawers, plus a queue of high-value withdrawals waiting for their approval. This dashboard is the supervisor's home screen in the bank-teller app.

### Scope
**In scope:**
- New `/dashboard` route in bank-teller (replaces the default landing for users with role `branch_manager`)
- Live tiles:
  - Active tellers (count + list with running balances per currency)
  - Pending approvals (list, click → approval modal — same as STORY-152's modal)
  - Today's branch volume per currency (deposits, withdrawals, net)
  - Active variance alerts (any open drawer with non-zero variance)
- Auto-refresh every 30 seconds
- New endpoint `GET /api/teller/branches/{branchId}/dashboard` aggregates all this
- Pending approvals list comes from the same `pending_supervisor_approval` rows from STORY-152

**Out of scope:**
- Vault dashboard (STORY-167)
- Historical reports (use existing reporting engine)

---

## Acceptance Criteria

- [ ] `/dashboard` route shows the dashboard for users with role `branch_manager` or `super_admin`
- [ ] Tellers tile lists every teller with an `Open` drawer in this branch, shows their drawer running balances
- [ ] Pending Approvals tile lists every withdrawal in `pending_supervisor_approval`, click opens the approval modal
- [ ] Today's Volume tile shows total deposits, withdrawals, net per currency for the branch
- [ ] Variance Alerts tile lists any drawer that closed with a non-zero variance today
- [ ] Auto-refresh every 30 seconds (with manual "Refresh" button)
- [ ] Endpoint returns the aggregated payload in < 500 ms p95
- [ ] Tellers role does NOT see this route — they get the regular dashboard

---

## Technical Notes

### Dashboard endpoint
```csharp
[HttpGet("branches/{branchId}/dashboard")]
public async Task<IActionResult> GetBranchDashboard(Guid branchId) { ... }
```
Returns:
```json
{
  "activeTellers": [...],
  "pendingApprovals": [...],
  "todayVolume": { "USD": { "in": ..., "out": ..., "net": ... }, ... },
  "varianceAlerts": [...]
}
```

---

## Dependencies

**Prerequisite Stories:** STORY-149, 152, 157

**Blocked Stories:** None

---

## Definition of Done

- [ ] Endpoint and dashboard screen implemented
- [ ] Auto-refresh works
- [ ] Approvals click-through works
- [ ] Code reviewed
- [ ] Merged

---

**This story was created using BMAD Method v6 - Phase 4 (Implementation)**
