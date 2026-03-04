# STORY-033: Cash-Out at Merchant Agent

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
I want to withdraw cash at a merchant agent,
So that I can access physical cash when needed.

---

## Description

### Background
While the goal of UniBank is to promote digital transactions, cash-out capability is essential for user adoption in Southern Africa. Users need confidence that they can convert their digital balance back to physical cash at any time. Agent cash-out is the reverse of cash-in: the customer's digital account is debited, and the agent hands over physical cash. The agent's float balance increases (they received digital funds) while their physical cash decreases.

Cash-out is the highest-risk agent transaction from a fraud perspective. Unlike cash-in (where the customer confirms receiving digital funds), cash-out involves the agent disbursing physical cash that cannot be recalled. Therefore, this flow requires PIN authorization from the customer and explicit agent confirmation of cash disbursement.

Functional Requirement: **FR-020**.

### Scope

**In scope:**
- Customer-initiated cash-out via mobile app (enters agent code and amount)
- Agent-initiated cash-out via POS/agent app (enters customer phone and amount)
- Customer PIN authorization required for all cash-outs (FR-061)
- Wolverine saga: DebitCustomerAccount -> CreditAgentFloat -> RecordCommission -> PublishTransactionCompleted
- Saga compensation (reverse customer debit if agent float credit fails)
- Agent confirmation of physical cash disbursement
- Commission calculation and crediting
- Digital receipts for both parties
- Transaction type classification as `cash_out`

**Out of scope:**
- ATM cash-out integration
- Cash-out without customer PIN (emergency withdrawal)
- Cash-out limits management (uses standard account transaction limits)
- Cardless ATM withdrawal codes

### User Flow

**Customer-Initiated Flow:**
1. Customer opens the app and selects "Cash Out"
2. Customer enters the agent code (displayed at agent location) or scans QR code
3. System validates agent exists, is active, and has sufficient float
4. Customer enters the withdrawal amount
5. System displays confirmation: agent name, amount, fee, total debit
6. Customer enters PIN for authorization
7. System validates PIN (FR-061)
8. System executes Wolverine saga: DebitCustomerAccount -> CreditAgentFloat -> RecordCommission -> PublishTransactionCompleted
9. System generates a cash-out authorization code (6-digit, time-limited)
10. Customer shows authorization code to agent
11. Agent enters the code on POS/app to confirm cash disbursement
12. Transaction marked as fully completed
13. Both parties receive digital receipts

**Agent-Initiated Flow:**
1. Agent selects "Cash Out" on POS/agent app
2. Agent enters customer phone number and amount
3. System sends authorization request to customer's mobile
4. Customer sees request details and enters PIN to authorize
5. System validates PIN and executes saga
6. Agent confirms cash disbursement on POS/app
7. Both parties receive receipts

---

## Acceptance Criteria

- [ ] Customer can initiate cash-out by entering an agent code or scanning a QR code
- [ ] Agent can initiate cash-out by entering the customer's phone number
- [ ] System validates agent exists, is active, and is enabled for cash-out transactions
- [ ] System validates customer has sufficient balance (amount + fee)
- [ ] Customer must enter their PIN to authorize the cash-out (FR-061)
- [ ] System rejects the transaction if PIN is incorrect (max 3 attempts before temporary lock)
- [ ] Confirmation screen displays: agent name/location, withdrawal amount, fee, total debit from account
- [ ] Wolverine saga executes: DebitCustomerAccount -> CreditAgentFloat -> RecordCommission -> PublishTransactionCompleted
- [ ] Customer account balance is decreased by amount + fee
- [ ] Agent float balance is increased by the withdrawal amount
- [ ] Commission is calculated per tenant configuration and credited to agent's commission balance
- [ ] If any saga step fails, compensation reverses all completed steps (customer debit reversed)
- [ ] For customer-initiated flow: a 6-digit authorization code is generated with a configurable TTL (default 10 minutes)
- [ ] Agent must enter the authorization code to confirm cash disbursement
- [ ] If authorization code expires before agent confirms, transaction is reversed and customer account is re-credited
- [ ] Unique reference number is generated: COT-{tenant}-{YYYYMMDD}-{seq}
- [ ] Both agent and customer receive digital receipts
- [ ] POS terminal prints physical receipt via MQTT (if POS-initiated or POS confirmation)
- [ ] Transaction is recorded with type `cash_out` in both agent and customer histories

---

## Technical Notes

### Components

**Module:** `UniBank.Core/Modules/Agents/`

```
Agents/
  Domain/
    Entities/
      AgentTransaction.cs             # Shared with cash-in (type differentiates)
      CashOutAuthorization.cs         # 6-digit code with TTL
    ValueObjects/
      AuthorizationCode.cs            # 6-digit code value object
    Events/
      CashOutInitiated.cs
      CashOutAuthorized.cs
      CashOutDisbursementConfirmed.cs
      CashOutCompleted.cs
      CashOutFailed.cs
      CashOutAuthorizationExpired.cs
  Application/
    Commands/
      InitiateCashOutCommand.cs       # Customer or agent starts
      AuthorizeCashOutCommand.cs      # Customer enters PIN
      ConfirmDisbursementCommand.cs   # Agent confirms cash given
    Queries/
      ValidateAgentCodeQuery.cs       # Validates agent code, returns agent info
    Validators/
      InitiateCashOutValidator.cs
      AuthorizeCashOutValidator.cs
    Sagas/
      CashOutSaga.cs                  # Wolverine saga for cash-out
  Infrastructure/
    Services/
      AgentService.cs                 # AgentService.CashOut
      AuthorizationCodeService.cs     # Generate/validate 6-digit codes
    Persistence/
      CashOutAuthorizationRepository.cs
```

### API / gRPC Endpoints

**InitiateCashOut (Customer-initiated):**
```protobuf
rpc InitiateCashOut(InitiateCashOutRequest) returns (InitiateCashOutResponse);

message InitiateCashOutRequest {
  string customer_account_id = 1;
  string agent_code = 2;            // Agent's unique code or QR content
  string amount = 3;
  string currency = 4;
  string pin = 5;                    // Customer PIN for authorization
  string idempotency_key = 6;
}

message InitiateCashOutResponse {
  bool success = 1;
  string transaction_id = 2;
  string authorization_code = 3;     // 6-digit code for agent
  string agent_name = 4;
  string amount = 5;
  string fee = 6;
  string total_debit = 7;
  google.protobuf.Timestamp code_expires_at = 8;
  string error_code = 9;
  string error_message = 10;
}
```

**InitiateCashOutByAgent (Agent-initiated):**
```protobuf
rpc InitiateCashOutByAgent(AgentCashOutRequest) returns (AgentCashOutResponse);

message AgentCashOutRequest {
  string agent_id = 1;
  string customer_phone = 2;
  string amount = 3;
  string currency = 4;
  string agent_pin = 5;
  string terminal_id = 6;
  string idempotency_key = 7;
}

message AgentCashOutResponse {
  bool success = 1;
  string transaction_id = 2;
  string customer_masked_name = 3;
  string status = 4;                // 'awaiting_customer_pin'
  string error_code = 5;
  string error_message = 6;
}
```

**ConfirmDisbursement (Agent confirms cash given):**
```protobuf
rpc ConfirmDisbursement(ConfirmDisbursementRequest) returns (ConfirmDisbursementResponse);

message ConfirmDisbursementRequest {
  string agent_id = 1;
  string transaction_id = 2;
  string authorization_code = 3;    // 6-digit code from customer
}

message ConfirmDisbursementResponse {
  bool success = 1;
  string reference_number = 2;      // COT-{tenant}-{YYYYMMDD}-{seq}
  string error_code = 3;
  string error_message = 4;
}
```

### Database Changes

**Table: `cash_out_authorizations` (tenant schema)**
```sql
CREATE TABLE {tenant_schema}.cash_out_authorizations (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    transaction_id UUID NOT NULL REFERENCES {tenant_schema}.agent_transactions(id),
    authorization_code VARCHAR(6) NOT NULL,
    customer_account_id UUID NOT NULL,
    agent_id UUID NOT NULL,
    amount DECIMAL(18,4) NOT NULL,
    currency VARCHAR(3) NOT NULL,
    status VARCHAR(20) NOT NULL DEFAULT 'active',    -- active, used, expired
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    expires_at TIMESTAMPTZ NOT NULL,
    used_at TIMESTAMPTZ,
    UNIQUE(authorization_code, agent_id, status)
);

CREATE INDEX idx_cashout_auth_code ON {tenant_schema}.cash_out_authorizations(authorization_code, agent_id)
    WHERE status = 'active';
CREATE INDEX idx_cashout_auth_expiry ON {tenant_schema}.cash_out_authorizations(expires_at)
    WHERE status = 'active';
```

Uses `agent_transactions` table from STORY-032 with `transaction_type = 'cash_out'`.

### Wolverine Saga: CashOutSaga

```csharp
public class CashOutSaga : Saga
{
    public Guid TransactionId { get; set; }
    public Guid AgentId { get; set; }
    public Guid CustomerAccountId { get; set; }
    public decimal Amount { get; set; }
    public decimal FeeAmount { get; set; }
    public decimal CommissionAmount { get; set; }
    public string Currency { get; set; }
    public bool CustomerDebited { get; set; }
    public bool AgentFloatCredited { get; set; }
    public string AuthorizationCode { get; set; }

    // Step 1: Debit customer account (amount + fee)
    public DebitAccount Handle(CashOutAuthorized cmd)
    {
        TransactionId = cmd.TransactionId;
        AgentId = cmd.AgentId;
        CustomerAccountId = cmd.CustomerAccountId;
        Amount = cmd.Amount;
        FeeAmount = cmd.FeeAmount;
        CommissionAmount = cmd.CommissionAmount;
        Currency = cmd.Currency;

        return new DebitAccount(CustomerAccountId, Amount + FeeAmount, TransactionId);
    }

    // Step 2: Credit agent float
    public CreditAgentFloat Handle(AccountDebited evt)
    {
        CustomerDebited = true;
        return new CreditAgentFloat(AgentId, Amount, TransactionId);
    }

    // Step 3: Record commission
    public RecordAgentCommission Handle(AgentFloatCredited evt)
    {
        AgentFloatCredited = true;
        return new RecordAgentCommission(AgentId, CommissionAmount, TransactionId);
    }

    // Step 4: Generate authorization code and wait for agent disbursement confirmation
    public object[] Handle(AgentCommissionRecorded evt)
    {
        return new object[]
        {
            new GenerateCashOutAuthorizationCode(TransactionId, AgentId, CustomerAccountId, Amount),
            new ScheduleCashOutExpiry(TransactionId, TimeSpan.FromMinutes(10))
        };
    }

    // Step 5: Complete when agent confirms disbursement
    public CashOutCompleted Handle(CashOutDisbursementConfirmed evt)
    {
        MarkCompleted();
        return new CashOutCompleted(TransactionId, AgentId, CustomerAccountId, Amount, CommissionAmount);
    }

    // Compensation: Authorization expired — reverse everything
    public object[] Handle(CashOutAuthorizationExpired evt)
    {
        var compensations = new List<object>();
        if (AgentFloatCredited)
            compensations.Add(new ReverseAgentFloatCredit(AgentId, Amount, TransactionId));
        if (CustomerDebited)
            compensations.Add(new ReverseDebit(CustomerAccountId, Amount + FeeAmount, TransactionId));
        MarkCompleted();
        return compensations.ToArray();
    }

    // Compensation: Float credit fails — reverse customer debit
    public ReverseDebit Handle(AgentFloatCreditFailed evt)
    {
        if (CustomerDebited)
        {
            return new ReverseDebit(CustomerAccountId, Amount + FeeAmount, TransactionId);
        }
        MarkCompleted();
        return null;
    }
}
```

### Authorization Code Service

```csharp
public class AuthorizationCodeService
{
    // Generate cryptographically secure 6-digit code
    public string GenerateCode()
    {
        using var rng = RandomNumberGenerator.Create();
        var bytes = new byte[4];
        rng.GetBytes(bytes);
        var number = BitConverter.ToUInt32(bytes) % 1000000;
        return number.ToString("D6");
    }

    // Validate code: check exists, not expired, not used, matches agent
    public async Task<bool> ValidateCodeAsync(string code, Guid agentId, Guid transactionId)
    {
        var auth = await _repository.GetActiveByCodeAndAgentAsync(code, agentId);
        if (auth == null || auth.TransactionId != transactionId) return false;
        if (auth.ExpiresAt < DateTime.UtcNow) return false;
        return true;
    }
}
```

### Security Considerations
- **Customer PIN required:** Cash-out always requires customer PIN authorization (FR-061) to prevent unauthorized withdrawals
- **Authorization code:** 6-digit code is cryptographically random; bound to specific transaction and agent; time-limited (default 10 min)
- **Agent authentication:** Agent must authenticate via agent PIN before initiating or confirming
- **Replay protection:** Authorization code can only be used once; marked as "used" after successful disbursement confirmation
- **Amount limits:** Cash-out subject to per-transaction and daily withdrawal limits per tenant/KYC tier
- **Fraud monitoring:** Large cash-out amounts or unusual patterns should trigger fraud alerts (future: STORY-060+)
- **Physical security:** System cannot guarantee agent actually hands over cash; dispute resolution is an operational process

### Edge Cases
- **Authorization code expires:** Full reversal — agent float debited back, customer account re-credited; both notified
- **Agent enters wrong authorization code:** Reject with error; allow retry (max 3 attempts, then lock transaction)
- **Customer PIN incorrect:** Reject cash-out; max 3 PIN attempts before temporary account lock
- **Concurrent cash-out requests from same customer:** Balance check with pessimistic locking prevents double-spend
- **Agent float credit fails (rare):** Compensation reverses customer debit; customer informed, no cash dispensed
- **Network failure after customer debit but before agent confirmation:** Transaction in "awaiting_disbursement" state; agent can confirm when reconnected; expiry timer still runs
- **Customer disputes cash not received:** Transaction record and authorization code usage provide audit trail; operational dispute resolution
- **Agent claims they gave cash but system shows no confirmation:** Agent must enter authorization code; without it, system-side transaction remains incomplete

---

## Dependencies

**Prerequisite Stories:**
- STORY-013: Account Balance & Mini-Statement (customer account debiting)
- STORY-035: Agent Float/Balance Management (float account structure and crediting)
- STORY-020: PIN Management & Transaction Authorization (customer PIN verification)

**Blocked Stories:**
- STORY-034: Agent Commission Engine (uses commission from cash-out flow)
- STORY-036: Agent Transaction Receipt (receipt generation from cash-out completion)

**External Dependencies:**
- PIN verification service must be available (STORY-020)
- MQTT broker for POS terminal communication

---

## Definition of Done

- [ ] Code implemented and committed
- [ ] Unit tests written and passing (>=80% coverage)
- [ ] Integration tests passing (saga happy path, compensation, authorization code flow)
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
