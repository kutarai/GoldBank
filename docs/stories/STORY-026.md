# STORY-026: Generate EMV QR Code

**Epic:** EPIC-004 EMV QR Code Payments
**Priority:** Must Have
**Story Points:** 5
**Status:** Not Started
**Assigned To:** Unassigned
**Created:** 2026-02-24
**Sprint:** 3

---

## User Story

As a **merchant or user**
I want **to generate an EMV QR code**
So that **others can pay me by scanning**

---

## Description

### Background

EMV QR Code payments are UniBank's second core payment method alongside NFC. While NFC requires a contactless POS terminal, QR codes work with any smartphone camera — making them ideal for informal merchants, street vendors, and peer-to-peer payments common in Southern Africa's informal economy. A vendor can simply print a static QR code and tape it to their stall, or generate a dynamic QR with a specific amount for each transaction.

The QR code follows the **EMV QR Code Payment Specification (QRCPS)** — the global standard maintained by EMVCo for interoperable QR payments. This ensures UniBank's QR codes could eventually interoperate with other QRCPS-compliant systems in the region.

There are two modes:
- **Static QR:** Fixed merchant identification, no amount. The payer enters the amount when scanning. Ideal for printed QR codes displayed at the point of sale.
- **Dynamic QR:** Contains a specific transaction amount. Generated per-transaction on the merchant's device. Used for exact-amount invoicing.

**Functional Requirements:** FR-012 (EMV QR Code Generation)

### Scope

**In scope:**
- EMV QRCPS compliant QR code data generation
- Static QR code generation (no amount — payer enters amount)
- Dynamic QR code generation (amount embedded)
- Payee identification encoding (merchant ID, account ID)
- QR code rendering as PNG and SVG for display and print
- `PaymentService.GenerateQRCode` gRPC endpoint
- Merchant can generate and print static QR for counter display
- CRC-16 checksum per EMV QRCPS specification
- QR code data validation before rendering
- Multi-currency support via ISO 4217 currency codes

**Out of scope:**
- QR code scanning and payment processing (STORY-027)
- Point of Initiation Method: dynamic QR with time-limited validity (future enhancement)
- QR code for bill presentment / invoicing (future enhancement)
- NFC tag encoding of QR data (future enhancement)
- Custom branding/logo embedding in QR image (future enhancement, non-standard)

### User Flow

**Static QR Generation (Merchant):**
1. Merchant opens UniBank app and navigates to "Receive Payment" / "My QR Code"
2. App calls `PaymentService.GenerateQRCode` with merchant's account info, point_of_initiation = "static"
3. Server builds EMV QRCPS data string with merchant identification and no amount
4. Server renders QR code as PNG/SVG
5. App displays the QR code on screen
6. Merchant can save to gallery or print for permanent display at their counter
7. Customers scan this same QR code for every payment — they enter the amount themselves

**Dynamic QR Generation (Merchant or User):**
1. Merchant/user opens UniBank app and navigates to "Request Payment"
2. Enters the amount to receive
3. App calls `PaymentService.GenerateQRCode` with account info, amount, point_of_initiation = "dynamic"
4. Server builds EMV QRCPS data string with merchant identification and the specific amount
5. Server renders QR code as PNG/SVG
6. App displays the QR code on screen
7. Payer scans the QR code — amount is pre-filled and read-only
8. Dynamic QR has a configurable expiry (default 15 minutes)

---

## Acceptance Criteria

- [ ] QR code is generated according to the EMV QR Code Payment Specification (QRCPS) — data structure is parseable by any QRCPS-compliant reader
- [ ] Static QR code contains payee identification but no amount — point of initiation method = "11" (static)
- [ ] Dynamic QR code contains payee identification and a specific amount — point of initiation method = "12" (dynamic)
- [ ] QR code contains all mandatory EMV data elements: Payload Format Indicator, Point of Initiation, Merchant Account Info, Transaction Currency, Country Code, Merchant Name, Merchant City, CRC
- [ ] CRC-16 checksum (per EMV QRCPS) is correctly computed and appended as the last data element (ID "63")
- [ ] QR code is renderable as PNG (for display) and SVG (for print) — minimum 300x300 pixels for PNG
- [ ] Merchant can print the static QR code from the app or save it to the device gallery
- [ ] Dynamic QR code has a configurable expiry (default 15 minutes) after which it cannot be used for payment
- [ ] `GenerateQRCode` gRPC endpoint validates all input fields and returns clear errors for invalid data

---

## Technical Notes

### Components

| Component | Location | Purpose |
|-----------|----------|---------|
| `QrCodeGenerator.cs` | `src/Modules/UniBank.Payment/Services/` | EMV QRCPS data builder |
| `EmvQrDataBuilder.cs` | `src/Modules/UniBank.Payment/Services/` | TLV encoder for EMV data elements |
| `QrImageRenderer.cs` | `src/Modules/UniBank.Payment/Services/` | PNG/SVG QR image generation |
| `Crc16Calculator.cs` | `src/Modules/UniBank.Payment/Utilities/` | CRC-16/CCITT-FALSE checksum |
| `PaymentGrpcService.cs` | `src/Modules/UniBank.Payment/Grpc/` | GenerateQRCode endpoint |
| `QrCodeScreen.kt` | `mobile/shared/.../payment/qr/` | KMP QR display screen |

### EMV QRCPS Data Structure

The EMV QR code payload is a flat TLV (Tag-Length-Value) string where each data element has a 2-digit ID, 2-digit length, and variable-length value.

```
ID  Name                           Format  Length  Required  Value
──────────────────────────────────────────────────────────────────────
00  Payload Format Indicator       N       02      M         "01"
01  Point of Initiation Method     N       02      M         "11" (static) or "12" (dynamic)
26  Merchant Account Information   ans     var     M         Sub-TLV (see below)
52  Merchant Category Code         N       04      O         MCC (e.g., "5411" grocery)
53  Transaction Currency           N       03      M         ISO 4217 numeric (e.g., "710" ZAR)
54  Transaction Amount             ans     var     C         Amount string (e.g., "500.00") — only for dynamic
58  Country Code                   ans     02      M         ISO 3166-1 alpha-2 (e.g., "ZA")
59  Merchant Name                  ans     25      M         Merchant display name
60  Merchant City                  ans     15      M         Merchant city
63  CRC                            ans     04      M         CRC-16 checksum (hex)

Sub-TLV for Merchant Account Information (ID 26):
──────────────────────────────────────────────────────────────────────
00  Globally Unique Identifier     ans     var     M         "com.unibank" (reverse domain)
01  Merchant ID                    ans     var     M         UniBank merchant identifier
02  Account ID                     ans     var     O         Account for crediting
```

**Example Static QR Payload:**
```
00020101021126430014com.unibank0115MERCH00012345670210ACC001234553037105802ZA5913ShopRite Main6012Johannesburg6304A1B2
```

**Breakdown:**
```
00 02 01           -> Payload Format Indicator: "01"
01 02 11           -> Point of Initiation: "11" (static)
26 43              -> Merchant Account Info (length 43)
  00 14 com.unibank      -> GUID
  01 15 MERCH0001234567   -> Merchant ID
  02 10 ACC0012345        -> Account ID
53 03 710          -> Currency: ZAR (710)
58 02 ZA           -> Country: South Africa
59 13 ShopRite Main -> Merchant Name (13 chars)
60 12 Johannesburg -> Merchant City (12 chars)
63 04 A1B2         -> CRC-16 checksum
```

### CRC-16 Calculation

```csharp
// EMV QRCPS uses CRC-16/CCITT-FALSE
// Polynomial: 0x1021, Init: 0xFFFF, No final XOR
// Input: entire QR data string INCLUDING "6304" (CRC tag+length) but EXCLUDING the 4-char CRC value itself

public static string CalculateCrc16(string data)
{
    // Append "6304" to data before computing CRC
    var input = data + "6304";
    ushort crc = 0xFFFF;
    foreach (byte b in Encoding.ASCII.GetBytes(input))
    {
        crc ^= (ushort)(b << 8);
        for (int i = 0; i < 8; i++)
        {
            if ((crc & 0x8000) != 0)
                crc = (ushort)((crc << 1) ^ 0x1021);
            else
                crc = (ushort)(crc << 1);
        }
    }
    return crc.ToString("X4"); // 4-char uppercase hex
}
```

### API / gRPC Endpoints

**GenerateQRCode** (`payment_service.proto`):

```protobuf
rpc GenerateQRCode (GenerateQRCodeRequest) returns (GenerateQRCodeResponse);

message GenerateQRCodeRequest {
  string account_id = 1;
  string merchant_id = 2;          // Empty for P2P users
  string merchant_name = 3;
  string merchant_city = 4;
  string country_code = 5;         // ISO 3166-1 alpha-2
  string currency_code = 6;        // ISO 4217 numeric (e.g., "710")
  string merchant_category_code = 7; // MCC (optional)
  string point_of_initiation = 8;  // "static" or "dynamic"
  int64 amount_cents = 9;          // Required if dynamic, 0 if static
  int32 expiry_minutes = 10;       // For dynamic QR, default 15
  string image_format = 11;        // "png" or "svg"
  string tenant_id = 12;
}

message GenerateQRCodeResponse {
  string qr_data_string = 1;      // Raw EMV QRCPS data string
  bytes qr_image = 2;             // Rendered QR image (PNG or SVG bytes)
  string image_format = 3;        // "png" or "svg"
  string qr_reference = 4;        // Server-side reference for dynamic QR tracking
  int64 expires_at_unix = 5;      // 0 for static (never expires)
  bool success = 6;
  string error_message = 7;
}
```

### Database Changes

**qr_codes table** (in tenant schema):

```sql
CREATE TABLE qr_codes (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    qr_reference        VARCHAR(50) NOT NULL UNIQUE,
    account_id          UUID NOT NULL REFERENCES accounts(id),
    merchant_id         VARCHAR(50),
    merchant_name       VARCHAR(100) NOT NULL,
    merchant_city       VARCHAR(50) NOT NULL,
    country_code        VARCHAR(2) NOT NULL,
    currency_code       VARCHAR(3) NOT NULL,
    point_of_initiation VARCHAR(10) NOT NULL,   -- static, dynamic
    amount_cents        BIGINT,                 -- NULL for static
    qr_data_string      TEXT NOT NULL,          -- Raw EMV payload
    status              VARCHAR(20) NOT NULL DEFAULT 'active', -- active, expired, used (for dynamic)
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    expires_at          TIMESTAMPTZ,            -- NULL for static (never expires)
    used_at             TIMESTAMPTZ             -- When a dynamic QR was scanned and paid
);

CREATE INDEX idx_qr_codes_account ON qr_codes (account_id, created_at DESC);
CREATE INDEX idx_qr_codes_reference ON qr_codes (qr_reference);
CREATE INDEX idx_qr_codes_active_dynamic ON qr_codes (status, expires_at)
    WHERE point_of_initiation = 'dynamic' AND status = 'active';
```

### QR Image Rendering

Use the `QRCoder` NuGet package (MIT licensed) for QR image generation:

```csharp
// PNG rendering
using var qrGenerator = new QRCodeGenerator();
var qrData = qrGenerator.CreateQrCode(emvDataString, QRCodeGenerator.ECCLevel.M);
using var qrCode = new PngByteQRCode(qrData);
byte[] pngBytes = qrCode.GetGraphic(10); // 10px per module

// SVG rendering
using var svgQrCode = new SvgQRCode(qrData);
string svgString = svgQrCode.GetGraphic(10);
byte[] svgBytes = Encoding.UTF8.GetBytes(svgString);
```

Error correction level M (15%) provides good readability even if the QR is slightly damaged or printed at low quality — important for printed QR codes on market stalls.

### Security Considerations

- **No Sensitive Data in QR:** The QR code contains only the merchant identifier and (optionally) the amount. It does not contain account numbers, PINs, or tokens. The QR is a payment invitation, not a credential.
- **Dynamic QR Expiry:** Dynamic QR codes expire after the configured period (default 15 minutes). Expired QR codes are rejected during the scan-and-pay flow (STORY-027). This prevents replay of old payment requests.
- **QR Reference Tracking:** Each dynamic QR is tracked by a `qr_reference` so the server can verify it was genuinely issued by UniBank and has not been tampered with. The reference is embedded in the Merchant Account Information sub-TLV.
- **Merchant Verification:** The merchant_id in the QR must correspond to an active, verified merchant in the system. Unverified or suspended merchants cannot generate QR codes.
- **Static QR Fraud Risk:** Static QR codes can be photographed and replicated. This is acceptable because the payer always sees the merchant name and must confirm before payment. Additionally, the real merchant still receives the funds, so QR cloning does not benefit the attacker (unlike card skimming).

### Edge Cases

- **Merchant Name Too Long:** EMV QRCPS limits merchant name to 25 characters. Truncate with ellipsis and store full name in database for display during scan confirmation.
- **Special Characters in Merchant Name:** EMV QRCPS allows alphanumeric and space only (ANS format). Strip or transliterate special characters. Many Southern African merchant names use standard Latin characters.
- **Amount = 0 for Dynamic QR:** Reject — dynamic QR must have a positive amount. If the merchant wants no amount, they should generate a static QR.
- **Multiple Active Static QR Codes:** A merchant can have only one active static QR per account. Generating a new static QR deactivates the previous one.
- **Dynamic QR Used Twice:** Once a dynamic QR is used for a successful payment, its status changes to `used` and it cannot be scanned again. The payer sees "This QR code has already been used."
- **Offline QR Display:** The mobile app should cache the latest static QR code locally so the merchant can display it even without network connectivity. The QR data does not change.
- **Currency Mismatch:** The currency in the QR must match the account's currency. If a multi-currency account is supported in the future, the currency is explicitly specified in the request.

---

## Dependencies

**Prerequisite Stories:**
- STORY-013: Account Management (provides merchant/account data for QR generation)

**Blocked Stories:**
- STORY-027: Scan QR Code & Process Payment (requires QR codes to exist)
- STORY-028: QR Payment Confirmation & Notifications (requires completed QR payment flow)

**External Dependencies:**
- `QRCoder` NuGet package for QR image rendering
- EMV QRCPS specification document (EMVCo public specification)

---

## Definition of Done

- [ ] Code implemented and committed
- [ ] Unit tests written and passing (>=80% coverage) — EMV data building, CRC-16 calculation, TLV encoding
- [ ] Integration tests passing — end-to-end QR generation, image rendering, database persistence
- [ ] Generated QR codes validated against EMV QRCPS specification (parseable by reference decoder)
- [ ] CRC-16 checksum verified against known test vectors
- [ ] Static and dynamic QR modes both functional
- [ ] QR image renders correctly as PNG and SVG at various sizes
- [ ] Dynamic QR expiry enforced
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
