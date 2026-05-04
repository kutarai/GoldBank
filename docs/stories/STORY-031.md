# STORY-031: Transfer Confirmation & Notifications

**Epic:** EPIC-005 P2P Transfers
**Priority:** Must Have
**Story Points:** 3
**Status:** Not Started
**Assigned To:** Unassigned
**Created:** 2026-02-24
**Sprint:** 4

---

## User Story

As a consumer,
I want confirmation before transfer and notifications after,
So that I can verify details and have a receipt.

---

## Description

### Background
Trust is paramount for unbanked users adopting digital financial services for the first time. Many users in Southern Africa have been victims of fraud or errors with informal money transfer channels. GoldBank must provide a clear two-phase transfer experience: first a preview/confirmation step where the user can verify all details before committing, then comprehensive notifications after the transfer completes so both parties have receipts and records.

This story formalizes the confirmation flow and notification pipeline for all P2P transfers (domestic and cross-border). The confirmation phase is a distinct API call that returns a preview without executing the transfer. The notification phase is event-driven, triggered by the TransferCompleted Wolverine event, and delivers push notifications to both sender and receiver.

Functional Requirements: **FR-017** (Confirmation), **FR-018** (Notifications).

### Scope

**In scope:**
- Two-phase transfer flow: Preview (read-only) and Execute (commits the transfer)
- Preview API returning: recipient name, phone (masked), amount, fees, total debit, exchange rate (if cross-border)
- Cancel capability before execution (no side effects)
- Reference number generation and display on success
- Push notification to sender with reference number and confirmation
- Push notification to receiver with sender name and amount received
- Transaction history entries for both parties
- SMS fallback notification for users without push capability

**Out of scope:**
- Email notifications (future enhancement)
- In-app messaging / chat
- Notification preferences/settings (future sprint)
- Notification templates management via admin panel

### User Flow
1. Consumer enters transfer details (recipient, amount) in the app
2. Consumer taps "Continue" — app calls PreviewTransfer API
3. System returns preview: recipient masked name, masked phone, amount, fees, total debit
4. App displays confirmation screen with all preview details
5. Consumer reviews details carefully
6. Option A: Consumer taps "Cancel" — no API call, no side effects, returns to previous screen
7. Option B: Consumer taps "Confirm" — app calls ConfirmTransfer API
8. System executes the transfer saga (STORY-029 / STORY-030)
9. On success, system returns reference number to the app
10. App displays success screen with reference number
11. Wolverine event TransferCompleted triggers notification pipeline
12. Sender receives push notification: "Transfer of {amount} to {recipient} successful. Ref: {reference}"
13. Receiver receives push notification: "{sender} sent you {amount}. Ref: {reference}"
14. Both parties see the transaction in their history with matching reference numbers

---

## Acceptance Criteria

- [ ] PreviewTransfer API returns recipient masked name, masked phone, transfer amount, fee amount, total debit, and currency without executing any transfer
- [ ] PreviewTransfer is idempotent and has no side effects (no balance changes, no records created)
- [ ] For cross-border transfers, preview also includes exchange rate, receiver amount, and receiver currency
- [ ] Consumer can cancel from the confirmation screen; no funds are moved and no transaction records are created
- [ ] ConfirmTransfer API requires the same parameters as the preview and executes the transfer saga
- [ ] Upon successful transfer, a unique reference number is returned in format TRF-{tenant}-{YYYYMMDD}-{seq} (domestic) or XBR-{tenant}-{YYYYMMDD}-{seq} (cross-border)
- [ ] Sender receives a push notification within 5 seconds of transfer completion containing: amount, recipient name, and reference number
- [ ] Receiver receives a push notification within 5 seconds of transfer completion containing: sender name, amount received, and reference number
- [ ] If push notification delivery fails, system falls back to SMS notification
- [ ] Both sender and receiver have corresponding entries in their transaction history
- [ ] Transaction history entries include: reference number, counterparty name (masked), amount, fee (sender only), timestamp, and status
- [ ] Notifications are recorded in the notifications table for in-app notification history

---

## Technical Notes

### Components

**Module:** `GoldBank.Core/Modules/Transfers/` (confirmation flow) and `GoldBank.Notifications/` (notification delivery)

```
Transfers/
  Application/
    Queries/
      PreviewP2PTransferQuery.cs       # Returns preview without side effects
      PreviewCrossBorderTransferQuery.cs
    Commands/
      ConfirmP2PTransferCommand.cs     # Triggers the saga
      ConfirmCrossBorderTransferCommand.cs

Notifications/
  Handlers/
    TransferCompletedNotificationHandler.cs  # Wolverine handler for TransferCompleted event
  Services/
    PushNotificationService.cs               # FCM/APNs integration
    SmsNotificationService.cs                # SMS gateway integration
    NotificationTemplateService.cs           # Template rendering
  Persistence/
    NotificationRepository.cs
```

**SharedKernel:**
- `SharedKernel/Events/TransferCompleted.cs` — Event consumed by notification service
- `SharedKernel/Notifications/NotificationPayload.cs` — Notification data structure

### API / gRPC Endpoints

**PreviewTransfer:**
```protobuf
rpc PreviewTransfer(PreviewTransferRequest) returns (PreviewTransferResponse);

message PreviewTransferRequest {
  string sender_account_id = 1;
  string recipient_identifier = 2;   // phone number
  string amount = 3;
  string currency = 4;
  TransferType transfer_type = 5;    // DOMESTIC or CROSS_BORDER
  string destination_country_code = 6; // only for cross-border
}

message PreviewTransferResponse {
  string recipient_masked_name = 1;
  string recipient_masked_phone = 2;
  string transfer_amount = 3;
  string fee_amount = 4;
  string total_debit = 5;
  string sender_currency = 6;
  // Cross-border specific
  string exchange_rate = 7;
  string receiver_amount = 8;
  string receiver_currency = 9;
  google.protobuf.Timestamp rate_valid_until = 10;
}
```

**ConfirmTransfer:**
```protobuf
rpc ConfirmTransfer(ConfirmTransferRequest) returns (ConfirmTransferResponse);

message ConfirmTransferRequest {
  string sender_account_id = 1;
  string recipient_identifier = 2;
  string amount = 3;
  string currency = 4;
  TransferType transfer_type = 5;
  string destination_country_code = 6;
  TransferPurpose purpose = 7;        // cross-border only
  string idempotency_key = 8;
}

message ConfirmTransferResponse {
  bool success = 1;
  string reference_number = 2;
  string error_code = 3;
  string error_message = 4;
  google.protobuf.Timestamp completed_at = 5;
}
```

### Database Changes

**Table: `notifications` (tenant schema)**
```sql
CREATE TABLE {tenant_schema}.notifications (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    account_id UUID NOT NULL REFERENCES {tenant_schema}.accounts(id),
    notification_type VARCHAR(30) NOT NULL,     -- 'transfer_sent', 'transfer_received'
    channel VARCHAR(20) NOT NULL,               -- 'push', 'sms'
    title VARCHAR(200) NOT NULL,
    body TEXT NOT NULL,
    reference_id UUID,                          -- links to transfer_id
    reference_type VARCHAR(30),                 -- 'transfer'
    delivered BOOLEAN NOT NULL DEFAULT FALSE,
    delivered_at TIMESTAMPTZ,
    failed_reason TEXT,
    read BOOLEAN NOT NULL DEFAULT FALSE,
    read_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_notifications_account ON {tenant_schema}.notifications(account_id);
CREATE INDEX idx_notifications_unread ON {tenant_schema}.notifications(account_id, read) WHERE read = FALSE;
CREATE INDEX idx_notifications_reference ON {tenant_schema}.notifications(reference_id, reference_type);
```

### Notification Templates

**Sender (domestic):**
```
Title: "Transfer Successful"
Body: "You sent {currency} {amount} to {recipient_name}. Fee: {currency} {fee}. Ref: {reference_number}"
```

**Receiver (domestic):**
```
Title: "Money Received"
Body: "{sender_name} sent you {currency} {amount}. Ref: {reference_number}"
```

**Sender (cross-border):**
```
Title: "International Transfer Successful"
Body: "You sent {sender_currency} {sender_amount} to {recipient_name} ({dest_country}). They receive {receiver_currency} {receiver_amount}. Ref: {reference_number}"
```

**Receiver (cross-border):**
```
Title: "Money Received"
Body: "{sender_name} sent you {receiver_currency} {receiver_amount}. Ref: {reference_number}"
```

### Reference Number Generation

```csharp
public class TransferReferenceGenerator
{
    // Format: TRF-{tenant_code}-{YYYYMMDD}-{6-digit-seq}
    // Example: TRF-ZW-20260224-000042
    // Cross-border: XBR-ZW-20260224-000001

    public async Task<string> GenerateAsync(string tenantCode, string prefix = "TRF")
    {
        var date = DateTime.UtcNow.ToString("yyyyMMdd");
        var sequence = await _sequenceService.GetNextAsync($"transfer_ref_{tenantCode}_{date}");
        return $"{prefix}-{tenantCode}-{date}-{sequence:D6}";
    }
}
```

### Wolverine Event Handler

```csharp
public class TransferCompletedNotificationHandler
{
    public async Task Handle(TransferCompleted evt, INotificationService notificationService)
    {
        // Send to sender
        await notificationService.SendAsync(new NotificationPayload
        {
            AccountId = evt.SenderAccountId,
            Type = "transfer_sent",
            Title = "Transfer Successful",
            Body = FormatSenderMessage(evt),
            ReferenceId = evt.TransferId,
            ReferenceType = "transfer"
        });

        // Send to receiver
        await notificationService.SendAsync(new NotificationPayload
        {
            AccountId = evt.ReceiverAccountId,
            Type = "transfer_received",
            Title = "Money Received",
            Body = FormatReceiverMessage(evt),
            ReferenceId = evt.TransferId,
            ReferenceType = "transfer"
        });
    }
}
```

### Security Considerations
- **Preview leaks no sensitive data:** Recipient name is always masked (first name + last initial)
- **Confirmation requires fresh authentication:** Token must be valid at confirmation time
- **Notification content:** SMS notifications use minimal detail (no account numbers); push notifications are slightly more detailed but still masked
- **Notification storage:** Notifications are stored per-tenant for data isolation
- **SMS gateway:** Must use tenant-configured SMS provider; sensitive data never in URL parameters

### Edge Cases
- **Preview and confirm with stale data:** If recipient status changes between preview and confirm, confirm re-validates and fails gracefully
- **Push notification service unavailable:** Queue notification for retry (max 3 attempts with exponential backoff), then fall back to SMS
- **SMS gateway unavailable:** Queue for retry; notification marked as "pending" in database
- **Both push and SMS fail:** Notification stored in database for in-app display; marked as delivery failed
- **User has no push token and no phone (unlikely):** Store notification in-app only
- **High notification volume:** Use Wolverine message batching for notification delivery
- **Duplicate notifications:** Idempotency based on transfer_id + notification_type + account_id

---

## Dependencies

**Prerequisite Stories:**
- STORY-029: P2P Domestic Transfer (transfer saga infrastructure)

**Blocked Stories:**
- STORY-036: Agent Transaction Receipt (reuses notification infrastructure)

**External Dependencies:**
- Push notification service (Firebase Cloud Messaging for Android, APNs for iOS)
- SMS gateway provider (tenant-configurable)

---

## Definition of Done

- [ ] Code implemented and committed
- [ ] Unit tests written and passing (>=80% coverage)
- [ ] Integration tests passing
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
