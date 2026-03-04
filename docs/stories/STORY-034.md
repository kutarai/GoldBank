# STORY-034: Agent Commission Engine

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
I want to earn commission on cash-in/cash-out transactions,
So that I'm incentivized to serve customers.

---

## Description

### Background
The agent banking model in Southern Africa relies on a network of independent merchants who act as banking access points for the unbanked. These agents invest their own capital as float and dedicate time and physical space to serve customers. A fair and transparent commission structure is essential to attract and retain quality agents. Without adequate compensation, agent networks shrink and financial inclusion goals fail.

UniBank's commission engine must be flexible enough to support different commission structures across tenants (each country deployment may have different regulatory requirements and market expectations). Commissions must be calculated automatically during each agent transaction, credited to a separate commission balance (distinct from the agent's float), and made available for periodic settlement.

Functional Requirement: **FR-021**.

### Scope

**In scope:**
- Configurable commission rates per transaction type (cash_in, cash_out) per tenant
- Support for percentage-based, flat-fee, and tiered commission structures
- Automatic commission calculation during Wolverine saga RecordCommission step
- Commission crediting to agent's dedicated commission_balance (separate from float)
- Real-time commission balance query for agents
- Commission transaction history for agents
- Commission rate configuration via tenant settings

**Out of scope:**
- Commission settlement/payout processing (STORY-052: Merchant Settlement)
- Commission disputes and adjustments
- Agent performance-based bonus commissions (future enhancement)
- Commission rate changes applied retroactively
- Tax withholding on commissions (future: regulatory compliance)

### User Flow
1. Agent performs a cash-in or cash-out transaction
2. System automatically calculates commission based on tenant-configured rates for the transaction type and amount
3. Commission is credited to the agent's commission_balance as part of the transaction saga
4. Agent can view their current commission balance on the agent dashboard
5. Agent can view their commission transaction history with details per transaction
6. Accumulated commissions are paid out during the settlement cycle (STORY-052)

---

## Acceptance Criteria

- [ ] Commission rates are configurable per tenant for each transaction type: cash_in and cash_out
- [ ] System supports percentage-based commission calculation (e.g., 1.5% of transaction amount)
- [ ] System supports flat-fee commission calculation (e.g., fixed $0.50 per transaction)
- [ ] System supports tiered commission calculation (e.g., 2% for first $100, 1.5% for $100-$500, 1% above $500)
- [ ] Commission is automatically calculated and credited during the RecordCommission step of the Wolverine saga
- [ ] Commission is credited to the agent's `commission_balance`, not their float balance
- [ ] Commission calculation uses the transaction amount (not amount + fee)
- [ ] Agent can query their current commission balance in real-time via API
- [ ] Agent can view a list of commission transactions with: date, transaction reference, type, amount, commission earned
- [ ] Commission amount is included in the transaction receipt for both agent and customer
- [ ] If commission recording fails, the parent transaction (cash-in/cash-out) still completes; commission is retried asynchronously
- [ ] Commission rates can be updated by admin without restarting the system
- [ ] Rate changes apply only to new transactions (no retroactive adjustments)
- [ ] Commission balance cannot go negative

---

## Technical Notes

### Components

**Module:** `UniBank.SharedKernel/` (FeeCalculator) and `UniBank.Core/Modules/Agents/`

```
SharedKernel/
  FeeCalculation/
    FeeCalculator.cs                 # Core calculation engine
    FeeStructure.cs                  # Rate configuration model
    FeeType.cs                       # Enum: Percentage, Flat, Tiered
    TieredRate.cs                    # Tier bracket definition
    IFeeConfigProvider.cs            # Interface for rate lookup

Agents/
  Domain/
    Entities/
      AgentCommission.cs             # Commission transaction record
      CommissionRate.cs              # Rate configuration entity
    ValueObjects/
      CommissionType.cs              # Enum: CashIn, CashOut
  Application/
    Commands/
      RecordCommissionCommand.cs     # Saga step handler
      RecordCommissionHandler.cs
    Queries/
      GetCommissionBalanceQuery.cs
      GetCommissionHistoryQuery.cs
      GetCommissionRatesQuery.cs
    Handlers/
      CommissionSagaStepHandler.cs   # Wolverine handler for RecordAgentCommission message
  Infrastructure/
    Services/
      CommissionService.cs           # Commission calculation and crediting
      CommissionConfigProvider.cs    # Loads rates from tenant config / database
    Persistence/
      CommissionRepository.cs
      CommissionRateRepository.cs
```

### API / gRPC Endpoints

**GetCommissionBalance:**
```protobuf
rpc GetCommissionBalance(GetCommissionBalanceRequest) returns (GetCommissionBalanceResponse);

message GetCommissionBalanceRequest {
  string agent_id = 1;
}

message GetCommissionBalanceResponse {
  string commission_balance = 1;
  string currency = 2;
  string last_commission_date = 3;
  string last_settlement_date = 4;
}
```

**GetCommissionHistory:**
```protobuf
rpc GetCommissionHistory(GetCommissionHistoryRequest) returns (GetCommissionHistoryResponse);

message GetCommissionHistoryRequest {
  string agent_id = 1;
  google.protobuf.Timestamp from_date = 2;
  google.protobuf.Timestamp to_date = 3;
  int32 page = 4;
  int32 page_size = 5;
}

message CommissionEntry {
  string id = 1;
  string transaction_reference = 2;
  string transaction_type = 3;       // 'cash_in' or 'cash_out'
  string transaction_amount = 4;
  string commission_amount = 5;
  string commission_rate = 6;        // e.g., "1.5%"
  string currency = 7;
  google.protobuf.Timestamp earned_at = 8;
}

message GetCommissionHistoryResponse {
  repeated CommissionEntry entries = 1;
  int32 total_count = 2;
  string total_commission = 3;       // sum for the period
}
```

**GetCommissionRates (Agent-facing, read-only):**
```protobuf
rpc GetCommissionRates(GetCommissionRatesRequest) returns (GetCommissionRatesResponse);

message GetCommissionRatesRequest {}

message CommissionRateInfo {
  string transaction_type = 1;
  string rate_type = 2;              // 'percentage', 'flat', 'tiered'
  string rate_value = 3;             // "1.5%" or "$0.50" or "tiered"
  repeated TierInfo tiers = 4;      // only for tiered
}

message TierInfo {
  string min_amount = 1;
  string max_amount = 2;
  string rate = 3;
}

message GetCommissionRatesResponse {
  repeated CommissionRateInfo rates = 1;
}
```

### Database Changes

**Table: `commission_rates` (tenant schema)**
```sql
CREATE TABLE {tenant_schema}.commission_rates (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    transaction_type VARCHAR(20) NOT NULL,          -- 'cash_in', 'cash_out'
    rate_type VARCHAR(20) NOT NULL,                 -- 'percentage', 'flat', 'tiered'
    rate_value DECIMAL(18,6),                       -- for percentage (0.015 = 1.5%) or flat amount
    currency VARCHAR(3) NOT NULL,
    effective_from TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    effective_until TIMESTAMPTZ,
    created_by UUID,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_commission_rates_type ON {tenant_schema}.commission_rates(transaction_type);
CREATE INDEX idx_commission_rates_effective ON {tenant_schema}.commission_rates(effective_from, effective_until);
```

**Table: `commission_tiers` (tenant schema)**
```sql
CREATE TABLE {tenant_schema}.commission_tiers (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    commission_rate_id UUID NOT NULL REFERENCES {tenant_schema}.commission_rates(id),
    min_amount DECIMAL(18,4) NOT NULL,
    max_amount DECIMAL(18,4),                       -- NULL = unlimited
    tier_rate DECIMAL(18,6) NOT NULL,               -- percentage for this tier
    sort_order INTEGER NOT NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_commission_tiers_rate ON {tenant_schema}.commission_tiers(commission_rate_id);
```

**Table: `agent_commissions` (tenant schema)**
```sql
CREATE TABLE {tenant_schema}.agent_commissions (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    agent_id UUID NOT NULL,
    agent_float_account_id UUID NOT NULL REFERENCES {tenant_schema}.agent_float_accounts(id),
    transaction_id UUID NOT NULL REFERENCES {tenant_schema}.agent_transactions(id),
    transaction_type VARCHAR(20) NOT NULL,
    transaction_amount DECIMAL(18,4) NOT NULL,
    commission_amount DECIMAL(18,4) NOT NULL,
    rate_type VARCHAR(20) NOT NULL,
    rate_applied VARCHAR(50) NOT NULL,              -- human-readable: "1.5%" or "$0.50" or "tiered"
    currency VARCHAR(3) NOT NULL,
    settled BOOLEAN NOT NULL DEFAULT FALSE,
    settled_at TIMESTAMPTZ,
    settlement_id UUID,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_agent_commissions_agent ON {tenant_schema}.agent_commissions(agent_id);
CREATE INDEX idx_agent_commissions_unsettled ON {tenant_schema}.agent_commissions(agent_id, settled)
    WHERE settled = FALSE;
CREATE INDEX idx_agent_commissions_tx ON {tenant_schema}.agent_commissions(transaction_id);
```

### FeeCalculator Implementation

```csharp
public class FeeCalculator
{
    public decimal Calculate(decimal transactionAmount, FeeStructure structure)
    {
        return structure.Type switch
        {
            FeeType.Percentage => Math.Round(transactionAmount * structure.Rate, 4),
            FeeType.Flat => structure.Rate,
            FeeType.Tiered => CalculateTiered(transactionAmount, structure.Tiers),
            _ => throw new InvalidOperationException($"Unknown fee type: {structure.Type}")
        };
    }

    private decimal CalculateTiered(decimal amount, IReadOnlyList<TieredRate> tiers)
    {
        decimal totalCommission = 0;
        decimal remainingAmount = amount;

        foreach (var tier in tiers.OrderBy(t => t.MinAmount))
        {
            if (remainingAmount <= 0) break;

            decimal tierMax = tier.MaxAmount ?? decimal.MaxValue;
            decimal tierRange = tierMax - tier.MinAmount;
            decimal amountInTier = Math.Min(remainingAmount, tierRange);

            totalCommission += Math.Round(amountInTier * tier.Rate, 4);
            remainingAmount -= amountInTier;
        }

        return totalCommission;
    }
}
```

### Wolverine Handler: RecordAgentCommission

```csharp
public class RecordAgentCommissionHandler
{
    public async Task<AgentCommissionRecorded> Handle(
        RecordAgentCommission cmd,
        ICommissionService commissionService,
        IAgentFloatRepository floatRepository)
    {
        // 1. Look up effective commission rate for transaction type
        var rate = await commissionService.GetEffectiveRateAsync(cmd.TransactionType);

        // 2. Calculate commission
        var commissionAmount = commissionService.Calculate(cmd.TransactionAmount, rate);

        // 3. Credit commission to agent's commission_balance
        await floatRepository.CreditCommissionAsync(cmd.AgentId, commissionAmount);

        // 4. Record commission transaction
        var commission = new AgentCommission
        {
            AgentId = cmd.AgentId,
            TransactionId = cmd.TransactionId,
            TransactionType = cmd.TransactionType,
            TransactionAmount = cmd.TransactionAmount,
            CommissionAmount = commissionAmount,
            RateType = rate.Type.ToString(),
            RateApplied = FormatRate(rate),
            Currency = cmd.Currency
        };
        await commissionService.RecordAsync(commission);

        return new AgentCommissionRecorded(cmd.TransactionId, commissionAmount);
    }
}
```

### Security Considerations
- **Rate tampering:** Commission rates are admin-only configuration; agents cannot modify rates
- **Commission balance integrity:** Commission credited atomically with transaction; separate from float to prevent agents from using commission as float
- **Audit trail:** Every commission credit is linked to a specific transaction with full rate details
- **Settlement security:** Commission settlement (STORY-052) requires admin approval
- **Rate effective dating:** Historical rates preserved; rate changes only affect future transactions

### Edge Cases
- **No commission rate configured for transaction type:** Use a default rate of 0 (zero commission); log a warning for admin attention
- **Commission calculation results in sub-cent amount:** Round to 4 decimal places; minimum commission of 0.0001 (or tenant-configured minimum)
- **Tiered rate with gaps:** Tiers must be contiguous; validator ensures no gaps between tier boundaries
- **Rate changes during high-volume period:** Rate lookup uses effective_from/effective_until; concurrent transactions may get different rates (acceptable)
- **Commission recording fails in saga:** The parent transaction (cash-in/cash-out) should still complete. Publish a `CommissionRecordingFailed` event for async retry. Do not block the customer-facing transaction
- **Very large transaction amount:** Commission cap per transaction configurable per tenant to prevent excessive commission on large amounts
- **Zero-amount transaction (edge):** Commission = 0; still record the zero-commission entry for audit completeness

---

## Dependencies

**Prerequisite Stories:**
- STORY-032: Cash-In at Merchant Agent (triggers commission on cash-in)
- STORY-033: Cash-Out at Merchant Agent (triggers commission on cash-out)

**Blocked Stories:**
- STORY-052: Merchant Settlement (uses accumulated commission data for settlement processing)

**External Dependencies:**
- None; commission engine is entirely internal

---

## Definition of Done

- [ ] Code implemented and committed
- [ ] Unit tests written and passing (>=80% coverage)
- [ ] Integration tests passing (all three commission types: percentage, flat, tiered)
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
