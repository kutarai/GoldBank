# STORY-017: Transaction History with Streaming

**Epic:** EPIC-002
**Priority:** Must Have
**Story Points:** 5
**Status:** Not Started
**Assigned To:** Unassigned
**Created:** 2026-02-24
**Sprint:** 2

---

## User Story

As a user
I want to view my transaction history
So that I can track my spending

---

## Description

### Background
Transaction history is a core feature that enables users to track their financial activity, verify incoming payments, reconcile spending, and detect unauthorized transactions. For UniBank's target users in Southern Africa, many of whom are transitioning from cash-only economies, having a clear, accessible record of digital transactions builds confidence in the platform and supports financial literacy.

This story implements the transaction history endpoint using gRPC server streaming, which is ideal for delivering potentially large result sets efficiently. Rather than loading all transactions into memory at once, the server streams transaction records to the client as they are read from the database. This approach is memory-efficient for both server and client, and provides a better user experience -- the app can begin rendering results immediately as they arrive.

The underlying `transactions` table is partitioned by date for query performance, which is critical as transaction volumes grow. Cursor-based pagination via streaming ensures consistent results even as new transactions are added during pagination.

### Scope
**In scope:**
- `GetTransactions` gRPC server streaming endpoint
- Cursor-based streaming pagination (using transaction ID or timestamp as cursor)
- Sorting by date (newest first by default)
- Filtering by transaction type (deposit, withdrawal, transfer, payment, etc.)
- Filtering by date range
- Minimum 90 days of transaction history accessible
- Each transaction record includes: date, type, amount, currency, recipient/sender, status, reference
- Efficient query using partitioned transactions table with proper indexing

**Out of scope:**
- Transaction search by keyword or recipient name
- Transaction export (CSV, PDF) -- future story
- Transaction dispute or reversal initiation from history
- Real-time transaction notifications (separate push notification feature)
- Transaction categorization or tagging
- Analytics or spending summaries

### User Flow
1. User navigates to "Transaction History" from the home screen or bottom navigation
2. App opens a gRPC server stream to `AccountService.GetTransactions`
3. App sends the request with optional filters (type, date range) and page size
4. Server begins streaming transaction records, newest first
5. App renders transactions as they arrive in a scrollable list
6. Each transaction shows: date/time, type icon, description, amount (+/-), status badge
7. User scrolls down; app detects nearing end of current results
8. App sends a new stream request with the cursor from the last received transaction
9. Server streams the next page of results
10. If no more results, server closes the stream
11. User can apply filters: tap filter icon to select transaction type or date range
12. Filtered request opens a new stream with the filter parameters

---

## Acceptance Criteria

- [ ] `GetTransactions` returns transactions via gRPC server streaming
- [ ] Transactions are sorted by date, newest first, by default
- [ ] Each transaction record includes: date, type, amount, currency, counterparty (recipient or sender), status, and reference number
- [ ] User can filter transactions by type (deposit, withdrawal, transfer, payment, fee)
- [ ] User can filter transactions by date range (start date and end date)
- [ ] Minimum 90 days of transaction history is accessible
- [ ] Streaming uses cursor-based pagination for consistent results
- [ ] Page size is configurable (default 20 transactions per stream batch)
- [ ] Empty transaction history returns an empty stream (not an error)
- [ ] Only the authenticated account owner can access their transaction history
- [ ] Query performance: first batch of results delivered within 1 second for accounts with up to 10,000 transactions

---

## Technical Notes

### Components
- **AccountModule** (`src/Modules/Account/`):
  - `AccountService.cs`: Add `GetTransactions` streaming gRPC method
  - `TransactionHistoryService.cs`: Business logic for querying and streaming transactions
  - `TransactionRepository.cs`: Database access with cursor-based queries
- **SharedKernel** (`src/SharedKernel/`):
  - `TransactionType.cs`: Enum for transaction types
  - `TransactionStatus.cs`: Enum for transaction statuses

### API / gRPC Endpoints

**Service:** `AccountService`

```protobuf
service AccountService {
  // Server streaming: returns transactions in batches
  rpc GetTransactions(GetTransactionsRequest) returns (stream TransactionRecord);
}

message GetTransactionsRequest {
  string account_id = 1;
  optional string cursor = 2;              // Opaque cursor from previous response
  int32 page_size = 3;                     // Default 20, max 100
  optional TransactionTypeFilter type_filter = 4;
  optional DateRangeFilter date_filter = 5;
}

message TransactionTypeFilter {
  repeated string types = 1;               // "deposit", "withdrawal", "transfer", "payment", "fee"
}

message DateRangeFilter {
  google.protobuf.Timestamp start_date = 1;
  google.protobuf.Timestamp end_date = 2;
}

message TransactionRecord {
  string transaction_id = 1;
  string type = 2;                          // "deposit", "withdrawal", "transfer_in", "transfer_out", "payment", "fee"
  string amount = 3;                        // Signed: positive for credits, negative for debits
  string currency = 4;                      // ISO 4217
  string counterparty_name = 5;             // Recipient or sender display name
  string counterparty_account = 6;          // Masked account/phone number
  string status = 7;                        // "completed", "pending", "failed", "reversed"
  string reference = 8;                     // Transaction reference number
  string description = 9;                   // Human-readable description
  google.protobuf.Timestamp created_at = 10;
  string cursor = 11;                       // Cursor for pagination (encoded transaction_id + timestamp)
  bool has_more = 12;                       // Indicates more results available
}
```

**Streaming Implementation:**

```csharp
public override async Task GetTransactions(
    GetTransactionsRequest request,
    IServerStreamWriter<TransactionRecord> responseStream,
    ServerCallContext context)
{
    var cursor = DecodeCursor(request.Cursor);
    var pageSize = Math.Clamp(request.PageSize, 1, 100);

    await foreach (var transaction in _transactionHistoryService
        .GetTransactionsAsync(request.AccountId, cursor, pageSize, request.TypeFilter, request.DateFilter)
        .WithCancellation(context.CancellationToken))
    {
        await responseStream.WriteAsync(MapToRecord(transaction));
    }
}
```

### Database Changes

**Table:** `transactions` (schema: `{tenant_schema}`, partitioned by month)

```sql
CREATE TABLE transactions (
    id UUID NOT NULL DEFAULT gen_random_uuid(),
    account_id UUID NOT NULL,
    type VARCHAR(30) NOT NULL,
    direction VARCHAR(10) NOT NULL,          -- 'credit' or 'debit'
    amount DECIMAL(18,2) NOT NULL,
    currency VARCHAR(3) NOT NULL,
    counterparty_account_id UUID,
    counterparty_name VARCHAR(200),
    counterparty_identifier VARCHAR(100),    -- Masked phone/account for display
    status VARCHAR(30) NOT NULL DEFAULT 'pending',
    reference VARCHAR(50) NOT NULL UNIQUE,
    description VARCHAR(500),
    metadata JSONB,                          -- Flexible additional data
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    PRIMARY KEY (id, created_at)
) PARTITION BY RANGE (created_at);

-- Create partitions for current and upcoming months
CREATE TABLE transactions_2026_01 PARTITION OF transactions
    FOR VALUES FROM ('2026-01-01') TO ('2026-02-01');
CREATE TABLE transactions_2026_02 PARTITION OF transactions
    FOR VALUES FROM ('2026-02-01') TO ('2026-03-01');
CREATE TABLE transactions_2026_03 PARTITION OF transactions
    FOR VALUES FROM ('2026-03-01') TO ('2026-04-01');
-- Additional partitions created by automated partition management job

-- Critical indexes for query performance
CREATE INDEX idx_transactions_account_created ON transactions(account_id, created_at DESC);
CREATE INDEX idx_transactions_account_type ON transactions(account_id, type, created_at DESC);
CREATE INDEX idx_transactions_reference ON transactions(reference);
```

**Cursor Encoding:**
The cursor is a Base64-encoded string containing `{created_at}|{transaction_id}`. This allows deterministic pagination even when multiple transactions share the same timestamp.

```sql
-- Cursor-based query
SELECT * FROM transactions
WHERE account_id = @account_id
  AND (created_at, id) < (@cursor_timestamp, @cursor_id)
  AND created_at >= @min_date      -- 90-day minimum
ORDER BY created_at DESC, id DESC
LIMIT @page_size;
```

### Security Considerations
- **Authorization:** Transaction history is strictly limited to the account owner. JWT `sub` claim must match the requested `account_id`.
- **Data Masking:** Counterparty account numbers and phone numbers are masked in the response (e.g., "****1234", "+265****5678") to protect third-party privacy.
- **Query Limits:** Maximum page size of 100 and 90-day default window prevent resource-exhausting queries. Administrators can access longer history through a separate admin API.
- **Streaming Cancellation:** If the client disconnects, the server stream is cancelled via `CancellationToken` to prevent orphaned database connections.
- **No Sensitive Data in Cursor:** The cursor contains only timestamp and ID -- no sensitive information. It is Base64-encoded for opacity but not encrypted.

### Edge Cases
- **No transactions:** Return an empty stream (zero records). Do not return an error.
- **Account with very high transaction volume:** Partitioned table and cursor-based pagination ensure consistent performance. Index on `(account_id, created_at DESC)` is critical.
- **Transactions spanning partition boundaries:** PostgreSQL handles cross-partition queries transparently with partition pruning based on the date filter.
- **Clock skew between services:** Transactions are timestamped by the database (`NOW()`) rather than application servers, ensuring consistent ordering.
- **Concurrent new transactions during streaming:** Cursor-based pagination using `(created_at, id) < cursor` ensures no duplicates or missed records. New transactions (with later timestamps) do not affect the current stream.
- **Filter returns zero results:** Return an empty stream, not an error. The `has_more` flag on the last record will be `false`.
- **Invalid cursor (tampered or expired):** Reject with `INVALID_ARGUMENT` error. Client should restart from the beginning.
- **Date filter beyond 90 days:** Allow the request but only return records within the available retention period. Do not error.
- **Transaction in pending status:** Include pending transactions in the history with `status = "pending"`. Users need to see these for awareness.

---

## Dependencies

**Prerequisite Stories:**
- STORY-013: Account Activation on KYC Approval (account must be active; balance and transaction infrastructure established)

**Blocked Stories:**
- None directly, but transaction history is a prerequisite for meaningful user experience in transaction-related features

**External Dependencies:**
- PostgreSQL 18 with partitioning support (standard feature)
- Automated partition management job (can be a cron job or pg_partman extension)

---

## Definition of Done

- [ ] Code implemented and committed
- [ ] Unit tests written and passing (>=80% coverage)
- [ ] Integration tests passing
- [ ] Code reviewed and approved
- [ ] Documentation updated
- [ ] Acceptance criteria validated
- [ ] Deployed to staging
- [ ] gRPC server streaming verified: client receives transaction records progressively
- [ ] Cursor-based pagination verified: consistent results across multiple pages
- [ ] Filtering verified: type filter and date range filter produce correct results
- [ ] Empty history verified: returns empty stream without error
- [ ] Performance verified: first batch returned within 1 second for 10,000-transaction account
- [ ] Partition pruning verified: queries on date-filtered requests only scan relevant partitions
- [ ] Data masking verified: counterparty details are properly masked

---

## Progress Tracking

**Status History:**
- 2026-02-24: Created

**Actual Effort:** TBD

---

**This story was created using BMAD Method v6 - Phase 4 (Implementation)**
