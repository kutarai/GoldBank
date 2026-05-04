# STORY-023: NFC Contactless Payment at POS

**Epic:** EPIC-003 NFC Contactless Payments
**Priority:** Must Have
**Story Points:** 8
**Status:** Not Started
**Assigned To:** Unassigned
**Created:** 2026-02-24
**Sprint:** 3

---

## User Story

As a **consumer**
I want **to pay by tapping my phone on a POS terminal**
So that **I can make contactless payments without a physical card**

---

## Description

### Background

NFC contactless payments are GoldBank's core differentiator for the unbanked population in Southern Africa. Many potential users do not have access to traditional banking cards, but a growing percentage own NFC-capable Android smartphones. By turning the phone into a contactless payment instrument via Host Card Emulation (HCE), GoldBank eliminates the need for physical card issuance — a significant cost and logistics barrier for serving the unbanked.

The NFC payment flow uses Android's `HostApduService` to emulate a contactless EMV card. When the user taps their phone on any standard contactless POS terminal, the terminal and phone exchange Application Protocol Data Units (APDUs) following the EMV contactless kernel specification. The phone presents the tokenized PAN (from STORY-022) rather than the real account number, signs the transaction data with a session key, and the payment flows through the acquirer/switch to GoldBank's core for authorization.

The end-to-end target is under 2 seconds from tap to authorization response — this requires an optimized hot path with minimal latency at every stage.

**Functional Requirements:** FR-008 (NFC Contactless Payments)

### Scope

**In scope:**
- Android HCE implementation using `HostApduService`
- EMV contactless APDU command/response handling (SELECT, GPO, READ RECORD, GENERATE AC)
- Payment application AID registration
- Token PAN presentation via APDU responses
- Transaction data signing with session key
- Server-side `PaymentService.ProcessNFCPayment` gRPC endpoint
- Token validation and de-tokenization during authorization
- Wolverine saga for debit-credit-fee atomic transaction
- Account balance check and debit
- Merchant credit
- Transaction fee recording
- Sub-2-second end-to-end performance target
- KMP shared logic for NFC payment with Android-specific HCE bridge

**Out of scope:**
- iOS NFC payment (Apple restricts HCE; future integration via Apple Pay)
- Offline payment authorization (all transactions are online-authorized)
- POS terminal software development (relies on standard EMV contactless terminals)
- Acquirer/switch integration details (stubbed for Sprint 3, full integration in Sprint 5)
- Contactless transit payments (specialized use case for later)

### User Flow

1. **Tap Initiation:** Consumer holds their phone near the POS terminal's contactless reader. The NFC field activates the phone's HCE service.
2. **Application Selection:** POS sends a `SELECT` APDU with GoldBank's registered AID. The HCE service responds with the File Control Information (FCI) confirming the payment application.
3. **Get Processing Options:** POS sends `GET PROCESSING OPTIONS` (GPO) with the terminal's Processing Data Object List (PDOL) data (amount, currency, terminal country, transaction type). HCE responds with the Application Interchange Profile (AIP) and Application File Locator (AFL).
4. **Read Records:** POS reads payment records — HCE returns the token PAN, expiry date, and card risk management data from the AFL entries.
5. **Transaction Signing:** POS sends `GENERATE APPLICATION CRYPTOGRAM` (GENERATE AC) with transaction data. The HCE service derives a session key from the stored seed (per STORY-022), computes an Application Cryptogram (AC) over the transaction data, and returns the cryptogram along with the Application Transaction Counter (ATC).
6. **Terminal to Switch:** The POS terminal packages the EMV data into an ISO 8583 authorization message and sends it to the acquirer/switch.
7. **Switch to GoldBank:** The switch routes the authorization request to GoldBank's `PaymentService.ProcessNFCPayment` endpoint.
8. **Server Authorization:**
   a. Validate the token PAN status (active, not revoked/expired)
   b. De-tokenize: look up real PAN from token PAN
   c. Validate the Application Cryptogram using the HSM (derive same session key, verify AC)
   d. Check account balance >= transaction amount + fee
   e. Execute Wolverine saga: Debit consumer account -> Credit merchant account -> Record transaction fee
   f. Return authorization response (approved/declined) with Authorization Response Cryptogram (ARPC)
9. **Response Chain:** Authorization response flows back through switch -> POS terminal. POS displays approved/declined.
10. **Consumer Notification:** Wolverine publishes `TransactionCompleted` event, triggering push notification to the consumer (handled by STORY-025).

---

## Acceptance Criteria

- [ ] Phone emulates a contactless payment card via Android HCE `HostApduService` when tapped on a standard EMV contactless POS terminal
- [ ] Payment application is registered with GoldBank's AID and is selectable by the terminal
- [ ] Token PAN (not real PAN) is presented in APDU responses to the terminal
- [ ] Application Cryptogram is correctly computed using a session key derived from the provisioned seed
- [ ] Transaction is initiated on tap and the complete end-to-end flow (tap to authorization response at POS) completes in under 2 seconds
- [ ] EMV contactless kernel commands (SELECT, GPO, READ RECORD, GENERATE AC) are handled correctly
- [ ] Server-side ProcessNFCPayment validates token status, verifies cryptogram via HSM, checks balance, and executes debit-credit-fee saga
- [ ] Insufficient balance results in a declined response with reason code
- [ ] Revoked or expired token results in a declined response with reason code
- [ ] Invalid cryptogram results in a declined response (potential fraud indicator)
- [ ] Wolverine saga ensures atomicity — if merchant credit fails, consumer debit is rolled back
- [ ] Transaction record is created with type `nfc_payment` and all relevant EMV data fields

---

## Technical Notes

### Components

| Component | Location | Purpose |
|-----------|----------|---------|
| `GoldBankHceService.kt` | `mobile/android/app/.../hce/` | Android HostApduService implementation |
| `EmvApduProcessor.kt` | `mobile/shared/.../payment/nfc/` | APDU command parsing and response building (KMP) |
| `CryptogramGenerator.kt` | `mobile/shared/.../payment/nfc/` | Session key derivation and AC computation (KMP) |
| `NfcPaymentController.kt` | `mobile/shared/.../payment/` | Payment flow orchestrator (KMP) |
| `PaymentGrpcService.cs` | `src/Modules/GoldBank.Payment/Grpc/` | ProcessNFCPayment gRPC endpoint |
| `NfcPaymentHandler.cs` | `src/Modules/GoldBank.Payment/Handlers/` | Server-side NFC payment authorization logic |
| `NfcPaymentSaga.cs` | `src/Modules/GoldBank.Payment/Sagas/` | Wolverine saga: debit -> credit -> fee |
| `TokenValidator.cs` | `src/Modules/GoldBank.Payment/Services/` | Token status check and de-tokenization |
| `CryptogramVerifier.cs` | `src/Modules/GoldBank.Payment/Services/` | AC verification via HSM |

### API / gRPC Endpoints

**ProcessNFCPayment** (`payment_service.proto`):

```protobuf
rpc ProcessNFCPayment (ProcessNFCPaymentRequest) returns (ProcessNFCPaymentResponse);

message ProcessNFCPaymentRequest {
  string token_pan = 1;
  bytes application_cryptogram = 2;  // AC computed by HCE
  int32 application_transaction_counter = 3;  // ATC
  int64 amount_cents = 4;           // Transaction amount in minor currency units
  string currency_code = 5;        // ISO 4217 (e.g., "710" for ZAR)
  string merchant_id = 6;
  string terminal_id = 7;
  string merchant_name = 8;
  string merchant_city = 9;
  bytes terminal_data = 10;        // PDOL data from terminal
  string transaction_type = 11;    // "purchase", "purchase_with_cashback"
  int64 cashback_amount_cents = 12;
  string tenant_id = 13;
}

message ProcessNFCPaymentResponse {
  string authorization_code = 1;   // 6-digit auth code on approval
  string response_code = 2;       // "00" = approved, "51" = insufficient funds, etc.
  string response_message = 3;
  bytes authorization_response_cryptogram = 4;  // ARPC for terminal
  string transaction_id = 5;      // GoldBank transaction reference
  int64 available_balance_cents = 6;  // Post-transaction balance
  bool success = 7;
}
```

### APDU Flow Detail

```
Terminal -> Phone: SELECT AID (00 A4 04 00 [Lc] [AID] 00)
Phone -> Terminal: FCI Template (6F [Lc] [84 AID] [A5 FCI Proprietary])

Terminal -> Phone: GPO (80 A8 00 00 [Lc] [PDOL data] 00)
Phone -> Terminal: AIP + AFL (80 [Lc] [AIP 2 bytes] [AFL entries])

Terminal -> Phone: READ RECORD (00 B2 [record] [SFI|04] 00)
Phone -> Terminal: Record data (70 [Lc] [5A token_pan] [5F24 expiry] [9F27 CID] ...)

Terminal -> Phone: GENERATE AC (80 AE [P1 type] [Lc] [CDOL1 data] 00)
Phone -> Terminal: Cryptogram (80 [Lc] [9F27 CID] [9F36 ATC] [9F26 AC] [9F10 IAD])
```

**AID Registration** (AndroidManifest.xml):
```xml
<service
    android:name=".hce.GoldBankHceService"
    android:exported="true"
    android:permission="android.permission.BIND_NFC_SERVICE">
    <intent-filter>
        <action android:name="android.nfc.cardemulation.action.HOST_APDU_SERVICE" />
    </intent-filter>
    <meta-data
        android:name="android.nfc.cardemulation.host_apdu_service"
        android:resource="@xml/hce_payment_aid" />
</service>
```

**AID resource** (`hce_payment_aid.xml`):
```xml
<host-apdu-service xmlns:android="http://schemas.android.com/apk/res/android"
    android:description="@string/goldbank_hce_description"
    android:apduServiceBanner="@drawable/goldbank_card_banner"
    android:requireDeviceUnlock="false">
    <aid-group
        android:category="payment"
        android:description="@string/goldbank_payment_aid">
        <aid-filter android:name="A000000999010101" /> <!-- GoldBank AID -->
    </aid-group>
</host-apdu-service>
```

### Database Changes

**nfc_transactions table** (in tenant schema):

```sql
CREATE TABLE nfc_transactions (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    transaction_id      UUID NOT NULL UNIQUE,          -- Public transaction reference
    token_pan           VARCHAR(19) NOT NULL,
    account_id          UUID NOT NULL REFERENCES accounts(id),
    merchant_id         VARCHAR(50) NOT NULL,
    terminal_id         VARCHAR(20),
    merchant_name       VARCHAR(100),
    merchant_city       VARCHAR(50),
    amount_cents        BIGINT NOT NULL,
    currency_code       VARCHAR(3) NOT NULL,
    cashback_amount_cents BIGINT DEFAULT 0,
    fee_cents           BIGINT NOT NULL DEFAULT 0,
    transaction_type    VARCHAR(30) NOT NULL,           -- purchase, purchase_with_cashback
    authorization_code  VARCHAR(6),
    response_code       VARCHAR(2) NOT NULL,
    application_cryptogram BYTEA,
    atc                 INT,
    status              VARCHAR(20) NOT NULL DEFAULT 'pending', -- pending, approved, declined, reversed
    cvm_method          VARCHAR(20) NOT NULL DEFAULT 'no_cvm', -- no_cvm, online_pin
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    authorized_at       TIMESTAMPTZ,
    settled_at          TIMESTAMPTZ
);

CREATE INDEX idx_nfc_txn_account ON nfc_transactions (account_id, created_at DESC);
CREATE INDEX idx_nfc_txn_merchant ON nfc_transactions (merchant_id, created_at DESC);
CREATE INDEX idx_nfc_txn_token ON nfc_transactions (token_pan, created_at DESC);
CREATE INDEX idx_nfc_txn_status ON nfc_transactions (status) WHERE status = 'pending';
```

### Wolverine Saga: NFC Payment

```csharp
// Saga steps:
// 1. ValidateToken -> DeTokenize -> VerifyCryptogram
// 2. DebitConsumerAccount (with idempotency key)
// 3. CreditMerchantAccount (with idempotency key)
// 4. RecordTransactionFee
// 5. PublishTransactionCompleted event

// Compensating actions:
// - If CreditMerchant fails -> ReverseConsumerDebit
// - If RecordFee fails -> ReverseConsumerDebit + ReverseMerchantCredit
// - All reversals are logged and alerted
```

### Security Considerations

- **Token-Only Transmission:** The real PAN never leaves the server. Only the token PAN is presented via HCE APDUs to the terminal. Even if the NFC communication is intercepted, the token cannot be used on a different device.
- **Application Cryptogram:** The AC is a MAC computed over the transaction data (amount, currency, ATC, terminal data) using a session key derived from the provisioned seed. The server independently derives the same session key via the HSM and verifies the MAC. This proves both the token and the transaction data are authentic.
- **Replay Protection:** The Application Transaction Counter (ATC) increments with every transaction. The server tracks the last seen ATC per token and rejects any transaction with an ATC less than or equal to the last seen value.
- **Device Binding:** The session key seed in Android Keystore is hardware-bound. Even if the app data is backed up and restored to another device, the key material cannot be extracted.
- **Amount Integrity:** The transaction amount is part of the AC computation. Any amount tampering between the phone and the server invalidates the cryptogram.

### Performance Optimization (Sub-2-Second Target)

- **HCE Response Time:** APDU processing must complete in <500ms total across all commands. Pre-compute responses where possible (token PAN, expiry are static per session). Only GENERATE AC requires computation.
- **Session Key Derivation:** Pre-derive session key on app launch or on NFC field detection, not during APDU processing.
- **Server Hot Path:** ProcessNFCPayment skips non-essential middleware. Token validation uses Redis cache (revocation blocklist). Cryptogram verification is a single HSM call. Balance check and debit are a single DB round-trip with SELECT FOR UPDATE.
- **Connection Pooling:** Persistent gRPC channels between switch and GoldBank core. No TLS handshake per transaction.
- **Database:** Use prepared statements and connection pooling. The debit-credit saga uses a single database transaction where possible (same tenant schema).

### Edge Cases

- **NFC Disabled on Phone:** The HCE service is not registered if NFC is disabled. The app shows a prompt to enable NFC in settings.
- **Multiple Payment Apps:** If the user has another payment app (e.g., Google Pay), Android's NFC payment default selection applies. GoldBank must be set as the default payment app, or the user chooses at tap time.
- **Transaction Timeout:** If the server does not respond within 1.5 seconds, the switch returns a timeout to the terminal. The terminal shows "declined." The server must handle the in-flight transaction correctly (either complete and reconcile later, or timeout and reverse).
- **Partial Saga Failure:** If debit succeeds but credit fails, the compensating action reverses the debit. The consumer sees a declined transaction. An alert fires for the operations team.
- **Duplicate Transaction:** Idempotency key (based on token_pan + ATC + merchant_id + amount) prevents double-charging if the switch retries.
- **Phone Battery Low:** HCE can work even when the phone is in low-power mode, but Android may throttle NFC. Below a certain battery level, the app warns the user that NFC payments may not work.
- **Terminal Does Not Support Contactless:** The tap simply does not activate. No error state on the phone.

---

## Dependencies

**Prerequisite Stories:**
- STORY-022: NFC Payment Tokenization (token must be provisioned before payment)
- STORY-021: HSM Interface Service (session key derivation and cryptogram verification)

**Blocked Stories:**
- STORY-024: PIN Entry for High-Value NFC Transactions (extends this flow with CVM)
- STORY-025: NFC Payment Notifications & Receipt (triggered by TransactionCompleted event from this flow)

**External Dependencies:**
- Android NFC/HCE APIs (API level 19+, target 34+)
- Standard EMV contactless POS terminal for testing
- Switch/acquirer stub for Sprint 3 (full integration Sprint 5)
- HSM service operational (SoftHSM2 for dev)

---

## Definition of Done

- [ ] Code implemented and committed
- [ ] Unit tests written and passing (>=80% coverage) — APDU handling, cryptogram generation, saga logic
- [ ] Integration tests passing — end-to-end flow with mock terminal, SoftHSM2, and test database
- [ ] HCE service tested on physical Android device with real NFC POS terminal (or NFC reader emulator)
- [ ] Performance tested — end-to-end authorization < 2 seconds
- [ ] Wolverine saga tested — including compensating actions for partial failures
- [ ] Idempotency verified — duplicate transactions correctly handled
- [ ] Token revocation correctly blocks payment attempts
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
