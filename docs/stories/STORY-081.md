# STORY-081: Off-Us Deposit Transaction Processing

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
I want to **receive a card deposit at a merchant who is not a client of my bank**
So that **money moves from the acquiring bank's account to my account**

---

## Description

### Background

An off-us deposit occurs when a bank client requests a cash deposit at a merchant belonging to another bank (the acquiring bank). The merchant accepts cash and initiates the transaction through their POS terminal. The acquiring bank's switch routes the ISO 20022 message through the national switch to UniBank (the issuing bank). Since the merchant is not a UniBank client, the bank cannot debit the merchant directly. Instead, the acquiring bank's suspense account at UniBank is debited, and the client's account is credited.

The acquiring bank is responsible for debiting their own merchant. UniBank's role is simply to credit the client's account and debit the acquiring bank's suspense account. The inter-bank settlement at end of day ensures the acquiring bank is made whole.

### Scope

**In scope:**
- Receive `ProcessDeposit` gRPC call from the switch with `is_on_us = false`
- Validate cardholder account: exists, active, currency match
- Resolve acquiring bank's suspense account by `acquiring_institution` code
- Debit acquiring bank's suspense account by deposit amount
- Credit cardholder's account by deposit amount (minus any deposit fee)
- Apply deposit fee (if configured per tenant)
- Create `CardTransaction` record with type `OffUsDeposit`
- Create `Transaction` record on cardholder's account
- Generate authorization code
- Return success response with response code "00" or appropriate decline code

**Out of scope:**
- On-us deposit processing (STORY-080)
- Managing the acquiring bank's suspense account balance
- Inter-bank settlement/clearing
- Reversal processing (future story)

### User Flow

1. **Client requests deposit:** Client presents card and cash to off-us merchant
2. **Merchant initiates:** Merchant enters deposit amount on POS terminal
3. **Acquiring bank switch:** Transaction flows through acquiring bank to national switch
4. **Switch routes to UniBank:** National switch delivers ISO 20022 message to UniBank switch
5. **Switch translates:** Switch identifies as off-us deposit, calls `ProcessDeposit` with `is_on_us = false`
6. **Validate cardholder:** Check account exists, active, currency = ZWG
7. **Resolve suspense account:** Look up acquiring bank's suspense account
8. **Debit suspense:** Reduce acquiring bank's suspense account balance by deposit amount
9. **Credit client:** Increase client's `Balance` and `AvailableBalance` by (deposit amount - fee)
10. **Record transaction:** Create `CardTransaction` + `Transaction` records
11. **Respond:** Return response code "00", authorization code, and updated available balance

---

## Acceptance Criteria

- [ ] `ProcessDepositHandler` processes off-us deposits when `is_on_us = false`
- [ ] Cardholder account is validated: exists, status = "active", currency matches
- [ ] Acquiring bank's suspense account is resolved by institution code
- [ ] If suspense account not found, return response code "96"
- [ ] Acquiring bank's suspense account is debited by deposit amount (suspense accounts may go negative — this represents money owed by the acquiring bank)
- [ ] Cardholder account is credited by (deposit amount - deposit fee)
- [ ] `CardTransaction` record created with type = "OffUsDeposit", acquiring_institution populated
- [ ] `Transaction` record created on cardholder's account (type = "CARD_DEPOSIT", positive amount)
- [ ] Authorization code generated and returned
- [ ] Account not found returns "14", account blocked returns "78"
- [ ] Duplicate STAN + source_institution returns original response (idempotent)
- [ ] All balance updates are atomic

---

## Technical Notes

### Components

| Component | Location | Purpose |
|-----------|----------|---------|
| `ProcessDepositHandler.cs` | `Modules/CardTransactions/Application/Handlers/` | Shared handler — branches on `is_on_us` flag |
| `SuspenseAccountResolver.cs` | `Modules/CardTransactions/Infrastructure/Services/` | Resolves acquiring bank suspense accounts (shared with STORY-079) |

### Key Difference from On-Us

For off-us deposits, the suspense account may go negative. This is expected behavior — a negative balance on the suspense account means the acquiring bank owes UniBank money, which will be settled at end of day through the national clearing system. Therefore, **no balance check is performed on the suspense account** for off-us deposits.

```csharp
// Off-us deposit: suspense account can go negative
suspenseAccount.Balance -= command.Amount;
// No AvailableBalance check — settlement will cover this
```

### Database Changes

No new tables — uses `card_transactions` table from STORY-077.

### Security Considerations

- Suspense accounts going negative should trigger monitoring alerts if the negative balance exceeds a configurable threshold
- Daily deposit limits per KYC tier should still be enforced for the cardholder

### Edge Cases

- **Unknown acquiring institution:** Return "96" and log alert
- **Deposit exceeds client daily limit:** Reject per KYC tier limits
- **Suspense account deeply negative:** Process the transaction but trigger an alert — indicates settlement may be delayed
- **Zero amount:** Reject with "13"

---

## Dependencies

**Prerequisite Stories:**
- STORY-077: CardTransactions Module Scaffolding & Domain Model
- STORY-079: Off-Us Purchase Transaction Processing (shares SuspenseAccountResolver)

**Blocked Stories:**
- None

**External Dependencies:**
- Acquiring bank suspense accounts must be pre-provisioned

---

## Definition of Done

- [ ] Code implemented and committed
- [ ] Unit tests for off-us deposit: approved, invalid account, unknown acquiring bank, duplicate (>=80% coverage)
- [ ] Integration test: end-to-end off-us deposit with balance verification
- [ ] Verify suspense account can go negative
- [ ] All decline codes verified
- [ ] Code reviewed and approved
- [ ] Deployed to staging

---

## Progress Tracking

**Status History:**
- 2026-03-15: Created

**Actual Effort:** TBD

---

**This story was created using BMAD Method v6 - Phase 4 (Implementation)**
