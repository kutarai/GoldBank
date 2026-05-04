# STORY-020: Transaction Authorization (High-Value PIN)

**Epic:** EPIC-014
**Priority:** Must Have
**Story Points:** 3
**Status:** Not Started
**Assigned To:** Unassigned
**Created:** 2026-02-24
**Sprint:** 2

---

## User Story

As a user
I want to confirm high-value transactions with PIN
So that large payments require authorization

---

## Description

### Background
Transaction authorization adds an extra layer of security for high-value transactions. While everyday low-value transactions (e.g., buying airtime, small transfers) can proceed with the existing session authentication, transactions above a configurable monetary threshold require explicit PIN confirmation. This protects users from unauthorized large transactions even if their phone is unlocked and the session is active.

This is a regulatory best practice in Southern African mobile money and banking markets, where central banks typically require step-up authentication for transactions exceeding certain thresholds. GoldBank implements this as a configurable feature at the tenant level, allowing each operator to set appropriate thresholds based on their country's regulations and risk appetite.

The authorization requirement applies regardless of how the user originally authenticated -- even if the user logged in with biometrics, high-value transactions still require a PIN. This ensures that biometric convenience does not compromise security for significant financial operations.

### Scope
**In scope:**
- Configurable authorization thresholds per transaction type and per tenant
- Transaction types with thresholds: NFC payments, transfers, cash-out (agent withdrawals)
- PIN verification before transaction execution for amounts above threshold
- Authorization applies regardless of login method (PIN or biometric)
- Failed authorization attempts logged in audit trail
- Threshold configuration stored in tenant configuration
- Authorization check integrated into PaymentService, TransferService, and AgentService

**Out of scope:**
- OTP-based authorization (PIN only for now)
- Transaction limits (maximum amounts) -- separate compliance feature
- Cumulative transaction thresholds (e.g., total daily spending) -- future enhancement
- Dynamic risk-based thresholds (machine learning) -- future enhancement
- Whitelisted recipients exempt from authorization -- future enhancement

### User Flow

**High-Value Transaction (above threshold):**
1. User initiates a transaction (e.g., transfer of 50,000 MWK when threshold is 20,000 MWK)
2. App sends the transaction request to the appropriate service (TransferService, PaymentService, etc.)
3. Service checks the transaction amount against the tenant's threshold for that transaction type
4. Amount exceeds threshold: service returns `AUTHORIZATION_REQUIRED` status with a challenge reference
5. App displays a PIN entry screen: "This transaction requires PIN confirmation"
6. User enters their PIN
7. App sends `AuthorizationService.AuthorizeTransaction` with the challenge reference and PIN
8. Server validates PIN against bcrypt hash
9. If PIN valid: marks the authorization as approved, returns authorization token
10. App retries the original transaction request with the authorization token attached
11. Service verifies the authorization token and proceeds with the transaction
12. Transaction completes normally

**Low-Value Transaction (below threshold):**
1. User initiates a transaction (e.g., transfer of 5,000 MWK when threshold is 20,000 MWK)
2. App sends the transaction request
3. Service checks amount against threshold
4. Amount is below threshold: transaction proceeds without additional authorization
5. Transaction completes normally

**Failed Authorization:**
1. User initiates a high-value transaction
2. Service returns `AUTHORIZATION_REQUIRED`
3. User enters incorrect PIN
4. Server rejects the authorization
5. App shows error: "Incorrect PIN. Transaction not authorized."
6. Failed attempt logged in audit trail
7. User can retry (subject to lockout rules from STORY-018)
8. After 3 failed authorization attempts for a single transaction, the transaction is cancelled

---

## Acceptance Criteria

- [ ] Authorization thresholds are configurable per transaction type and per tenant
- [ ] Transaction types supporting authorization thresholds: NFC payment, transfer, cash-out
- [ ] Transactions above the threshold require PIN re-entry before execution
- [ ] PIN authorization is required regardless of whether the user logged in with PIN or biometric
- [ ] Successful PIN authorization generates a short-lived authorization token (valid for 60 seconds)
- [ ] The authorization token is single-use and bound to the specific transaction
- [ ] Failed authorization attempts are logged in the audit trail with: account_id, transaction_type, amount, timestamp
- [ ] 3 consecutive failed authorizations for a single transaction cancel the transaction
- [ ] Transactions below the threshold proceed without additional authorization
- [ ] Authorization thresholds can be updated by tenant administrators without system restart
- [ ] If no threshold is configured for a transaction type, all transactions of that type require authorization (fail-safe default)

---

## Technical Notes

### Components
- **AuthModule** (`src/Modules/Auth/`):
  - `TransactionAuthorizationService.cs`: Authorization challenge creation, PIN validation, token generation
  - `AuthorizationTokenService.cs`: Short-lived token generation and validation
- **PaymentModule** (`src/Modules/Payment/`):
  - `PaymentService.cs`: Integrates authorization check before NFC payment execution
- **TransferModule** (`src/Modules/Transfer/`):
  - `TransferService.cs`: Integrates authorization check before transfer execution
- **AgentModule** (`src/Modules/Agent/`):
  - `AgentService.cs`: Integrates authorization check before cash-out execution
- **SharedKernel** (`src/SharedKernel/`):
  - `IAuthorizationRequired.cs`: Interface for services requiring transaction authorization
  - `AuthorizationInterceptor.cs`: Shared interceptor that checks authorization before transaction execution
- **AuditModule** (`src/Modules/Audit/`):
  - `AuthorizationAuditHandler.cs`: Logs all authorization attempts

### API / gRPC Endpoints

**Service:** `AuthorizationService`

```protobuf
service AuthorizationService {
  rpc CreateChallenge(CreateChallengeRequest) returns (CreateChallengeResponse);
  rpc AuthorizeTransaction(AuthorizeTransactionRequest) returns (AuthorizeTransactionResponse);
}

message CreateChallengeRequest {
  string account_id = 1;
  string transaction_type = 2;           // "nfc_payment", "transfer", "cash_out"
  string amount = 3;                     // Transaction amount
  string currency = 4;
  string transaction_reference = 5;      // Links to the pending transaction
}

message CreateChallengeResponse {
  string challenge_id = 1;
  bool authorization_required = 2;       // True if amount exceeds threshold
  string message = 3;                    // "PIN required for this transaction"
  int32 challenge_expires_in = 4;        // Seconds until challenge expires (120s)
}

message AuthorizeTransactionRequest {
  string challenge_id = 1;
  string pin = 2;
  string account_id = 3;
}

message AuthorizeTransactionResponse {
  bool authorized = 1;
  string authorization_token = 2;        // Short-lived token (60s), single-use
  string message = 3;
  int32 remaining_attempts = 4;          // Attempts left before transaction cancellation
  int64 token_expires_in = 5;            // Seconds until authorization token expires
}
```

**Integration Pattern for Transaction Services:**

```csharp
// Shared authorization check used by Payment, Transfer, and Agent services
public class AuthorizationInterceptor
{
    public async Task<AuthorizationResult> CheckAuthorization(
        string accountId, string tenantId, string transactionType,
        decimal amount, string authorizationToken = null)
    {
        var threshold = await _configService.GetThreshold(tenantId, transactionType);

        if (amount <= threshold)
            return AuthorizationResult.NotRequired();

        if (string.IsNullOrEmpty(authorizationToken))
            return AuthorizationResult.Required(transactionType, amount);

        // Validate the authorization token
        var isValid = await _authTokenService.ValidateToken(authorizationToken, accountId, transactionType, amount);
        return isValid
            ? AuthorizationResult.Authorized()
            : AuthorizationResult.Invalid("Authorization token invalid or expired");
    }
}
```

### Database Changes

**Table:** `authorization_challenges` (schema: `{tenant_schema}`)

```sql
CREATE TABLE authorization_challenges (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    account_id UUID NOT NULL REFERENCES accounts(id),
    transaction_type VARCHAR(30) NOT NULL,
    transaction_reference VARCHAR(50) NOT NULL,
    amount DECIMAL(18,2) NOT NULL,
    currency VARCHAR(3) NOT NULL,
    status VARCHAR(30) NOT NULL DEFAULT 'pending',
    failed_attempts INT NOT NULL DEFAULT 0,
    max_attempts INT NOT NULL DEFAULT 3,
    authorization_token_hash VARCHAR(64),
    token_expires_at TIMESTAMPTZ,
    expires_at TIMESTAMPTZ NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    resolved_at TIMESTAMPTZ,
    CONSTRAINT fk_auth_challenge_account FOREIGN KEY (account_id) REFERENCES accounts(id)
);

CREATE INDEX idx_auth_challenges_account ON authorization_challenges(account_id);
CREATE INDEX idx_auth_challenges_status ON authorization_challenges(status) WHERE status = 'pending';
```

**Status Values:** `pending`, `authorized`, `failed`, `expired`, `cancelled`

**Table:** `tenant_authorization_config` (schema: `{tenant_schema}`)

```sql
CREATE TABLE tenant_authorization_config (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id UUID NOT NULL,
    transaction_type VARCHAR(30) NOT NULL,
    threshold_amount DECIMAL(18,2) NOT NULL,
    currency VARCHAR(3) NOT NULL,
    is_enabled BOOLEAN NOT NULL DEFAULT TRUE,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_by UUID,
    CONSTRAINT uq_tenant_auth_config UNIQUE (tenant_id, transaction_type, currency)
);

-- Example configuration for a Malawian tenant
INSERT INTO tenant_authorization_config (tenant_id, transaction_type, threshold_amount, currency)
VALUES
    ('tenant-mw-uuid', 'nfc_payment', 20000.00, 'MWK'),
    ('tenant-mw-uuid', 'transfer', 50000.00, 'MWK'),
    ('tenant-mw-uuid', 'cash_out', 30000.00, 'MWK');
```

### Security Considerations
- **PIN Transmission:** PIN sent over TLS-encrypted gRPC. Same transport security as login (STORY-018).
- **Authorization Token:** Short-lived (60 seconds), single-use, bound to a specific transaction (type + amount + account). Stored as SHA-256 hash in database. Cannot be reused for a different transaction.
- **Fail-Safe Default:** If no threshold is configured for a transaction type, ALL transactions require authorization. This prevents a configuration gap from allowing unauthorized large transactions.
- **Lockout Integration:** Failed PIN attempts in authorization contribute to the same lockout counter as login attempts (STORY-018). This prevents attackers from using the authorization flow to brute-force the PIN.
- **Audit Trail:** Every authorization attempt (success and failure) is logged with full context: account, transaction type, amount, outcome, timestamp, IP, device.
- **Challenge Expiry:** Authorization challenges expire after 120 seconds. The user must initiate a new challenge if the old one expires. This limits the window for intercepting authorization flows.
- **Race Condition Prevention:** The authorization token is consumed atomically (marked as used in the same database transaction that initiates the actual financial transaction). This prevents double-spend via token reuse.

### Edge Cases
- **Threshold exactly equal to transaction amount:** Transactions at exactly the threshold amount do NOT require authorization (threshold is exclusive: amount > threshold requires auth).
- **Currency mismatch:** If the transaction currency differs from the configured threshold currency, convert using the tenant's configured exchange rate before comparison. If no rate is available, require authorization (fail-safe).
- **Multiple pending challenges for same account:** Only one active challenge per account per transaction type. Creating a new challenge cancels the previous one.
- **Authorization token used after session expires:** The authorization token is tied to the session. If the session expires between authorization and transaction execution, the user must re-authenticate and re-authorize.
- **Tenant admin updates threshold during active challenge:** The challenge retains the threshold at creation time. The new threshold applies to future challenges only.
- **Transaction amount changes after authorization:** If the final transaction amount differs from the authorized amount (e.g., due to fees), the authorization is invalid. The user must re-authorize with the correct total.
- **Concurrent authorization from same account:** Serial processing ensured via database locking on the challenge record.

---

## Dependencies

**Prerequisite Stories:**
- STORY-018: PIN & Biometric Authentication (PIN validation infrastructure, lockout tracking)

**Blocked Stories:**
- Future transaction stories (NFC payments, transfers, cash-out) will integrate with this authorization framework

**External Dependencies:**
- None beyond existing infrastructure (Redis, PostgreSQL)

---

## Definition of Done

- [ ] Code implemented and committed
- [ ] Unit tests written and passing (>=80% coverage)
- [ ] Integration tests passing
- [ ] Code reviewed and approved
- [ ] Documentation updated
- [ ] Acceptance criteria validated
- [ ] Deployed to staging
- [ ] Authorization required correctly for amounts above threshold
- [ ] Authorization not required for amounts below threshold
- [ ] PIN validation verified for authorization
- [ ] Authorization token verified: single-use, time-limited, transaction-bound
- [ ] Failed authorization logging verified in audit trail
- [ ] 3 failed attempts cancels the transaction
- [ ] Fail-safe default verified: missing configuration requires authorization
- [ ] Tenant-configurable thresholds verified with different tenant configurations

---

## Progress Tracking

**Status History:**
- 2026-02-24: Created

**Actual Effort:** TBD

---

**This story was created using BMAD Method v6 - Phase 4 (Implementation)**
