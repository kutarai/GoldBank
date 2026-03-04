# STORY-036: Agent Transaction Receipt

**Epic:** EPIC-006 Agent Cash-In/Cash-Out
**Priority:** Must Have
**Story Points:** 3
**Status:** Not Started
**Assigned To:** Unassigned
**Created:** 2026-02-24
**Sprint:** 4

---

## User Story

As a merchant agent,
I want receipts for all agent transactions,
So that I have records for reconciliation.

---

## Description

### Background
Receipts serve as the definitive record of agent banking transactions in Southern Africa. For unbanked customers, a printed receipt from a POS terminal may be the only proof of a financial transaction. For agents, receipts are essential for daily reconciliation — matching physical cash collected/disbursed against digital records. Regulatory requirements in most SADC countries mandate that both parties receive a transaction receipt.

This story implements a comprehensive receipt system that generates both physical (printed) and digital receipts for all agent transactions (cash-in and cash-out). Physical receipts are delivered to POS terminals via MQTT for thermal printing. Digital receipts are stored as notifications and visible in the transaction history of both agent and customer apps.

Functional Requirement: **FR-023**.

### Scope

**In scope:**
- Receipt generation triggered by TransactionCompleted Wolverine event
- Receipt data assembly: reference number, datetime, type, amount, fee, commission, customer phone (masked)
- Physical receipt delivery to POS terminal via MQTT for thermal printing
- Thermal printer compatible format (58mm width, plain text layout)
- Digital receipt storage in notifications table for in-app display
- Agent receipt with commission details
- Customer receipt with amount and reference
- Receipt reprint capability for agents
- Receipt lookup by reference number

**Out of scope:**
- PDF receipt generation
- Email receipt delivery
- Receipt template customization via admin panel
- Receipt archiving/retention policies
- QR code on receipts (future enhancement)

### User Flow

**Automatic Receipt (Post-Transaction):**
1. Agent completes a cash-in or cash-out transaction
2. Wolverine TransactionCompleted event fires
3. Receipt handler generates receipt data
4. If POS-initiated: receipt payload sent to POS terminal via MQTT topic `terminals/{terminal_id}/print`
5. POS terminal prints the physical receipt for the customer
6. Digital receipt stored as notification for both agent and customer
7. Agent sees receipt in transaction history
8. Customer sees receipt in transaction history and notification inbox

**Receipt Reprint:**
1. Agent opens transaction history in agent app
2. Agent selects a past transaction
3. Agent taps "Reprint Receipt"
4. System sends receipt payload to the POS terminal via MQTT
5. POS terminal reprints the receipt

---

## Acceptance Criteria

- [ ] Receipt is automatically generated for every completed cash-in transaction
- [ ] Receipt is automatically generated for every completed cash-out transaction
- [ ] Receipt contains: transaction reference number, date and time, transaction type (Cash In / Cash Out), amount, fee charged, customer phone number (masked as ****1234), agent name/code
- [ ] Agent receipt additionally contains: commission earned, new float balance
- [ ] Receipt for POS terminal is delivered via MQTT to topic `terminals/{terminal_id}/print`
- [ ] POS receipt format is compatible with 58mm thermal printers (max 32 characters per line)
- [ ] Digital receipt is stored in the notifications table for both agent and customer
- [ ] Agent can view receipt details in their transaction history
- [ ] Customer can view receipt details in their transaction history and notification inbox
- [ ] Agent can reprint a receipt for any past transaction from the transaction history
- [ ] Receipt reprint sends the same payload to the POS terminal via MQTT
- [ ] Customer receipt does not contain commission details (agent-only information)
- [ ] Receipt reference number matches the transaction reference number exactly

---

## Technical Notes

### Components

**Module:** `UniBank.Core/Modules/Agents/` and `UniBank.Notifications/`

```
Agents/
  Application/
    Handlers/
      AgentReceiptHandler.cs           # Wolverine handler for receipt generation
    Queries/
      GetReceiptQuery.cs               # Retrieve receipt by transaction reference
      ReprintReceiptCommand.cs         # Trigger reprint via MQTT
  Infrastructure/
    Services/
      AgentReceiptService.cs           # Receipt formatting and assembly
      ThermalReceiptFormatter.cs       # 58mm thermal printer layout
      MqttReceiptPublisher.cs          # MQTT publish to POS terminal

Notifications/
  Handlers/
    AgentTransactionReceiptNotificationHandler.cs
```

**SharedKernel:**
- `SharedKernel/Events/TransactionCompleted.cs` — Event trigger
- `SharedKernel/Events/CashInCompleted.cs`
- `SharedKernel/Events/CashOutCompleted.cs`

### API / gRPC Endpoints

**GetReceipt:**
```protobuf
rpc GetReceipt(GetReceiptRequest) returns (GetReceiptResponse);

message GetReceiptRequest {
  string reference_number = 1;
}

message GetReceiptResponse {
  string reference_number = 1;
  string transaction_type = 2;
  string amount = 3;
  string fee = 4;
  string currency = 5;
  string customer_phone_masked = 6;
  string agent_name = 7;
  string agent_code = 8;
  string commission = 9;            // only in agent-facing response
  string new_float_balance = 10;    // only in agent-facing response
  google.protobuf.Timestamp transaction_time = 11;
}
```

**ReprintReceipt:**
```protobuf
rpc ReprintReceipt(ReprintReceiptRequest) returns (ReprintReceiptResponse);

message ReprintReceiptRequest {
  string agent_id = 1;
  string reference_number = 2;
  string terminal_id = 3;
}

message ReprintReceiptResponse {
  bool success = 1;
  string error_code = 2;
  string error_message = 3;
}
```

### Database Changes

**Table: `transaction_receipts` (tenant schema)**
```sql
CREATE TABLE {tenant_schema}.transaction_receipts (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    transaction_id UUID NOT NULL,
    reference_number VARCHAR(50) NOT NULL,
    transaction_type VARCHAR(20) NOT NULL,
    receipt_data JSONB NOT NULL,                     -- full receipt payload
    agent_receipt_text TEXT NOT NULL,                -- pre-formatted agent thermal print text
    customer_receipt_text TEXT NOT NULL,             -- pre-formatted customer thermal print text
    printed BOOLEAN NOT NULL DEFAULT FALSE,
    printed_at TIMESTAMPTZ,
    print_count INTEGER NOT NULL DEFAULT 0,
    terminal_id VARCHAR(50),
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_receipts_reference ON {tenant_schema}.transaction_receipts(reference_number);
CREATE INDEX idx_receipts_transaction ON {tenant_schema}.transaction_receipts(transaction_id);
```

### Thermal Receipt Format (58mm / 32 chars)

**Agent Receipt:**
```
================================
         UNIBANK
      AGENT RECEIPT
================================
Date: 2026-02-24 14:30:00
Ref:  CIN-ZW-20260224-000001
Type: CASH IN
--------------------------------
Customer: ****1234
Amount:        USD    100.00
Fee:           USD      2.00
--------------------------------
Commission:    USD      1.50
Float Balance: USD  4,900.00
================================
    AGENT: John's Shop
    CODE:  AG-00142
================================
    Thank you for using
         UniBank
================================
```

**Customer Receipt:**
```
================================
         UNIBANK
    TRANSACTION RECEIPT
================================
Date: 2026-02-24 14:30:00
Ref:  CIN-ZW-20260224-000001
Type: CASH IN
--------------------------------
Amount:        USD    100.00
Fee:           USD      2.00
Total:         USD    102.00
--------------------------------
Agent: John's Shop
================================
    Thank you for using
         UniBank
================================
```

### MQTT Integration

**Topic:** `terminals/{terminal_id}/print`

**Payload Schema:**
```json
{
  "message_type": "print_receipt",
  "receipt_id": "uuid",
  "reference_number": "CIN-ZW-20260224-000001",
  "receipt_type": "agent",
  "receipt_text": "================================\n         UNIBANK\n      AGENT RECEIPT\n...",
  "timestamp": "2026-02-24T14:30:00Z",
  "is_reprint": false
}
```

**Second message for customer receipt (separate print job):**
```json
{
  "message_type": "print_receipt",
  "receipt_id": "uuid",
  "reference_number": "CIN-ZW-20260224-000001",
  "receipt_type": "customer",
  "receipt_text": "================================\n         UNIBANK\n    TRANSACTION RECEIPT\n...",
  "timestamp": "2026-02-24T14:30:00Z",
  "is_reprint": false
}
```

### Wolverine Event Handler

```csharp
public class AgentReceiptHandler
{
    public async Task Handle(CashInCompleted evt, IAgentReceiptService receiptService,
        IMqttReceiptPublisher mqttPublisher, INotificationService notificationService)
    {
        // 1. Generate receipt data
        var receiptData = await receiptService.GenerateReceiptAsync(evt);

        // 2. Format for thermal printing
        var agentText = receiptService.FormatAgentReceipt(receiptData);
        var customerText = receiptService.FormatCustomerReceipt(receiptData);

        // 3. Store receipt
        await receiptService.StoreReceiptAsync(receiptData, agentText, customerText);

        // 4. Send to POS terminal via MQTT (if terminal_id present)
        if (!string.IsNullOrEmpty(evt.TerminalId))
        {
            await mqttPublisher.PublishAsync(evt.TerminalId, agentText, "agent", receiptData.ReferenceNumber);
            await mqttPublisher.PublishAsync(evt.TerminalId, customerText, "customer", receiptData.ReferenceNumber);
        }

        // 5. Store digital receipts as notifications
        await notificationService.CreateReceiptNotificationAsync(evt.AgentId, receiptData, isAgent: true);
        await notificationService.CreateReceiptNotificationAsync(evt.CustomerAccountId, receiptData, isAgent: false);
    }
}
```

### ThermalReceiptFormatter

```csharp
public class ThermalReceiptFormatter
{
    private const int LineWidth = 32;
    private const string Separator = "================================";
    private const string ThinSeparator = "--------------------------------";

    public string FormatAgentReceipt(ReceiptData data)
    {
        var sb = new StringBuilder();
        sb.AppendLine(Separator);
        sb.AppendLine(Center("UNIBANK"));
        sb.AppendLine(Center("AGENT RECEIPT"));
        sb.AppendLine(Separator);
        sb.AppendLine($"Date: {data.TransactionTime:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"Ref:  {data.ReferenceNumber}");
        sb.AppendLine($"Type: {data.TransactionType.ToUpper()}");
        sb.AppendLine(ThinSeparator);
        sb.AppendLine($"Customer: {data.CustomerPhoneMasked}");
        sb.AppendLine(FormatAmountLine("Amount:", data.Currency, data.Amount));
        sb.AppendLine(FormatAmountLine("Fee:", data.Currency, data.Fee));
        sb.AppendLine(ThinSeparator);
        sb.AppendLine(FormatAmountLine("Commission:", data.Currency, data.Commission));
        sb.AppendLine(FormatAmountLine("Float Balance:", data.Currency, data.NewFloatBalance));
        sb.AppendLine(Separator);
        sb.AppendLine(Center($"AGENT: {data.AgentName}"));
        sb.AppendLine(Center($"CODE:  {data.AgentCode}"));
        sb.AppendLine(Separator);
        sb.AppendLine(Center("Thank you for using"));
        sb.AppendLine(Center("UniBank"));
        sb.AppendLine(Separator);
        return sb.ToString();
    }

    private string Center(string text) =>
        text.PadLeft((LineWidth + text.Length) / 2).PadRight(LineWidth);

    private string FormatAmountLine(string label, string currency, decimal amount) =>
        $"{label,-15}{currency} {amount,11:N2}";
}
```

### Security Considerations
- **Customer phone masking:** Only last 4 digits shown on receipt (****1234)
- **Commission privacy:** Customer receipt does not include commission details
- **Reprint authorization:** Only the originating agent can reprint their own receipts
- **MQTT security:** POS terminal MQTT topics are authenticated per terminal; agent cannot print to another agent's terminal
- **Receipt data integrity:** Receipt data stored as JSONB is read-only after creation; no modification API
- **Print count tracking:** Reprint count tracked for audit purposes

### Edge Cases
- **POS terminal offline when receipt is sent:** MQTT QoS level 1 (at least once); terminal receives receipt when reconnected
- **MQTT broker unavailable:** Receipt still stored digitally; physical print queued for retry
- **Reprint of very old transaction:** Receipt data stored permanently in `transaction_receipts` table; always available for reprint
- **Terminal paper jam / out of paper:** Terminal-side issue; not detectable by system; customer can request reprint
- **Receipt text exceeds thermal printer buffer:** Formatter ensures receipt fits within reasonable length (max ~30 lines)
- **Unicode characters in agent/customer names:** Thermal printer may not support full Unicode; formatter uses ASCII-safe transliteration for print, full Unicode for digital
- **Concurrent reprint requests:** Idempotent; multiple MQTT messages are harmless (printer prints each)

---

## Dependencies

**Prerequisite Stories:**
- STORY-032: Cash-In at Merchant Agent (cash-in completion triggers receipt)
- STORY-033: Cash-Out at Merchant Agent (cash-out completion triggers receipt)

**Blocked Stories:**
- None directly; receipts are a terminal deliverable in the agent banking flow

**External Dependencies:**
- MQTT broker must be configured and accessible (STORY-007)
- POS terminals must be provisioned and connected to MQTT (TerminalManager satellite service)
- Push notification service for digital receipt notifications

---

## Definition of Done

- [ ] Code implemented and committed
- [ ] Unit tests written and passing (>=80% coverage)
- [ ] Integration tests passing (receipt generation, MQTT publishing, notification creation)
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
