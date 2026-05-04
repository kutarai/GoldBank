# STORY-052: Merchant Settlement & Payout

**Epic:** EPIC-010 Merchant Management
**Priority:** Must Have
**Story Points:** 5
**Status:** Not Started
**Assigned To:** Unassigned
**Created:** 2026-02-24
**Sprint:** 5

---

## User Story

As a **merchant**
I want **automated settlement and payout**
So that **I receive my earnings regularly without manual intervention**

---

## Description

### Background

Merchants in GoldBank's ecosystem — corner shops, informal traders, mobile money agents — depend on timely settlement of their sales proceeds. When a customer pays a merchant via GoldBank (QR payment, NFC tap, USSD transfer), the transaction amount (minus fees) must eventually land in the merchant's designated bank account. This is the settlement and payout process.

Settlement is not instantaneous. Transactions accumulate throughout the business day (or week, depending on the merchant's configured schedule). At the end of the settlement period, the system aggregates all completed transactions for the merchant, calculates the gross amount, deducts transaction fees, adds any agent commissions earned, and computes the net settlement amount. A payout is then initiated to the merchant's designated bank account — either as an internal GoldBank transfer (if the merchant banks with GoldBank) or as an external transfer via the Switching Server (if the merchant banks elsewhere).

For the unbanked merchants who are GoldBank's primary market, this automated settlement replaces the manual cash collection that is common in informal economies. It is a key value proposition of the platform.

**Functional Requirements:** FR-039 (Merchant Settlement & Payout)

### Scope

**In scope:**
- Scheduled settlement job (configurable: daily at EOD, or weekly on Monday)
- Transaction aggregation per merchant for the settlement period
- Fee calculation: gross amount - transaction fees = pre-commission amount
- Commission calculation for merchant agents (cash-in/cash-out commissions)
- Net settlement computation: pre-commission amount + agent commissions earned = net payout
- Settlement record creation with full breakdown
- Payout initiation: internal transfer (GoldBank account) or external transfer (via Switching Server)
- Settlement notification to merchant (settlement ready, payout initiated, payout completed)
- Per-merchant schedule configuration (daily or weekly)
- Settlement approval workflow for amounts above a configurable threshold

**Out of scope:**
- Merchant onboarding and registration (STORY-050)
- Transaction processing (handled by Payments module)
- Fee schedule management (admin configuration, separate story)
- Tax withholding calculations (future regulatory requirement)
- Multi-currency settlement (single currency per merchant for MVP)
- Chargeback handling (future enhancement)

### User Flow

**Automated Daily Settlement Flow:**

1. **Scheduled Trigger:** At EOD (configurable, e.g., 23:00 local time), the settlement job fires for all merchants with daily settlement
2. **Merchant Enumeration:** Query all active merchants with settlement_schedule = 'daily' (or 'weekly' if today is their settlement day)
3. **Transaction Aggregation:** For each merchant, aggregate all completed transactions since the last settlement:
   - Sum of purchase amounts (gross sales)
   - Count of transactions
   - Sum of transaction fees charged
4. **Commission Calculation:** For merchants who are also agents:
   - Sum of cash-in commission amounts for the period
   - Sum of cash-out commission amounts for the period
5. **Net Settlement Calculation:**
   - Net = Gross Sales - Transaction Fees + Agent Commissions
6. **Create Settlement Record:** Persist the settlement with full breakdown (gross, fees, commissions, net)
7. **Payout Decision:**
   - If net amount > 0 and net amount < auto-approval threshold: initiate payout automatically
   - If net amount >= auto-approval threshold: queue for manual approval
   - If net amount <= 0: no payout (fees exceeded sales), flag for review
8. **Payout Initiation:**
   - **Internal (GoldBank account):** Create an internal transfer from the merchant settlement pool account to the merchant's GoldBank account
   - **External (other bank):** Publish `RouteOutboundTransaction` command to the Switching Server with the merchant's external bank details
9. **Notification:** Send notification to merchant:
   - "Your daily settlement of ZAR X,XXX.XX has been processed. Payout initiated to account ending in XXXX."
10. **Payout Confirmation:** When the payout completes (internal transfer confirmation or switch approval), update settlement status to "paid" and send confirmation notification

**Weekly Settlement Variation:**
- Same flow but triggered weekly (default: Monday at 02:00)
- Aggregation covers the entire previous week (Monday 00:00 to Sunday 23:59)
- Larger amounts, same approval thresholds apply

---

## Acceptance Criteria

- [ ] Settlement job runs automatically on the configured schedule (cron expression, default: daily at 23:00 local time)
- [ ] All completed transactions for the settlement period are aggregated per merchant
- [ ] Settlement calculation includes: gross amount, transaction fees deducted, agent commissions added, net payout amount
- [ ] Settlement record is created with full breakdown (gross_amount, fees, commissions, net_amount, period_start, period_end)
- [ ] Payout is initiated to the merchant's designated bank account:
  - Internal GoldBank transfer if the merchant's payout account is a GoldBank account
  - External transfer via Switching Server if the merchant's payout account is at another bank
- [ ] Merchants with weekly settlement schedule receive settlement only on the configured day (default: Monday)
- [ ] Settlement schedule is configurable per merchant (daily or weekly)
- [ ] Settlements above the auto-approval threshold are queued for manual approval by operations
- [ ] Merchant receives notification when settlement is processed and when payout is completed
- [ ] Zero or negative net settlement (fees > sales) is flagged for review and no payout is initiated
- [ ] Settlement job handles errors gracefully — failure for one merchant does not block settlement for others

---

## Technical Notes

### Components

| Component | Location | Purpose |
|-----------|----------|---------|
| `SettlementJob.cs` | `src/Core/GoldBank.Core/Modules/Merchants/Jobs/` | Scheduled settlement job orchestrator |
| `MerchantSettlementService.cs` | `src/Core/GoldBank.Core/Modules/Merchants/Application/Services/` | Settlement calculation logic |
| `SettlementCalculator.cs` | `src/Core/GoldBank.Core/Modules/Merchants/Application/Services/` | Gross, fees, commissions, net computation |
| `PayoutService.cs` | `src/Core/GoldBank.Core/Modules/Merchants/Application/Services/` | Initiates payouts (internal or external) |
| `SettlementApprovalHandler.cs` | `src/Core/GoldBank.Core/Modules/Merchants/Application/Handlers/` | Handles manual approval workflow |
| `SettlementNotificationHandler.cs` | `src/Core/GoldBank.Core/Modules/Merchants/Application/Handlers/` | Sends settlement notifications |
| `Settlement.cs` | `src/Core/GoldBank.Core/Modules/Merchants/Domain/Entities/` | Settlement domain entity |
| `SettlementStatus.cs` | `src/Core/GoldBank.Core/Modules/Merchants/Domain/ValueObjects/` | Settlement status value object |

### API / gRPC Endpoints

**Merchant Settlement gRPC Service:**

```protobuf
service MerchantSettlementService {
  // Get settlements for a merchant
  rpc GetSettlements (GetSettlementsRequest) returns (stream SettlementResponse);

  // Get settlement details
  rpc GetSettlementDetail (GetSettlementDetailRequest) returns (SettlementDetailResponse);

  // Trigger manual settlement for a merchant
  rpc TriggerSettlement (TriggerSettlementRequest) returns (TriggerSettlementResponse);

  // Approve a pending settlement (admin)
  rpc ApproveSettlement (ApproveSettlementRequest) returns (ApproveSettlementResponse);

  // Update merchant settlement schedule
  rpc UpdateSettlementSchedule (UpdateScheduleRequest) returns (UpdateScheduleResponse);
}

message GetSettlementsRequest {
  string tenant_id = 1;
  string merchant_id = 2;
  string from_date = 3;
  string to_date = 4;
  int32 page_size = 5;
  string page_token = 6;
}

message SettlementResponse {
  string settlement_id = 1;
  string merchant_id = 2;
  string period_start = 3;
  string period_end = 4;
  int32 transaction_count = 5;
  string gross_amount = 6;
  string total_fees = 7;
  string total_commissions = 8;
  string net_amount = 9;
  string currency = 10;
  string status = 11;           // pending, approved, paid, failed, flagged
  string payout_reference = 12;
  string payout_account = 13;
  string created_at = 14;
  string paid_at = 15;
}

message SettlementDetailResponse {
  SettlementResponse settlement = 1;
  repeated TransactionSummary transactions = 2;
  repeated FeeSummary fees = 3;
  repeated CommissionSummary commissions = 4;
}

message TriggerSettlementRequest {
  string tenant_id = 1;
  string merchant_id = 2;
  string period_end = 3;  // settle up to this date/time
}

message TriggerSettlementResponse {
  string settlement_id = 1;
  bool success = 2;
  string error_message = 3;
}

message ApproveSettlementRequest {
  string tenant_id = 1;
  string settlement_id = 2;
  string approved_by = 3;
  string notes = 4;
}
```

**Settlement Calculator (pseudocode):**

```csharp
public class SettlementCalculator
{
    public SettlementBreakdown Calculate(
        string merchantId,
        IReadOnlyList<CompletedTransaction> transactions,
        IReadOnlyList<CommissionTransaction> commissions,
        FeeSchedule feeSchedule)
    {
        var grossAmount = transactions.Sum(t => t.Amount);
        var transactionCount = transactions.Count;

        // Calculate fees per transaction type
        var totalFees = transactions.Sum(t =>
            feeSchedule.CalculateFee(t.TransactionType, t.Amount));

        // Calculate commissions earned as agent
        var totalCommissions = commissions.Sum(c => c.CommissionAmount);

        var netAmount = grossAmount - totalFees + totalCommissions;

        return new SettlementBreakdown
        {
            GrossAmount = grossAmount,
            TransactionCount = transactionCount,
            TotalFees = totalFees,
            FeeBreakdown = CalculateFeeBreakdown(transactions, feeSchedule),
            TotalCommissions = totalCommissions,
            CommissionBreakdown = CalculateCommissionBreakdown(commissions),
            NetAmount = netAmount
        };
    }
}
```

**Payout Flow:**

```csharp
public class PayoutService
{
    public async Task<PayoutResult> InitiatePayoutAsync(
        Settlement settlement,
        MerchantPayoutConfig config,
        CancellationToken ct)
    {
        if (config.PayoutAccountIsInternal)
        {
            // Internal GoldBank transfer
            var transferCommand = new CreateInternalTransfer(
                SourceAccountId: _settlementPoolAccountId,
                DestAccountId: config.PayoutAccountId,
                Amount: settlement.NetAmount,
                Currency: settlement.Currency,
                Reference: $"SETTLE-{settlement.Id}",
                Description: $"Settlement payout {settlement.PeriodStart:d}-{settlement.PeriodEnd:d}"
            );
            return await _wolverine.InvokeAsync<PayoutResult>(transferCommand, ct);
        }
        else
        {
            // External transfer via Switching Server
            var switchCommand = new RouteOutboundTransaction(
                TenantId: settlement.TenantId,
                SourceInstitution: _goldbankInstitutionCode,
                DestinationInstitution: config.PayoutBankCode,
                SourceAccount: _settlementPoolAccount,
                DestinationAccount: config.PayoutAccountNumber,
                Amount: settlement.NetAmountInMinorUnits,
                Currency: settlement.Currency,
                ProcessingCode: "200000",  // credit transfer
                TransactionReference: $"SETTLE-{settlement.Id}",
                AdditionalData: new Dictionary<string, string>
                {
                    ["settlement_id"] = settlement.Id.ToString(),
                    ["merchant_id"] = settlement.MerchantId
                }
            );
            return await _wolverine.InvokeAsync<PayoutResult>(switchCommand, ct);
        }
    }
}
```

### Database Changes

**settlements table** (in the tenant schema):

```sql
CREATE TABLE settlements (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    merchant_id         UUID NOT NULL REFERENCES merchants(id),
    period_start        TIMESTAMPTZ NOT NULL,
    period_end          TIMESTAMPTZ NOT NULL,
    transaction_count   INT NOT NULL DEFAULT 0,
    gross_amount        DECIMAL(18, 2) NOT NULL DEFAULT 0,
    total_fees          DECIMAL(18, 2) NOT NULL DEFAULT 0,
    total_commissions   DECIMAL(18, 2) NOT NULL DEFAULT 0,
    net_amount          DECIMAL(18, 2) NOT NULL DEFAULT 0,
    currency            VARCHAR(3) NOT NULL,
    status              VARCHAR(20) NOT NULL DEFAULT 'pending',
    payout_type         VARCHAR(10),       -- 'internal' or 'external'
    payout_account      VARCHAR(34),
    payout_bank_code    VARCHAR(20),
    payout_reference    VARCHAR(50),
    payout_initiated_at TIMESTAMPTZ,
    payout_completed_at TIMESTAMPTZ,
    payout_failed_reason VARCHAR(500),
    approved_by         VARCHAR(100),
    approved_at         TIMESTAMPTZ,
    flagged_reason      VARCHAR(500),
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_settlements_merchant ON settlements (merchant_id, period_end DESC);
CREATE INDEX idx_settlements_status ON settlements (status);
CREATE INDEX idx_settlements_period ON settlements (period_start, period_end);
CREATE INDEX idx_settlements_payout ON settlements (status, payout_type)
    WHERE status IN ('approved', 'paying');
```

**settlement_line_items table** (links individual transactions to the settlement):

```sql
CREATE TABLE settlement_line_items (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    settlement_id   UUID NOT NULL REFERENCES settlements(id),
    transaction_id  UUID NOT NULL,
    transaction_type VARCHAR(30) NOT NULL,
    amount          DECIMAL(18, 2) NOT NULL,
    fee_amount      DECIMAL(18, 2) NOT NULL DEFAULT 0,
    net_amount      DECIMAL(18, 2) NOT NULL,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_settlement_items_settlement ON settlement_line_items (settlement_id);
CREATE INDEX idx_settlement_items_txn ON settlement_line_items (transaction_id);
```

**merchant_payout_config table:**

```sql
CREATE TABLE merchant_payout_config (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    merchant_id         UUID NOT NULL REFERENCES merchants(id) UNIQUE,
    settlement_schedule VARCHAR(10) NOT NULL DEFAULT 'daily' CHECK (settlement_schedule IN ('daily', 'weekly')),
    settlement_day      INT,  -- 1=Monday, 7=Sunday (only for weekly)
    settlement_time     TIME NOT NULL DEFAULT '23:00',
    payout_account_type VARCHAR(10) NOT NULL CHECK (payout_account_type IN ('internal', 'external')),
    payout_account_id   UUID,                -- GoldBank account ID (if internal)
    payout_account_number VARCHAR(34),       -- Account number (if external)
    payout_bank_code    VARCHAR(20),         -- Bank institution code (if external)
    payout_bank_name    VARCHAR(100),        -- Bank name for display
    auto_approval_limit DECIMAL(18, 2) NOT NULL DEFAULT 50000.00,
    min_payout_amount   DECIMAL(18, 2) NOT NULL DEFAULT 10.00,
    is_active           BOOLEAN NOT NULL DEFAULT true,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
```

### Security Considerations

- **Settlement Pool Account:** All merchant settlements are funded from a settlement pool account (a GoldBank internal ledger account). This account must have controls: only the settlement service can debit it, and the balance is monitored for adequacy.
- **Payout Authorization:** Payouts above the `auto_approval_limit` require manual approval by an authorized operations team member. The approval is recorded with the approver's identity and timestamp.
- **External Payout Verification:** When initiating an external payout via the Switching Server, the merchant's payout bank details (account number, bank code) must be verified during merchant onboarding (STORY-050). Changes to payout details require re-verification and a cooling-off period.
- **Reconciliation:** Every settlement must be reconcilable. The `settlement_line_items` table links each settlement to its constituent transactions, enabling full audit trail.
- **Fraud Prevention:** Monitor for anomalous settlement patterns: unusually high settlement amounts, frequent changes to payout accounts, settlements triggered outside normal schedule. Flag for review.
- **Idempotency:** The settlement job must be idempotent. Running it twice for the same period and merchant should produce the same result (not double-settle). Check for existing settlement records before creating new ones.

### Edge Cases

- **Zero Transactions:** If a merchant has zero completed transactions in the settlement period, skip settlement for that merchant (do not create a zero-amount record).
- **Negative Net Settlement:** If fees exceed gross sales (rare but possible for low-volume merchants with fixed fees), create a settlement record with status "flagged" and net_amount < 0. Do not initiate a payout. Alert operations.
- **External Payout Failure:** If the Switching Server returns a decline for the external payout, mark the settlement as "payout_failed" with the decline reason. Retry once after 1 hour. If still failing, flag for manual intervention.
- **Internal Payout Failure:** If the internal transfer fails (e.g., settlement pool insufficient funds), this is a critical system error. Alert operations immediately. Do not mark the settlement as paid.
- **Merchant Deactivated Mid-Period:** If a merchant is deactivated during the settlement period, still process the final settlement for transactions already completed. Mark it as the final settlement.
- **Settlement Job Failure:** If the settlement job crashes partway through (e.g., database outage), it must be resumable. Use the settlement record status to track progress. On restart, skip merchants already settled for the period and continue with remaining merchants.
- **Concurrent Settlement:** If the scheduled job and a manual trigger fire simultaneously for the same merchant, use a database advisory lock or unique constraint on (merchant_id, period_start, period_end) to prevent double settlement.
- **Timezone Handling:** Settlement periods are defined in the merchant's local timezone (or the tenant's configured timezone). Ensure correct conversion to UTC for database queries.
- **Minimum Payout Amount:** If the net settlement is below the `min_payout_amount` (e.g., ZAR 10), carry it over to the next settlement period. Do not initiate micro-payouts that cost more in fees than they are worth.

---

## Dependencies

**Prerequisite Stories:**
- STORY-050: Merchant Registration & KYC — merchant records and payout configuration must exist
- STORY-034: Agent Cash-In / Cash-Out — agent commission transactions feed into settlement calculations

**Blocked Stories:**
- None directly. Settlement is a downstream process.

**External Dependencies:**
- Switching Server (STORY-043) for external payouts to non-GoldBank bank accounts
- Notification service for settlement notifications to merchants
- Scheduler infrastructure (Wolverine scheduled commands or Hangfire)

---

## Definition of Done

- [ ] Code implemented and committed
- [ ] Unit tests written and passing (>=80% coverage) — settlement calculator tested with various fee/commission scenarios
- [ ] Integration tests passing with mock switching service for external payouts
- [ ] Daily settlement schedule tested: runs at configured time, aggregates correctly
- [ ] Weekly settlement schedule tested: runs on correct day, covers correct period
- [ ] Internal payout tested: settlement pool debited, merchant account credited
- [ ] External payout tested: `RouteOutboundTransaction` published with correct details
- [ ] Approval workflow tested: settlements above threshold queued, approved, then paid
- [ ] Notification tested: merchant receives settlement and payout notifications
- [ ] Idempotency tested: duplicate runs do not double-settle
- [ ] Code reviewed and approved
- [ ] Documentation updated (settlement schedule, fee calculation, payout flow)
- [ ] Acceptance criteria validated
- [ ] Deployed to staging

---

## Progress Tracking

**Status History:**
- 2026-02-24: Created

**Actual Effort:** TBD

---

**This story was created using BMAD Method v6 - Phase 4 (Implementation)**
