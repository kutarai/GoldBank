# STORY-035: Agent Float/Balance Management

**Epic:** EPIC-006 Agent Cash-In/Cash-Out
**Priority:** Must Have
**Story Points:** 5
**Status:** Not Started
**Assigned To:** Unassigned
**Created:** 2026-02-24
**Sprint:** 4

---

## User Story

As a merchant agent,
I want to manage my float balance,
So that I have funds for cash-in/cash-out transactions.

---

## Description

### Background
In the agent banking model, "float" represents the pool of digital funds an agent has available for cash-in transactions. When a customer deposits cash (cash-in), the agent's float decreases (they give digital funds to the customer) and their physical cash increases. When a customer withdraws cash (cash-out), the reverse happens: the agent's float increases (they receive digital funds) and their physical cash decreases.

Maintaining adequate float is critical for agent operations. An agent who runs out of float cannot process cash-in transactions, which directly impacts customer satisfaction and the agent's commission income. GoldBank must provide agents with real-time float visibility, configurable low-float alerts, and convenient top-up mechanisms.

The float account is a special account type (`agent_float`) linked to the merchant entity. It is separate from the agent's personal account and their commission balance, providing clear separation of concerns for accounting and settlement purposes.

Functional Requirement: **FR-022**.

### Scope

**In scope:**
- Dedicated agent float account creation and management (type: `agent_float`)
- Real-time float balance query
- Float decrease on cash-in transactions (agent gives digital funds)
- Float increase on cash-out transactions (agent receives digital funds)
- Configurable low-float threshold per agent
- Low-float alert generation via Wolverine event when balance drops below threshold
- Float top-up via bank transfer
- Float top-up via super-agent transfer
- Float transaction history
- Float balance dashboard for agent app

**Out of scope:**
- Agent registration and onboarding (STORY-050)
- Float funding from GoldBank treasury (operational process)
- Float lending/credit facility for agents
- Automatic float rebalancing between agents
- Multi-currency float management (single currency per tenant for MVP)

### User Flow

**Float Balance Check:**
1. Agent opens the agent app
2. Dashboard displays current float balance prominently
3. Agent can view float transaction history (top-ups, cash-in debits, cash-out credits)

**Low Float Alert:**
1. After a cash-in transaction, the system checks if float is below the configured threshold
2. If below threshold, Wolverine publishes `LowFloatAlert` event
3. Agent receives push notification: "Low float warning: {currency} {balance} remaining. Top up to continue serving customers."
4. Agent dashboard shows a visual low-float warning indicator

**Float Top-Up via Bank Transfer:**
1. Agent selects "Top Up Float" in the agent app
2. Agent selects "Bank Transfer" as top-up method
3. System displays GoldBank's settlement bank account details and a unique reference code
4. Agent initiates a bank transfer from their personal bank account using the reference code
5. GoldBank reconciliation process matches the incoming bank transfer and credits the float (manual or automated)

**Float Top-Up via Super-Agent:**
1. Agent selects "Top Up Float" and chooses "From Super-Agent"
2. Agent enters super-agent code and top-up amount
3. Super-agent confirms the float transfer
4. System debits super-agent float and credits agent float atomically

---

## Acceptance Criteria

- [ ] Each registered agent has a dedicated float account (type: `agent_float`) with a single currency
- [ ] Agent can query their current float balance in real-time
- [ ] Float balance decreases when the agent processes a cash-in transaction
- [ ] Float balance increases when the agent processes a cash-out transaction
- [ ] Agent can view their float transaction history showing all float movements
- [ ] Each float transaction entry includes: date, type (cash_in_debit, cash_out_credit, top_up, super_agent_transfer), amount, reference, resulting balance
- [ ] Low-float threshold is configurable per agent (default from tenant configuration)
- [ ] When float balance drops below the configured threshold, a LowFloatAlert Wolverine event is published
- [ ] Agent receives a push notification when low-float alert triggers
- [ ] Low-float alert does not fire repeatedly for the same threshold crossing (only on transition from above to below)
- [ ] Agent can view bank transfer details for float top-up with a unique reference code
- [ ] Agent can request a float top-up from a super-agent
- [ ] Super-agent can confirm or reject float transfer requests
- [ ] Super-agent to agent float transfer is atomic (debit super-agent, credit agent)
- [ ] Float balance cannot go negative (enforced at database and application level)
- [ ] Float account uses optimistic concurrency (version column) to prevent race conditions

---

## Technical Notes

### Components

**Module:** `GoldBank.Core/Modules/Agents/`

```
Agents/
  Domain/
    Entities/
      AgentFloat.cs                   # Float account aggregate root
      FloatTransaction.cs             # Float movement record
      FloatTopUpRequest.cs            # Top-up request entity
    ValueObjects/
      FloatTransactionType.cs         # Enum: CashInDebit, CashOutCredit, TopUp, SuperAgentTransfer, Adjustment
    Events/
      LowFloatAlert.cs               # Published when below threshold
      FloatTopUpCompleted.cs
      FloatTopUpRequested.cs
  Application/
    Commands/
      TopUpFloatCommand.cs            # Bank transfer top-up (admin/reconciliation)
      RequestSuperAgentTopUpCommand.cs  # Agent requests from super-agent
      ConfirmSuperAgentTopUpCommand.cs  # Super-agent confirms
      AdjustFloatCommand.cs           # Admin adjustment
    Queries/
      GetFloatBalanceQuery.cs
      GetFloatHistoryQuery.cs
      GetFloatTopUpDetailsQuery.cs    # Bank account details + reference
    Validators/
      TopUpFloatValidator.cs
    Handlers/
      LowFloatAlertHandler.cs        # Sends push notification on LowFloatAlert
  Infrastructure/
    Services/
      FloatService.cs                 # AgentService.GetFloatBalance, TopUpFloat
      FloatAlertService.cs            # Threshold monitoring
    Persistence/
      AgentFloatRepository.cs
      FloatTransactionRepository.cs
```

### API / gRPC Endpoints

**GetFloatBalance:**
```protobuf
rpc GetFloatBalance(GetFloatBalanceRequest) returns (GetFloatBalanceResponse);

message GetFloatBalanceRequest {
  string agent_id = 1;
}

message GetFloatBalanceResponse {
  string float_balance = 1;
  string commission_balance = 2;
  string currency = 3;
  string low_float_threshold = 4;
  bool is_low_float = 5;
  google.protobuf.Timestamp last_updated = 6;
}
```

**GetFloatHistory:**
```protobuf
rpc GetFloatHistory(GetFloatHistoryRequest) returns (GetFloatHistoryResponse);

message GetFloatHistoryRequest {
  string agent_id = 1;
  google.protobuf.Timestamp from_date = 2;
  google.protobuf.Timestamp to_date = 3;
  int32 page = 4;
  int32 page_size = 5;
}

message FloatTransactionEntry {
  string id = 1;
  string transaction_type = 2;      // 'cash_in_debit', 'cash_out_credit', 'top_up', 'super_agent_transfer'
  string amount = 3;
  string direction = 4;             // 'debit' or 'credit'
  string resulting_balance = 5;
  string reference = 6;
  string description = 7;
  google.protobuf.Timestamp timestamp = 8;
}

message GetFloatHistoryResponse {
  repeated FloatTransactionEntry entries = 1;
  int32 total_count = 2;
}
```

**GetTopUpDetails:**
```protobuf
rpc GetTopUpDetails(GetTopUpDetailsRequest) returns (GetTopUpDetailsResponse);

message GetTopUpDetailsRequest {
  string agent_id = 1;
}

message GetTopUpDetailsResponse {
  string bank_name = 1;
  string account_number = 2;
  string branch_code = 3;
  string reference_code = 4;        // unique per agent for reconciliation
  string instructions = 5;
}
```

**RequestSuperAgentTopUp:**
```protobuf
rpc RequestSuperAgentTopUp(SuperAgentTopUpRequest) returns (SuperAgentTopUpResponse);

message SuperAgentTopUpRequest {
  string agent_id = 1;
  string super_agent_code = 2;
  string amount = 3;
  string currency = 4;
}

message SuperAgentTopUpResponse {
  bool success = 1;
  string request_id = 2;
  string status = 3;                // 'pending_approval'
  string error_code = 4;
  string error_message = 5;
}
```

**ConfirmSuperAgentTopUp:**
```protobuf
rpc ConfirmSuperAgentTopUp(ConfirmSuperAgentTopUpRequest) returns (ConfirmSuperAgentTopUpResponse);

message ConfirmSuperAgentTopUpRequest {
  string super_agent_id = 1;
  string request_id = 2;
  bool approved = 3;
  string pin = 4;                   // super-agent PIN
}

message ConfirmSuperAgentTopUpResponse {
  bool success = 1;
  string reference_number = 2;
  string new_agent_float = 3;
  string new_super_agent_float = 4;
  string error_code = 5;
  string error_message = 6;
}
```

### Database Changes

Uses `agent_float_accounts` table from STORY-032.

**Table: `float_transactions` (tenant schema)**
```sql
CREATE TABLE {tenant_schema}.float_transactions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    agent_float_account_id UUID NOT NULL REFERENCES {tenant_schema}.agent_float_accounts(id),
    agent_id UUID NOT NULL,
    transaction_type VARCHAR(30) NOT NULL,          -- 'cash_in_debit', 'cash_out_credit', 'top_up', 'super_agent_transfer', 'adjustment'
    direction VARCHAR(10) NOT NULL,                 -- 'debit', 'credit'
    amount DECIMAL(18,4) NOT NULL,
    resulting_balance DECIMAL(18,4) NOT NULL,
    reference_id UUID,                              -- links to agent_transactions, top_up_requests, etc.
    reference_type VARCHAR(30),
    description TEXT,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_float_tx_account ON {tenant_schema}.float_transactions(agent_float_account_id);
CREATE INDEX idx_float_tx_agent ON {tenant_schema}.float_transactions(agent_id);
CREATE INDEX idx_float_tx_date ON {tenant_schema}.float_transactions(created_at);
```

**Table: `float_top_up_requests` (tenant schema)**
```sql
CREATE TABLE {tenant_schema}.float_top_up_requests (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    agent_id UUID NOT NULL,
    agent_float_account_id UUID NOT NULL REFERENCES {tenant_schema}.agent_float_accounts(id),
    top_up_type VARCHAR(20) NOT NULL,               -- 'bank_transfer', 'super_agent'
    super_agent_id UUID,
    amount DECIMAL(18,4) NOT NULL,
    currency VARCHAR(3) NOT NULL,
    status VARCHAR(20) NOT NULL DEFAULT 'pending',   -- pending, approved, rejected, completed, expired
    bank_reference VARCHAR(100),
    approved_at TIMESTAMPTZ,
    completed_at TIMESTAMPTZ,
    expires_at TIMESTAMPTZ,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_float_topup_agent ON {tenant_schema}.float_top_up_requests(agent_id);
CREATE INDEX idx_float_topup_super ON {tenant_schema}.float_top_up_requests(super_agent_id)
    WHERE super_agent_id IS NOT NULL;
CREATE INDEX idx_float_topup_status ON {tenant_schema}.float_top_up_requests(status);
```

### Low Float Alert Logic

```csharp
public class FloatAlertService
{
    public async Task CheckAndAlertAsync(Guid agentId, decimal newBalance, decimal threshold)
    {
        // Only alert on transition from above to below threshold
        var previouslyAbove = await _cache.GetAsync<bool>($"float_above_threshold:{agentId}");

        if (newBalance < threshold && (previouslyAbove == true || previouslyAbove == null))
        {
            // Publish Wolverine event
            await _messageBus.PublishAsync(new LowFloatAlert
            {
                AgentId = agentId,
                CurrentBalance = newBalance,
                Threshold = threshold,
                Currency = await _floatService.GetCurrencyAsync(agentId),
                AlertedAt = DateTime.UtcNow
            });

            await _cache.SetAsync($"float_above_threshold:{agentId}", false, TimeSpan.FromHours(24));
        }
        else if (newBalance >= threshold)
        {
            await _cache.SetAsync($"float_above_threshold:{agentId}", true, TimeSpan.FromHours(24));
        }
    }
}
```

### Super-Agent Float Transfer

```csharp
// Atomic super-agent to agent float transfer
public async Task<Result> TransferFloatAsync(Guid superAgentId, Guid agentId, decimal amount)
{
    using var transaction = await _dbContext.Database.BeginTransactionAsync();

    try
    {
        // Debit super-agent float (with optimistic concurrency)
        var superAgentFloat = await _floatRepo.GetByAgentIdAsync(superAgentId);
        if (superAgentFloat.FloatBalance < amount)
            return Result.Failure("Insufficient super-agent float");

        superAgentFloat.DebitFloat(amount);

        // Credit agent float
        var agentFloat = await _floatRepo.GetByAgentIdAsync(agentId);
        agentFloat.CreditFloat(amount);

        await _dbContext.SaveChangesAsync();
        await transaction.CommitAsync();

        return Result.Success();
    }
    catch (DbUpdateConcurrencyException)
    {
        await transaction.RollbackAsync();
        return Result.Failure("Concurrent modification detected. Please retry.");
    }
}
```

### Security Considerations
- **Float account isolation:** Float account is separate from personal accounts; agents cannot transfer float to personal accounts directly
- **Optimistic concurrency:** Version column on float accounts prevents race conditions during concurrent transactions
- **Negative balance prevention:** Database CHECK constraint `float_balance >= 0` as a safety net; application validates before debit
- **Super-agent PIN:** Super-agent must authorize float transfers with their PIN
- **Top-up reference uniqueness:** Bank transfer reference codes are unique per agent; prevents cross-agent misattribution
- **Admin adjustments audited:** Any manual float adjustment requires admin authorization and is fully logged

### Edge Cases
- **Concurrent cash-in transactions deplete float:** Optimistic concurrency ensures only transactions that find sufficient balance succeed; others fail with "insufficient float" and can be retried
- **Low float alert storm:** Alert fires only on above-to-below transition; subsequent transactions below threshold do not re-trigger until float returns above threshold
- **Super-agent also has low float:** System checks super-agent float sufficiency before allowing transfer
- **Bank transfer reconciliation mismatch:** Unmatched bank transfers are flagged for manual review; float not credited until matched
- **Float top-up during active transactions:** Top-up credits are independent of transaction flow; no conflict
- **Agent account suspended:** Float operations blocked; remaining float handled through operational process
- **Timezone differences in threshold checks:** All timestamps UTC; threshold checks are balance-based, not time-based
- **Very high float balance:** No upper limit for MVP; future: configurable max float per agent tier

---

## Dependencies

**Prerequisite Stories:**
- STORY-050: Merchant Registration (agent/merchant entity must exist)

**Blocked Stories:**
- STORY-032: Cash-In at Merchant Agent (requires float account for debit)
- STORY-033: Cash-Out at Merchant Agent (requires float account for credit)
- STORY-034: Agent Commission Engine (commission balance on float account)

**External Dependencies:**
- Bank reconciliation process for bank-transfer top-ups (operational; not automated in MVP)
- Push notification service for low-float alerts

---

## Definition of Done

- [ ] Code implemented and committed
- [ ] Unit tests written and passing (>=80% coverage)
- [ ] Integration tests passing (float operations, concurrency, alerts)
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
