# STORY-080: On-Us Deposit Transaction Processing

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
I want to **receive a card deposit at a merchant who is a client of my bank**
So that **money moves from the merchant's account to my account**

---

## Description

### Background

An on-us deposit occurs when a bank client requests a cash deposit at a merchant (typically an agent or retail partner) who is also a client of the same bank. The merchant accepts cash from the client and initiates a deposit transaction through their POS terminal. The national switch routes the ISO 20022 message to the bank, where Core Banking must verify the merchant has sufficient funds (float) and transfer money from the merchant's account to the client's account.

This is the reverse flow of an on-us purchase. The merchant's account is debited and the client's account is credited. The merchant must have sufficient balance (float) to fund the deposit. This is common in agent banking where agents maintain float to serve customers.

### Scope

**In scope:**
- Receive `ProcessDeposit` gRPC call from the switch with `is_on_us = true`
- Validate cardholder account: exists, active, currency match
- Validate merchant: exists, active, has sufficient balance (float) to fund the deposit
- Debit merchant's account by deposit amount
- Credit cardholder's account by deposit amount (minus any deposit fee)
- Apply deposit fee (if configured per tenant)
- Create `CardTransaction` record with type `OnUsDeposit`
- Create corresponding `Transaction` records on both accounts
- Generate authorization code
- Return success response with response code "00" or appropriate decline code

**Out of scope:**
- Off-us deposit processing (STORY-081)
- Agent float management (existing Agents module)
- Cash handling/reconciliation at the merchant
- Reversal processing (future story)

### User Flow

1. **Client requests deposit:** Client presents card and cash to on-us merchant/agent
2. **Merchant initiates:** Merchant enters deposit amount on POS terminal
3. **Switch receives:** National switch routes ISO 20022 message to UniBank switch
4. **Switch translates:** Switch identifies as on-us deposit, calls `ProcessDeposit` with `is_on_us = true`
5. **Validate cardholder:** Check account exists, active, currency = ZWG
6. **Validate merchant:** Look up merchant, check active, check merchant account balance >= deposit amount
7. **Debit merchant:** Reduce merchant owner account's `Balance` and `AvailableBalance` by deposit amount
8. **Credit client:** Increase client's `Balance` and `AvailableBalance` by (deposit amount - fee)
9. **Record transactions:** Create `CardTransaction` + two `Transaction` records
10. **Respond:** Return response code "00", authorization code, and updated available balance

---

## Acceptance Criteria

- [ ] `ProcessDepositHandler` processes on-us deposits when `is_on_us = true`
- [ ] Cardholder account is validated: exists, status = "active", currency matches
- [ ] Merchant is validated: exists by merchant_id, status = "active"
- [ ] Merchant owner account has sufficient balance (float) to fund the deposit
- [ ] Merchant owner account is debited by deposit amount
- [ ] Cardholder account is credited by (deposit amount - deposit fee)
- [ ] `CardTransaction` record created with type = "OnUsDeposit", status = "completed"
- [ ] `Transaction` record created on cardholder's account (type = "CARD_DEPOSIT", positive amount)
- [ ] `Transaction` record created on merchant's account (type = "CARD_DEPOSIT_DISBURSEMENT", negative amount)
- [ ] Authorization code generated and returned
- [ ] Merchant insufficient float returns response code "51" (insufficient funds)
- [ ] Cardholder account not found returns "14", account blocked returns "78"
- [ ] Merchant not found or inactive returns "03"
- [ ] Duplicate STAN + source_institution returns original response (idempotent)
- [ ] All balance updates are atomic within a single database transaction

---

## Technical Notes

### Components

| Component | Location | Purpose |
|-----------|----------|---------|
| `ProcessDepositCommand.cs` | `Modules/CardTransactions/Application/Commands/` | Command carrying deposit request data |
| `ProcessDepositHandler.cs` | `Modules/CardTransactions/Application/Handlers/` | Orchestrates on-us and off-us deposit logic |

### Key Logic

```csharp
// ProcessDepositHandler - On-Us path
// 1. Validate cardholder
var account = await GetAccount(command.CardHolderAccount, command.TenantId, ct);
if (account == null) return Decline("14", "Invalid account");
if (account.Status != "active") return Decline("78", "Account blocked");

// 2. Validate merchant and float
var merchant = await GetMerchant(command.MerchantId, command.TenantId, ct);
if (merchant == null || merchant.Status != "active") return Decline("03", "Invalid merchant");

var merchantAccount = await GetAccountById(merchant.OwnerAccountId, ct);
if (merchantAccount.AvailableBalance < command.Amount) return Decline("51", "Insufficient funds");

// 3. Calculate fee and execute
var depositFee = await GetFee("CARD_DEPOSIT", command.TenantId, ct);

await using var dbTransaction = await BeginTransactionAsync(ct);
merchantAccount.Balance -= command.Amount;
merchantAccount.AvailableBalance -= command.Amount;
account.Balance += (command.Amount - depositFee);
account.AvailableBalance += (command.Amount - depositFee);
await dbTransaction.CommitAsync(ct);
```

### Database Changes

No new tables — uses `card_transactions` table from STORY-077.

### Security Considerations

- The deposit fee is charged to the cardholder (deducted from credited amount), not the merchant
- Merchant float balance should be monitored — low float means the merchant cannot serve more deposit requests
- Deposits increase client balance — ensure daily deposit limits are enforced per KYC tier

### Edge Cases

- **Merchant float exhausted mid-transaction:** If another transaction depletes the merchant's float between validation and execution, optimistic concurrency prevents the debit
- **Deposit exceeds client daily limit:** Check accumulated daily deposits against KYC tier limits before processing
- **Zero amount deposit:** Reject with response code "13" (invalid amount)
- **Client account at maximum balance:** If the account has a maximum balance cap, reject if credit would exceed it

---

## Dependencies

**Prerequisite Stories:**
- STORY-077: CardTransactions Module Scaffolding & Domain Model

**Blocked Stories:**
- None

**External Dependencies:**
- Merchant data and float must exist (STORY-050, STORY-036)

---

## Definition of Done

- [ ] Code implemented and committed
- [ ] Unit tests for on-us deposit: approved, insufficient merchant float, invalid account, invalid merchant, duplicate (>=80% coverage)
- [ ] Integration test: end-to-end deposit with balance verification on both accounts
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
