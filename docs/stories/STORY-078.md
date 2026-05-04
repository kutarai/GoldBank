# STORY-078: On-Us Purchase Transaction Processing

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
I want to **make card purchases at merchants who are also clients of my bank**
So that **money moves directly from my account to the merchant's account within the same bank**

---

## Description

### Background

An on-us purchase occurs when a bank client uses their card at a POS terminal belonging to a merchant who is also a client of the same bank. The national switch routes the ISO 20022 transaction to the bank's switch, which translates it and calls Core Banking. Because both the cardholder and the merchant are clients of the bank, the transaction is an internal book transfer — money moves from the client's account to the merchant's settlement account without involving any external institution.

On-us transactions are the simplest and fastest card transaction type. No inter-bank settlement is required. The bank earns the full interchange fee. The transaction should complete in near real-time since both accounts are in the same database.

### Scope

**In scope:**
- Receive `ProcessPurchase` gRPC call from the switch with `is_on_us = true`
- Validate cardholder account: exists, active, sufficient balance, currency match
- Validate merchant: exists in Merchants module, active, accepts payments
- Debit cardholder account by transaction amount
- Credit merchant's settlement account by transaction amount (minus any merchant fee)
- Apply cardholder transaction fee (if configured per tenant)
- Create `CardTransaction` record with type `OnUsPurchase`
- Create corresponding `Transaction` records on both accounts (debit on client, credit on merchant)
- Generate authorization code
- Return success response with response code "00" (approved) or appropriate decline code

**Out of scope:**
- Off-us purchase processing (STORY-079)
- Merchant settlement batching (existing Merchants module handles this)
- Real-time notification to cardholder/merchant (handled by notifications module)
- Reversal processing (future story)

### User Flow

1. **Card Tap/Insert:** Bank client presents card at on-us merchant's POS terminal
2. **Switch Receives:** National switch routes ISO 20022 `pacs.008` to GoldBank switch
3. **Switch Translates:** Switch converts to canonical format, identifies as on-us (merchant belongs to GoldBank)
4. **gRPC Call:** Switch calls `CardTransactionService.ProcessPurchase` with `is_on_us = true`
5. **Validate Cardholder:** Check account exists, active, balance >= amount + fee, currency = ZWG
6. **Validate Merchant:** Look up merchant by merchant_id, check active and accepting payments
7. **Resolve Merchant Account:** Get merchant's owner account (via `Merchant.OwnerAccountId`)
8. **Debit Client:** Reduce client's `AvailableBalance` and `Balance` by (amount + fee)
9. **Credit Merchant:** Increase merchant owner account's `Balance` by (amount - merchant_fee)
10. **Record Transactions:** Create `CardTransaction` + two `Transaction` records
11. **Respond:** Return response code "00", authorization code, and updated available balance

---

## Acceptance Criteria

- [ ] `ProcessPurchaseHandler` processes on-us purchases when `is_on_us = true`
- [ ] Cardholder account is validated: exists, status = "active", available balance >= amount + fee, currency matches
- [ ] Merchant is validated: exists by merchant_id, status = "active"
- [ ] Cardholder account is debited by (amount + cardholder fee)
- [ ] Merchant owner account is credited by (amount - merchant fee)
- [ ] `CardTransaction` record created with type = "OnUsPurchase", status = "completed", response_code = "00"
- [ ] `Transaction` record created on cardholder's account (type = "CARD_PURCHASE", negative amount)
- [ ] `Transaction` record created on merchant's account (type = "CARD_SALE", positive amount)
- [ ] Authorization code generated (6 alphanumeric characters) and returned in response
- [ ] Insufficient balance returns response code "51" (insufficient funds)
- [ ] Account not found returns response code "14"
- [ ] Account frozen/closed returns response code "78"
- [ ] Merchant not found or inactive returns response code "03" (invalid merchant)
- [ ] Duplicate STAN + source_institution returns original response (idempotent)
- [ ] All account balance updates are performed within a single database transaction (atomicity)

---

## Technical Notes

### Components

| Component | Location | Purpose |
|-----------|----------|---------|
| `ProcessPurchaseCommand.cs` | `Modules/CardTransactions/Application/Commands/` | Command carrying purchase request data |
| `ProcessPurchaseHandler.cs` | `Modules/CardTransactions/Application/Handlers/` | Orchestrates on-us and off-us purchase logic |
| `CardTransactionValidator.cs` | `Modules/CardTransactions/Application/Validators/` | Validates cardholder account |

### Key Logic

```csharp
// ProcessPurchaseHandler - On-Us path
public async Task<CardTransactionResult> Handle(ProcessPurchaseCommand command, CancellationToken ct)
{
    // 1. Check idempotency (STAN + source_institution)
    var existing = await FindExistingTransaction(command.Stan, command.SourceInstitution, ct);
    if (existing != null)
        return existing.ToResult();

    // 2. Validate cardholder account
    var account = await GetAccount(command.CardHolderAccount, command.TenantId, ct);
    if (account == null) return Decline("14", "Invalid account");
    if (account.Status != "active") return Decline("78", "Account blocked");
    if (account.Currency != command.Currency) return Decline("12", "Currency mismatch");

    // 3. Calculate fees
    var cardholderFee = await GetFee("CARD_PURCHASE", command.TenantId, ct);
    var totalDebit = command.Amount + cardholderFee;
    if (account.AvailableBalance < totalDebit) return Decline("51", "Insufficient funds");

    // 4. Validate merchant (on-us)
    var merchant = await GetMerchant(command.MerchantId, command.TenantId, ct);
    if (merchant == null || merchant.Status != "active") return Decline("03", "Invalid merchant");

    var merchantAccount = await GetAccountById(merchant.OwnerAccountId, ct);

    // 5. Execute within transaction
    await using var dbTransaction = await BeginTransactionAsync(ct);

    account.Balance -= totalDebit;
    account.AvailableBalance -= totalDebit;

    var merchantFee = await GetMerchantFee(command.TenantId, ct);
    merchantAccount.Balance += (command.Amount - merchantFee);
    merchantAccount.AvailableBalance += (command.Amount - merchantFee);

    // 6. Create records
    var cardTxn = CreateCardTransaction(command, account, "OnUsPurchase", "00", cardholderFee);
    var debitTxn = CreateAccountTransaction(account, -totalDebit, "CARD_PURCHASE", ...);
    var creditTxn = CreateAccountTransaction(merchantAccount, command.Amount - merchantFee, "CARD_SALE", ...);

    await dbTransaction.CommitAsync(ct);

    return Approve(cardTxn, account.AvailableBalance);
}
```

### Database Changes

No new tables — uses `card_transactions` table from STORY-077 and existing `transactions` table.

### Security Considerations

- Balance updates must be atomic — use database transaction to prevent partial updates
- Concurrent transactions on the same account must be serialized — use optimistic concurrency (row version) or `SELECT ... FOR UPDATE`
- Fee calculation must use tenant-specific configuration, not hardcoded values

### Edge Cases

- **Concurrent purchases:** Two POS transactions on the same account at the same time — optimistic concurrency check prevents double-spend
- **Merchant account frozen:** If the merchant's owner account is frozen, decline with "03" — merchant cannot accept payments
- **Zero amount:** Reject with response code "13" (invalid amount)
- **Fee exceeds amount:** If cardholder fee + amount > balance but amount alone < balance, still decline "51"

---

## Dependencies

**Prerequisite Stories:**
- STORY-077: CardTransactions Module Scaffolding & Domain Model

**Blocked Stories:**
- None

**External Dependencies:**
- Merchant data must exist in the Merchants module (STORY-050)
- Fee configuration must exist in WhiteLabel module (STORY-066)

---

## Definition of Done

- [ ] Code implemented and committed
- [ ] Unit tests for on-us purchase: approved, insufficient funds, invalid account, invalid merchant, duplicate (>=80% coverage)
- [ ] Integration test: end-to-end on-us purchase with balance verification on both accounts
- [ ] All decline codes verified
- [ ] Idempotency tested
- [ ] Concurrent transaction test (optimistic concurrency)
- [ ] Code reviewed and approved
- [ ] Deployed to staging

---

## Progress Tracking

**Status History:**
- 2026-03-15: Created

**Actual Effort:** TBD

---

**This story was created using BMAD Method v6 - Phase 4 (Implementation)**
