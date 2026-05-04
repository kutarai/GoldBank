# STORY-025: NFC Payment Notifications & Receipt

**Epic:** EPIC-003 NFC Contactless Payments
**Priority:** Must Have
**Story Points:** 3
**Status:** Not Started
**Assigned To:** Unassigned
**Created:** 2026-02-24
**Sprint:** 3

---

## User Story

As a **consumer**
I want **a notification when my NFC payment completes**
So that **I have confirmation and a receipt**

---

## Description

### Background

For GoldBank's target users — many of whom are new to digital payments — immediate transaction confirmation is essential for building trust and confidence. When a consumer taps their phone at a POS terminal, they need to know within seconds whether the payment succeeded or failed, along with the key transaction details.

This story implements the notification and receipt pipeline that fires after every NFC payment processed by STORY-023. It leverages Wolverine's event-driven architecture: the `TransactionCompleted` event published at the end of the NFC payment saga triggers a notification handler that pushes confirmations to both the payer and the merchant via Firebase Cloud Messaging (FCM). The transaction also becomes immediately visible in the user's transaction history through cache invalidation.

For the unbanked demographic, SMS fallback is also important — not all users will have reliable internet connectivity at the moment of the transaction. However, push notification is the primary channel, with SMS as a secondary channel configurable per tenant.

**Functional Requirements:** FR-011 (NFC Payment Notifications)

### Scope

**In scope:**
- Wolverine event handler for `TransactionCompleted` (type: `nfc_payment`)
- Push notification to payer via FCM with transaction details
- Push notification to merchant via FCM with transaction details
- Notification payload: amount, merchant name, date/time, status, transaction reference
- Transaction record immediately visible in `GetTransactions` gRPC stream (cache invalidation)
- Failure notifications (declined transactions) with reason
- Notification preferences per user (opt-out granularity)
- Notification persistence for in-app notification center
- SMS fallback for users without push notification capability

**Out of scope:**
- Email notifications (not primary channel for target demographic)
- Detailed merchant analytics/reporting (separate epic)
- Dispute initiation from notification (future feature)
- WhatsApp or USSD notification channels (future enhancement)
- Rich notification with inline actions (future enhancement)

### User Flow

**Successful Payment:**
1. NFC payment saga completes successfully (STORY-023)
2. Wolverine publishes `TransactionCompleted` event with type `nfc_payment`
3. Notification handler receives the event
4. Handler resolves payer and merchant notification preferences and FCM tokens
5. Handler constructs notification payloads for payer and merchant (different content for each)
6. Push notifications sent via FCM
7. Notification record persisted to `notifications` table for in-app history
8. Transaction history cache invalidated for both payer and merchant accounts
9. Consumer opens app and sees: push notification banner, updated balance, transaction in history

**Failed Payment:**
1. NFC payment authorization fails (insufficient funds, invalid PIN, token revoked, etc.)
2. Wolverine publishes `TransactionFailed` event with type `nfc_payment` and reason code
3. Notification handler sends a failure notification to the payer only (merchant sees decline on terminal)
4. Failure notification includes: amount attempted, merchant name, reason (human-readable), and suggested action

---

## Acceptance Criteria

- [ ] Push notification is sent to the payer on successful NFC payment completion, containing: amount, merchant name, date/time, status ("Approved"), and transaction reference
- [ ] Push notification is sent to the merchant on successful NFC payment completion, containing: amount, payer reference (masked), date/time, status ("Received"), and transaction reference
- [ ] Push notification is sent to the payer on failed NFC payment, containing: amount attempted, merchant name, status ("Declined"), reason, and suggested action
- [ ] Notification is triggered by the Wolverine `TransactionCompleted` / `TransactionFailed` event — not by direct coupling to the payment handler
- [ ] Transaction appears in the payer's transaction history immediately after payment (cache invalidated)
- [ ] Transaction appears in the merchant's transaction history immediately after payment (cache invalidated)
- [ ] Notification payload is structured for display in the mobile app notification center
- [ ] Notification records are persisted to the database for in-app notification history
- [ ] Users can opt out of push notifications for NFC payments (preference respected)
- [ ] SMS fallback is sent if the user does not have a registered FCM token and SMS notifications are enabled for the tenant

---

## Technical Notes

### Components

| Component | Location | Purpose |
|-----------|----------|---------|
| `TransactionCompletedHandler.cs` | `src/Modules/GoldBank.Notification/Handlers/` | Wolverine handler for TransactionCompleted |
| `TransactionFailedHandler.cs` | `src/Modules/GoldBank.Notification/Handlers/` | Wolverine handler for TransactionFailed |
| `FcmNotificationSender.cs` | `src/Modules/GoldBank.Notification/Services/` | Firebase Cloud Messaging integration |
| `SmsNotificationSender.cs` | `src/Modules/GoldBank.Notification/Services/` | SMS gateway integration (fallback) |
| `NotificationTemplateEngine.cs` | `src/Modules/GoldBank.Notification/Services/` | Template rendering for notification content |
| `NotificationPreferenceService.cs` | `src/Modules/GoldBank.Notification/Services/` | User notification preference lookup |
| `TransactionCacheInvalidator.cs` | `src/Modules/GoldBank.Payment/Handlers/` | Redis cache invalidation on transaction events |

### Wolverine Events

```csharp
// Published by NfcPaymentSaga on completion
public record TransactionCompleted(
    Guid TransactionId,
    string TransactionType,        // "nfc_payment"
    Guid PayerAccountId,
    Guid MerchantAccountId,
    long AmountCents,
    string CurrencyCode,
    long FeeCents,
    string MerchantName,
    string MerchantCity,
    string AuthorizationCode,
    string ResponseCode,           // "00" for approved
    string CvmMethod,
    DateTimeOffset Timestamp,
    string TenantId
);

// Published by NfcPaymentHandler on failure
public record TransactionFailed(
    Guid TransactionId,
    string TransactionType,        // "nfc_payment"
    Guid PayerAccountId,
    long AmountCents,
    string CurrencyCode,
    string MerchantName,
    string ResponseCode,           // "51" insufficient, "55" wrong PIN, etc.
    string ResponseMessage,        // Human-readable reason
    string SuggestedAction,        // "Check balance", "Re-enter PIN", etc.
    DateTimeOffset Timestamp,
    string TenantId
);
```

### Notification Payloads

**Payer — Successful Payment:**
```json
{
  "notification": {
    "title": "Payment Successful",
    "body": "R500.00 paid to ShopRite Woodmead"
  },
  "data": {
    "type": "nfc_payment_success",
    "transaction_id": "txn_abc123",
    "amount": "50000",
    "currency": "ZAR",
    "merchant_name": "ShopRite Woodmead",
    "datetime": "2026-02-24T14:30:00Z",
    "reference": "AUTH-123456",
    "balance_after": "150000"
  }
}
```

**Merchant — Successful Payment:**
```json
{
  "notification": {
    "title": "Payment Received",
    "body": "R500.00 received - Ref: AUTH-123456"
  },
  "data": {
    "type": "nfc_payment_received",
    "transaction_id": "txn_abc123",
    "amount": "50000",
    "currency": "ZAR",
    "payer_reference": "****4321",
    "datetime": "2026-02-24T14:30:00Z",
    "reference": "AUTH-123456"
  }
}
```

**Payer — Failed Payment:**
```json
{
  "notification": {
    "title": "Payment Declined",
    "body": "R500.00 at ShopRite Woodmead - Insufficient funds"
  },
  "data": {
    "type": "nfc_payment_declined",
    "transaction_id": "txn_abc123",
    "amount": "50000",
    "currency": "ZAR",
    "merchant_name": "ShopRite Woodmead",
    "datetime": "2026-02-24T14:30:00Z",
    "reason": "Insufficient funds",
    "suggested_action": "Top up your account and try again"
  }
}
```

### Notification Templates (Multi-language Support)

```csharp
// Templates stored per tenant and locale
// Example keys:
//   nfc_payment_success_title_{locale}
//   nfc_payment_success_body_{locale}
//   nfc_payment_declined_title_{locale}
//   nfc_payment_declined_body_{locale}

// English (en-ZA):
// "Payment Successful" / "{amount} paid to {merchant_name}"
// "Payment Declined"   / "{amount} at {merchant_name} - {reason}"

// Zulu (zu-ZA):
// "Ukukhokha Kuphumelele" / "{amount} kukhokhelwe ku-{merchant_name}"

// Shona (sn-ZW):
// "Muripo Wabudirira" / "{amount} yakabhadharwa ku{merchant_name}"
```

### API / gRPC Endpoints

No new external endpoints. Internal event-driven flow only.

**Cache invalidation** triggers `GetTransactions` stream to pick up new data:

```csharp
// In TransactionCacheInvalidator.cs
public async Task Handle(TransactionCompleted evt)
{
    // Invalidate payer's transaction cache
    await _redis.KeyDeleteAsync($"txn_cache:{evt.TenantId}:{evt.PayerAccountId}");

    // Invalidate merchant's transaction cache
    await _redis.KeyDeleteAsync($"txn_cache:{evt.TenantId}:{evt.MerchantAccountId}");

    // Invalidate balance cache for both
    await _redis.KeyDeleteAsync($"balance:{evt.TenantId}:{evt.PayerAccountId}");
    await _redis.KeyDeleteAsync($"balance:{evt.TenantId}:{evt.MerchantAccountId}");
}
```

### Database Changes

**notifications table** (in tenant schema):

```sql
CREATE TABLE notifications (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    account_id      UUID NOT NULL REFERENCES accounts(id),
    type            VARCHAR(50) NOT NULL,     -- nfc_payment_success, nfc_payment_declined, etc.
    title           VARCHAR(200) NOT NULL,
    body            TEXT NOT NULL,
    data_payload    JSONB,                    -- Structured data for app rendering
    channel         VARCHAR(20) NOT NULL,     -- push, sms
    status          VARCHAR(20) NOT NULL DEFAULT 'sent', -- sent, delivered, failed, read
    sent_at         TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    delivered_at    TIMESTAMPTZ,
    read_at         TIMESTAMPTZ,
    fcm_message_id  VARCHAR(100),
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_notifications_account ON notifications (account_id, created_at DESC);
CREATE INDEX idx_notifications_unread ON notifications (account_id, read_at) WHERE read_at IS NULL;
CREATE INDEX idx_notifications_type ON notifications (type, created_at DESC);
```

**notification_preferences table** (in tenant schema):

```sql
CREATE TABLE notification_preferences (
    account_id          UUID PRIMARY KEY REFERENCES accounts(id),
    push_enabled        BOOLEAN NOT NULL DEFAULT true,
    sms_enabled         BOOLEAN NOT NULL DEFAULT false,
    nfc_payment_notify  BOOLEAN NOT NULL DEFAULT true,
    qr_payment_notify   BOOLEAN NOT NULL DEFAULT true,
    transfer_notify     BOOLEAN NOT NULL DEFAULT true,
    marketing_notify    BOOLEAN NOT NULL DEFAULT false,
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
```

### Security Considerations

- **No Sensitive Data in Push:** Push notifications travel through Google's FCM infrastructure. Never include full account numbers, PINs, or balances in the notification `title` or `body` (visible on lock screen). Use masked references only. The full details are in the `data` payload, which the app renders after authentication.
- **Balance in Data Payload:** The `balance_after` field is included in the data payload (not the visible notification) so the app can update the balance display without an extra API call. This is acceptable because the data payload is only accessible to the app, not shown on the lock screen.
- **Notification Delivery Tracking:** Track delivery via FCM delivery receipts. If a notification fails, retry up to 3 times. Do not retry SMS (cost concerns).
- **User Privacy:** Merchant notifications include a masked payer reference (last 4 digits), never the full name or account number of the payer.

### Edge Cases

- **User Has No FCM Token:** If the user has never opened the app or has disabled notifications at the OS level, no FCM token is available. Fall back to SMS if the tenant has SMS enabled. If neither channel is available, still persist the notification for in-app display when the user next opens the app.
- **FCM Delivery Failure:** FCM may fail (invalid token, unregistered device). Mark notification as `failed`, log the FCM error. On "not registered" error, remove the stale FCM token from the device registry.
- **High Volume:** During peak hours, notification handlers may queue up. Wolverine's message processing handles backpressure. Notifications are non-blocking to the payment flow — the saga completes before notifications are sent.
- **Duplicate Events:** Wolverine may deliver the same event twice (at-least-once). The notification handler uses the `TransactionId` as an idempotency key — check if a notification for this transaction already exists before sending.
- **Merchant Offline:** If the merchant's device is offline, the push notification is queued by FCM for up to 4 weeks. The transaction is still recorded and visible when the merchant next opens the app.
- **Multi-Language:** Notification templates are selected based on the user's locale preference. Default to English if locale is not configured.

---

## Dependencies

**Prerequisite Stories:**
- STORY-023: NFC Contactless Payment at POS (publishes the TransactionCompleted event)

**Blocked Stories:**
- None directly (STORY-028 will reuse the same notification pattern for QR payments)

**External Dependencies:**
- Firebase Cloud Messaging (FCM) — requires FCM project setup and server key
- SMS gateway provider (e.g., Twilio, Africa's Talking) for SMS fallback
- Redis for cache invalidation

---

## Definition of Done

- [ ] Code implemented and committed
- [ ] Unit tests written and passing (>=80% coverage) — handler logic, template rendering, idempotency
- [ ] Integration tests passing — event published, notification sent (FCM mock), cache invalidated, notification persisted
- [ ] Push notification tested on physical Android device
- [ ] Both payer and merchant receive correct notifications
- [ ] Failure notifications tested (declined transaction)
- [ ] SMS fallback tested when FCM token absent
- [ ] Idempotency verified (duplicate events do not produce duplicate notifications)
- [ ] Notification preferences respected (opt-out works)
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
