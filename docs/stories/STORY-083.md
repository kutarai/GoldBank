# STORY-083: Statement Enquiry Transaction

**Epic:** EPIC-015 Card Transaction Processing
**Priority:** Should Have
**Story Points:** 3
**Status:** Not Started
**Assigned To:** Unassigned
**Created:** 2026-03-15
**Sprint:** 9

---

## User Story

As a **bank client**
I want to **request a mini-statement at a POS terminal or ATM via card**
So that **I can see my recent transactions without visiting a branch or using the mobile app**

---

## Description

### Background

A statement enquiry (mini-statement) is a non-financial card transaction where the client requests their recent transaction history at a POS terminal or ATM. The terminal sends the request through the national switch to the issuing bank (UniBank). Core Banking retrieves the most recent transactions (typically last 5-10) and returns them in a format suitable for printing on a receipt or displaying on an ATM screen.

Statement enquiries are common at ATMs where clients want a quick view of recent activity. The response format is constrained by the terminal's capabilities — typically a limited number of entries with short descriptions.

### Scope

**In scope:**
- Receive `StatementEnquiry` gRPC call from the switch
- Validate cardholder account: exists, active
- Retrieve the most recent transactions (configurable, default 10)
- Optionally apply a statement enquiry fee (if configured per tenant)
- Create `CardTransaction` record with type `StatementEnquiry` (audit trail)
- Return `StatementEnquiryResponse` with transaction entries and current available balance

**Out of scope:**
- Full statement generation (PDF, email — handled by admin/reporting modules)
- Date-range queries (mini-statement is always "most recent N")
- Balance enquiry (STORY-082)

### User Flow

1. **Client requests statement:** Client selects "Mini Statement" at POS/ATM
2. **Switch routes:** Transaction flows through national switch to UniBank switch
3. **gRPC call:** Switch calls `CardTransactionService.StatementEnquiry`
4. **Validate account:** Check account exists, active
5. **Apply fee (if any):** Deduct statement enquiry fee if configured
6. **Retrieve transactions:** Query last N transactions from the account's transaction history
7. **Record transaction:** Create `CardTransaction` with type `StatementEnquiry`
8. **Respond:** Return response code "00" with statement entries and available balance

---

## Acceptance Criteria

- [ ] `StatementEnquiryHandler` processes statement enquiry requests
- [ ] Cardholder account is validated: exists, status = "active"
- [ ] Most recent transactions are returned (default 10, configurable via `max_records` in request, capped at 20)
- [ ] Each statement entry includes: date, description, amount (with sign: positive for credits, negative for debits), type, reference, balance_after
- [ ] Current available balance is included in the response
- [ ] If a statement enquiry fee is configured, it is deducted from the account (waived if insufficient funds)
- [ ] `CardTransaction` record created with type = "StatementEnquiry", amount = 0
- [ ] Account not found returns response code "14"
- [ ] Account blocked returns response code "78"
- [ ] If account has no transactions, return success with empty entries list and current balance
- [ ] Duplicate STAN + source_institution returns original response (idempotent)

---

## Technical Notes

### Components

| Component | Location | Purpose |
|-----------|----------|---------|
| `StatementEnquiryCommand.cs` | `Modules/CardTransactions/Application/Commands/` | Command carrying statement enquiry request |
| `StatementEnquiryHandler.cs` | `Modules/CardTransactions/Application/Handlers/` | Retrieves recent transactions and formats response |

### Key Logic

```csharp
public async Task<StatementEnquiryResult> Handle(StatementEnquiryCommand command, CancellationToken ct)
{
    // 1. Check idempotency
    var existing = await FindExistingTransaction(command.Stan, command.SourceInstitution, ct);
    if (existing != null) return existing.ToStatementResult();

    // 2. Validate account
    var account = await GetAccount(command.CardHolderAccount, command.TenantId, ct);
    if (account == null) return DeclineStatement("14", "Invalid account");
    if (account.Status != "active") return DeclineStatement("78", "Account blocked");

    // 3. Apply fee if configured
    var enquiryFee = await GetFee("STATEMENT_ENQUIRY", command.TenantId, ct);
    if (enquiryFee > 0 && account.AvailableBalance >= enquiryFee)
    {
        account.Balance -= enquiryFee;
        account.AvailableBalance -= enquiryFee;
    }
    else
    {
        enquiryFee = 0;
    }

    // 4. Retrieve recent transactions
    var maxRecords = Math.Min(command.MaxRecords > 0 ? command.MaxRecords : 10, 20);
    var transactions = await dbContext.Transactions
        .Where(t => t.AccountId == account.Id && t.TenantId == command.TenantId)
        .OrderByDescending(t => t.CreatedAt)
        .Take(maxRecords)
        .ToListAsync(ct);

    // 5. Map to statement entries
    var entries = transactions.Select(t => new StatementEntry
    {
        Date = t.CreatedAt,
        Description = t.Description ?? t.Type,
        Amount = t.Amount,
        Type = t.Type,
        Reference = t.Reference ?? "",
        BalanceAfter = t.BalanceAfter
    }).ToList();

    // 6. Record enquiry
    var cardTxn = new CardTransaction
    {
        TransactionType = "StatementEnquiry",
        Amount = 0,
        Fee = enquiryFee,
        ResponseCode = "00",
        Status = "completed",
    };

    return new StatementEnquiryResult
    {
        Success = true,
        ResponseCode = "00",
        Entries = entries,
        AvailableBalance = account.AvailableBalance,
        Currency = account.Currency
    };
}
```

### Database Changes

No new tables — reads from existing `transactions` table and writes to `card_transactions` from STORY-077.

### Security Considerations

- Transaction history is sensitive PII — only return through the authenticated switch channel
- Limit the number of entries to prevent large payloads (cap at 20)
- Do not include counterparty phone numbers in statement entries sent to external terminals
- Description field should be sanitized (no internal account IDs or system references)

### Edge Cases

- **New account with no transactions:** Return success with empty entries list
- **Account with only pending transactions:** Only return completed transactions in the statement
- **max_records = 0 or negative:** Use default of 10
- **max_records > 20:** Cap at 20
- **Fee configured but zero balance:** Waive fee, still return the statement

---

## Dependencies

**Prerequisite Stories:**
- STORY-077: CardTransactions Module Scaffolding & Domain Model

**Blocked Stories:**
- None

**External Dependencies:**
- Transaction history must exist in the Accounts module (STORY-017)

---

## Definition of Done

- [ ] Code implemented and committed
- [ ] Unit tests: successful enquiry, empty statement, max_records capping, invalid account, fee waiver (>=80% coverage)
- [ ] Integration test: statement enquiry returns correct recent transactions
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
