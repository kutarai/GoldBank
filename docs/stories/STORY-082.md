# STORY-082: Balance Enquiry Transaction

**Epic:** EPIC-015 Card Transaction Processing
**Priority:** Must Have
**Story Points:** 3
**Status:** Not Started
**Assigned To:** Unassigned
**Created:** 2026-03-15
**Sprint:** 9

---

## User Story

As a **bank client**
I want to **check my account balance at a POS terminal or ATM via card**
So that **I can see my available and ledger balance without visiting a branch or using the mobile app**

---

## Description

### Background

A balance enquiry is a non-financial card transaction where the client requests their account balance at a POS terminal or ATM. The terminal sends the request through the national switch to the issuing bank (UniBank). Core Banking looks up the account, retrieves the current available and ledger balances, and returns them in the response.

Balance enquiries do not move money but must still be logged for audit purposes. Some banks charge a fee for balance enquiries at terminals (especially ATMs) — the fee configuration is tenant-specific.

The ISO 20022 processing code for balance enquiry is "310000" (balance inquiry) or the equivalent ISO 20022 message type.

### Scope

**In scope:**
- Receive `BalanceEnquiry` gRPC call from the switch
- Validate cardholder account: exists, active
- Retrieve available balance and ledger balance
- Optionally apply a balance enquiry fee (if configured per tenant)
- Create `CardTransaction` record with type `BalanceEnquiry` (audit trail)
- Return `BalanceEnquiryResponse` with available and ledger balances

**Out of scope:**
- Statement enquiry (STORY-083)
- Mini-statement at ATM (STORY-083)
- PIN verification (handled at switch/HSM level)

### User Flow

1. **Client requests balance:** Client inserts/taps card at POS/ATM and selects "Balance Enquiry"
2. **Switch routes:** Transaction flows through national switch to UniBank switch
3. **gRPC call:** Switch calls `CardTransactionService.BalanceEnquiry`
4. **Validate account:** Check account exists, active
5. **Apply fee (if any):** Deduct balance enquiry fee from account if configured
6. **Retrieve balances:** Get available_balance and balance (ledger) from account
7. **Record transaction:** Create `CardTransaction` with type `BalanceEnquiry`
8. **Respond:** Return response code "00" with both balances

---

## Acceptance Criteria

- [ ] `BalanceEnquiryHandler` processes balance enquiry requests
- [ ] Cardholder account is validated: exists, status = "active"
- [ ] Available balance and ledger balance are returned in the response
- [ ] If a balance enquiry fee is configured for the tenant, the fee is deducted from the account before returning the balance
- [ ] If balance enquiry fee is configured but account has insufficient funds, still return the balance but with fee = 0 (fee is waived — balance enquiry should not fail due to fee)
- [ ] `CardTransaction` record created with type = "BalanceEnquiry", amount = 0, fee = enquiry_fee
- [ ] Account not found returns response code "14"
- [ ] Account blocked returns response code "78"
- [ ] Duplicate STAN + source_institution returns original response (idempotent)
- [ ] Response includes both `available_balance` and `ledger_balance` with correct currency

---

## Technical Notes

### Components

| Component | Location | Purpose |
|-----------|----------|---------|
| `BalanceEnquiryCommand.cs` | `Modules/CardTransactions/Application/Commands/` | Command carrying balance enquiry request |
| `BalanceEnquiryHandler.cs` | `Modules/CardTransactions/Application/Handlers/` | Retrieves and returns account balances |

### Key Logic

```csharp
public async Task<BalanceEnquiryResult> Handle(BalanceEnquiryCommand command, CancellationToken ct)
{
    // 1. Check idempotency
    var existing = await FindExistingTransaction(command.Stan, command.SourceInstitution, ct);
    if (existing != null) return existing.ToBalanceResult();

    // 2. Validate account
    var account = await GetAccount(command.CardHolderAccount, command.TenantId, ct);
    if (account == null) return DeclineBalance("14", "Invalid account");
    if (account.Status != "active") return DeclineBalance("78", "Account blocked");

    // 3. Apply fee if configured
    var enquiryFee = await GetFee("BALANCE_ENQUIRY", command.TenantId, ct);
    if (enquiryFee > 0 && account.AvailableBalance >= enquiryFee)
    {
        account.Balance -= enquiryFee;
        account.AvailableBalance -= enquiryFee;
    }
    else
    {
        enquiryFee = 0; // Waive fee if insufficient
    }

    // 4. Record
    var cardTxn = new CardTransaction
    {
        TransactionType = "BalanceEnquiry",
        Amount = 0,
        Fee = enquiryFee,
        ResponseCode = "00",
        Status = "completed",
        // ... other fields
    };

    // 5. Return balances
    return new BalanceEnquiryResult
    {
        Success = true,
        ResponseCode = "00",
        AvailableBalance = account.AvailableBalance,
        LedgerBalance = account.Balance,
        Currency = account.Currency
    };
}
```

### Database Changes

No new tables — uses `card_transactions` table from STORY-077.

### Security Considerations

- Balance information is sensitive PII — only return it through the authenticated switch channel
- The balance returned to an off-us terminal/ATM is limited to available_balance only (some schemes do not return ledger balance to external terminals)
- Log the enquiry but do not log the actual balance values in plain text logs

### Edge Cases

- **Fee configured but zero balance:** Waive the fee, still return the balance
- **Account with holds:** Available balance should reflect any authorization holds (available_balance < balance)
- **Multiple rapid enquiries:** Each enquiry is logged separately; if fees apply, they accumulate

---

## Dependencies

**Prerequisite Stories:**
- STORY-077: CardTransactions Module Scaffolding & Domain Model

**Blocked Stories:**
- None

**External Dependencies:**
- None

---

## Definition of Done

- [ ] Code implemented and committed
- [ ] Unit tests: successful enquiry, with/without fee, invalid account, blocked account, fee waiver on zero balance (>=80% coverage)
- [ ] Integration test: balance enquiry returns correct balances
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
