# STORY-152: High-Value Withdrawal Supervisor Approval Flow

**Epic:** EPIC-021 Bank Teller & Vault Client Application
**Priority:** Must Have
**Story Points:** 2
**Status:** Not Started
**Created:** 2026-04-08
**Sprint:** 25

---

## User Story

As a **branch supervisor**
I want **to be required to enter my PIN when a teller processes a cash withdrawal above a configurable threshold**
So that **two-person control protects the bank against fraud and large-value mistakes**

---

## Description

### Background
FR-005 requires that withdrawals over a per-currency threshold cannot proceed without an explicit supervisor approval. The supervisor must be physically present at the teller's workstation and re-authenticate with their PIN.

### Scope
**In scope:**
- Per-currency thresholds stored in `system_config` (e.g., `cash.high_value_threshold.USD = 1000`, `cash.high_value_threshold.ZWG = 50000`)
- Withdrawal handler checks the requested amount against the threshold and short-circuits to "approval required" if exceeded
- New endpoint `POST /api/teller/withdrawals/{pendingId}/approve` that the front-end calls with `{ supervisorUsername, supervisorPin }`
- Verifies the supervisor's username + PIN against `admin_users.password_hash` (BCrypt)
- Verifies the supervisor's role is `branch_manager` or `super_admin` and tenant matches
- On success, completes the withdrawal and stamps `supervisor_approver_id` + `supervisor_approved_at`
- Audit log entries for both the request and the approval

**Out of scope:**
- UI modal (STORY-156)
- Customizing thresholds via UI (admin can edit `system_config` directly)

### Workflow
1. Teller submits withdrawal via `POST /api/teller/withdrawals`
2. Server checks `amount > threshold[currency]`. If yes:
   - Insert the `branch_cash_transactions` row with `status = pending_supervisor_approval` and **do not** debit the account yet
   - Return `202 Accepted` with the pending transaction ID and `requiresApproval = true`
3. Front-end shows the supervisor approval modal
4. Supervisor enters username + PIN
5. Front-end calls `POST /api/teller/withdrawals/{pendingId}/approve`
6. Server verifies supervisor credentials, marks the row approved, debits the account, returns success

---

## Acceptance Criteria

- [ ] `system_config` keys `cash.high_value_threshold.{currency}` are read at startup and cached for 5 minutes
- [ ] If a key is missing for a currency, the threshold defaults to `decimal.MaxValue` (no approval required)
- [ ] Withdrawal endpoint returns `202 + { requiresApproval: true, pendingTransactionId }` when amount exceeds the threshold
- [ ] The pending row is created with `status = pending_supervisor_approval`; the account balance is NOT yet debited
- [ ] `POST /api/teller/withdrawals/{pendingId}/approve` validates: supervisor username exists, password matches BCrypt hash, role is in (`branch_manager`, `super_admin`), tenant matches the teller's tenant
- [ ] On approval success: row transitions to `pending_signature` (or `completed` if signature not required), account is debited, `supervisor_approver_id` and `supervisor_approved_at` are set, audit log entries are written
- [ ] On approval failure (wrong PIN, role mismatch, etc.): the pending row stays open, returns 401 with a generic "approval failed" message; an audit log entry records the failed attempt
- [ ] Pending rows older than 15 minutes auto-expire (background sweeper) — the front-end must obtain fresh approval if it took too long
- [ ] Approver cannot be the same admin as the teller (no self-approval)
- [ ] Integration tests cover: under threshold passes through normally; over threshold returns 202; valid approval succeeds; wrong PIN fails; expired pending row rejected; self-approval blocked

---

## Technical Notes

### Threshold lookup
```csharp
public sealed class HighValueThresholdProvider
{
    public decimal GetThreshold(string currency) =>
        _cache.GetOrCreate($"cash.high_value_threshold.{currency}", _ => {
            var cfg = _db.SystemConfigs
                .FirstOrDefault(c => c.Key == $"cash.high_value_threshold.{currency}");
            return cfg == null ? decimal.MaxValue : decimal.Parse(cfg.ValueJson);
        });
}
```

### PIN verification
```csharp
var supervisor = await _db.AdminUsers
    .FirstOrDefaultAsync(u => u.Username == request.SupervisorUsername && u.TenantId == tenantId);

if (supervisor == null
    || !BCrypt.Net.BCrypt.Verify(request.SupervisorPin, supervisor.PasswordHash)
    || supervisor.Role is not ("branch_manager" or "super_admin")
    || supervisor.Id == tellerId)
{
    _audit.Log("withdrawal.approval.failed", ...);
    return Result.Failure(ApprovalErrors.Invalid);
}
```

### Pending row sweeper
Wolverine scheduled job runs every 5 minutes; rolls back any `pending_supervisor_approval` rows older than 15 minutes (no balance impact since we hadn't debited yet).

---

## Dependencies

**Prerequisite Stories:** STORY-149 (withdrawal endpoint exists)

**Blocked Stories:** STORY-156 (withdrawal screen renders the approval modal)

---

## Definition of Done

- [ ] Threshold provider implemented and cached
- [ ] Withdrawal endpoint short-circuits to approval flow when over threshold
- [ ] Approval endpoint implemented with PIN verification
- [ ] Sweeper expires stale pending rows
- [ ] Audit log entries on every action and failure
- [ ] Integration tests cover happy and failure paths
- [ ] Code reviewed
- [ ] Merged to main

---

**This story was created using BMAD Method v6 - Phase 4 (Implementation)**
