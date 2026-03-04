# STORY-022: NFC Payment Tokenization

**Epic:** EPIC-003 NFC Contactless Payments
**Priority:** Must Have
**Story Points:** 8
**Status:** Not Started
**Assigned To:** Unassigned
**Created:** 2026-02-24
**Sprint:** 3

---

## User Story

As a **user**
I want **a secure payment token provisioned to my phone**
So that **my actual account credentials are never exposed during NFC payments**

---

## Description

### Background

In traditional card payments, the cardholder's Primary Account Number (PAN) is transmitted during every transaction, creating a persistent target for fraud. EMV tokenization replaces the real PAN with a token PAN — a surrogate value that is useless if intercepted because it can only be used within the specific device and domain it was issued for.

For UniBank's unbanked target users in Southern Africa, this is critically important. Many users are new to digital payments and need confidence that their money is safe. Tokenization ensures that even if a phone is compromised, the attacker cannot use the token on another device or for card-not-present fraud.

The tokenization flow integrates the HSM service (STORY-021) for secure token generation and the mobile app's Android Keystore / HCE infrastructure for secure token storage on-device. Tokens have a full lifecycle — they can be provisioned, suspended (e.g., lost phone reported), revoked, and refreshed on expiry.

**Functional Requirements:** FR-009 (Payment Tokenization)

### Scope

**In scope:**
- Token generation via HSM during account activation or on-demand provisioning
- Token-to-PAN mapping stored server-side (never on device)
- Token storage in Android Keystore backed by HCE secure storage
- Token lifecycle management: active, suspended, revoked, expired
- Remote token revocation via push notification and server-side blocklist
- Token refresh/renewal before expiry
- PaymentService.ProvisionToken gRPC endpoint
- Database schema for payment_tokens table
- Token status check during payment authorization

**Out of scope:**
- iOS Secure Element / Apple Pay integration (future sprint)
- Physical card tokenization (UniBank is phone-first)
- Token provisioning for wearables
- Visa/Mastercard Token Service Provider (TSP) integration (UniBank acts as its own TSP for the closed-loop network)

### User Flow

1. **Account Activation Trigger:** User completes account activation (KYC verified, account created). System determines device supports NFC/HCE.
2. **Token Provisioning Request:** Mobile app calls `PaymentService.ProvisionToken` via gRPC with account_id and device_id.
3. **HSM Token Generation:** Server calls `HSMService.GenerateToken` — HSM generates a token PAN that maps to the real account PAN. The mapping is stored server-side only.
4. **Token Delivery:** Token PAN, expiry date, and cryptographic material (session key seed derived from token) are returned to the mobile app over the encrypted gRPC channel.
5. **Secure On-Device Storage:** Mobile app stores the token PAN and cryptographic material in Android Keystore, accessible only to the HCE payment application.
6. **Ready for Payment:** Token is now active. When the user taps at a POS, the HCE service uses this token instead of the real PAN.
7. **Token Refresh:** 30 days before expiry, the app automatically requests a new token. Old token transitions to `expired` after the new one is active.
8. **Token Revocation:** If the user reports a lost phone or the bank detects fraud, the token is remotely revoked — push notification instructs the app to delete local token data, and the server adds the token to the blocklist.

---

## Acceptance Criteria

- [ ] Payment token is generated via HSMService.GenerateToken during account activation, producing a unique token PAN per device
- [ ] Token PAN is distinct from the real account PAN and passes Luhn check validation
- [ ] Real account PAN is never stored on the mobile device — only the token PAN and associated cryptographic material
- [ ] Token is stored in Android Keystore with hardware-backed key protection (where available)
- [ ] Token has a defined lifecycle with states: `active`, `suspended`, `revoked`, `expired`
- [ ] Remote revocation marks the token as `revoked` server-side and sends a push notification to the device to delete local token data
- [ ] Token status is validated server-side during every payment authorization — revoked/expired tokens are rejected
- [ ] Token can be refreshed before expiry, with seamless transition (no payment downtime)
- [ ] ProvisionToken gRPC endpoint validates that the requesting account exists, is active, and the device is registered
- [ ] Multiple devices can each have their own token for the same account (one active token per device)

---

## Technical Notes

### Components

| Component | Location | Purpose |
|-----------|----------|---------|
| `TokenProvisioningService.cs` | `src/Modules/UniBank.Payment/Services/` | Orchestrates token provisioning flow |
| `PaymentService.ProvisionToken` | `src/Modules/UniBank.Payment/Grpc/` | gRPC endpoint for token provisioning |
| `TokenLifecycleManager.cs` | `src/Modules/UniBank.Payment/Services/` | Manages token state transitions |
| `TokenRevocationHandler.cs` | `src/Modules/UniBank.Payment/Handlers/` | Wolverine handler for revocation events |
| `TokenRefreshJob.cs` | `src/Modules/UniBank.Payment/Jobs/` | Background job for proactive token refresh |
| `HceTokenStore.kt` | `mobile/android/app/.../hce/` | Android Keystore token storage |
| `TokenProvisioner.kt` | `mobile/shared/.../payment/` | KMP shared token provisioning logic |
| `hsm_service.proto` | `src/Shared/UniBank.Protos/` | HSMService.GenerateToken definition |
| `payment_service.proto` | `src/Shared/UniBank.Protos/` | PaymentService.ProvisionToken definition |

### API / gRPC Endpoints

**ProvisionToken** (`payment_service.proto`):

```protobuf
rpc ProvisionToken (ProvisionTokenRequest) returns (ProvisionTokenResponse);

message ProvisionTokenRequest {
  string account_id = 1;
  string device_id = 2;
  string device_fingerprint = 3; // Hardware attestation data
  string tenant_id = 4;
}

message ProvisionTokenResponse {
  string token_pan = 1;           // Token PAN for HCE use
  string token_reference = 2;     // Server-side reference ID
  int64 expires_at_unix = 3;      // Token expiry timestamp
  bytes session_key_seed = 4;     // Cryptographic seed for session key derivation on-device
  string token_status = 5;        // "active"
  bool success = 6;
  string error_message = 7;
}
```

**RevokeToken** (`payment_service.proto`):

```protobuf
rpc RevokeToken (RevokeTokenRequest) returns (RevokeTokenResponse);

message RevokeTokenRequest {
  string token_reference = 1;
  string reason = 2;              // "lost_device", "fraud", "user_request"
  string tenant_id = 3;
}

message RevokeTokenResponse {
  bool success = 1;
  string error_message = 2;
}
```

**RefreshToken** (`payment_service.proto`):

```protobuf
rpc RefreshToken (RefreshTokenRequest) returns (ProvisionTokenResponse);

message RefreshTokenRequest {
  string current_token_reference = 1;
  string device_id = 2;
  string tenant_id = 3;
}
```

**GetTokenStatus** (`payment_service.proto`):

```protobuf
rpc GetTokenStatus (GetTokenStatusRequest) returns (GetTokenStatusResponse);

message GetTokenStatusRequest {
  string token_pan = 1;
  string tenant_id = 2;
}

message GetTokenStatusResponse {
  string token_reference = 1;
  string status = 2;             // active, suspended, revoked, expired
  string account_id = 3;         // For de-tokenization during payment processing
  int64 expires_at_unix = 4;
  bool success = 5;
  string error_message = 6;
}
```

### Database Changes

**payment_tokens table** (in tenant schema):

```sql
CREATE TABLE payment_tokens (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    account_id      UUID NOT NULL REFERENCES accounts(id),
    token_pan       VARCHAR(19) NOT NULL UNIQUE,    -- Token PAN (Luhn-valid)
    token_reference VARCHAR(50) NOT NULL UNIQUE,    -- Internal reference
    real_pan_hash   VARCHAR(64) NOT NULL,           -- SHA-256 of real PAN (for lookup, not storage of raw PAN)
    status          VARCHAR(20) NOT NULL DEFAULT 'active',  -- active, suspended, revoked, expired
    device_id       VARCHAR(100) NOT NULL,
    device_fingerprint VARCHAR(200),
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    expires_at      TIMESTAMPTZ NOT NULL,
    suspended_at    TIMESTAMPTZ,
    revoked_at      TIMESTAMPTZ,
    revocation_reason VARCHAR(50),
    last_used_at    TIMESTAMPTZ,
    CONSTRAINT chk_token_status CHECK (status IN ('active', 'suspended', 'revoked', 'expired'))
);

CREATE INDEX idx_payment_tokens_account ON payment_tokens (account_id);
CREATE INDEX idx_payment_tokens_device ON payment_tokens (device_id);
CREATE INDEX idx_payment_tokens_status ON payment_tokens (status) WHERE status = 'active';
CREATE INDEX idx_payment_tokens_expiry ON payment_tokens (expires_at) WHERE status = 'active';
```

**token_pan_mapping table** (in core schema, highly restricted access):

```sql
CREATE TABLE token_pan_mapping (
    token_pan       VARCHAR(19) PRIMARY KEY,
    account_pan     VARCHAR(19) NOT NULL,          -- Real PAN (encrypted at rest via column-level encryption)
    account_id      UUID NOT NULL,
    tenant_id       VARCHAR(50) NOT NULL,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- This table is accessed ONLY by the PaymentService during de-tokenization
-- Column-level encryption on account_pan via pgcrypto or application-level AES
```

### Security Considerations

- **Token PAN Generation:** The HSM generates the token PAN using a deterministic algorithm seeded by the real PAN and a domain restriction (device_id). The token PAN passes Luhn validation and uses a dedicated BIN range reserved for tokens (e.g., `9999xxxx`). This ensures tokens are distinguishable from real PANs in the system.
- **Android Keystore Storage:** Token material is stored using Android Keystore APIs with `setIsStrongBoxBacked(true)` where StrongBox is available (hardware security). On devices without StrongBox, TEE-backed Keystore is used. The key is bound to the device and cannot be extracted.
- **Session Key Seed:** The `session_key_seed` returned during provisioning is used by the HCE app to derive per-transaction session keys locally. This avoids needing to call the server for every tap. The seed is derived in the HSM from the token's Zone Master Key.
- **De-tokenization:** During payment processing, the server looks up the token PAN in `token_pan_mapping` to recover the real PAN for account debit. This lookup is restricted to the PaymentService with strict access controls.
- **Revocation Speed:** Revocation must take effect within seconds. The server-side blocklist is checked in the hot path (Redis cache of revoked tokens). Push notification to the device is best-effort — the server-side check is the authoritative control.
- **No Token Reuse:** Once a token is revoked or expired, its token PAN is never reissued. A new provisioning generates a fresh token PAN.
- **Rate Limiting:** Token provisioning is rate-limited to 3 requests per account per hour to prevent abuse.

### Edge Cases

- **Device Without StrongBox:** Fall back to TEE-backed Keystore. Log a warning. Still secure but lower hardware assurance level.
- **Multiple Devices:** A user with two phones gets two separate tokens, each bound to its device_id. Revoking one does not affect the other.
- **Token Expiry During Transaction:** If a token expires mid-transaction (edge timing), the server rejects and the app prompts for re-provisioning. The payment fails gracefully with a clear error message.
- **Network Unavailable During Revocation Push:** The device may not receive the revocation push immediately. Server-side blocklist is the authoritative check, so the token is effectively revoked even if the device hasn't cleaned up yet.
- **Account Closure:** When an account is closed, all associated tokens are revoked automatically via a Wolverine event handler.
- **HSM Unavailable During Provisioning:** If the HSM service is down, provisioning fails with a retryable error. The user is told to try again later. No partial tokens are created.
- **Duplicate Provisioning Request:** If a device already has an active token, the existing token is returned rather than creating a duplicate. Only one active token per device per account.

---

## Dependencies

**Prerequisite Stories:**
- STORY-021: HSM Interface Service (provides HSMService.GenerateToken)
- STORY-013: Account Management (provides account and device registration data)

**Blocked Stories:**
- STORY-023: NFC Contactless Payment at POS (requires provisioned token)
- STORY-024: PIN Entry for High-Value NFC (requires token for payment flow)

**External Dependencies:**
- Android Keystore API (StrongBox preferred, TEE minimum)
- Firebase Cloud Messaging (FCM) for revocation push notifications
- HSM service operational (or SoftHSM2 in dev)

---

## Definition of Done

- [ ] Code implemented and committed
- [ ] Unit tests written and passing (>=80% coverage) — token generation, lifecycle transitions, revocation logic
- [ ] Integration tests passing — end-to-end provisioning via HSM (SoftHSM2), storage verification, revocation flow
- [ ] Android Keystore integration tested on emulator and physical device
- [ ] Token PAN Luhn validation verified
- [ ] Revocation tested — server blocklist check confirmed, push notification sent
- [ ] Rate limiting on provisioning endpoint verified
- [ ] Code reviewed and approved
- [ ] Documentation updated
- [ ] Acceptance criteria validated
- [ ] Deployed to staging

---

## Progress Tracking

**Status History:**
- 2026-02-24: Created

**Actual Effort:** TBD

---

**This story was created using BMAD Method v6 - Phase 4 (Implementation)**
