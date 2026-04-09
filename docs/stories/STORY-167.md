# STORY-167: Vault Manager Screens (bank-teller app)

**Epic:** EPIC-021 Bank Teller & Vault Client Application
**Priority:** Must Have
**Story Points:** 5
**Status:** Not Started
**Created:** 2026-04-08
**Sprint:** 28

---

## User Story

As a **branch vault manager**
I want **dedicated screens for the vault dashboard, float-out, surrender, injection, withdrawal-to-HQ, and the daily denomination grid**
So that **I can run the cash side of the branch from a single tool with full denomination fidelity**

---

## Description

### Background
Vault managers don't process customer-facing transactions. They issue float to tellers, receive surrenders, run spot checks, accept HQ deliveries, and ship surplus back. The bank-teller app gains a vault section accessible only to users with role `vault_manager` (or higher).

### Scope
**In scope:**
- New routes under `/vault/*` in bank-teller
- Vault Dashboard `/vault` — current stock per currency with denomination grid, today's movements, pending spot check task, tellers currently holding float, last spot check result
- Float-Out screen `/vault/float-out` — pick teller, currency, denomination grid, vault manager PIN
- Surrender screen `/vault/surrender` — pick drawer session, count, variance review
- Spot Check screen `/vault/spot-check` — denomination count, witness PIN
- Injection screen `/vault/injection` — denomination grid, supervisor PIN
- Withdrawal-to-HQ screen `/vault/withdrawal-hq` — denomination grid, supervisor PIN
- Role guard: only `vault_manager`, `branch_manager`, `super_admin` can access `/vault/*`
- Vault Dashboard auto-refreshes every 60 seconds

**Out of scope:**
- Spot check workflow logic itself (STORY-168)
- PDF reports (STORY-169)

---

## Acceptance Criteria

- [ ] `/vault` shows the dashboard with all the elements above
- [ ] Dashboard refreshes every 60 seconds (and on demand via Refresh button)
- [ ] Stock display: per currency, table of denomination | type (Note/Coin) | count | sub-total | grand total
- [ ] Float-Out screen: teller dropdown (filtered to same branch), currency dropdown, denomination grid, vault manager PIN field, Confirm button — calls `POST /api/vault/{vaultId}/float-out`
- [ ] On float-out success, navigate to dashboard with a success snackbar showing the new stock
- [ ] Surrender screen: drawer dropdown (open drawers in this branch), denomination grid, variance display, Confirm — calls `POST /api/vault/{vaultId}/surrender`
- [ ] Variance > 0 highlighted, requires a Notes field before Confirm enables
- [ ] Injection and Withdrawal-to-HQ screens follow the same pattern with both vault manager + branch supervisor PINs
- [ ] All screens redirect to `/login` if user is not vault_manager / branch_manager / super_admin
- [ ] Mobile responsive

---

## Technical Notes

### Role guard
Reuse `ProtectedRoute` from STORY-153 with a `requiredRoles` prop:
```jsx
<Route element={<ProtectedRoute requiredRoles={['vault_manager','branch_manager','super_admin']} />}>
  <Route path="/vault" element={<VaultDashboard />} />
  ...
</Route>
```

### Denomination grid component reuse
The component built in STORY-155 should be parameterised so the same component renders for vault movements.

---

## Dependencies

**Prerequisite Stories:** STORY-153, 155 (denomination grid), 163, 164, 165, 166

**Blocked Stories:** STORY-168, 169

---

## Definition of Done

- [ ] All five vault screens implemented
- [ ] Role guard enforced
- [ ] Auto-refresh on dashboard
- [ ] Manual test of every flow on dev DB
- [ ] Code reviewed
- [ ] Merged

---

**This story was created using BMAD Method v6 - Phase 4 (Implementation)**
