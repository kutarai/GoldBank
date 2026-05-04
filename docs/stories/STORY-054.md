# STORY-054: Merchant Commission Reporting

**Epic:** EPIC-010 Merchant Management
**Priority:** Must Have
**Story Points:** 3
**Status:** Not Started
**Assigned To:** Unassigned
**Created:** 2026-02-24
**Sprint:** 5

---

## User Story

As a **merchant agent**
I want to **see my commission earnings**
So that **I can track income from agent services and plan my business**

---

## Description

### Background

In GoldBank's agent banking model, merchants in Southern Africa serve as human ATMs — they perform cash-in (deposit) and cash-out (withdrawal) services for GoldBank customers. For each agent transaction, the merchant earns a commission. This commission is a key income stream for agents and a primary incentive for merchants to join the GoldBank network.

Cash-in commissions compensate the agent for accepting cash from a customer and crediting their GoldBank account. Cash-out commissions compensate the agent for dispensing cash to a customer who debits their GoldBank account. Commission rates may differ by transaction type, amount tier, and agent level (standard vs. premium agent).

Agents need clear, detailed reporting on their commission earnings. They need to see how much they earned today, this week, this month. They need breakdowns by transaction type (how much from cash-in vs. cash-out). And they need to be able to export this data for their own bookkeeping, tax filings, or business planning.

This story builds the commission reporting capability. The commission transactions themselves are created by the agent cash-in/cash-out flow (STORY-034). This story provides the read-side: querying, aggregating, summarizing, and exporting commission data.

**Functional Requirements:** FR-041 (Merchant Commission Reporting)

### Scope

**In scope:**
- Commission report query via `AgentService.GetCommissionReport` gRPC endpoint
- Breakdown by commission type (cash-in, cash-out)
- Running total and period summaries (daily, weekly, monthly)
- Commission detail: individual commission transactions with amount, rate, linked transaction
- Aggregated views: total commission, average per transaction, by type
- Export as CSV via gRPC streaming
- Date range filtering
- Commission rate visibility (what rate was applied to each transaction)

**Out of scope:**
- Commission rate configuration and management (admin function, separate story)
- Commission calculation logic (handled by STORY-034 during cash-in/cash-out)
- Commission payout (handled by STORY-052 as part of merchant settlement)
- Commission disputes or adjustments
- Multi-currency commission reporting

### User Flow

**Commission Report Flow:**

1. Merchant agent logs into the GoldBank merchant app
2. Agent navigates to "Commission Earnings" section
3. App calls `AgentService.GetCommissionReport` with the agent's merchant ID and date range
4. Server returns the commission summary and detailed breakdown
5. Agent sees:
   - **Summary Card:** Total commission earned for the period, transaction count
   - **Type Breakdown:** Cash-in commissions (count, total) vs. Cash-out commissions (count, total)
   - **Period View:** Toggle between daily, weekly, monthly views
   - **Trend:** Running total over the selected period
6. Agent can drill into details to see individual commission transactions:
   - Date/time of the original cash-in or cash-out transaction
   - Transaction amount (the customer's cash-in or cash-out amount)
   - Commission rate applied (e.g., 1.5%)
   - Commission earned (e.g., ZAR 15.00)
   - Transaction reference
7. Agent can export the report:
   - Clicks "Export" button
   - App calls `AgentService.ExportCommissions` gRPC streaming endpoint
   - Receives CSV-formatted rows for all commission transactions in the period
   - Saves or shares the file

**Period Summary Flow:**

1. Agent selects "Monthly Summary" from the period selector
2. App calls `AgentService.GetCommissionSummary` with monthly granularity
3. Server returns month-by-month summary for the last 12 months
4. Agent sees a list of months with total commission per month
5. Agent taps a month to see the detailed breakdown for that month

---

## Acceptance Criteria

- [ ] Agent can view commission earnings via `GetCommissionReport` gRPC endpoint
- [ ] Commission is displayed separately from regular transaction amounts
- [ ] Breakdown by type is provided: cash-in commissions vs. cash-out commissions with counts and totals
- [ ] Running total for the selected period is calculated and returned
- [ ] Period summaries are available: daily totals, weekly totals, monthly totals
- [ ] Individual commission transactions show: date/time, original transaction amount, commission rate, commission amount, transaction reference
- [ ] Date range filtering is supported (from_date, to_date)
- [ ] Export as CSV is available via gRPC streaming (`ExportCommissions` endpoint)
- [ ] Exported CSV includes: date, time, type, transaction_amount, commission_rate, commission_amount, reference
- [ ] Monthly summary returns data for up to 12 months of history
- [ ] Commission totals are consistent with individual commission transaction sums (no rounding drift)
- [ ] Agent can only view their own commission data (authorization enforced)

---

## Technical Notes

### Components

| Component | Location | Purpose |
|-----------|----------|---------|
| `AgentCommissionService.cs` | `src/Core/GoldBank.Core/Modules/Agents/Application/Services/` | Commission report query and aggregation |
| `CommissionReportBuilder.cs` | `src/Core/GoldBank.Core/Modules/Agents/Application/Services/` | Builds commission report with breakdowns |
| `CommissionExporter.cs` | `src/Core/GoldBank.Core/Modules/Agents/Application/Services/` | CSV export via streaming |
| `AgentGrpcService.cs` | `src/Core/GoldBank.Core/Modules/Agents/Grpc/` | gRPC service implementation |
| `CommissionTransaction.cs` | `src/Core/GoldBank.Core/Modules/Agents/Domain/Entities/` | Commission transaction entity |

### API / gRPC Endpoints

**Agent Commission gRPC Service:**

```protobuf
service AgentService {
  // Get commission report with summary and breakdown
  rpc GetCommissionReport (GetCommissionReportRequest) returns (CommissionReportResponse);

  // Get period summaries (daily/weekly/monthly totals)
  rpc GetCommissionSummary (GetCommissionSummaryRequest) returns (stream CommissionPeriodSummary);

  // Get individual commission transactions
  rpc GetCommissionTransactions (GetCommissionTransactionsRequest) returns (stream CommissionTransactionDetail);

  // Export commissions as CSV via streaming
  rpc ExportCommissions (ExportCommissionsRequest) returns (stream CommissionExportRow);
}

message GetCommissionReportRequest {
  string tenant_id = 1;
  string agent_id = 2;         // merchant/agent ID
  string from_date = 3;        // ISO 8601
  string to_date = 4;          // ISO 8601
}

message CommissionReportResponse {
  string agent_id = 1;
  string period_start = 2;
  string period_end = 3;

  // Overall summary
  int32 total_transactions = 4;
  string total_commission = 5;
  string average_commission = 6;
  string currency = 7;

  // Breakdown by type
  CommissionTypeBreakdown cash_in = 8;
  CommissionTypeBreakdown cash_out = 9;

  // Daily running totals for the period
  repeated DailyCommissionTotal daily_totals = 10;
}

message CommissionTypeBreakdown {
  string type = 1;              // cash_in, cash_out
  int32 transaction_count = 2;
  string total_commission = 3;
  string average_commission = 4;
  string total_transaction_volume = 5;  // sum of underlying transaction amounts
}

message DailyCommissionTotal {
  string date = 1;              // ISO 8601 date
  int32 transaction_count = 2;
  string total_commission = 3;
  string cash_in_commission = 4;
  string cash_out_commission = 5;
}

message GetCommissionSummaryRequest {
  string tenant_id = 1;
  string agent_id = 2;
  string granularity = 3;       // daily, weekly, monthly
  int32 periods = 4;            // number of periods to return (default 12)
}

message CommissionPeriodSummary {
  string period_label = 1;      // "2026-02-24" or "2026-W09" or "2026-02"
  string period_start = 2;
  string period_end = 3;
  int32 transaction_count = 4;
  string total_commission = 5;
  string cash_in_commission = 6;
  string cash_out_commission = 7;
  string currency = 8;
}

message GetCommissionTransactionsRequest {
  string tenant_id = 1;
  string agent_id = 2;
  string from_date = 3;
  string to_date = 4;
  string commission_type = 5;   // cash_in, cash_out, all
  int32 page_size = 6;          // default 50
  string page_token = 7;
}

message CommissionTransactionDetail {
  string commission_id = 1;
  string transaction_id = 2;
  string timestamp = 3;
  string commission_type = 4;   // cash_in, cash_out
  string transaction_amount = 5; // the original cash-in/cash-out amount
  string commission_rate = 6;   // e.g., "0.015" for 1.5%
  string commission_amount = 7;
  string currency = 8;
  string transaction_reference = 9;
  string customer_reference = 10; // masked
}

message ExportCommissionsRequest {
  string tenant_id = 1;
  string agent_id = 2;
  string from_date = 3;
  string to_date = 4;
}

message CommissionExportRow {
  string date = 1;
  string time = 2;
  string type = 3;
  string transaction_amount = 4;
  string commission_rate = 5;
  string commission_amount = 6;
  string reference = 7;
  string running_total = 8;
}
```

**Commission Report Builder (pseudocode):**

```csharp
public class CommissionReportBuilder
{
    public async Task<CommissionReportResponse> BuildReport(
        string agentId,
        DateTimeOffset fromDate,
        DateTimeOffset toDate,
        CancellationToken ct)
    {
        var commissions = await _dbContext.CommissionTransactions
            .Where(c => c.AgentId == agentId)
            .Where(c => c.CreatedAt >= fromDate && c.CreatedAt <= toDate)
            .ToListAsync(ct);

        var cashInCommissions = commissions.Where(c => c.Type == "cash_in").ToList();
        var cashOutCommissions = commissions.Where(c => c.Type == "cash_out").ToList();

        var totalCommission = commissions.Sum(c => c.CommissionAmount);
        var avgCommission = commissions.Count > 0
            ? totalCommission / commissions.Count
            : 0m;

        // Build daily totals
        var dailyTotals = commissions
            .GroupBy(c => c.CreatedAt.Date)
            .OrderBy(g => g.Key)
            .Select(g => new DailyCommissionTotal
            {
                Date = g.Key.ToString("yyyy-MM-dd"),
                TransactionCount = g.Count(),
                TotalCommission = g.Sum(c => c.CommissionAmount).ToString("F2"),
                CashInCommission = g.Where(c => c.Type == "cash_in")
                    .Sum(c => c.CommissionAmount).ToString("F2"),
                CashOutCommission = g.Where(c => c.Type == "cash_out")
                    .Sum(c => c.CommissionAmount).ToString("F2")
            })
            .ToList();

        return new CommissionReportResponse
        {
            AgentId = agentId,
            PeriodStart = fromDate.ToString("o"),
            PeriodEnd = toDate.ToString("o"),
            TotalTransactions = commissions.Count,
            TotalCommission = totalCommission.ToString("F2"),
            AverageCommission = avgCommission.ToString("F2"),
            CashIn = BuildTypeBreakdown("cash_in", cashInCommissions),
            CashOut = BuildTypeBreakdown("cash_out", cashOutCommissions),
            DailyTotals = { dailyTotals }
        };
    }
}
```

**CSV Export Streaming (pseudocode):**

```csharp
public override async Task ExportCommissions(
    ExportCommissionsRequest request,
    IServerStreamWriter<CommissionExportRow> responseStream,
    ServerCallContext context)
{
    var query = _dbContext.CommissionTransactions
        .Where(c => c.AgentId == request.AgentId)
        .Where(c => c.TenantId == request.TenantId)
        .Where(c => c.CreatedAt >= DateTimeOffset.Parse(request.FromDate))
        .Where(c => c.CreatedAt <= DateTimeOffset.Parse(request.ToDate))
        .OrderBy(c => c.CreatedAt);

    decimal runningTotal = 0m;

    await foreach (var commission in query.AsAsyncEnumerable()
        .WithCancellation(context.CancellationToken))
    {
        runningTotal += commission.CommissionAmount;

        await responseStream.WriteAsync(new CommissionExportRow
        {
            Date = commission.CreatedAt.ToString("yyyy-MM-dd"),
            Time = commission.CreatedAt.ToString("HH:mm:ss"),
            Type = commission.Type,
            TransactionAmount = commission.TransactionAmount.ToString("F2"),
            CommissionRate = commission.CommissionRate.ToString("F4"),
            CommissionAmount = commission.CommissionAmount.ToString("F2"),
            Reference = commission.TransactionReference,
            RunningTotal = runningTotal.ToString("F2")
        });
    }
}
```

### Database Changes

**commission_transactions table** (created by STORY-034, referenced here for querying):

```sql
-- This table is created by STORY-034 (Agent Cash-In / Cash-Out).
-- Documented here for reference by the reporting queries.
CREATE TABLE commission_transactions (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    agent_id            UUID NOT NULL REFERENCES merchants(id),
    transaction_id      UUID NOT NULL,
    type                VARCHAR(10) NOT NULL CHECK (type IN ('cash_in', 'cash_out')),
    transaction_amount  DECIMAL(18, 2) NOT NULL,
    commission_rate     DECIMAL(8, 6) NOT NULL,
    commission_amount   DECIMAL(18, 2) NOT NULL,
    currency            VARCHAR(3) NOT NULL,
    transaction_reference VARCHAR(50),
    customer_reference  VARCHAR(50),
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- Indexes optimized for commission reporting queries
CREATE INDEX idx_commission_txn_agent_date ON commission_transactions (agent_id, created_at DESC);
CREATE INDEX idx_commission_txn_type ON commission_transactions (agent_id, type, created_at DESC);
CREATE INDEX idx_commission_txn_date ON commission_transactions (created_at DESC);
```

**commission_summaries table** (pre-computed summaries for performance):

```sql
CREATE TABLE commission_summaries (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    agent_id        UUID NOT NULL REFERENCES merchants(id),
    period_type     VARCHAR(10) NOT NULL CHECK (period_type IN ('daily', 'weekly', 'monthly')),
    period_start    DATE NOT NULL,
    period_end      DATE NOT NULL,
    total_count     INT NOT NULL DEFAULT 0,
    cash_in_count   INT NOT NULL DEFAULT 0,
    cash_out_count  INT NOT NULL DEFAULT 0,
    total_commission     DECIMAL(18, 2) NOT NULL DEFAULT 0,
    cash_in_commission   DECIMAL(18, 2) NOT NULL DEFAULT 0,
    cash_out_commission  DECIMAL(18, 2) NOT NULL DEFAULT 0,
    total_transaction_volume DECIMAL(18, 2) NOT NULL DEFAULT 0,
    currency        VARCHAR(3) NOT NULL,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (agent_id, period_type, period_start)
);

CREATE INDEX idx_commission_summary_agent ON commission_summaries (agent_id, period_type, period_start DESC);
```

### Security Considerations

- **Agent Authorization:** The `GetCommissionReport` and related endpoints must verify that the authenticated user is the agent themselves or an admin with agent-view permissions. An agent must not be able to view another agent's commission data.
- **Tenant Isolation:** All queries must include the `tenant_id` filter. Commission data is stored in the tenant-specific schema, providing an additional layer of isolation.
- **Customer Data Masking:** The `customer_reference` field in commission transaction details must be masked (last 4 digits only) to protect customer identity. Agents should know that a transaction occurred, but not the full details of the customer involved.
- **Export Rate Limiting:** CSV export can produce large datasets for high-volume agents. Rate limit to 10 exports per hour per agent to prevent abuse and excessive database load.
- **Decimal Precision:** Commission amounts use DECIMAL(18,2) for storage and must be formatted with exactly 2 decimal places in all responses. Running totals must accumulate without floating-point drift — use decimal arithmetic throughout.
- **Data Retention:** Commission transaction data follows the same retention policy as transaction data (7 years per regulatory requirements). Pre-computed summaries can be retained indefinitely as they contain no personally identifiable information.

### Edge Cases

- **New Agent (No Commissions):** If an agent has zero commission transactions for the requested period, return a report with all zero values rather than an error. This confirms the query executed successfully.
- **Agent With Only One Type:** If an agent only performs cash-in (no cash-out), the cash-out breakdown should show zero values. Do not omit the breakdown.
- **Very Large Date Range:** If an agent requests a report for a full year, the daily_totals array could have 365+ entries. Cap the daily totals to the most recent 90 days and suggest the agent use monthly granularity for longer periods.
- **Commission Rate Changes:** Commission rates may change over time. Each commission transaction records the rate that was applied at the time of the transaction. Reports must use the stored rate, not the current rate. This ensures historical accuracy.
- **Concurrent Commission Creation:** If a cash-in/cash-out transaction is being processed while a report is being generated, the report may or may not include it depending on transaction isolation level. Use READ COMMITTED isolation — the report will include only committed transactions, which is the correct behavior.
- **Timezone in Daily Totals:** Daily totals should be grouped by the agent's local date (tenant timezone), not UTC date. A commission at 23:30 UTC might be the next day locally. Use the tenant timezone configuration for date grouping.
- **Running Total Accuracy:** The running total in the CSV export must be computed as a cumulative sum of commission amounts ordered by timestamp. If there are rounding issues at the individual transaction level (rare with decimal storage), the running total will still be accurate as it sums stored values.
- **Pre-Computed Summary Staleness:** The `commission_summaries` table is updated by a scheduled job (runs after settlement). For the current day, the summary may not yet be computed. Fall back to a live query against `commission_transactions` for the current day's data.

---

## Dependencies

**Prerequisite Stories:**
- STORY-034: Agent Cash-In / Cash-Out — creates the `commission_transactions` records that this story reports on

**Blocked Stories:**
- None directly. Commission reporting is a read-only view.

**External Dependencies:**
- None. All data is sourced from GoldBank's own database.

---

## Definition of Done

- [ ] Code implemented and committed
- [ ] Unit tests written and passing (>=80% coverage) — report builder, aggregation, CSV export
- [ ] Integration tests passing with test commission data
- [ ] Commission breakdown by type (cash-in vs. cash-out) verified
- [ ] Period summaries (daily, weekly, monthly) verified with test data
- [ ] Running totals verified for accuracy (no floating-point drift)
- [ ] CSV export tested: correct format, streaming delivery, running total column
- [ ] Agent authorization tested: agent can only see their own data
- [ ] Customer data masking verified in responses
- [ ] Empty report (new agent, no commissions) handled gracefully
- [ ] Code reviewed and approved
- [ ] Documentation updated (gRPC API, report format, export format)
- [ ] Acceptance criteria validated
- [ ] Deployed to staging

---

## Progress Tracking

**Status History:**
- 2026-02-24: Created

**Actual Effort:** TBD

---

**This story was created using BMAD Method v6 - Phase 4 (Implementation)**
