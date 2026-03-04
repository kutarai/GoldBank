# STORY-053: Merchant Transaction History & Statements

**Epic:** EPIC-010 Merchant Management
**Priority:** Must Have
**Story Points:** 3
**Status:** Not Started
**Assigned To:** Unassigned
**Created:** 2026-02-24
**Sprint:** 5

---

## User Story

As a **merchant**
I want to **view my transaction history and statements**
So that **I can track business performance and reconcile my records**

---

## Description

### Background

Merchants need visibility into their transaction activity. A corner shop owner accepting UniBank payments needs to see which payments were received today, what fees were charged, and what the running total looks like. At the end of the month, they need a statement summarizing all activity — total sales, total fees, net revenue — for their own bookkeeping and potentially for tax purposes.

This story provides merchants with real-time access to their transaction history and the ability to generate periodic statements. The transaction history is queryable in real time via server-streaming gRPC (for efficient delivery of potentially large result sets). Statements are pre-generated summaries stored for retrieval, covering daily or monthly periods with aggregated totals.

For UniBank's target market — informal merchants in Southern Africa — this visibility is often the first time they have had structured financial records for their business. It is a significant step toward financial inclusion and business formalization.

**Functional Requirements:** FR-040 (Merchant Transaction History & Statements)

### Scope

**In scope:**
- Real-time transaction history query with filtering (date range, transaction type, status, terminal)
- Server-streaming gRPC endpoint for efficient large result set delivery
- Daily statement generation (automated, runs after daily settlement)
- Monthly statement generation (automated, runs on 1st of each month)
- Statement content: transaction count, gross volume, total fees, net amount, breakdown by transaction type
- Statement storage in the database for retrieval
- Statement export as structured data via gRPC response
- Pagination support for transaction history
- Terminal-level filtering (merchants may have multiple terminals)

**Out of scope:**
- PDF or printable statement generation (future enhancement)
- Real-time push notifications for individual transactions (handled by Notifications module)
- Statement delivery via email or SMS (future enhancement)
- Comparative analytics (e.g., this month vs. last month — future enhancement)
- Chargeback or dispute visibility (future enhancement)

### User Flow

**Transaction History Flow:**

1. Merchant logs in via the UniBank merchant app or portal
2. Merchant navigates to "Transaction History"
3. App calls `MerchantService.GetTransactions` gRPC endpoint with merchant ID and optional filters
4. Server streams transaction records back to the client, sorted by most recent first
5. Merchant can filter by:
   - Date range (from/to)
   - Transaction type (purchase, refund, cash-in, cash-out)
   - Status (completed, pending, declined, reversed)
   - Terminal ID (if merchant has multiple terminals)
6. Each transaction shows: date/time, type, amount, fee, net, status, reference number, customer reference (masked)
7. Running totals are displayed: total transactions, total gross, total fees, total net for the filtered period

**Statement Flow:**

1. Merchant navigates to "Statements" in the app
2. App calls `MerchantService.GetStatements` gRPC endpoint with merchant ID
3. Server returns a list of available statements (daily and monthly)
4. Merchant selects a statement to view
5. App calls `MerchantService.GetStatementDetail` to retrieve the full statement
6. Statement shows:
   - Period covered (date range)
   - Opening balance (carryover from previous period, if applicable)
   - Transaction summary: count and volume by type
   - Fee summary: total fees, breakdown by fee type
   - Net settlement amount
   - Payout reference (links to settlement from STORY-052)
7. Merchant can export the statement data (via gRPC streaming for integration with accounting tools)

---

## Acceptance Criteria

- [ ] Merchant can view all transactions at their terminal(s) via `GetTransactions` gRPC server-streaming endpoint
- [ ] Transaction history supports filtering by date range, transaction type, status, and terminal ID
- [ ] Transaction records include: timestamp, type, amount, fee amount, net amount, status, reference, customer reference (masked)
- [ ] Transaction history supports pagination (page size and page token)
- [ ] Daily statements are generated automatically after daily settlement completes
- [ ] Monthly statements are generated automatically on the 1st of each month for the preceding month
- [ ] Statements include: transaction count, gross volume, total fees, net amount, breakdown by transaction type
- [ ] Statements are stored in `merchant_statements` table and retrievable via gRPC
- [ ] Statement data is exportable via gRPC streaming response
- [ ] Totals in statements match the sum of individual transactions for the period (consistency check)
- [ ] Filtering by terminal ID correctly scopes results to the selected terminal only
- [ ] Empty periods (no transactions) produce a statement with zero totals rather than no statement

---

## Technical Notes

### Components

| Component | Location | Purpose |
|-----------|----------|---------|
| `MerchantTransactionService.cs` | `src/Core/UniBank.Core/Modules/Merchants/Application/Services/` | Transaction history query logic |
| `MerchantStatementService.cs` | `src/Core/UniBank.Core/Modules/Merchants/Application/Services/` | Statement generation and retrieval |
| `StatementGenerator.cs` | `src/Core/UniBank.Core/Modules/Merchants/Application/Services/` | Calculates statement aggregates |
| `MerchantGrpcService.cs` | `src/Core/UniBank.Core/Modules/Merchants/Grpc/` | gRPC service implementation |
| `MerchantStatement.cs` | `src/Core/UniBank.Core/Modules/Merchants/Domain/Entities/` | Statement domain entity |
| `StatementGenerationJob.cs` | `src/Core/UniBank.Core/Modules/Merchants/Jobs/` | Scheduled job for statement generation |

### API / gRPC Endpoints

**Merchant Service gRPC (transaction history and statements):**

```protobuf
service MerchantService {
  // Server-streaming: returns merchant transactions with filters
  rpc GetTransactions (GetTransactionsRequest) returns (stream MerchantTransaction);

  // Get transaction summary totals for a period
  rpc GetTransactionSummary (GetTransactionSummaryRequest) returns (TransactionSummaryResponse);

  // List available statements for a merchant
  rpc GetStatements (GetStatementsRequest) returns (stream MerchantStatementSummary);

  // Get detailed statement
  rpc GetStatementDetail (GetStatementDetailRequest) returns (MerchantStatementDetail);

  // Export statement data as streaming records
  rpc ExportStatement (ExportStatementRequest) returns (stream StatementExportRow);
}

message GetTransactionsRequest {
  string tenant_id = 1;
  string merchant_id = 2;
  string from_date = 3;          // ISO 8601
  string to_date = 4;            // ISO 8601
  string transaction_type = 5;   // purchase, refund, cash_in, cash_out, all
  string status = 6;             // completed, pending, declined, reversed, all
  string terminal_id = 7;        // optional, filter by specific terminal
  int32 page_size = 8;           // default 50, max 200
  string page_token = 9;         // opaque cursor for pagination
}

message MerchantTransaction {
  string transaction_id = 1;
  string timestamp = 2;
  string transaction_type = 3;
  string amount = 4;
  string fee_amount = 5;
  string net_amount = 6;
  string currency = 7;
  string status = 8;
  string reference = 9;
  string customer_reference = 10; // masked: "****1234"
  string terminal_id = 11;
  string description = 12;
}

message GetTransactionSummaryRequest {
  string tenant_id = 1;
  string merchant_id = 2;
  string from_date = 3;
  string to_date = 4;
  string terminal_id = 5;
}

message TransactionSummaryResponse {
  int32 total_count = 1;
  string gross_volume = 2;
  string total_fees = 3;
  string net_volume = 4;
  string currency = 5;
  repeated TypeBreakdown by_type = 6;
  repeated StatusBreakdown by_status = 7;
}

message TypeBreakdown {
  string transaction_type = 1;
  int32 count = 2;
  string amount = 3;
}

message StatusBreakdown {
  string status = 1;
  int32 count = 2;
}

message GetStatementsRequest {
  string tenant_id = 1;
  string merchant_id = 2;
  string statement_type = 3;     // daily, monthly, all
  int32 limit = 4;               // default 30
}

message MerchantStatementSummary {
  string statement_id = 1;
  string statement_type = 2;     // daily, monthly
  string period_start = 3;
  string period_end = 4;
  int32 transaction_count = 5;
  string gross_volume = 6;
  string total_fees = 7;
  string net_amount = 8;
  string currency = 9;
  string generated_at = 10;
}

message MerchantStatementDetail {
  MerchantStatementSummary summary = 1;
  repeated TypeBreakdown transaction_breakdown = 2;
  repeated FeeBreakdown fee_breakdown = 3;
  string settlement_id = 4;
  string payout_reference = 5;
  string payout_status = 6;
}

message FeeBreakdown {
  string fee_type = 1;
  int32 transaction_count = 2;
  string total_fee_amount = 3;
}

message ExportStatementRequest {
  string tenant_id = 1;
  string statement_id = 2;
  string format = 3;             // csv, json
}

message StatementExportRow {
  string date = 1;
  string time = 2;
  string transaction_type = 3;
  string reference = 4;
  string amount = 5;
  string fee = 6;
  string net = 7;
  string status = 8;
  string terminal_id = 9;
}
```

**Server-Streaming Transaction Query (pseudocode):**

```csharp
public override async Task GetTransactions(
    GetTransactionsRequest request,
    IServerStreamWriter<MerchantTransaction> responseStream,
    ServerCallContext context)
{
    var query = _dbContext.Transactions
        .Where(t => t.MerchantId == request.MerchantId)
        .Where(t => t.TenantId == request.TenantId);

    // Apply filters
    if (!string.IsNullOrEmpty(request.FromDate))
        query = query.Where(t => t.CreatedAt >= DateTimeOffset.Parse(request.FromDate));
    if (!string.IsNullOrEmpty(request.ToDate))
        query = query.Where(t => t.CreatedAt <= DateTimeOffset.Parse(request.ToDate));
    if (request.TransactionType != "all" && !string.IsNullOrEmpty(request.TransactionType))
        query = query.Where(t => t.Type == request.TransactionType);
    if (request.Status != "all" && !string.IsNullOrEmpty(request.Status))
        query = query.Where(t => t.Status == request.Status);
    if (!string.IsNullOrEmpty(request.TerminalId))
        query = query.Where(t => t.TerminalId == request.TerminalId);

    // Pagination
    query = query.OrderByDescending(t => t.CreatedAt);
    if (!string.IsNullOrEmpty(request.PageToken))
        query = query.Where(t => t.CreatedAt < DecodeCursor(request.PageToken));
    query = query.Take(request.PageSize > 0 ? request.PageSize : 50);

    // Stream results
    await foreach (var txn in query.AsAsyncEnumerable()
        .WithCancellation(context.CancellationToken))
    {
        await responseStream.WriteAsync(MapToGrpc(txn));
    }
}
```

### Database Changes

**merchant_statements table:**

```sql
CREATE TABLE merchant_statements (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    merchant_id         UUID NOT NULL REFERENCES merchants(id),
    statement_type      VARCHAR(10) NOT NULL CHECK (statement_type IN ('daily', 'monthly')),
    period_start        TIMESTAMPTZ NOT NULL,
    period_end          TIMESTAMPTZ NOT NULL,
    transaction_count   INT NOT NULL DEFAULT 0,
    gross_volume        DECIMAL(18, 2) NOT NULL DEFAULT 0,
    total_fees          DECIMAL(18, 2) NOT NULL DEFAULT 0,
    net_amount          DECIMAL(18, 2) NOT NULL DEFAULT 0,
    currency            VARCHAR(3) NOT NULL,
    type_breakdown      JSONB NOT NULL DEFAULT '{}',
    fee_breakdown       JSONB NOT NULL DEFAULT '{}',
    settlement_id       UUID REFERENCES settlements(id),
    generated_at        TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (merchant_id, statement_type, period_start)
);

CREATE INDEX idx_merchant_statements_merchant ON merchant_statements (merchant_id, period_start DESC);
CREATE INDEX idx_merchant_statements_type ON merchant_statements (statement_type, period_start DESC);
CREATE INDEX idx_merchant_statements_settlement ON merchant_statements (settlement_id);
```

### Security Considerations

- **Merchant Authorization:** The `GetTransactions` and statement endpoints must verify that the authenticated user is either the merchant themselves or an admin with merchant-view permissions. A merchant must not be able to view another merchant's transactions.
- **Customer Data Masking:** Customer references (PAN, account numbers) in transaction records must be masked before being returned to the merchant. Show only the last 4 digits: `****1234`. This protects customer privacy per POPIA (Protection of Personal Information Act).
- **Tenant Isolation:** All queries must include the `tenant_id` filter to ensure schema-per-tenant isolation is enforced at the application level as well.
- **Export Rate Limiting:** Statement export (server streaming) can produce large datasets. Rate limit export requests per merchant to prevent abuse (max 10 exports per hour).
- **Statement Integrity:** Once generated, statements should be immutable. If transactions are later adjusted (e.g., chargeback), a new corrected statement is generated rather than modifying the original.

### Edge Cases

- **Large Transaction Volume:** High-volume merchants may have thousands of transactions per day. Server streaming prevents memory issues, but database queries must be efficient. Use cursor-based pagination (not OFFSET) and ensure indexes on (merchant_id, created_at).
- **No Transactions in Period:** If a merchant has no transactions in a statement period, generate a zero-total statement rather than skipping it. This confirms the system is working and the period was accounted for.
- **Terminal Decommissioned:** If a merchant's terminal was decommissioned during the period, historical transactions for that terminal must still be queryable. Do not filter out transactions from decommissioned terminals.
- **Timezone Display:** Transactions are stored in UTC. When displaying to the merchant, convert to the merchant's configured timezone (or the tenant's timezone). The gRPC response should include UTC timestamps; the client app handles timezone conversion for display.
- **Concurrent Statement Generation:** If the daily statement job and a manual statement request fire for the same period, the unique constraint on (merchant_id, statement_type, period_start) prevents duplicates. The second attempt should detect the existing statement and return it.
- **Statement-Settlement Mismatch:** If the statement totals do not match the settlement totals (due to late-arriving transactions or corrections), flag the discrepancy for review. Both should be based on the same transaction set, but timing differences may cause drift.
- **Page Token Expiry:** Cursor-based pagination tokens encode the last-seen timestamp. If the merchant pauses for a long time between pages, new transactions may have arrived. The cursor is still valid — it returns the next page after the cursor point, which may include recently inserted records with older timestamps (e.g., late-arriving settlement adjustments). This is acceptable.

---

## Dependencies

**Prerequisite Stories:**
- STORY-050: Merchant Registration & KYC — merchant records must exist for transaction querying
- STORY-052: Merchant Settlement & Payout — settlement data feeds into statements (settlement references, payout status)

**Blocked Stories:**
- None directly. Transaction history and statements are read-only views.

**External Dependencies:**
- None. All data is sourced from UniBank's own database.

---

## Definition of Done

- [ ] Code implemented and committed
- [ ] Unit tests written and passing (>=80% coverage) — statement generator, query filters, pagination
- [ ] Integration tests passing with test transaction data and statement generation
- [ ] Server-streaming verified: large result sets stream efficiently without memory issues
- [ ] All filters tested: date range, transaction type, status, terminal ID
- [ ] Pagination tested: cursor-based, handles edge cases (empty pages, last page)
- [ ] Daily and monthly statement generation tested
- [ ] Statement totals verified against individual transaction sums
- [ ] Customer data masking verified in responses
- [ ] Code reviewed and approved
- [ ] Documentation updated (gRPC API, statement format, filter options)
- [ ] Acceptance criteria validated
- [ ] Deployed to staging

---

## Progress Tracking

**Status History:**
- 2026-02-24: Created

**Actual Effort:** TBD

---

**This story was created using BMAD Method v6 - Phase 4 (Implementation)**
