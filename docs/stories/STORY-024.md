# STORY-024: PIN Entry for High-Value NFC Transactions

**Epic:** EPIC-003 NFC Contactless Payments
**Priority:** Must Have
**Story Points:** 5
**Status:** Not Started
**Assigned To:** Unassigned
**Created:** 2026-02-24
**Sprint:** 3

---

## User Story

As a **consumer**
I want **to enter my PIN on the terminal for large NFC payments**
So that **high-value transactions have additional security**

---

## Description

### Background

Contactless payments are designed for speed and convenience, but high-value transactions require additional cardholder verification to mitigate fraud risk. This follows the EMV Cardholder Verification Method (CVM) framework, which defines how the terminal and card (or phone in this case) negotiate the verification method based on transaction characteristics.

For UniBank, the CVM threshold is configurable per tenant — this is critical for the white-label model where each bank deploying UniBank may have different risk appetites and regulatory requirements across Southern African countries. For example, a Zambian bank might set the threshold at 500 ZMW while a South African bank sets it at 500 ZAR.

Below the threshold, the transaction proceeds with "no CVM" — just a tap. Above the threshold, the terminal prompts the consumer to enter their PIN on the POS terminal keypad. The PIN is encrypted at the terminal using a session key derived from the HSM, transmitted as an encrypted PIN block through the switch, and verified server-side by the HSM.

**Functional Requirements:** FR-010 (CVM for High-Value NFC)

### Scope

**In scope:**
- CVM list configuration in HCE APDU responses indicating supported verification methods
- Tenant-configurable PIN threshold stored in tenant configuration
- CVM selection logic based on transaction amount vs. threshold
- Terminal PIN entry flow (PIN entered on POS keypad, not on phone)
- PIN block encryption at terminal using session key from HSM
- PIN block transmission through switch to UniBank
- Server-side PIN block decryption via HSMService.DecryptPINBlock
- PIN verification against stored account PIN hash
- Integration with STORY-023 NFC payment flow (CVM is an additional step in the same flow)

**Out of scope:**
- On-device PIN entry (CDCVM — Consumer Device CVM). This is a future enhancement where PIN is entered on the phone screen instead of the terminal keypad.
- Biometric CVM (fingerprint/face on phone). Future enhancement.
- Offline PIN verification (all PIN verification is online in UniBank)
- PIN change flow (separate story)
- PIN retry counting and lockout (handled by existing account security module)

### User Flow

1. **Low-Value Transaction (Below Threshold):**
   a. Consumer taps phone on terminal
   b. HCE responds with CVM list indicating "no CVM required" for the transaction amount
   c. Terminal processes without PIN prompt
   d. Transaction authorized as per STORY-023 flow

2. **High-Value Transaction (Above Threshold):**
   a. Consumer taps phone on terminal
   b. Terminal reads the CVM list from HCE and determines that "online PIN" is required for the amount
   c. Terminal displays "Enter PIN" prompt on its screen
   d. Consumer enters their 4-6 digit PIN on the terminal's physical keypad
   e. Terminal encrypts the PIN into an ISO 9564 Format 0 PIN block using the terminal session key
   f. Terminal sends the encrypted PIN block along with the transaction data through the switch to UniBank
   g. UniBank's PaymentService receives the request, calls HSMService.DecryptPINBlock to recover the cleartext PIN
   h. Cleartext PIN is hashed and compared against the stored account PIN hash
   i. If PIN matches: transaction authorized (proceed with debit-credit saga)
   j. If PIN does not match: transaction declined with response code "55" (incorrect PIN)
   k. PIN retry counter incremented; after 3 failed attempts, token is suspended

---

## Acceptance Criteria

- [ ] CVM threshold is configurable per tenant via the `nfc_pin_threshold` setting in tenant configuration
- [ ] Transactions below the threshold proceed with "no CVM" — no PIN prompt on terminal
- [ ] Transactions at or above the threshold trigger "online PIN" CVM — terminal prompts for PIN entry
- [ ] HCE APDU responses include a correctly formatted CVM list that the terminal can parse to determine the required verification method
- [ ] PIN entered on the terminal keypad is encrypted into an ISO 9564 Format 0 PIN block using the terminal session key
- [ ] Server-side PIN verification decrypts the PIN block via HSMService.DecryptPINBlock and validates against the stored PIN hash
- [ ] Correct PIN results in transaction approval (subject to balance check)
- [ ] Incorrect PIN results in decline with response code "55" and increments the PIN retry counter
- [ ] Three consecutive incorrect PIN attempts suspend the payment token and require re-activation
- [ ] Threshold value of 0 means "always require PIN" and a very high value means "never require PIN" (effectively no-CVM always)
- [ ] CVM method used is recorded in the transaction record (`cvm_method` field)

---

## Technical Notes

### Components

| Component | Location | Purpose |
|-----------|----------|---------|
| `CvmListBuilder.kt` | `mobile/shared/.../payment/nfc/` | Builds CVM list for APDU responses (KMP) |
| `EmvApduProcessor.kt` | `mobile/shared/.../payment/nfc/` | Updated to include CVM data in GPO/records |
| `UniBankHceService.kt` | `mobile/android/app/.../hce/` | Updated HCE service with CVM list |
| `PinVerificationService.cs` | `src/Modules/UniBank.Payment/Services/` | Server-side PIN verification logic |
| `NfcPaymentHandler.cs` | `src/Modules/UniBank.Payment/Handlers/` | Updated to handle PIN block in auth request |
| `TenantConfigService.cs` | `src/Modules/UniBank.Tenant/Services/` | Provides nfc_pin_threshold per tenant |

### CVM List Encoding

The CVM list is included in the payment application records returned during the READ RECORD phase of the EMV contactless flow.

**EMV Tag 8E: CVM List**

```
CVM List Structure:
- Bytes 1-4: Amount X (CVM threshold in minor units, big-endian)
- Bytes 5-8: Amount Y (secondary threshold, set to same as X for UniBank)
- Remaining: CVM rules (2 bytes each)

CVM Rules for UniBank:
  Rule 1: 02 03  -> Encrypted PIN online, if terminal supports (condition: amount >= X)
  Rule 2: 1F 00  -> No CVM required (condition: always, fallback)

Encoding example for threshold 50000 cents (500.00 ZAR):
  8E 0C 0000C350 0000C350 0203 1F00
  |  |  |        |        |    |
  |  |  |        |        |    +-- Rule 2: No CVM, always (fallback)
  |  |  |        |        +------- Rule 1: Online PIN, if amount >= X
  |  |  |        +----------------- Amount Y (50000)
  |  |  +-------------------------- Amount X (50000)
  |  +----------------------------- Length: 12 bytes
  +-------------------------------- Tag: CVM List
```

### API / gRPC Endpoints

The `ProcessNFCPaymentRequest` from STORY-023 already includes fields for PIN data. This story populates them for high-value transactions:

```protobuf
// Extended fields in ProcessNFCPaymentRequest:
message ProcessNFCPaymentRequest {
  // ... existing fields from STORY-023 ...
  bytes encrypted_pin_block = 14;   // ISO 9564 Format 0, present if CVM = online_pin
  string pin_key_reference = 15;    // Terminal session key reference for PIN decryption
  string cvm_method = 16;           // "no_cvm" or "online_pin"
}
```

**Internal call to HSM for PIN verification:**

```csharp
// In PinVerificationService.cs
public async Task<bool> VerifyPinAsync(
    byte[] encryptedPinBlock,
    string pan,
    string pinKeyReference,
    string storedPinHash,
    string tenantId)
{
    // 1. Decrypt PIN block via HSM
    var decryptResponse = await _hsmClient.DecryptPINBlockAsync(new DecryptPINBlockRequest
    {
        PinBlock = ByteString.CopyFrom(encryptedPinBlock),
        Pan = pan,
        ZonePinKeyRef = pinKeyReference,
        TenantId = tenantId
    });

    if (!decryptResponse.Success)
        throw new HsmOperationException(decryptResponse.ErrorMessage);

    // 2. Hash recovered PIN and compare
    var recoveredPinHash = HashPin(decryptResponse.Pin, salt);
    return CryptographicEquals(recoveredPinHash, storedPinHash);
}
```

### Tenant Configuration

```sql
-- In tenant_config table
INSERT INTO tenant_config (tenant_id, config_key, config_value, description)
VALUES
  ('tenant_za_01', 'nfc_pin_threshold', '50000', 'NFC PIN threshold in cents (500.00 ZAR)'),
  ('tenant_zm_01', 'nfc_pin_threshold', '50000', 'NFC PIN threshold in cents (500.00 ZMW)');
```

The threshold is cached in Redis with a TTL of 5 minutes. Changes take effect within 5 minutes without service restart.

### Database Changes

No new tables required. Updates to existing structures:

```sql
-- Add cvm_method to nfc_transactions if not already present (from STORY-023)
-- Already included in STORY-023 schema: cvm_method VARCHAR(20) NOT NULL DEFAULT 'no_cvm'

-- PIN retry tracking (in tenant schema)
CREATE TABLE pin_retry_tracker (
    account_id      UUID PRIMARY KEY REFERENCES accounts(id),
    consecutive_failures INT NOT NULL DEFAULT 0,
    last_failure_at TIMESTAMPTZ,
    locked_until    TIMESTAMPTZ,
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
```

### Security Considerations

- **PIN Never in Logs:** The cleartext PIN recovered from HSM decryption must never be written to any log, metric, or trace. The PIN exists only in memory for the duration of the hash comparison, then is zeroed.
- **PIN Hashing:** Account PINs are stored as bcrypt (or Argon2id) hashes with per-account salt. Comparison uses constant-time equality to prevent timing attacks.
- **Terminal Session Key:** The terminal's PIN encryption key (Terminal PIN Key, TPK) is derived from the Terminal Master Key (TMK) established during terminal key exchange. This key exchange uses the HSM to ensure the TPK is known only to the terminal and the HSM.
- **PIN Block in Transit:** The encrypted PIN block travels through the switch as opaque bytes. Only the HSM can decrypt it. The switch cannot read the PIN.
- **PIN Try Counter:** After 3 consecutive failed PIN attempts, the token is suspended (not the account — other payment methods still work). Reactivation requires customer support or in-app identity verification.
- **Threshold Manipulation:** The threshold is read from server-side tenant config, not from the device. Even if the CVM list in the HCE response is tampered with, the server independently checks whether the transaction amount requires PIN and whether a valid PIN block was provided.
- **PIN Entered on Terminal, Not Phone:** The PIN is entered on the POS terminal's physical keypad, which is a PCI PTS certified secure cryptographic device (SCD). This is more secure than software PIN entry on a phone.

### Edge Cases

- **Terminal Does Not Support Online PIN:** Some older terminals may not support encrypted PIN for contactless. In this case, the terminal's CVM processing falls back to "no CVM." The server must decide: approve without PIN (risk acceptance) or decline. This is configurable per tenant via `nfc_require_pin_above_threshold` (strict or lenient mode).
- **PIN Entry Timeout:** If the consumer does not enter the PIN within the terminal's timeout (typically 30 seconds), the terminal cancels the transaction. No request reaches UniBank.
- **Amount Exactly at Threshold:** Amounts equal to the threshold require PIN (>=, not >).
- **Currency Mismatch:** The CVM threshold is in the tenant's base currency. If the transaction currency differs (unlikely in Southern African domestic transactions), the threshold comparison should be in the transaction currency after conversion. For Sprint 3, assume same currency.
- **Contactless Limit vs. PIN Threshold:** Some markets have a regulatory contactless limit (e.g., transactions above a certain amount cannot be contactless at all). This is separate from the PIN threshold and can be configured as `nfc_max_amount` in tenant config.
- **Token Suspension After PIN Failures:** When a token is suspended, the consumer receives a push notification explaining the suspension and how to reactivate. Other tokens on other devices are not affected.

---

## Dependencies

**Prerequisite Stories:**
- STORY-023: NFC Contactless Payment at POS (this story extends the NFC payment flow)
- STORY-021: HSM Interface Service (provides DecryptPINBlock and DeriveSessionKey for terminal key exchange)

**Blocked Stories:**
- None directly (STORY-025 notifications already triggered by STORY-023's TransactionCompleted event)

**External Dependencies:**
- POS terminal with contactless and online PIN support for testing
- HSM service operational (SoftHSM2 for dev)
- Terminal key exchange process (TMK injection — may be manual for Sprint 3 testing)

---

## Definition of Done

- [ ] Code implemented and committed
- [ ] Unit tests written and passing (>=80% coverage) — CVM list building, PIN verification logic, threshold comparison
- [ ] Integration tests passing — end-to-end flow with PIN entry via mock terminal, HSM PIN block decryption
- [ ] CVM threshold configurable per tenant and correctly cached
- [ ] Below-threshold transactions proceed without PIN
- [ ] Above-threshold transactions correctly require and verify PIN
- [ ] PIN retry counter tested — 3 failures suspend token
- [ ] Security audit: no PIN material in logs confirmed
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
