# STORY-161: Reversal Flow + Audit Trail Integration

**Epic:** EPIC-021 Bank Teller & Vault Client Application
**Priority:** Must Have
**Story Points:** 2
**Status:** Not Started
**Created:** 2026-04-08
**Sprint:** 27

---

## User Story

As a **bank teller who just made a mistake**
I want **to reverse my own same-shift cash transaction with supervisor approval**
So that **I can correct miscounts immediately without waiting for the back-office dispute workflow**

---

## Description

### Background
Tellers occasionally make mistakes — wrong amount, wrong account, mis-keyed denomination. They need a fast in-shift reversal path that requires supervisor sign-off and produces a compensating ledger entry. Cross-shift errors go through the existing dispute workflow.

### Scope
**In scope:**
- "Reverse" button on the day's transaction list (same shift only)
- Reversal modal: warning, reason field (required), supervisor PIN
- POST `/api/teller/transactions/{id}/reverse` (already exists in STORY-149) — wires the supervisor PIN check from STORY-152
- Creates a compensating `transactions` row with `type=cash_reversal`
- Updates the original `branch_cash_transactions.reversed_by_transaction_id` and `reversed_at`
- Account balance flipped back
- Drawer running balance recomputed
- Audit log entries: `cash.reverse.requested`, `cash.reverse.approved`, `cash.reverse.failed`
- All audit logs accessible from bank-client Audit Trail screen (existing)

**Out of scope:**
- Cross-shift reversal (use disputes)
- Reversal of reversals

---

## Acceptance Criteria

- [ ] Reverse button appears on transactions list for any non-reversed, same-shift transaction
- [ ] Reversal modal collects: reason (required), supervisor username + PIN
- [ ] On confirm, calls `POST /api/teller/transactions/{id}/reverse` with the supervisor credentials
- [ ] Server validates: original transaction is in the current open drawer, not already reversed, supervisor PIN is valid, supervisor is not the same teller
- [ ] On success: compensating transaction created, original marked as reversed, drawer running balance updated, audit logs written
- [ ] On failure: friendly error, no partial state
- [ ] Audit logs visible in bank-client Audit Trail page with the action `cash.reverse.approved` and the supervisor + teller IDs
- [ ] Reversed transactions are visually struck through on the transactions list

---

## Technical Notes

### Reuses STORY-152's PIN flow
The `POST /reverse` endpoint follows the same supervisor PIN verification logic — extract a `SupervisorAuthService` so both endpoints share it.

---

## Dependencies

**Prerequisite Stories:** STORY-149, 152

**Blocked Stories:** None

---

## Definition of Done

- [ ] Reversal button + modal implemented
- [ ] Endpoint wired with supervisor PIN check
- [ ] Compensating transaction logic correct
- [ ] Drawer balance recomputes
- [ ] Audit logs written and visible in bank-client
- [ ] Code reviewed
- [ ] Merged

---

**This story was created using BMAD Method v6 - Phase 4 (Implementation)**
