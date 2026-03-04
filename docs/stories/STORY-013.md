# STORY-013: Account Activation on KYC Approval

**Epic:** EPIC-001
**Priority:** Must Have
**Story Points:** 3
**Status:** Not Started
**Assigned To:** Unassigned
**Created:** 2026-02-24
**Sprint:** 2

---

## User Story

As a verified user
I want my account activated after KYC approval
So that I can start transacting

---

## Description

### Background
Once a user's KYC process is approved (either automatically via selfie match in STORY-012 or manually by a KYC reviewer), their account must be activated so they can begin using UniBank's financial services. This is the pivotal transition point in the onboarding journey -- moving a user from a registration-only state to a fully functional banking customer.

Account activation is an event-driven process orchestrated by a Wolverine saga. When the `KYCApproved` event is published (from STORY-012), the saga coordinates several actions: updating the account status, initializing the zero balance record, sending welcome notifications, and publishing the `AccountCreated` event for downstream consumers. This must happen reliably and atomically -- if any step fails, the saga must handle compensation.

No fees are charged during account activation. The zero-balance initialization ensures the user's account is ready for deposits (cash-in via agent, incoming transfers, etc.) immediately after activation.

### Scope
**In scope:**
- Wolverine saga that reacts to `KYCApproved` event
- Account status transition from `pending_kyc` to `active`
- Zero balance record initialization (0.00 in tenant's primary currency)
- Publishing `AccountCreated` Wolverine event
- Welcome push notification via Notification Service
- Welcome SMS to the registered phone number
- Idempotent activation (re-processing `KYCApproved` for an already active account is a no-op)

**Out of scope:**
- KYC approval logic (handled in STORY-012)
- Initial deposit or promotional credit
- Account tier assignment (future story)
- Welcome email (target users are unlikely to have email)
- Account activation for manual KYC review approvals (same saga, triggered by same event from reviewer UI)

### User Flow
1. KYC selfie match completes with score above auto-approve threshold (STORY-012)
2. `KYCApproved` event is published to the Wolverine message bus
3. `AccountActivationSaga` picks up the event
4. Saga updates account status from `pending_kyc` to `active` in the `accounts` table
5. Saga creates a balance record with `available_balance = 0.00` and `ledger_balance = 0.00`
6. Saga publishes `AccountCreated` event (consumed by analytics, notification service, etc.)
7. Notification Service receives `AccountCreated` and sends:
   - Push notification: "Welcome to {TenantBrandName}! Your account is now active."
   - SMS: "Welcome to {TenantBrandName}. Your account {masked_account_number} is active. Dial *XXX# or use the app to get started."
8. User sees their account home screen with a zero balance and a prompt to make their first deposit

---

## Acceptance Criteria

- [ ] Account status transitions from `pending_kyc` to `active` when `KYCApproved` event is received
- [ ] A balance record is created with `available_balance = 0.00` and `ledger_balance = 0.00` in the tenant's primary currency
- [ ] `AccountCreated` Wolverine event is published after successful activation
- [ ] A welcome push notification is sent to the user's registered device
- [ ] A welcome SMS is sent to the user's registered phone number
- [ ] No fees are charged during the activation process
- [ ] Activation is idempotent: processing `KYCApproved` for an already active account does not create duplicate records or send duplicate notifications
- [ ] Account status transition is atomic: if balance creation fails, account status is rolled back
- [ ] The entire activation sequence completes within 5 seconds of receiving the `KYCApproved` event
- [ ] Activation failure triggers an alert for operations team investigation

---

## Technical Notes

### Components
- **AccountModule** (`src/Modules/Account/`):
  - `AccountActivationSaga.cs`: Wolverine saga orchestrating the activation sequence
  - `AccountRepository.cs`: Update account status, create balance record
  - `BalanceRepository.cs`: Initialize balance record
- **KYCModule** (`src/Modules/KYC/`): Publishes `KYCApproved` event (from STORY-012)
- **NotificationModule** (`src/Modules/Notification/`):
  - `WelcomeNotificationHandler.cs`: Handles `AccountCreated` event to send push + SMS
  - `PushNotificationService.cs`: Firebase Cloud Messaging integration
  - `SmsNotificationService.cs`: SMS gateway integration
- **Wolverine Infrastructure** (`src/Infrastructure/Messaging/`): Saga persistence, event routing

### API / gRPC Endpoints

No new gRPC endpoints are introduced in this story. Activation is entirely event-driven.

**Wolverine Saga:** `AccountActivationSaga`

```csharp
public class AccountActivationSaga : Saga
{
    public Guid AccountId { get; set; }
    public string TenantId { get; set; }

    // Entry point: triggered by KYCApproved event
    public void Start(KYCApproved @event, IAccountRepository accountRepo, IBalanceRepository balanceRepo)
    {
        AccountId = @event.AccountId;
        TenantId = @event.TenantId;

        // 1. Update account status
        // 2. Create balance record
        // 3. Publish AccountCreated
        // Saga completes on success
    }

    // Compensation if downstream fails
    public void Compensate(AccountActivationFailed @event) { /* rollback logic */ }
}
```

**Events:**

```csharp
public record AccountCreated(
    Guid AccountId,
    string PhoneNumber,
    string AccountNumber,
    string TenantId,
    string Currency,
    DateTimeOffset ActivatedAt
);

public record AccountActivationFailed(
    Guid AccountId,
    string TenantId,
    string Reason,
    DateTimeOffset FailedAt
);
```

### Database Changes

**Table:** `account_balances` (schema: `{tenant_schema}`)

```sql
CREATE TABLE account_balances (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    account_id UUID NOT NULL REFERENCES accounts(id),
    currency VARCHAR(3) NOT NULL,
    available_balance DECIMAL(18,2) NOT NULL DEFAULT 0.00,
    ledger_balance DECIMAL(18,2) NOT NULL DEFAULT 0.00,
    last_transaction_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    version INT NOT NULL DEFAULT 1,
    CONSTRAINT uq_account_balance UNIQUE (account_id, currency),
    CONSTRAINT fk_balance_account FOREIGN KEY (account_id) REFERENCES accounts(id)
);

CREATE INDEX idx_account_balances_account_id ON account_balances(account_id);
```

**Table Update:** `accounts` (status transition)

```sql
-- Status values: registered, pending_kyc, active, suspended, closed
-- This story transitions: pending_kyc -> active
UPDATE accounts SET status = 'active', activated_at = NOW(), updated_at = NOW()
WHERE id = @account_id AND status = 'pending_kyc';
```

**Column Addition:** `accounts.activated_at`

```sql
ALTER TABLE accounts ADD COLUMN activated_at TIMESTAMPTZ;
```

### Security Considerations
- **Idempotency:** The saga must be idempotent. If the `KYCApproved` event is delivered more than once (at-least-once delivery), the saga should detect that the account is already active and skip processing.
- **Status Transition Guards:** Only accounts in `pending_kyc` status can transition to `active`. Any other status transition attempt should be rejected and logged.
- **Balance Initialization:** The balance record must be created atomically with the status update. Use a database transaction to ensure consistency.
- **Notification Security:** Welcome SMS must not contain sensitive information (no PIN, no full account number). Use masked account number.
- **Event Ordering:** Ensure `AccountCreated` is only published after the database transaction commits successfully (transactional outbox pattern via Wolverine).

### Edge Cases
- **Duplicate `KYCApproved` events:** Saga checks if account is already `active` before processing. If active, log and skip.
- **Account not found:** If the account referenced in `KYCApproved` does not exist, log an error, publish `AccountActivationFailed`, and alert operations.
- **Balance record already exists:** If a balance record already exists for this account and currency, do not create a duplicate. This handles idempotent re-processing.
- **Notification service unavailable:** Activation should still succeed even if push/SMS fails. Notification failures should be retried asynchronously. Account status must not depend on notification delivery.
- **Concurrent KYC approval:** If both auto-approve and manual approve fire for the same account, the idempotency guard prevents double activation.
- **Tenant currency configuration missing:** Fall back to a safe default (e.g., USD) and log a critical warning. Tenant configuration should always include primary currency.

---

## Dependencies

**Prerequisite Stories:**
- STORY-012: KYC - Selfie Capture & Photo Match (publishes `KYCApproved` event)

**Blocked Stories:**
- STORY-015: Account Profile View & Edit (requires active account)
- STORY-016: Account Balance Inquiry (requires balance record)
- STORY-017: Transaction History (requires active account)

**External Dependencies:**
- Firebase Cloud Messaging (FCM) for push notifications
- SMS gateway provider (e.g., Twilio, Africa's Talking) for welcome SMS
- Wolverine message bus infrastructure

---

## Definition of Done

- [ ] Code implemented and committed
- [ ] Unit tests written and passing (>=80% coverage)
- [ ] Integration tests passing
- [ ] Code reviewed and approved
- [ ] Documentation updated
- [ ] Acceptance criteria validated
- [ ] Deployed to staging
- [ ] Saga verified end-to-end: KYCApproved -> account active -> balance created -> AccountCreated published -> notifications sent
- [ ] Idempotency verified: duplicate KYCApproved events do not cause duplicate activations
- [ ] Compensation verified: failed activation rolls back partial state changes
- [ ] Push notification and SMS delivery confirmed in staging environment

---

## Progress Tracking

**Status History:**
- 2026-02-24: Created

**Actual Effort:** TBD

---

**This story was created using BMAD Method v6 - Phase 4 (Implementation)**
