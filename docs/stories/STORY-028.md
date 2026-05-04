# STORY-028: QR Payment Confirmation & Notifications

**Epic:** EPIC-004 EMV QR Code Payments
**Priority:** Must Have
**Story Points:** 3
**Status:** Not Started
**Assigned To:** Unassigned
**Created:** 2026-02-24
**Sprint:** 3

---

## User Story

As a **consumer**
I want **confirmation and receipt after QR payment**
So that **I know the payment was successful**

---

## Description

### Background

This story completes the QR payment experience by providing immediate confirmation and receipts after a QR code payment (STORY-027). It mirrors the notification pattern established in STORY-025 for NFC payments, reusing the same Wolverine event-driven infrastructure, notification service, and push notification pipeline.

For GoldBank's target users in the informal economy, the confirmation serves a critical trust function. When a consumer pays a street vendor via QR code, both parties need immediate digital proof of the transaction. The consumer sees a confirmation screen on their phone, and both parties receive push notifications. The transaction appears in both their histories immediately.

The receipt is also shareable — the consumer can share it with the merchant via the phone's system share intent (WhatsApp, SMS, etc.), which is important in environments where the merchant may not have the GoldBank app installed (future: merchant receives SMS receipt).

**Functional Requirements:** FR-014 (QR Payment Confirmation & Notifications)

### Scope

**In scope:**
- Confirmation screen displayed to the payer immediately after successful QR payment
- Confirmation details: merchant name, amount, fee, total, reference number, date/time
- Push notification to payer on success and failure
- Push notification to merchant/payee on successful receipt
- Wolverine event: `TransactionCompleted` (type: `qr_payment`) triggers notification handlers
- Transaction visible in both payer and payee transaction histories immediately (cache invalidation)
- Receipt shareable via system share intent (text summary and/or image)
- Failure confirmation screen with reason and suggested action
- SMS fallback notification for users without push capability

**Out of scope:**
- PDF receipt generation (future enhancement)
- Email receipt (not primary channel for target demographic)
- Merchant receipt printer integration
- Dispute initiation from confirmation screen (future feature)
- Downloadable transaction statement (separate feature)

### User Flow

**Successful Payment:**
1. QR payment saga completes successfully (STORY-027)
2. `ProcessQRPaymentResponse` returns to the mobile app with success
3. App displays confirmation screen:
   - Green checkmark animation
   - "Payment Successful"
   - Merchant name
   - Amount paid
   - Transaction fee
   - Total debited
   - New available balance
   - Transaction reference number
   - Date and time
   - "Share Receipt" button
   - "Done" button (returns to home screen)
4. Simultaneously, Wolverine publishes `TransactionCompleted` event (type: `qr_payment`)
5. Notification handler sends push to payer and payee
6. Transaction cache invalidated for both accounts
7. Transaction appears in both users' history feeds

**Failed Payment:**
1. `ProcessQRPaymentResponse` returns with failure (insufficient funds, expired QR, etc.)
2. App displays failure screen:
   - Red X animation
   - "Payment Declined"
   - Reason (human-readable)
   - Suggested action (e.g., "Top up your account and try again")
   - "Try Again" button (returns to scanner)
   - "Done" button (returns to home screen)
3. Wolverine publishes `TransactionFailed` event (type: `qr_payment`)
4. Notification handler sends push to payer only

**Share Receipt:**
1. Consumer taps "Share Receipt" on the confirmation screen
2. App generates a text receipt summary and/or an image (screenshot of confirmation screen)
3. System share sheet opens with sharing options (WhatsApp, SMS, Bluetooth, etc.)
4. Consumer selects sharing target and sends

---

## Acceptance Criteria

- [ ] Confirmation screen is displayed immediately after successful QR payment with: merchant name, amount, fee, reference number, date/time, and new balance
- [ ] Failure screen is displayed after declined QR payment with: reason and suggested action
- [ ] Push notification is sent to the payer on successful QR payment with transaction details
- [ ] Push notification is sent to the payee/merchant on successful QR payment with received amount and reference
- [ ] Push notification is sent to the payer on failed QR payment with reason
- [ ] Notifications are triggered by Wolverine `TransactionCompleted` / `TransactionFailed` events (type: `qr_payment`) — same pattern as STORY-025
- [ ] Transaction appears in both payer and payee transaction history immediately after payment (cache invalidated)
- [ ] Receipt is shareable via system share intent — produces a readable text summary
- [ ] Notification records are persisted to the notifications table for in-app history
- [ ] Both payer and payee can view the transaction in their respective transaction history screens

---

## Technical Notes

### Components

| Component | Location | Purpose |
|-----------|----------|---------|
| `QrPaymentConfirmationScreen.kt` | `mobile/shared/.../payment/qr/` | KMP confirmation/failure screen |
| `ReceiptGenerator.kt` | `mobile/shared/.../payment/` | Text/image receipt generation (KMP) |
| `ShareIntentLauncher.kt` | `mobile/android/app/.../share/` | Android system share intent |
| `TransactionCompletedHandler.cs` | `src/Modules/GoldBank.Notification/Handlers/` | Reused from STORY-025 — handles qr_payment type |
| `TransactionFailedHandler.cs` | `src/Modules/GoldBank.Notification/Handlers/` | Reused from STORY-025 — handles qr_payment type |
| `FcmNotificationSender.cs` | `src/Modules/GoldBank.Notification/Services/` | Reused from STORY-025 |
| `TransactionCacheInvalidator.cs` | `src/Modules/GoldBank.Payment/Handlers/` | Reused from STORY-025 |

### Wolverine Events

The `TransactionCompleted` and `TransactionFailed` events defined in STORY-025 are reused here. The `TransactionType` field distinguishes `qr_payment` from `nfc_payment`:

```csharp
// Published by QrPaymentSaga on completion (STORY-027)
new TransactionCompleted(
    TransactionId: txnId,
    TransactionType: "qr_payment",    // <-- distinguishes from "nfc_payment"
    PayerAccountId: payerAccountId,
    MerchantAccountId: merchantAccountId,
    AmountCents: amountCents,
    CurrencyCode: "710",
    FeeCents: feeCents,
    MerchantName: "ShopRite Woodmead",
    MerchantCity: "Johannesburg",
    AuthorizationCode: "789012",
    ResponseCode: "00",
    CvmMethod: "biometric",
    Timestamp: DateTimeOffset.UtcNow,
    TenantId: tenantId
);
```

The notification handlers from STORY-025 handle both transaction types. Template selection is based on the `TransactionType`:

```csharp
// In TransactionCompletedHandler.cs
public async Task Handle(TransactionCompleted evt)
{
    var templatePrefix = evt.TransactionType switch
    {
        "nfc_payment" => "nfc_payment",
        "qr_payment" => "qr_payment",
        _ => "generic_payment"
    };

    var payerTemplate = _templateEngine.Resolve($"{templatePrefix}_success", locale);
    var merchantTemplate = _templateEngine.Resolve($"{templatePrefix}_received", locale);

    // ... send notifications using templates ...
}
```

### Notification Payloads (QR-specific)

**Payer — Successful QR Payment:**
```json
{
  "notification": {
    "title": "QR Payment Successful",
    "body": "R250.00 paid to Mama's Kitchen"
  },
  "data": {
    "type": "qr_payment_success",
    "transaction_id": "txn_def456",
    "amount": "25000",
    "fee": "125",
    "currency": "ZAR",
    "merchant_name": "Mama's Kitchen",
    "datetime": "2026-02-24T15:45:00Z",
    "reference": "AUTH-789012",
    "balance_after": "97500"
  }
}
```

**Payee/Merchant — Successful QR Payment:**
```json
{
  "notification": {
    "title": "Payment Received",
    "body": "R250.00 received via QR - Ref: AUTH-789012"
  },
  "data": {
    "type": "qr_payment_received",
    "transaction_id": "txn_def456",
    "amount": "25000",
    "currency": "ZAR",
    "payer_reference": "****5678",
    "datetime": "2026-02-24T15:45:00Z",
    "reference": "AUTH-789012"
  }
}
```

**Payer — Failed QR Payment:**
```json
{
  "notification": {
    "title": "QR Payment Declined",
    "body": "R250.00 at Mama's Kitchen - Insufficient funds"
  },
  "data": {
    "type": "qr_payment_declined",
    "transaction_id": "txn_def456",
    "amount": "25000",
    "currency": "ZAR",
    "merchant_name": "Mama's Kitchen",
    "datetime": "2026-02-24T15:45:00Z",
    "reason": "Insufficient funds",
    "suggested_action": "Top up your account and try again"
  }
}
```

### Confirmation Screen UI

```kotlin
// KMP Compose Multiplatform confirmation screen
@Composable
fun QrPaymentConfirmationScreen(
    result: QrPaymentResult,
    onShareReceipt: () -> Unit,
    onDone: () -> Unit
) {
    Column(
        modifier = Modifier.fillMaxSize().padding(24.dp),
        horizontalAlignment = Alignment.CenterHorizontally
    ) {
        // Success/failure animation
        if (result.isSuccess) {
            SuccessAnimation() // Green checkmark with animation
        } else {
            FailureAnimation() // Red X with animation
        }

        Spacer(modifier = Modifier.height(24.dp))

        // Status text
        Text(
            text = if (result.isSuccess) "Payment Successful" else "Payment Declined",
            style = MaterialTheme.typography.headlineMedium
        )

        Spacer(modifier = Modifier.height(32.dp))

        // Transaction details
        TransactionDetailRow("Merchant", result.merchantName)
        TransactionDetailRow("Amount", formatCurrency(result.amountCents, result.currency))
        if (result.isSuccess) {
            TransactionDetailRow("Fee", formatCurrency(result.feeCents, result.currency))
            TransactionDetailRow("Total", formatCurrency(result.totalDebitCents, result.currency))
            TransactionDetailRow("Reference", result.authorizationCode)
            TransactionDetailRow("Date/Time", formatDateTime(result.timestamp))
            TransactionDetailRow("Balance", formatCurrency(result.balanceAfterCents, result.currency))
        } else {
            TransactionDetailRow("Reason", result.responseMessage)
            Text(
                text = result.suggestedAction,
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.secondary
            )
        }

        Spacer(modifier = Modifier.weight(1f))

        // Action buttons
        if (result.isSuccess) {
            OutlinedButton(onClick = onShareReceipt) {
                Icon(Icons.Default.Share, contentDescription = null)
                Spacer(modifier = Modifier.width(8.dp))
                Text("Share Receipt")
            }
        }

        Spacer(modifier = Modifier.height(12.dp))

        Button(onClick = onDone, modifier = Modifier.fillMaxWidth()) {
            Text(if (result.isSuccess) "Done" else "Try Again")
        }
    }
}
```

### Receipt Sharing

```kotlin
// Text receipt format for sharing
fun generateTextReceipt(result: QrPaymentResult): String {
    return buildString {
        appendLine("================================")
        appendLine("       GoldBank Payment Receipt")
        appendLine("================================")
        appendLine()
        appendLine("Status:    Payment Successful")
        appendLine("Merchant:  ${result.merchantName}")
        appendLine("Amount:    ${formatCurrency(result.amountCents, result.currency)}")
        appendLine("Fee:       ${formatCurrency(result.feeCents, result.currency)}")
        appendLine("Total:     ${formatCurrency(result.totalDebitCents, result.currency)}")
        appendLine("Reference: ${result.authorizationCode}")
        appendLine("Date:      ${formatDateTime(result.timestamp)}")
        appendLine()
        appendLine("================================")
        appendLine("     Thank you for using GoldBank")
        appendLine("================================")
    }
}

// Android share intent
fun shareReceipt(context: Context, receiptText: String) {
    val shareIntent = Intent(Intent.ACTION_SEND).apply {
        type = "text/plain"
        putExtra(Intent.EXTRA_SUBJECT, "GoldBank Payment Receipt")
        putExtra(Intent.EXTRA_TEXT, receiptText)
    }
    context.startActivity(Intent.createChooser(shareIntent, "Share Receipt"))
}
```

### API / gRPC Endpoints

No new server-side endpoints. This story consumes:
- `ProcessQRPaymentResponse` from STORY-027 (for confirmation screen data)
- Wolverine events from STORY-027 saga (for notifications)
- Notification infrastructure from STORY-025 (reused)

**Client-side only:** The confirmation screen and share functionality are purely mobile-side, driven by the response from `ProcessQRPayment`.

### Database Changes

No new tables. Reuses:
- `notifications` table (from STORY-025) for persisting notification records
- `notification_preferences` table (from STORY-025) for user preferences
- `qr_transactions` table (from STORY-027) for transaction history display

The notification handler creates records with type `qr_payment_success`, `qr_payment_received`, or `qr_payment_declined` in the notifications table.

### Security Considerations

- **Balance Display:** The post-transaction balance is shown on the confirmation screen. This screen should be dismissable and not persist if the app is backgrounded for more than 30 seconds (auto-navigate to home with balance hidden behind authentication).
- **Share Receipt Content:** The shared receipt text includes only the transaction reference, merchant name, amount, and date. It does not include account numbers, balance, or any sensitive PII. The reference number allows the merchant to verify the payment on their side.
- **Push Notification Privacy:** Same policy as STORY-025 — no sensitive data (full account numbers, balance) in the notification title/body (visible on lock screen). Detailed data is in the notification data payload, accessible only within the app after authentication.
- **Screenshot Protection:** On Android, the confirmation screen should set `FLAG_SECURE` to prevent screenshots that might capture the balance. However, the share receipt function deliberately allows sharing a text-only receipt (no balance).

### Edge Cases

- **App Killed Before Confirmation:** If the app is killed after the payment processes but before the confirmation screen displays, the consumer still receives the push notification. When they reopen the app, the transaction appears in their history. No payment is lost.
- **Network Loss After Payment Success:** The payment already completed server-side. The mobile app receives the response from the gRPC call before the network drops. If the network drops during the gRPC response, the app should retry with the idempotency key to get the result (the server returns the existing result, not a new payment).
- **Notification Delivery Delay:** Push notifications may be delayed (FCM is best-effort). The confirmation screen on the payer's phone is the immediate feedback. The merchant's push notification may arrive within 1-5 seconds. If the merchant does not receive notification quickly, they can check their transaction history in the app.
- **Share Intent Failure:** If the system share intent fails (no sharing apps installed — extremely unlikely on Android), log the error and inform the user. Offer to copy the receipt text to clipboard instead.
- **Multiple Rapid Payments:** If the consumer makes several QR payments in quick succession, each gets its own confirmation screen and notification. The notification handler uses transaction_id idempotency to prevent duplicate notifications.
- **Merchant Without GoldBank App:** If the payee is a merchant account but the merchant device does not have the app installed or FCM registered, the notification fails silently. The merchant can check their balance/history when they next open the app. SMS fallback (per STORY-025 pattern) applies if configured.
- **Locale and Currency Formatting:** The confirmation screen formats currency using the device locale (e.g., "R 250.00" for ZAR, "ZMW 250.00" for ZMW). The notification templates use the tenant's default locale. Mismatched locales between payer and payee are handled by sending each notification in the recipient's preferred locale.

---

## Dependencies

**Prerequisite Stories:**
- STORY-027: Scan QR Code & Process Payment (provides the ProcessQRPaymentResponse and publishes TransactionCompleted event)
- STORY-025: NFC Payment Notifications & Receipt (provides the notification infrastructure, handlers, and templates — reused for QR payments)

**Blocked Stories:**
- None (this is a leaf story in the Sprint 3 dependency graph)

**External Dependencies:**
- Firebase Cloud Messaging (FCM) — already set up for STORY-025
- SMS gateway (already set up for STORY-025 fallback)
- Android Share Intent API

---

## Definition of Done

- [ ] Code implemented and committed
- [ ] Unit tests written and passing (>=80% coverage) — confirmation screen rendering, receipt generation, notification template selection for qr_payment type
- [ ] Integration tests passing — end-to-end QR payment flow produces correct confirmation and notifications
- [ ] Confirmation screen displays correctly on various Android screen sizes
- [ ] Failure screen displays with correct reason and suggested action
- [ ] Push notifications delivered to both payer and payee (tested on physical device)
- [ ] Share receipt functionality tested via system share intent
- [ ] Transaction appears in both payer and payee history immediately
- [ ] Notification templates correct for qr_payment type in all supported locales
- [ ] Idempotency verified — duplicate events do not produce duplicate notifications
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
