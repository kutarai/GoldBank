# STORY-093: Dual-Currency Accounts with Virtual Card Numbers on Registration

**Epic:** EPIC-001 User Registration & KYC
**Priority:** Must Have
**Story Points:** 8
**Status:** Not Started
**Created:** 2026-03-22
**Sprint:** Backlog

---

## User Story

As a **bank client**
I want to **automatically receive both a ZWG and USD account with virtual card numbers when I register**
So that **I can transact in either currency and use NFC/card payments from day one**

---

## Description

### Background

Currently, registration creates a single ZWG account per phone number. The bank needs to support dual-currency operations (ZWG and USD are both legal tender in Zimbabwe). Each account needs a virtual card number (PAN) that can be tokenized for NFC payments at POS terminals.

### Scope

**In scope:**
- On OTP verification, create TWO accounts per user: one ZWG, one USD
- Change unique constraint from `(phone)` to `(phone, currency)` so one phone can have two accounts
- Generate a virtual card number (PAN) for each account following Luhn-valid format
- Store the virtual card PAN on the Account entity (new field)
- Auto-tokenize the virtual card for the registering device
- Mobile app home screen allows switching between ZWG and USD accounts
- Balance display shows the selected account's balance
- All transactions operate on the currently selected account

**Out of scope:**
- Physical card issuance
- Additional currencies beyond ZWG and USD
- Currency exchange between accounts (future story)

### Virtual Card Number Format

PAN format: `6275 XXXX XXXX XXXX` (16 digits)
- `6275` — Bank Identification Number (BIN) for GoldBank virtual cards
- Next 8 digits — derived from account sequence
- Last 4 digits — includes Luhn check digit
- Luhn algorithm validates the full PAN

---

## Acceptance Criteria

- [ ] On successful OTP verification, TWO accounts are created: one with currency "ZWG", one with currency "USD"
- [ ] Database unique constraint changed from `(phone)` to `(phone, currency)` with soft-delete filter
- [ ] Each account is assigned a unique 16-digit virtual card PAN (Luhn-valid, BIN prefix 6275)
- [ ] New `CardPan` field added to Account entity (VARCHAR 19, nullable for legacy accounts)
- [ ] Both accounts share the same PIN, device binding, KYC status, and personal info
- [ ] `AccountCreated` domain event published for each account
- [ ] Mobile home screen shows account selector (ZWG / USD toggle or dropdown)
- [ ] Balance, transaction history, and all operations use the currently selected account
- [ ] Get profile/balance API returns all accounts for the phone number
- [ ] Existing single-account users are not broken (backward compatible)

---

## Technical Notes

### Database Changes

```sql
-- Remove old unique index, add composite unique
DROP INDEX IF EXISTS ix_accounts_phone_unique;
CREATE UNIQUE INDEX ix_accounts_phone_currency ON accounts (phone, currency) WHERE deleted_at IS NULL;

-- Add virtual card PAN column
ALTER TABLE accounts ADD COLUMN card_pan VARCHAR(19);
CREATE UNIQUE INDEX ix_accounts_card_pan ON accounts (card_pan) WHERE card_pan IS NOT NULL AND deleted_at IS NULL;
```

### Virtual Card PAN Generation

```csharp
// BIN prefix for GoldBank virtual cards
const string BIN_PREFIX = "6275";

// Generate: 6275 + 11 random digits + 1 Luhn check digit = 16 digits
static string GenerateVirtualPan()
{
    var random = RandomNumberGenerator.GetInt32(0, 99999999);
    var partial = $"{BIN_PREFIX}{random:D11}";  // 15 digits
    var checkDigit = CalculateLuhnCheckDigit(partial);
    return $"{partial}{checkDigit}";
}
```

### VerifyOtpHandler Changes

```csharp
// After OTP validated, create both accounts:
var zwgAccount = new Account { Currency = "ZWG", CardPan = GenerateVirtualPan(), ... };
var usdAccount = new Account { Currency = "USD", CardPan = GenerateVirtualPan(), ... };
_dbContext.Accounts.Add(zwgAccount);
_dbContext.Accounts.Add(usdAccount);

// Both share: PhoneNumber, TenantId, DeviceId, Status, KycLevel
// PIN is set on both accounts in CreatePINHandler
```

### Mobile Changes

- `SessionManager` tracks `selectedAccountId` (defaults to ZWG account)
- Home screen shows currency toggle (ZWG | USD) with balance for each
- Account switch updates `selectedAccountId` and refreshes balance/transactions
- Get balance API call uses the selected account ID

### Backward Compatibility

- Existing accounts without `CardPan` continue to work (nullable field)
- The `GetBalance` and `GetProfile` endpoints already accept `account_id`
- New `ListAccounts` or modified `GetProfile` returns all accounts for the phone

---

## Dependencies

**Prerequisite Stories:**
- STORY-009: User Self-Registration with Phone & OTP
- STORY-010: Create Account PIN

**Blocked Stories:**
- None (enhances existing registration flow)

---

## Definition of Done

- [ ] Code implemented and committed
- [ ] Database migration created and tested
- [ ] Both ZWG and USD accounts created on registration
- [ ] Virtual card PANs are Luhn-valid and unique
- [ ] Mobile home screen shows account selector
- [ ] Existing users not broken
- [ ] Unit tests for PAN generation (Luhn validation)
- [ ] Integration test for dual-account registration
- [ ] Code reviewed and approved

---

**This story was created using BMAD Method v6 - Phase 4 (Implementation)**
