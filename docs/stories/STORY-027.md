# STORY-027: Scan QR Code & Process Payment

**Epic:** EPIC-004 EMV QR Code Payments
**Priority:** Must Have
**Story Points:** 5
**Status:** Not Started
**Assigned To:** Unassigned
**Created:** 2026-02-24
**Sprint:** 3

---

## User Story

As a **consumer**
I want **to scan a QR code to make a payment**
So that **I can pay without NFC**

---

## Description

### Background

QR code scanning is the payer-side complement to STORY-026 (QR generation). Together, they form GoldBank's second payment rail — designed for merchants without NFC terminals, street vendors with printed QR codes, and peer-to-peer payments between individuals.

The flow is consumer-initiated: the payer opens their phone camera (or the in-app scanner), scans the merchant's EMV QR code, confirms the payment details, authorizes with PIN or biometric, and the payment is processed. The entire flow targets sub-2-second processing after authorization.

This payment method is particularly important for Southern Africa's informal economy. A market vendor can accept digital payments with nothing more than a printed QR code — no POS terminal, no card reader, no special hardware. The consumer's phone does all the work.

The scanning flow must handle both static QR codes (payer enters amount) and dynamic QR codes (amount pre-filled from the QR data). Authorization follows the same threshold-based CVM rules as NFC: below the threshold, biometric (fingerprint) is sufficient; above the threshold, PIN entry is required.

**Functional Requirements:** FR-013 (QR Code Payment Processing)

### Scope

**In scope:**
- Camera-based QR code scanning in the mobile app
- EMV QRCPS data parsing and validation
- Merchant identification and name display for payer confirmation
- Amount entry for static QR codes
- Pre-filled amount display for dynamic QR codes
- CVM selection: biometric (below threshold) or PIN (above threshold) per FR-061
- `PaymentService.ProcessQRPayment` gRPC endpoint
- Wolverine saga: debit payer, credit merchant, record fee
- Sub-2-second processing after authorization
- Dynamic QR validity check (not expired, not already used)
- KMP shared logic for QR parsing with platform-specific camera bridge

**Out of scope:**
- QR code generation (STORY-026)
- Payment confirmation and notifications (STORY-028)
- Image-from-gallery QR scanning (camera only for Sprint 3)
- QR code scanning for non-payment purposes (e.g., loyalty, identity)
- Cross-border QR payments (domestic only for Sprint 3)

### User Flow

1. **Open Scanner:** Consumer taps "Pay" or "Scan QR" in the GoldBank app. The camera opens with a QR scanning overlay.
2. **Scan QR Code:** Consumer points the camera at the merchant's QR code (screen or printed). The scanner detects and decodes the QR in real-time.
3. **Parse EMV Data:** The app parses the EMV QRCPS data string, extracting merchant info, amount (if dynamic), and currency. Validates CRC-16 checksum.
4. **Display Confirmation:** The app shows a confirmation screen:
   - Merchant name and city (from QR data)
   - Amount (from QR if dynamic, or input field if static)
   - Currency
   - Transaction fee (calculated based on tenant fee schedule)
   - Total debit amount (payment + fee)
5. **Amount Entry (Static QR Only):** If the QR is static (no amount), the consumer enters the amount manually. The app validates it is positive and within transaction limits.
6. **Authorization:** Based on the amount vs. the tenant's CVM threshold:
   - **Below threshold:** Biometric prompt (fingerprint via Android BiometricPrompt)
   - **Above threshold:** PIN entry on the phone screen (secure PIN input with scrambled keypad)
7. **Process Payment:** App calls `PaymentService.ProcessQRPayment` with all transaction details.
8. **Server Processing:**
   a. Validate QR reference (if dynamic: check not expired, not used)
   b. Validate merchant exists and is active
   c. Validate payer account has sufficient balance
   d. Execute Wolverine saga: debit payer -> credit merchant -> record fee
   e. Mark dynamic QR as `used`
   f. Return authorization response
9. **Confirmation:** App displays success/failure screen (detailed in STORY-028).

---

## Acceptance Criteria

- [ ] Camera opens within 1 second when the user taps "Scan QR" and displays a scanning overlay
- [ ] QR code is parsed according to EMV QRCPS specification — all mandatory data elements extracted
- [ ] CRC-16 checksum is validated — QR codes with invalid CRC are rejected with a clear error message
- [ ] Payment details are displayed for confirmation before authorization: merchant name, amount, fee, total
- [ ] For static QR codes, the consumer can enter the amount — validated as positive and within tenant transaction limits
- [ ] For dynamic QR codes, the amount is pre-filled and read-only
- [ ] Authorization requires biometric (below threshold) or PIN (above threshold) per tenant CVM configuration
- [ ] `PaymentService.ProcessQRPayment` processes the payment and returns a response in under 2 seconds
- [ ] Wolverine saga atomically debits payer, credits merchant, and records the fee
- [ ] Expired dynamic QR codes are rejected with a clear "QR code expired" message
- [ ] Already-used dynamic QR codes are rejected with a clear "QR code already used" message
- [ ] Insufficient balance results in a declined response with the reason displayed to the consumer
- [ ] Transaction record is created with type `qr_payment` on success

---

## Technical Notes

### Components

| Component | Location | Purpose |
|-----------|----------|---------|
| `QrScannerScreen.kt` | `mobile/shared/.../payment/qr/` | KMP scanner UI with confirmation |
| `EmvQrParser.kt` | `mobile/shared/.../payment/qr/` | EMV QRCPS data string parser (KMP) |
| `Crc16Validator.kt` | `mobile/shared/.../payment/qr/` | CRC-16 checksum validation (KMP) |
| `QrPaymentController.kt` | `mobile/shared/.../payment/qr/` | Payment flow orchestrator (KMP) |
| `CameraQrScanner.kt` | `mobile/android/app/.../camera/` | Android CameraX + ML Kit QR scanner |
| `BiometricAuthenticator.kt` | `mobile/shared/.../auth/` | Biometric prompt wrapper (KMP) |
| `SecurePinInput.kt` | `mobile/shared/.../auth/` | Scrambled PIN input (KMP) |
| `PaymentGrpcService.cs` | `src/Modules/GoldBank.Payment/Grpc/` | ProcessQRPayment endpoint |
| `QrPaymentHandler.cs` | `src/Modules/GoldBank.Payment/Handlers/` | Server-side QR payment logic |
| `QrPaymentSaga.cs` | `src/Modules/GoldBank.Payment/Sagas/` | Wolverine saga: debit -> credit -> fee |
| `QrCodeValidator.cs` | `src/Modules/GoldBank.Payment/Services/` | QR reference validation (expiry, used) |
| `FeeCalculator.cs` | `src/Modules/GoldBank.Payment/Services/` | Transaction fee computation |

### EMV QRCPS Parsing

```kotlin
// KMP shared parser
data class EmvQrData(
    val payloadFormatIndicator: String,      // "01"
    val pointOfInitiation: String,            // "11" static, "12" dynamic
    val merchantAccountInfo: MerchantAccountInfo,
    val merchantCategoryCode: String?,        // MCC
    val transactionCurrency: String,          // ISO 4217 numeric
    val transactionAmount: Long?,             // Cents, null for static
    val countryCode: String,                  // ISO 3166-1 alpha-2
    val merchantName: String,
    val merchantCity: String,
    val crc: String                           // 4-char hex
)

data class MerchantAccountInfo(
    val globallyUniqueIdentifier: String,     // "com.goldbank"
    val merchantId: String,
    val accountId: String?
)

fun parseEmvQr(data: String): Result<EmvQrData> {
    // 1. Validate CRC-16 first
    val crcInput = data.substring(0, data.length - 4) // Everything except last 4 chars
    val expectedCrc = data.substring(data.length - 4)
    val computedCrc = calculateCrc16(crcInput)
    if (computedCrc != expectedCrc) return Result.failure(InvalidCrcException())

    // 2. Parse TLV elements
    val elements = parseTlv(data)

    // 3. Extract and validate mandatory fields
    // ... field extraction with validation ...
}

private fun parseTlv(data: String): Map<String, String> {
    val elements = mutableMapOf<String, String>()
    var pos = 0
    while (pos < data.length - 4) { // Stop before CRC
        val id = data.substring(pos, pos + 2)
        val length = data.substring(pos + 2, pos + 4).toInt()
        val value = data.substring(pos + 4, pos + 4 + length)
        elements[id] = value
        pos += 4 + length
    }
    return elements
}
```

### API / gRPC Endpoints

**ProcessQRPayment** (`payment_service.proto`):

```protobuf
rpc ProcessQRPayment (ProcessQRPaymentRequest) returns (ProcessQRPaymentResponse);

message ProcessQRPaymentRequest {
  string payer_account_id = 1;
  string merchant_id = 2;           // From QR merchant account info
  string merchant_account_id = 3;   // From QR or resolved from merchant_id
  int64 amount_cents = 4;           // From QR (dynamic) or user input (static)
  string currency_code = 5;         // ISO 4217 numeric from QR
  string qr_reference = 6;          // QR reference for dynamic QR validation
  string point_of_initiation = 7;   // "static" or "dynamic"
  string cvm_method = 8;            // "biometric" or "pin"
  string pin_hash = 9;              // Hashed PIN if CVM = pin (client-side hash over TLS)
  string biometric_token = 10;      // Biometric auth token if CVM = biometric
  string tenant_id = 11;
  string idempotency_key = 12;      // Client-generated UUID for duplicate prevention
}

message ProcessQRPaymentResponse {
  string transaction_id = 1;
  string authorization_code = 2;
  string response_code = 3;         // "00" approved, "51" insufficient, etc.
  string response_message = 4;
  int64 fee_cents = 5;
  int64 total_debit_cents = 6;      // amount + fee
  int64 available_balance_cents = 7; // Post-transaction balance
  string merchant_name = 8;         // Confirmed merchant name
  int64 timestamp_unix = 9;
  bool success = 10;
  string error_message = 11;
}
```

### Wolverine Saga: QR Payment

```csharp
public class QrPaymentSaga : Saga
{
    // State
    public Guid TransactionId { get; set; }
    public Guid PayerAccountId { get; set; }
    public Guid MerchantAccountId { get; set; }
    public long AmountCents { get; set; }
    public long FeeCents { get; set; }
    public string TenantId { get; set; }

    // Step 1: Debit payer
    public DebitAccount Handle(StartQrPayment cmd)
    {
        TransactionId = cmd.TransactionId;
        PayerAccountId = cmd.PayerAccountId;
        MerchantAccountId = cmd.MerchantAccountId;
        AmountCents = cmd.AmountCents;
        FeeCents = cmd.FeeCents;
        TenantId = cmd.TenantId;

        return new DebitAccount(PayerAccountId, AmountCents + FeeCents, TransactionId, TenantId);
    }

    // Step 2: Credit merchant (after debit succeeds)
    public CreditAccount Handle(AccountDebited evt)
    {
        return new CreditAccount(MerchantAccountId, AmountCents, TransactionId, TenantId);
    }

    // Step 3: Record fee (after credit succeeds)
    public RecordTransactionFee Handle(AccountCredited evt)
    {
        return new RecordTransactionFee(TransactionId, FeeCents, TenantId);
    }

    // Step 4: Complete (after fee recorded)
    public TransactionCompleted Handle(TransactionFeeRecorded evt)
    {
        MarkCompleted();
        return new TransactionCompleted(
            TransactionId, "qr_payment", PayerAccountId, MerchantAccountId,
            AmountCents, /* ... other fields ... */ TenantId
        );
    }

    // Compensating: Reverse debit if credit fails
    public ReverseDebit Handle(CreditFailed evt)
    {
        return new ReverseDebit(PayerAccountId, AmountCents + FeeCents, TransactionId, TenantId);
    }
}
```

### Fee Calculation

```csharp
public class FeeCalculator
{
    // Fee schedule per tenant, loaded from tenant_config
    // Example: QR payment fee = 0.5% of amount, min 50 cents, max 5000 cents
    public long CalculateQrPaymentFee(long amountCents, string tenantId)
    {
        var schedule = _tenantConfig.GetFeeSchedule(tenantId, "qr_payment");
        var fee = (long)(amountCents * schedule.PercentageRate);
        fee = Math.Max(fee, schedule.MinimumFeeCents);
        fee = Math.Min(fee, schedule.MaximumFeeCents);
        return fee;
    }
}
```

### Database Changes

**qr_transactions table** (in tenant schema):

```sql
CREATE TABLE qr_transactions (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    transaction_id      UUID NOT NULL UNIQUE,
    payer_account_id    UUID NOT NULL REFERENCES accounts(id),
    merchant_account_id UUID NOT NULL REFERENCES accounts(id),
    merchant_id         VARCHAR(50) NOT NULL,
    merchant_name       VARCHAR(100),
    amount_cents        BIGINT NOT NULL,
    fee_cents           BIGINT NOT NULL DEFAULT 0,
    currency_code       VARCHAR(3) NOT NULL,
    qr_reference        VARCHAR(50),            -- Reference to the QR code used
    point_of_initiation VARCHAR(10) NOT NULL,    -- static, dynamic
    cvm_method          VARCHAR(20) NOT NULL,    -- biometric, pin
    authorization_code  VARCHAR(6),
    response_code       VARCHAR(2) NOT NULL,
    status              VARCHAR(20) NOT NULL DEFAULT 'pending',
    idempotency_key     UUID NOT NULL UNIQUE,    -- Client-generated for duplicate prevention
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    authorized_at       TIMESTAMPTZ,
    settled_at          TIMESTAMPTZ
);

CREATE INDEX idx_qr_txn_payer ON qr_transactions (payer_account_id, created_at DESC);
CREATE INDEX idx_qr_txn_merchant ON qr_transactions (merchant_account_id, created_at DESC);
CREATE INDEX idx_qr_txn_idempotency ON qr_transactions (idempotency_key);
```

### Security Considerations

- **QR Data Integrity:** The CRC-16 checksum validates that the QR data has not been corrupted. However, CRC is not a cryptographic check — it does not prevent deliberate tampering. The server validates the merchant_id and qr_reference against its database, which is the authoritative integrity check.
- **PIN Entry on Device:** Unlike NFC (where PIN is entered on the POS terminal), QR payment PIN entry happens on the consumer's phone. The PIN input uses a scrambled numeric keypad to prevent shoulder-surfing. The PIN is hashed client-side before transmission over TLS.
- **Biometric Authentication:** Uses Android BiometricPrompt API with `BIOMETRIC_STRONG` authenticators (Class 3 biometric). The biometric authentication produces a cryptographic token that the server validates.
- **Amount Tampering (Static QR):** For static QR codes, the consumer enters the amount. There is no risk of over-charging because the consumer explicitly confirms the amount. Under-payment is the merchant's risk to monitor.
- **QR Code Replacement Attack:** An attacker could place their own QR code over a merchant's printed QR code. The consumer sees the attacker's merchant name on the confirmation screen. Mitigation: educate users to verify the merchant name matches the actual business. Additional mitigation: allow merchants to register a "display name" that matches their storefront signage.
- **Replay Prevention:** The `idempotency_key` (client-generated UUID) prevents the same payment from being processed twice if the consumer accidentally presses "Pay" twice or the network causes a retry.
- **Transaction Limits:** Per-tenant transaction limits (min, max, daily cumulative) are enforced before the saga executes.

### Edge Cases

- **Camera Permission Denied:** If the user denies camera permission, the scanner cannot open. Display a message explaining why camera access is needed and a button to open app settings.
- **QR Code Not GoldBank:** If the scanned QR code is not an EMV QRCPS code or does not contain "com.goldbank" as the Globally Unique Identifier, display "Unsupported QR code." Do not attempt to process non-GoldBank QR codes.
- **Merchant Not Found:** If the merchant_id from the QR does not exist in the system, display "Unknown merchant. This QR code may be invalid."
- **Merchant Suspended:** If the merchant account is suspended, decline the payment with "This merchant cannot currently accept payments."
- **Self-Payment:** If the payer and merchant are the same account, reject with "You cannot pay yourself."
- **Amount Below Minimum:** Each tenant has a minimum transaction amount. Amounts below this are rejected with "Amount is below the minimum transaction amount of [X]."
- **Daily Limit Exceeded:** If the transaction would exceed the payer's daily cumulative limit, decline with "Daily transaction limit reached."
- **Network Loss After Authorization:** If the consumer authorizes (enters PIN) but the network drops before the gRPC call completes, the app retries with the same idempotency_key. The payment is only processed once.
- **QR Code Partially Obscured:** The QR scanner library (ML Kit) handles partial occlusion up to a point. Severely damaged QR codes fail to decode — the scanner keeps trying until the user cancels or presents a better QR.
- **Low Light Conditions:** The app should enable the camera flashlight toggle in the scanner UI for low-light environments (common in informal market settings).

---

## Dependencies

**Prerequisite Stories:**
- STORY-026: Generate EMV QR Code (QR codes must exist to be scanned)
- STORY-018: Biometric Authentication (provides biometric authorization framework)

**Blocked Stories:**
- STORY-028: QR Payment Confirmation & Notifications (triggered by this story's TransactionCompleted event)

**External Dependencies:**
- Android CameraX API for camera access
- Google ML Kit Barcode Scanning for QR code decoding
- Android BiometricPrompt API for biometric authentication
- HSM service for PIN verification (if PIN CVM selected)

---

## Definition of Done

- [ ] Code implemented and committed
- [ ] Unit tests written and passing (>=80% coverage) — EMV parsing, CRC validation, saga logic, fee calculation
- [ ] Integration tests passing — end-to-end scan-to-payment flow with test QR codes
- [ ] Camera scanner tested on physical Android device
- [ ] Both static and dynamic QR code flows tested
- [ ] Biometric and PIN authorization paths both tested
- [ ] Wolverine saga tested — including compensating actions for partial failures
- [ ] Idempotency verified — duplicate submissions produce single payment
- [ ] Expired and used dynamic QR codes correctly rejected
- [ ] Performance verified — payment processing < 2 seconds after authorization
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
