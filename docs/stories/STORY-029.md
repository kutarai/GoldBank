# STORY-029: P2P Domestic Transfer

**Epic:** EPIC-005 P2P Transfers
**Priority:** Must Have
**Story Points:** 5
**Status:** Not Started
**Assigned To:** Unassigned
**Created:** 2026-02-24
**Sprint:** 4

---

## User Story

As a consumer,
I want to send money to another UniBank user,
So that I can pay people remotely.

---

## Description

### Background
Peer-to-peer (P2P) domestic transfers are a foundational capability for any mobile money platform targeting the unbanked in Southern Africa. Many users rely on remittances within the same country to support family, pay for services, or settle informal debts. Unlike traditional bank transfers that can take hours or days, UniBank's P2P transfers must be real-time to match user expectations set by existing mobile money services (M-Pesa, EcoCash, etc.).

This story implements intra-tenant P2P transfers where both sender and receiver hold accounts within the same UniBank tenant deployment. Because both accounts reside in the same tenant schema, the transaction can be completed atomically using a Wolverine saga that orchestrates debit, credit, and fee recording steps with full compensation support on failure.

Functional Requirement: **FR-015**.

### Scope

**In scope:**
- Recipient lookup by phone number or selection from device contacts
- Validation that recipient account exists and is active
- Amount entry with fee calculation and confirmation screen
- Real-time transfer execution via Wolverine saga (debit sender, credit receiver, record fee)
- Saga compensation (automatic debit reversal if credit fails)
- Transaction fee calculation from tenant configuration
- Transaction history entry for both sender and receiver
- Transfer amount limits per tenant configuration (daily/per-transaction)

**Out of scope:**
- Cross-border transfers to users in different country deployments (STORY-030)
- Transfers to non-UniBank accounts or external bank accounts
- Scheduled/recurring transfers (future sprint)
- Bulk/batch transfers
- Transfer to unregistered phone numbers (future: invite flow)

### User Flow
1. Consumer opens the "Send Money" screen in the mobile app
2. Consumer enters recipient phone number manually or selects from device contacts
3. System looks up recipient by phone number and displays recipient name (masked for privacy, e.g., "John M.")
4. Consumer enters the transfer amount
5. System calculates fees and displays confirmation screen: recipient name, phone (masked), amount, fee, total debit
6. Consumer reviews and confirms the transfer
7. System executes the Wolverine saga: DebitSenderAccount -> CreditReceiverAccount -> RecordTransactionFee -> PublishTransactionCompleted
8. Consumer sees success screen with reference number (TRF-{tenant}-{YYYYMMDD}-{seq})
9. Both sender and receiver see the transaction in their history
10. Receiver receives a push notification with sender name and amount

---

## Acceptance Criteria

- [ ] Consumer can enter a recipient phone number manually to initiate a transfer
- [ ] Consumer can select a recipient from device contacts (phone number matched)
- [ ] System validates that the recipient phone number belongs to an active UniBank account in the same tenant
- [ ] System displays an appropriate error if recipient is not found, inactive, or in a different tenant
- [ ] Consumer can enter a transfer amount within configured limits (per-transaction and daily)
- [ ] System displays clear error messages when amount exceeds per-transaction limit or daily cumulative limit
- [ ] Confirmation screen displays: recipient masked name, recipient masked phone, transfer amount, fee amount, and total debit amount
- [ ] Consumer can cancel the transfer from the confirmation screen without any funds being moved
- [ ] Upon confirmation, the Wolverine saga executes: DebitSenderAccount -> CreditReceiverAccount -> RecordTransactionFee -> PublishTransactionCompleted
- [ ] Transfer is real-time: sender balance debited and receiver balance credited within the same saga execution
- [ ] If credit to receiver fails, the saga compensation automatically reverses the sender debit
- [ ] Fee is calculated based on tenant-configured fee schedule (percentage, flat, or tiered)
- [ ] A unique reference number is generated in the format TRF-{tenant}-{YYYYMMDD}-{seq}
- [ ] Both sender and receiver have matching transaction history entries after successful transfer
- [ ] Sender's transaction entry shows: type=p2p_send, amount (negative), fee, reference, recipient info
- [ ] Receiver's transaction entry shows: type=p2p_receive, amount (positive), reference, sender info
- [ ] Receiver receives a push notification with sender name and transfer amount
- [ ] Insufficient balance (including fee) returns a clear error before saga execution begins

---

## Technical Notes

### Components

**Module:** `UniBank.Core/Modules/Transfers/`

```
Transfers/
  Domain/
    Entities/
      Transfer.cs                    # Transfer aggregate root
      TransferStatus.cs              # Enum: Pending, Completed, Failed, Reversed
    ValueObjects/
      TransferReference.cs           # TRF-{tenant}-{YYYYMMDD}-{seq} generator
      Money.cs                       # Amount + currency value object
    Events/
      TransferInitiated.cs
      TransferCompleted.cs
      TransferFailed.cs
      TransferReversed.cs
  Application/
    Commands/
      SendP2PTransferCommand.cs      # Input: sender_account_id, recipient_phone, amount
      SendP2PTransferHandler.cs
    Queries/
      PreviewP2PTransferQuery.cs     # Returns fees, recipient name preview
      PreviewP2PTransferHandler.cs
      LookupRecipientQuery.cs        # Phone lookup, returns masked name
      LookupRecipientHandler.cs
    Validators/
      SendP2PTransferValidator.cs    # FluentValidation rules
    Sagas/
      P2PTransferSaga.cs            # Wolverine saga orchestrator
  Infrastructure/
    Persistence/
      TransferRepository.cs
      TransferEntityConfiguration.cs
    Services/
      TransferService.cs             # Implements TransferService.SendP2P
      FeeCalculationService.cs       # Delegates to SharedKernel.FeeCalculator
  Grpc/
    TransferGrpcService.cs           # gRPC endpoint mapping
```

**SharedKernel:**
- `SharedKernel/FeeCalculation/FeeCalculator.cs` — Shared fee calculation engine
- `SharedKernel/Events/TransactionCompleted.cs` — Cross-module event

### API / gRPC Endpoints

**LookupRecipient:**
```protobuf
rpc LookupRecipient(LookupRecipientRequest) returns (LookupRecipientResponse);

message LookupRecipientRequest {
  string phone_number = 1;
}

message LookupRecipientResponse {
  bool found = 1;
  string masked_name = 2;       // "John M."
  string masked_phone = 3;      // "****1234"
  string recipient_id = 4;      // opaque ID for subsequent calls
}
```

**PreviewP2PTransfer:**
```protobuf
rpc PreviewP2PTransfer(PreviewTransferRequest) returns (PreviewTransferResponse);

message PreviewTransferRequest {
  string sender_account_id = 1;
  string recipient_id = 2;
  string amount = 3;            // decimal as string
  string currency = 4;
}

message PreviewTransferResponse {
  string recipient_masked_name = 1;
  string recipient_masked_phone = 2;
  string transfer_amount = 3;
  string fee_amount = 4;
  string total_debit = 5;
  string currency = 6;
}
```

**SendP2PTransfer:**
```protobuf
rpc SendP2PTransfer(SendP2PTransferRequest) returns (SendP2PTransferResponse);

message SendP2PTransferRequest {
  string sender_account_id = 1;
  string recipient_id = 2;
  string amount = 3;
  string currency = 4;
  string idempotency_key = 5;   // client-generated UUID for retry safety
}

message SendP2PTransferResponse {
  bool success = 1;
  string reference_number = 2;   // TRF-{tenant}-{YYYYMMDD}-{seq}
  string error_code = 3;
  string error_message = 4;
  google.protobuf.Timestamp completed_at = 5;
}
```

### Database Changes

**Table: `transfers` (tenant schema)**
```sql
CREATE TABLE {tenant_schema}.transfers (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    reference_number VARCHAR(50) NOT NULL UNIQUE,
    transfer_type VARCHAR(20) NOT NULL DEFAULT 'p2p_domestic',
    sender_account_id UUID NOT NULL REFERENCES {tenant_schema}.accounts(id),
    receiver_account_id UUID NOT NULL REFERENCES {tenant_schema}.accounts(id),
    amount DECIMAL(18,4) NOT NULL,
    fee_amount DECIMAL(18,4) NOT NULL DEFAULT 0,
    currency VARCHAR(3) NOT NULL,
    status VARCHAR(20) NOT NULL DEFAULT 'pending',
    initiated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    completed_at TIMESTAMPTZ,
    failed_at TIMESTAMPTZ,
    failure_reason TEXT,
    idempotency_key UUID NOT NULL UNIQUE,
    saga_id UUID,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_transfers_sender ON {tenant_schema}.transfers(sender_account_id);
CREATE INDEX idx_transfers_receiver ON {tenant_schema}.transfers(receiver_account_id);
CREATE INDEX idx_transfers_reference ON {tenant_schema}.transfers(reference_number);
CREATE INDEX idx_transfers_idempotency ON {tenant_schema}.transfers(idempotency_key);
CREATE INDEX idx_transfers_status ON {tenant_schema}.transfers(status);
```

**Table: `transfer_limits` (tenant schema)**
```sql
CREATE TABLE {tenant_schema}.transfer_limits (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    transfer_type VARCHAR(20) NOT NULL,
    per_transaction_limit DECIMAL(18,4) NOT NULL,
    daily_limit DECIMAL(18,4) NOT NULL,
    monthly_limit DECIMAL(18,4),
    currency VARCHAR(3) NOT NULL,
    kyc_tier VARCHAR(20) NOT NULL DEFAULT 'basic',
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
```

### Wolverine Saga: P2PTransferSaga

```csharp
public class P2PTransferSaga : Saga
{
    public Guid TransferId { get; set; }
    public Guid SenderAccountId { get; set; }
    public Guid ReceiverAccountId { get; set; }
    public decimal Amount { get; set; }
    public decimal FeeAmount { get; set; }
    public string Currency { get; set; }
    public bool SenderDebited { get; set; }
    public bool ReceiverCredited { get; set; }

    // Step 1: Debit sender
    public DebitAccount Handle(StartP2PTransfer cmd)
    {
        TransferId = cmd.TransferId;
        SenderAccountId = cmd.SenderAccountId;
        ReceiverAccountId = cmd.ReceiverAccountId;
        Amount = cmd.Amount;
        FeeAmount = cmd.FeeAmount;
        Currency = cmd.Currency;

        return new DebitAccount(SenderAccountId, Amount + FeeAmount, TransferId);
    }

    // Step 2: Credit receiver (after debit succeeds)
    public CreditAccount Handle(AccountDebited evt)
    {
        SenderDebited = true;
        return new CreditAccount(ReceiverAccountId, Amount, TransferId);
    }

    // Step 3: Record fee (after credit succeeds)
    public RecordTransactionFee Handle(AccountCredited evt)
    {
        ReceiverCredited = true;
        return new RecordTransactionFee(TransferId, FeeAmount, Currency);
    }

    // Step 4: Complete (after fee recorded)
    public TransferCompleted Handle(TransactionFeeRecorded evt)
    {
        MarkCompleted();
        return new TransferCompleted(TransferId, SenderAccountId, ReceiverAccountId, Amount);
    }

    // Compensation: Reverse debit if credit fails
    public ReverseDebit Handle(CreditFailed evt)
    {
        if (SenderDebited)
        {
            return new ReverseDebit(SenderAccountId, Amount + FeeAmount, TransferId);
        }
        MarkCompleted();
        return null;
    }
}
```

### Security Considerations
- **Authentication:** Sender must be authenticated via JWT with valid session
- **Authorization:** Sender can only transfer from their own accounts
- **Rate limiting:** Max transfer attempts per minute per account (configurable, default 5)
- **Idempotency:** Client-provided idempotency_key prevents duplicate transfers on retry
- **Data masking:** Recipient name and phone are masked in preview (only partial reveal)
- **Audit trail:** All transfer attempts (success and failure) are logged with full context
- **Amount validation:** Server-side validation of amount > 0, within limits, sufficient balance
- **Tenant isolation:** Both accounts must be in the same tenant; cross-tenant is handled by STORY-030

### Edge Cases
- **Insufficient balance:** Reject before saga starts; include fee in balance check (amount + fee <= available_balance)
- **Sender and receiver are the same account:** Reject with clear error
- **Recipient account frozen/suspended:** Reject with error "Recipient account is not available"
- **Concurrent transfers draining balance:** Use optimistic concurrency on account balance (row-level locking or version column)
- **Saga credit step fails:** Compensation reverses the debit automatically; transfer marked as "failed"
- **Saga timeout:** If any saga step doesn't complete within 30 seconds, trigger compensation
- **Duplicate request (retry):** Idempotency key returns previous result without re-executing
- **Daily limit exceeded mid-transfer:** Check cumulative daily amount before initiating saga
- **Network failure during saga:** Wolverine durability ensures saga resumes on restart

---

## Dependencies

**Prerequisite Stories:**
- STORY-013: Account Balance & Mini-Statement (account balance query and transaction recording)
- STORY-020: PIN Management & Transaction Authorization (PIN verification for transfers)

**Blocked Stories:**
- STORY-030: P2P Cross-Border Transfer (extends this domestic transfer logic)
- STORY-031: Transfer Confirmation & Notifications (builds on transfer events)

**External Dependencies:**
- Wolverine messaging framework must be configured (STORY-007)
- Account balance service must support atomic debit/credit operations

---

## Definition of Done

- [ ] Code implemented and committed
- [ ] Unit tests written and passing (>=80% coverage)
- [ ] Integration tests passing (saga happy path and compensation paths)
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
