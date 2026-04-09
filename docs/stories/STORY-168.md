# STORY-168: Spot Check Workflow

**Epic:** EPIC-021 Bank Teller & Vault Client Application
**Priority:** Must Have
**Story Points:** 3
**Status:** Not Started
**Created:** 2026-04-08
**Sprint:** 28

---

## User Story

As a **branch vault manager**
I want **the system to remind me when a spot check is due, walk me through the count with a witness, and automatically reconcile any variance against vault stock**
So that **the vault is verified on schedule and the books always match physical reality**

---

## Description

### Background
FR-016 requires periodic spot checks based on a per-vault schedule. The vault manager and a witness physically count the vault, enter the actual denomination breakdown, and any variance is recorded plus auto-applied as a `SpotCheckAdjustment` movement to keep the digital ledger in sync with the physical truth.

### Scope
**In scope:**
- Background task scheduler that reads each vault's `spot_check_cron` and raises a "spot check due" task on the dashboard when overdue
- The task surfaces as a banner on the Vault Dashboard with a "Start Spot Check" CTA
- Spot Check screen flow:
  1. System shows the EXPECTED stock (per currency, per denomination) loaded from `vault_denomination_stock`
  2. Vault manager + witness physically count
  3. Both enter their PINs (re-auth)
  4. Vault manager enters the ACTUAL count via the denomination grid
  5. System computes variance per denomination per currency
  6. If variance is non-zero, manager enters a Notes/explanation
  7. Confirm — server inserts `vault_spot_checks` row + a `SpotCheckAdjustment` `vault_movements` row that brings stock in line with reality
- After spot check, the vault's `last_spot_check_at` and `last_spot_check_result` are updated
- The variance and Notes are flagged in audit logs for branch manager / finance review

**Out of scope:**
- Spot check PDF report (STORY-169)
- Adjustment "approval" flow — adjustments are recorded immediately because the vault manager + witness already provide dual control

---

## Acceptance Criteria

- [ ] Background scheduler runs every 5 minutes; for each active vault, computes whether `last_spot_check_at + spot_check_cron_interval` ≤ now; if so, sets a flag in Redis `vault.{id}.spot_check_due = true`
- [ ] Vault Dashboard reads the flag and shows a banner "Spot Check Due — Start Now"
- [ ] Clicking the banner opens `/vault/spot-check`
- [ ] Spot Check screen loads the expected stock and displays it side-by-side with empty count inputs
- [ ] Witness PIN field requires a username + PIN of a different admin user with role `branch_manager`, `vault_manager`, or `super_admin`
- [ ] Variance per denomination computed live as the manager enters counts
- [ ] If any variance ≠ 0, Notes field is required (min 10 chars)
- [ ] Confirm calls `POST /api/vault/{vaultId}/spot-check` with `{ actualBreakdownsByCurrency, witnessUsername, witnessPin, notes }`
- [ ] Server creates `vault_spot_checks` row, computes variance, inserts compensating `vault_movements` row if any variance, updates `vault_denomination_stock` via `VaultStockService`, updates vault's `last_spot_check_at` and `last_spot_check_result`
- [ ] Audit log entry `vault.spot_check.completed` with manager + witness IDs and variance summary
- [ ] After successful submission, navigate back to dashboard with a success banner

---

## Technical Notes

### Cron interpretation
Use `Cronos` NuGet package for parsing — supports presets ("daily", "weekly") and standard cron strings.

### Variance check
Server-side, never trust the client variance — always recompute from `expected = current_stock`, `actual = client_input`.

---

## Dependencies

**Prerequisite Stories:** STORY-164, 165, 166, 167

**Blocked Stories:** None

---

## Definition of Done

- [ ] Scheduler raises tasks correctly
- [ ] Spot check screen flow works end-to-end
- [ ] Auto-adjustment movement applied correctly
- [ ] Audit log entries written
- [ ] Manual test of zero-variance and non-zero-variance cases
- [ ] Code reviewed
- [ ] Merged

---

**This story was created using BMAD Method v6 - Phase 4 (Implementation)**
