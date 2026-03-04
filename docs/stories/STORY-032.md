# STORY-032: Cash-In at Merchant Agent

**Epic:** EPIC-006 Agent Cash-In/Cash-Out
**Priority:** Must Have
**Story Points:** 5
**Status:** Not Started
**Assigned To:** Unassigned
**Created:** 2026-02-24
**Sprint:** 4

---

## User Story

As a consumer,
I want to deposit cash at a merchant agent,
So that I can load money into my UniBank account.

---

## Description

### Background
For the unbanked population in Southern Africa, physical cash remains the primary medium of exchange. The ability to convert physical cash into digital funds is the critical on-ramp for mobile money adoption. UniBank's agent banking model leverages a network of merchant agents — typically small shop owners, petrol stations, and kiosks — who act as human ATMs, accepting cash from customers and crediting their UniBank accounts.

The cash-in flow involves a three-party interaction: the merchant agent (who accepts physical cash), the consumer (who receives digital funds), and the system (which orchestrates the float debit, customer credit, and commission recording). The agent's "float" balance represents their pool of digital funds available for cash-in operations; accepting cash decreases their float while increasing the customer's balance.

This story implements the core cash-in transaction using a Wolverine saga that atomically handles float management, customer crediting, commission calculation, and receipt generation.

Functional Requirement: **FR-019**.

### Scope

**In scope:**
- Agent-initiated cash-in via POS terminal or agent mobile app
- Customer identification by phone number
- Customer confirmation via push notification or in-app prompt
- Wolverine saga: DebitAgentFloat -> CreditCustomerAccount -> RecordCommission -> PublishTransactionCompleted
- Agent float sufficiency validation before transaction
- Commission calculation and crediting to agent's commission balance
- Digital receipt generation for both agent and customer
- Transaction type classification as `cash_in`

**Out of scope:**
- Agent registration and onboarding (STORY-050)
- Agent float top-up mechanisms (STORY-035 handles float management)
- POS terminal provisioning and management
- Cash-in via ATM or bank branch
- Cash-in limits management (handled in STORY-035 float management)

### User Flow
1. Consumer visits a merchant agent location with physical cash
2. Agent opens the agent app or POS terminal and selects "Cash-In"
3. Agent enters the customer's phone number
4. System validates customer exists and account is active
5. Agent enters the cash amount received from the customer
6. System validates agent has sufficient float for the amount
7. System sends a confirmation request to the customer's mobile device
8. Customer receives push notification: "Agent {agent_name} wants to deposit {amount} to your account. Confirm?"
9. Customer confirms the transaction in their app
10. System executes Wolverine saga: DebitAgentFloat -> CreditCustomerAccount -> RecordCommission -> PublishTransactionCompleted
11. Agent sees success screen with receipt details
12. Customer sees updated balance and transaction receipt
13. POS terminal prints physical receipt (if applicable via MQTT)
14. Both parties receive digital receipts in their transaction history

---

## Acceptance Criteria

- [ ] Agent can initiate a cash-in transaction from the agent app or POS terminal
- [ ] Agent can enter the customer's phone number to identify the recipient
- [ ] System validates that the customer phone number belongs to an active UniBank account
- [ ] System displays an error if the customer is not found or account is inactive/frozen
- [ ] Agent can enter the cash amount; system validates it is within acceptable limits
- [ ] System validates that the agent has sufficient float balance for the cash-in amount
- [ ] System displays an error if agent float is insufficient, with current float balance shown
- [ ] Customer receives a push notification requesting confirmation of the cash-in
- [ ] Customer can confirm or reject the cash-in from their mobile app
- [ ] If customer does not respond within 5 minutes (configurable), the transaction times out and is cancelled
- [ ] If customer rejects, the transaction is cancelled and the agent is notified
- [ ] Upon customer confirmation, the Wolverine saga executes: DebitAgentFloat -> CreditCustomerAccount -> RecordCommission -> PublishTransactionCompleted
- [ ] Agent float balance is decreased by the cash-in amount
- [ ] Customer account balance is increased by the cash-in amount
- [ ] Commission is calculated per tenant configuration and credited to agent's commission balance
- [ ] If any saga step fails, compensation reverses all completed steps
- [ ] Unique reference number is generated: CIN-{tenant}-{YYYYMMDD}-{seq}
- [ ] Agent receives a digital receipt with transaction details
- [ ] Customer receives a digital receipt with transaction details
- [ ] POS terminal prints a physical receipt via MQTT (if POS-initiated)
- [ ] Transaction is recorded with type `cash_in` in both agent and customer histories

---

## Technical Notes

### Components

**Module:** `UniBank.Core/Modules/Agents/`

```
Agents/
  Domain/
    Entities/
      AgentTransaction.cs            # Cash-in/cash-out transaction entity
      AgentFloat.cs                  # Float balance tracking
    ValueObjects/
      AgentTransactionType.cs        # Enum: CashIn, CashOut
      AgentCode.cs                   # Unique agent identifier
    Events/
      CashInInitiated.cs
      CashInConfirmed.cs
      CashInCompleted.cs
      CashInFailed.cs
      CashInTimedOut.cs
  Application/
    Commands/
      InitiateCashInCommand.cs       # Agent starts the cash-in
      ConfirmCashInCommand.cs        # Customer confirms
      RejectCashInCommand.cs         # Customer rejects
    Queries/
      GetPendingCashInQuery.cs       # Customer checks pending requests
    Validators/
      InitiateCashInValidator.cs
    Sagas/
      CashInSaga.cs                  # Wolverine saga for cash-in flow
  Infrastructure/
    Services/
      AgentService.cs                # AgentService.CashIn implementation
      FloatService.cs                # Float balance management
      AgentReceiptService.cs         # Receipt generation
    Persistence/
      AgentTransactionRepository.cs
      AgentFloatRepository.cs
```

### API / gRPC Endpoints

**InitiateCashIn (Agent-facing):**
```protobuf
rpc InitiateCashIn(InitiateCashInRequest) returns (InitiateCashInResponse);

message InitiateCashInRequest {
  string agent_id = 1;
  string customer_phone = 2;
  string amount = 3;
  string currency = 4;
  string agent_pin = 5;           // Agent authorization
  string terminal_id = 6;         // POS terminal ID (if POS-initiated)
  string idempotency_key = 7;
}

message InitiateCashInResponse {
  bool success = 1;
  string transaction_id = 2;      // Pending transaction ID
  string customer_masked_name = 3;
  string status = 4;              // 'awaiting_confirmation'
  string error_code = 5;
  string error_message = 6;
}
```

**ConfirmCashIn (Customer-facing):**
```protobuf
rpc ConfirmCashIn(ConfirmCashInRequest) returns (ConfirmCashInResponse);

message ConfirmCashInRequest {
  string transaction_id = 1;
  string customer_account_id = 2;
  bool confirmed = 3;             // true = confirm, false = reject
}

message ConfirmCashInResponse {
  bool success = 1;
  string reference_number = 2;    // CIN-{tenant}-{YYYYMMDD}-{seq}
  string amount = 3;
  string new_balance = 4;
  string error_code = 5;
  string error_message = 6;
}
```

### Database Changes

**Table: `agent_transactions` (tenant schema)**
```sql
CREATE TABLE {tenant_schema}.agent_transactions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    reference_number VARCHAR(50) NOT NULL UNIQUE,
    transaction_type VARCHAR(20) NOT NULL,         -- 'cash_in', 'cash_out'
    agent_id UUID NOT NULL,
    agent_merchant_id UUID NOT NULL,
    customer_account_id UUID NOT NULL REFERENCES {tenant_schema}.accounts(id),
    amount DECIMAL(18,4) NOT NULL,
    commission_amount DECIMAL(18,4) NOT NULL DEFAULT 0,
    currency VARCHAR(3) NOT NULL,
    status VARCHAR(20) NOT NULL DEFAULT 'pending',  -- pending, awaiting_confirmation, confirmed, completed, failed, timed_out, rejected
    terminal_id VARCHAR(50),
    initiated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    confirmed_at TIMESTAMPTZ,
    completed_at TIMESTAMPTZ,
    failed_at TIMESTAMPTZ,
    timeout_at TIMESTAMPTZ,                         -- when confirmation expires
    failure_reason TEXT,
    idempotency_key UUID NOT NULL UNIQUE,
    saga_id UUID,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_agent_tx_agent ON {tenant_schema}.agent_transactions(agent_id);
CREATE INDEX idx_agent_tx_customer ON {tenant_schema}.agent_transactions(customer_account_id);
CREATE INDEX idx_agent_tx_status ON {tenant_schema}.agent_transactions(status);
CREATE INDEX idx_agent_tx_reference ON {tenant_schema}.agent_transactions(reference_number);
CREATE INDEX idx_agent_tx_pending ON {tenant_schema}.agent_transactions(status, timeout_at)
    WHERE status = 'awaiting_confirmation';
```

**Table: `agent_float_accounts` (tenant schema)**
```sql
CREATE TABLE {tenant_schema}.agent_float_accounts (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    agent_id UUID NOT NULL UNIQUE,
    merchant_id UUID NOT NULL,
    float_balance DECIMAL(18,4) NOT NULL DEFAULT 0,
    commission_balance DECIMAL(18,4) NOT NULL DEFAULT 0,
    currency VARCHAR(3) NOT NULL,
    low_float_threshold DECIMAL(18,4) NOT NULL DEFAULT 1000,
    status VARCHAR(20) NOT NULL DEFAULT 'active',
    version INTEGER NOT NULL DEFAULT 1,             -- optimistic concurrency
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_agent_float_merchant ON {tenant_schema}.agent_float_accounts(merchant_id);
```

### Wolverine Saga: CashInSaga

```csharp
public class CashInSaga : Saga
{
    public Guid TransactionId { get; set; }
    public Guid AgentId { get; set; }
    public Guid CustomerAccountId { get; set; }
    public decimal Amount { get; set; }
    public decimal CommissionAmount { get; set; }
    public string Currency { get; set; }
    public bool FloatDebited { get; set; }
    public bool CustomerCredited { get; set; }

    // Triggered when customer confirms the cash-in
    // Step 1: Debit agent float
    public DebitAgentFloat Handle(CashInConfirmed cmd)
    {
        TransactionId = cmd.TransactionId;
        AgentId = cmd.AgentId;
        CustomerAccountId = cmd.CustomerAccountId;
        Amount = cmd.Amount;
        CommissionAmount = cmd.CommissionAmount;
        Currency = cmd.Currency;

        return new DebitAgentFloat(AgentId, Amount, TransactionId);
    }

    // Step 2: Credit customer account
    public CreditAccount Handle(AgentFloatDebited evt)
    {
        FloatDebited = true;
        return new CreditAccount(CustomerAccountId, Amount, TransactionId);
    }

    // Step 3: Record commission
    public RecordAgentCommission Handle(AccountCredited evt)
    {
        CustomerCredited = true;
        return new RecordAgentCommission(AgentId, CommissionAmount, TransactionId);
    }

    // Step 4: Complete
    public CashInCompleted Handle(AgentCommissionRecorded evt)
    {
        MarkCompleted();
        return new CashInCompleted(TransactionId, AgentId, CustomerAccountId, Amount, CommissionAmount);
    }

    // Compensation: Reverse float debit if customer credit fails
    public ReverseAgentFloatDebit Handle(CreditFailed evt)
    {
        if (FloatDebited)
        {
            return new ReverseAgentFloatDebit(AgentId, Amount, TransactionId);
        }
        MarkCompleted();
        return null;
    }

    // Timeout handler for customer confirmation
    public CashInTimedOut Handle(CashInConfirmationTimeout evt)
    {
        MarkCompleted();
        return new CashInTimedOut(TransactionId, AgentId);
    }
}
```

### MQTT Integration (POS Receipt Printing)

```
Topic: terminals/{terminal_id}/print
Payload:
{
  "type": "cash_in_receipt",
  "reference": "CIN-ZW-20260224-000001",
  "agent_name": "John's Shop",
  "customer_phone": "****1234",
  "amount": "100.00",
  "currency": "USD",
  "commission": "2.00",
  "datetime": "2026-02-24T14:30:00Z",
  "new_float_balance": "4900.00"
}
```

### Security Considerations
- **Agent authentication:** Agent must authenticate with agent PIN before initiating cash-in
- **Customer confirmation:** Customer must explicitly confirm on their own device to prevent unauthorized deposits (fraud vector: agent deposits stolen money to launder)
- **Float validation:** Float check and debit must be atomic (optimistic concurrency via version column) to prevent race conditions with concurrent transactions
- **Amount limits:** Cash-in amounts subject to per-transaction and daily limits per tenant AML configuration
- **Audit trail:** Full audit trail of all cash-in attempts including rejections, timeouts, and failures
- **Customer phone masking:** Agent sees only masked customer name to prevent social engineering

### Edge Cases
- **Customer confirmation timeout:** After configurable timeout (default 5 minutes), transaction auto-cancels; agent notified
- **Customer app offline:** Push notification queued; customer can check pending transactions when app opens
- **Agent float depleted during confirmation wait:** Re-check float balance at saga start; if insufficient, fail with clear error to agent
- **Concurrent cash-in requests draining float:** Optimistic concurrency on float balance (version column) ensures only one transaction succeeds; others retry or fail
- **Agent device loses connectivity after initiation:** Transaction remains in "awaiting_confirmation" state; agent can check status when reconnected
- **Customer confirms after timeout:** Reject late confirmation; customer sees "Transaction expired" message
- **Duplicate initiation (agent retry):** Idempotency key prevents duplicate transactions
- **Commission calculation error:** If commission recording fails, the cash-in still completes (commission step is non-critical and can be retried asynchronously)

---

## Dependencies

**Prerequisite Stories:**
- STORY-013: Account Balance & Mini-Statement (customer account crediting)
- STORY-035: Agent Float/Balance Management (float account structure)

**Blocked Stories:**
- STORY-034: Agent Commission Engine (uses commission recording from this flow)
- STORY-036: Agent Transaction Receipt (receipt generation triggered by cash-in completion)

**External Dependencies:**
- MQTT broker for POS terminal communication (STORY-007)
- Push notification service for customer confirmation requests

---

## Definition of Done

- [ ] Code implemented and committed
- [ ] Unit tests written and passing (>=80% coverage)
- [ ] Integration tests passing (saga happy path, compensation, timeout)
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
