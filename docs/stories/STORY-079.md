# STORY-079: Off-Us Purchase Transaction Processing

**Epic:** EPIC-015 Card Transaction Processing
**Priority:** Must Have
**Story Points:** 5
**Status:** Not Started
**Assigned To:** Unassigned
**Created:** 2026-03-15
**Sprint:** 9

---

## User Story

As a **bank client**
I want to **make card purchases at merchants who are not clients of my bank**
So that **money moves from my account to the acquiring bank's suspense account for inter-bank settlement**

---

## Description

### Background

An off-us purchase occurs when a bank client uses their card at a POS terminal belonging to a merchant at another bank (the acquiring bank). The national switch routes the ISO 20022 transaction to the issuing bank (UniBank). Since the merchant is not a UniBank client, the bank cannot credit the merchant directly. Instead, the transaction amount is moved from the client's account to the acquiring bank's suspense/settlement account held at UniBank.

The acquiring bank's suspense account is a nostro-style account that UniBank maintains for inter-bank settlement. At end of day, the net position between banks is settled through the national clearing system. The suspense account accumulates all off-us purchase debits throughout the day.

This is the most common card transaction type in practice, as clients frequently transact at merchants belonging to other banks.

### Scope

**In scope:**
- Receive `ProcessPurchase` gRPC call from the switch with `is_on_us = false`
- Validate cardholder account: exists, active, sufficient balance, currency match
- Resolve acquiring bank's suspense account by `acquiring_institution` code
- Debit cardholder account by transaction amount + fee
- Credit acquiring bank's suspense account by transaction amount
- Apply cardholder transaction fee (if configured per tenant)
- Create `CardTransaction` record with type `OffUsPurchase`
- Create `Transaction` record on cardholder's account
- Generate authorization code
- Return success response with response code "00" or appropriate decline code

**Out of scope:**
- On-us purchase processing (STORY-078)
- Creating or managing acquiring bank suspense accounts (assumed to be pre-provisioned)
- Inter-bank settlement/clearing (handled by reconciliation module)
- Reversal processing (future story)

### User Flow

1. **Card Tap/Insert:** Bank client presents card at off-us merchant's POS terminal
2. **Switch Receives:** National switch routes ISO 20022 message to UniBank switch
3. **Switch Translates:** Switch identifies as off-us (merchant does not belong to UniBank)
4. **gRPC Call:** Switch calls `CardTransactionService.ProcessPurchase` with `is_on_us = false`
5. **Validate Cardholder:** Check account exists, active, balance >= amount + fee, currency = ZWG
6. **Resolve Suspense Account:** Look up acquiring bank's suspense account by institution code
7. **Debit Client:** Reduce client's `AvailableBalance` and `Balance` by (amount + fee)
8. **Credit Suspense:** Increase acquiring bank's suspense account `Balance` by amount
9. **Record Transaction:** Create `CardTransaction` record with acquiring_institution populated
10. **Respond:** Return response code "00", authorization code, and updated available balance

---

## Acceptance Criteria

- [ ] `ProcessPurchaseHandler` processes off-us purchases when `is_on_us = false`
- [ ] Cardholder account is validated: exists, status = "active", available balance >= amount + fee, currency matches
- [ ] Acquiring bank's suspense account is resolved by institution code from `acquiring_institution` field
- [ ] If suspense account not found, return response code "96" (system error) — configuration issue
- [ ] Cardholder account is debited by (amount + cardholder fee)
- [ ] Acquiring bank's suspense account is credited by amount (no merchant fee deducted — that's the acquiring bank's concern)
- [ ] `CardTransaction` record created with type = "OffUsPurchase", acquiring_institution populated
- [ ] `Transaction` record created on cardholder's account (type = "CARD_PURCHASE", negative amount)
- [ ] Authorization code generated and returned in response
- [ ] Insufficient balance returns response code "51"
- [ ] Account not found returns "14", account blocked returns "78"
- [ ] Duplicate STAN + source_institution returns original response (idempotent)
- [ ] All balance updates are atomic within a single database transaction

---

## Technical Notes

### Components

| Component | Location | Purpose |
|-----------|----------|---------|
| `ProcessPurchaseHandler.cs` | `Modules/CardTransactions/Application/Handlers/` | Shared handler — branches on `is_on_us` flag |
| `SuspenseAccountResolver.cs` | `Modules/CardTransactions/Infrastructure/Services/` | Resolves acquiring bank suspense accounts |

### Suspense Account Resolution

Acquiring bank suspense accounts are pre-provisioned `Account` records with a convention-based identifier:

```csharp
// Suspense accounts follow naming convention: SUSPENSE-{INSTITUTION_CODE}
// e.g., SUSPENSE-NATBANK, SUSPENSE-FIRSTBN
var suspenseAccountNumber = $"SUSPENSE-{acquiringInstitution}";
var suspenseAccount = await dbContext.Accounts
    .FirstOrDefaultAsync(a => a.PhoneNumber == suspenseAccountNumber && a.TenantId == tenantId, ct);
```

These accounts have `Status = "active"` and `Currency = "ZWG"`. They are created during tenant provisioning or when a new acquiring bank relationship is established.

### Database Changes

No new tables — uses `card_transactions` table from STORY-077.

### Security Considerations

- Suspense account credits should be auditable — every credit must have a corresponding `CardTransaction` and `Transaction` record
- Suspense account balances should be monitored for anomalies (unexpectedly high balances may indicate settlement failures)
- The acquiring institution code must be validated against a known list to prevent crediting unknown suspense accounts

### Edge Cases

- **Unknown acquiring institution:** If no suspense account exists for the institution code, return "96" and log an alert — this is a configuration gap that operations must address
- **Suspense account frozen:** Should not happen in normal operations — return "96" and alert
- **Very large transaction:** Apply per-transaction limits from tenant configuration before processing
- **Currency mismatch on suspense account:** Suspense accounts should always match tenant's home currency — return "96" if mismatch

---

## Dependencies

**Prerequisite Stories:**
- STORY-077: CardTransactions Module Scaffolding & Domain Model
- STORY-078: On-Us Purchase Transaction Processing (shared handler)

**Blocked Stories:**
- None

**External Dependencies:**
- Acquiring bank suspense accounts must be pre-provisioned during tenant setup

---

## Definition of Done

- [ ] Code implemented and committed
- [ ] Unit tests for off-us purchase: approved, insufficient funds, invalid account, unknown acquiring bank, duplicate (>=80% coverage)
- [ ] Integration test: end-to-end off-us purchase with balance verification on cardholder and suspense accounts
- [ ] All decline codes verified
- [ ] Idempotency tested
- [ ] Code reviewed and approved
- [ ] Deployed to staging

---

## Progress Tracking

**Status History:**
- 2026-03-15: Created

**Actual Effort:** TBD

---

**This story was created using BMAD Method v6 - Phase 4 (Implementation)**
