# STORY-045: Daily Reconciliation

**Epic:** EPIC-008 National Network Switching
**Priority:** Must Have
**Story Points:** 5
**Status:** Not Started
**Assigned To:** Unassigned
**Created:** 2026-02-24
**Sprint:** 5

---

## User Story

As an **operations team**
I want **daily reconciliation with the national switch**
So that **all transactions are matched and discrepancies flagged for investigation**

---

## Description

### Background

Reconciliation is a critical operational process for any bank connected to a national payment switch. Every day, GoldBank must verify that its record of transactions matches what the national switch recorded. Discrepancies can arise from network failures, timeouts, partial processing, clock skew, or bugs in either system. Without daily reconciliation, financial errors can accumulate undetected, leading to customer complaints, regulatory issues, and financial loss.

The reconciliation process compares GoldBank's `switch_messages` and transaction records against settlement files or statement messages (camt.053) received from the national switch. Transactions are categorized as: matched (amounts and references agree), mismatched (amounts differ), unmatched local (in GoldBank but not in the switch file), or unmatched remote (in the switch file but not in GoldBank). Mismatches and unmatched transactions are flagged for manual review by the operations team.

This process runs on a configurable schedule — typically once per day after the switch's end-of-day cutoff — and produces a reconciliation report. The report feeds into the net settlement calculation, which determines how much GoldBank owes or is owed by the national switch for the day's transactions.

**Functional Requirements:** FR-030 (Daily Reconciliation)

### Scope

**In scope:**
- Automated reconciliation job on a configurable schedule
- Retrieval of GoldBank's switch transactions for the reconciliation period from `switch_messages` table
- Settlement file parsing from the national switch (configurable format per switch)
- camt.053 (Bank to Customer Statement) parsing via the ISO 20022 adapter
- Transaction matching by reference number (RRN, end-to-end ID)
- Categorization: matched, mismatched, unmatched_local, unmatched_remote
- Reconciliation report generation and storage
- Mismatch flagging for manual review via Wolverine event (`ReconciliationMismatchFound`)
- Net settlement calculation: sum of debits vs credits
- Dashboard-ready reconciliation summary data
- Retry logic for failed reconciliation runs

**Out of scope:**
- Automated dispute resolution (manual process)
- Settlement file delivery to the switch (switch pushes or we pull — not generating outbound settlement files)
- Multi-day reconciliation (each run covers a single day)
- Real-time reconciliation (this is a batch process)
- SARB (South African Reserve Bank) regulatory reporting format (future enhancement)

### User Flow

**Automated Daily Reconciliation Flow:**

1. **Scheduled Trigger:** At the configured time (e.g., 02:00 AM local time, after the switch's EOD cutoff), the reconciliation job fires
2. **Retrieve Local Transactions:** Query `switch_messages` table for all transactions within the reconciliation date range (previous business day, from switch cutoff to cutoff)
3. **Retrieve Switch File:** Obtain the settlement file from the national switch:
   - Pull from SFTP or API endpoint (configurable per switch)
   - Or parse a camt.053 message received earlier via the ISO 20022 adapter
4. **Parse Switch File:** Parse the settlement file into a normalized list of transactions (reference, amount, direction, status)
5. **Match Transactions:** For each GoldBank transaction, look for a matching switch transaction by reference number:
   - **Matched:** Reference found, amounts agree — mark as reconciled
   - **Mismatched:** Reference found, but amounts differ — flag for review
6. **Find Unmatched:**
   - **Unmatched Local:** GoldBank transactions with no matching switch record
   - **Unmatched Remote:** Switch transactions with no matching GoldBank record
7. **Generate Report:** Create a reconciliation report with summary statistics and detailed line items
8. **Calculate Settlement:** Net settlement = sum of GoldBank debits to switch - sum of GoldBank credits from switch
9. **Flag Mismatches:** Publish `ReconciliationMismatchFound` Wolverine event for each mismatch, triggering:
   - Notification to operations team
   - Entry in the admin dashboard's reconciliation queue
10. **Store Report:** Persist the reconciliation report and all line items in the database

**Manual Reconciliation Trigger:**

1. Operations team member triggers a reconciliation run for a specific date via the admin dashboard
2. Same flow as automated, but for the specified date
3. Useful for re-running failed reconciliations or reconciling after a switch outage

---

## Acceptance Criteria

- [ ] Reconciliation job runs automatically on a configurable schedule (cron expression, default: daily at 02:00 local time)
- [ ] All switch transactions for the reconciliation period are retrieved from `switch_messages` table
- [ ] Settlement file is retrieved from the national switch (SFTP pull or camt.053 message parsing)
- [ ] Settlement file parser supports configurable format per switch (CSV, fixed-width, ISO 20022 camt.053)
- [ ] Transactions are matched by reference number (RRN for ISO 8583, EndToEndId for ISO 20022)
- [ ] Matched transactions with agreeing amounts are marked as reconciled
- [ ] Mismatched transactions (amounts differ) are flagged for manual review
- [ ] Unmatched local transactions (in GoldBank, not in switch) are identified and flagged
- [ ] Unmatched remote transactions (in switch, not in GoldBank) are identified and flagged
- [ ] Reconciliation report is generated with: total matched, total mismatched, total unmatched local, total unmatched remote, net settlement amount
- [ ] `ReconciliationMismatchFound` Wolverine event is published for each mismatch, triggering notification to operations
- [ ] Net settlement is calculated: sum of debits minus sum of credits
- [ ] Reconciliation report and all line items are persisted in the database
- [ ] Failed reconciliation runs are retried up to 3 times with exponential backoff
- [ ] Manual reconciliation can be triggered for a specific date via admin API

---

## Technical Notes

### Components

| Component | Location | Purpose |
|-----------|----------|---------|
| `ReconciliationJob.cs` | `src/Satellites/GoldBank.Switching/Reconciliation/` | Scheduled job orchestrator |
| `ReconciliationEngine.cs` | `src/Satellites/GoldBank.Switching/Reconciliation/` | Core matching logic |
| `SettlementFileParser.cs` | `src/Satellites/GoldBank.Switching/Reconciliation/Parsers/` | Base class for settlement file parsers |
| `CsvSettlementParser.cs` | `src/Satellites/GoldBank.Switching/Reconciliation/Parsers/` | CSV format parser |
| `FixedWidthSettlementParser.cs` | `src/Satellites/GoldBank.Switching/Reconciliation/Parsers/` | Fixed-width format parser |
| `Camt053SettlementParser.cs` | `src/Satellites/GoldBank.Switching/Reconciliation/Parsers/` | ISO 20022 camt.053 parser |
| `SettlementFileRetriever.cs` | `src/Satellites/GoldBank.Switching/Reconciliation/` | SFTP/API client to pull settlement files |
| `ReconciliationReportBuilder.cs` | `src/Satellites/GoldBank.Switching/Reconciliation/` | Generates reconciliation report |
| `NetSettlementCalculator.cs` | `src/Satellites/GoldBank.Switching/Reconciliation/` | Calculates net settlement position |
| `ReconciliationMismatchHandler.cs` | `src/Satellites/GoldBank.Switching/Handlers/` | Publishes mismatch events |

### API / gRPC Endpoints

**Admin API for Manual Reconciliation:**

```protobuf
service ReconciliationService {
  rpc TriggerReconciliation (TriggerReconciliationRequest) returns (TriggerReconciliationResponse);
  rpc GetReconciliationReport (GetReportRequest) returns (ReconciliationReport);
  rpc GetReconciliationHistory (GetHistoryRequest) returns (stream ReconciliationSummary);
  rpc GetMismatches (GetMismatchesRequest) returns (stream ReconciliationMismatch);
  rpc ResolveMismatch (ResolveMismatchRequest) returns (ResolveMismatchResponse);
}

message TriggerReconciliationRequest {
  string tenant_id = 1;
  string switch_id = 2;
  string reconciliation_date = 3;  // ISO 8601 date: 2026-02-24
}

message TriggerReconciliationResponse {
  string reconciliation_id = 1;
  string status = 2;  // "started", "already_running", "already_completed"
  bool success = 3;
  string error_message = 4;
}

message ReconciliationReport {
  string reconciliation_id = 1;
  string reconciliation_date = 2;
  string switch_id = 3;
  string status = 4;
  int32 total_local_transactions = 5;
  int32 total_remote_transactions = 6;
  int32 matched_count = 7;
  int32 mismatched_count = 8;
  int32 unmatched_local_count = 9;
  int32 unmatched_remote_count = 10;
  string net_settlement_amount = 11;
  string net_settlement_currency = 12;
  string net_settlement_direction = 13;  // "payable" or "receivable"
  string completed_at = 14;
}

message ReconciliationMismatch {
  string id = 1;
  string transaction_ref = 2;
  string local_amount = 3;
  string remote_amount = 4;
  string mismatch_type = 5;     // "amount_mismatch", "unmatched_local", "unmatched_remote"
  string status = 6;            // "pending_review", "resolved", "disputed"
  string resolution_notes = 7;
}
```

**Reconciliation Engine (pseudocode):**

```csharp
public class ReconciliationEngine
{
    public ReconciliationResult Reconcile(
        IReadOnlyList<SwitchTransaction> localTransactions,
        IReadOnlyList<SettlementEntry> remoteTransactions)
    {
        var result = new ReconciliationResult();
        var remoteByRef = remoteTransactions.ToDictionary(r => r.Reference);
        var matchedRemoteRefs = new HashSet<string>();

        foreach (var local in localTransactions)
        {
            if (remoteByRef.TryGetValue(local.RetrievalReference, out var remote))
            {
                matchedRemoteRefs.Add(remote.Reference);

                if (local.Amount == remote.Amount
                    && local.Currency == remote.Currency)
                {
                    result.Matched.Add(new MatchedTransaction(local, remote));
                }
                else
                {
                    result.Mismatched.Add(new MismatchedTransaction(
                        local, remote,
                        reason: $"Amount: local={local.Amount} remote={remote.Amount}"));
                }
            }
            else
            {
                result.UnmatchedLocal.Add(local);
            }
        }

        // Find remote transactions not matched to any local transaction
        foreach (var remote in remoteTransactions)
        {
            if (!matchedRemoteRefs.Contains(remote.Reference))
            {
                result.UnmatchedRemote.Add(remote);
            }
        }

        return result;
    }
}
```

**Net Settlement Calculation:**

```csharp
public class NetSettlementCalculator
{
    public NetSettlement Calculate(IReadOnlyList<MatchedTransaction> matched)
    {
        // Debits: transactions where GoldBank account was debited
        //         (outbound to other banks)
        var totalDebits = matched
            .Where(m => m.Local.Direction == "outbound")
            .Sum(m => m.Local.Amount);

        // Credits: transactions where GoldBank account was credited
        //          (inbound from other banks)
        var totalCredits = matched
            .Where(m => m.Local.Direction == "inbound")
            .Sum(m => m.Local.Amount);

        var netAmount = totalCredits - totalDebits;

        return new NetSettlement
        {
            GrossDebits = totalDebits,
            GrossCredits = totalCredits,
            NetAmount = Math.Abs(netAmount),
            Direction = netAmount >= 0 ? "receivable" : "payable",
            Currency = matched.First().Local.Currency
        };
    }
}
```

**Scheduled Job Configuration:**

```json
{
  "Reconciliation": {
    "Schedule": "0 2 * * *",
    "ReconciliationWindowHours": 24,
    "CutoffTimeUtc": "22:00",
    "MaxRetries": 3,
    "RetryDelaySeconds": [60, 300, 900],
    "SettlementFile": {
      "RetrievalMethod": "SFTP",
      "SftpHost": "sftp.nationalswitch.example",
      "SftpPath": "/settlements/",
      "FilePattern": "settlement_{date:yyyyMMdd}.csv",
      "Format": "CSV"
    }
  }
}
```

### Database Changes

**reconciliation_reports table:**

```sql
CREATE TABLE switching.reconciliation_reports (
    id                      UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id               VARCHAR(50) NOT NULL,
    switch_id               VARCHAR(50) NOT NULL,
    reconciliation_date     DATE NOT NULL,
    status                  VARCHAR(20) NOT NULL DEFAULT 'running',
    total_local_txns        INT NOT NULL DEFAULT 0,
    total_remote_txns       INT NOT NULL DEFAULT 0,
    matched_count           INT NOT NULL DEFAULT 0,
    mismatched_count        INT NOT NULL DEFAULT 0,
    unmatched_local_count   INT NOT NULL DEFAULT 0,
    unmatched_remote_count  INT NOT NULL DEFAULT 0,
    gross_debits            DECIMAL(18, 2) NOT NULL DEFAULT 0,
    gross_credits           DECIMAL(18, 2) NOT NULL DEFAULT 0,
    net_settlement_amount   DECIMAL(18, 2) NOT NULL DEFAULT 0,
    net_settlement_direction VARCHAR(10),
    currency                VARCHAR(3) NOT NULL,
    settlement_file_name    VARCHAR(200),
    started_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    completed_at            TIMESTAMPTZ,
    error_message           VARCHAR(1000),
    retry_count             INT NOT NULL DEFAULT 0,
    created_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (switch_id, reconciliation_date)
);

CREATE INDEX idx_recon_reports_date ON switching.reconciliation_reports (reconciliation_date DESC);
CREATE INDEX idx_recon_reports_status ON switching.reconciliation_reports (status);
CREATE INDEX idx_recon_reports_tenant ON switching.reconciliation_reports (tenant_id, reconciliation_date DESC);
```

**reconciliation_items table** (individual transaction-level results):

```sql
CREATE TABLE switching.reconciliation_items (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    report_id           UUID NOT NULL REFERENCES switching.reconciliation_reports(id),
    category            VARCHAR(20) NOT NULL CHECK (category IN ('matched', 'mismatched', 'unmatched_local', 'unmatched_remote')),
    transaction_ref     VARCHAR(50),
    retrieval_ref       VARCHAR(12),
    local_amount        DECIMAL(18, 2),
    remote_amount       DECIMAL(18, 2),
    local_currency      VARCHAR(3),
    remote_currency     VARCHAR(3),
    local_direction     VARCHAR(10),
    remote_direction    VARCHAR(10),
    local_response_code VARCHAR(4),
    remote_status       VARCHAR(20),
    mismatch_reason     VARCHAR(500),
    review_status       VARCHAR(20) NOT NULL DEFAULT 'pending',
    reviewed_by         VARCHAR(100),
    review_notes        VARCHAR(1000),
    resolved_at         TIMESTAMPTZ,
    switch_message_id   UUID,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_recon_items_report ON switching.reconciliation_items (report_id);
CREATE INDEX idx_recon_items_category ON switching.reconciliation_items (category);
CREATE INDEX idx_recon_items_review ON switching.reconciliation_items (review_status)
    WHERE review_status = 'pending';
CREATE INDEX idx_recon_items_txn_ref ON switching.reconciliation_items (transaction_ref);
```

**settlement_files table** (tracks received settlement files):

```sql
CREATE TABLE switching.settlement_files (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    switch_id       VARCHAR(50) NOT NULL,
    file_name       VARCHAR(200) NOT NULL,
    file_date       DATE NOT NULL,
    file_format     VARCHAR(20) NOT NULL,
    file_size_bytes BIGINT,
    record_count    INT,
    total_debits    DECIMAL(18, 2),
    total_credits   DECIMAL(18, 2),
    raw_content     TEXT,
    retrieved_at    TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    parsed_at       TIMESTAMPTZ,
    parse_errors    INT NOT NULL DEFAULT 0,
    UNIQUE (switch_id, file_name)
);
```

### Security Considerations

- **SFTP Credentials:** SFTP credentials for settlement file retrieval must be stored encrypted (data protection API or vault). Never in plaintext configuration files. Use SSH key authentication where possible.
- **Settlement File Integrity:** Verify settlement file checksums (if provided by the switch) before parsing. Some switches include a hash or trailer record count for validation.
- **Access Control:** Reconciliation reports contain sensitive financial data. Access to the reconciliation gRPC endpoints and database tables must be restricted to the operations team role. Audit all access.
- **Mismatch Resolution Audit:** When an operations team member resolves a mismatch (marks it as reviewed), the resolution must be logged with the reviewer's identity, timestamp, and notes. This is a regulatory requirement.
- **Data Retention:** Reconciliation reports and items must be retained per regulatory requirements (typically 7 years in Southern Africa). Implement archival strategy for old reports.

### Edge Cases

- **Settlement File Not Available:** If the settlement file is not available at the scheduled time (switch delay, SFTP outage), retry up to 3 times with exponential backoff (1 min, 5 min, 15 min). If still unavailable, mark the reconciliation as "failed — file not available" and alert operations.
- **Empty Settlement File:** If the settlement file contains zero transactions (holiday, switch outage), create a reconciliation report with all GoldBank transactions as "unmatched_local". Alert operations of the anomaly.
- **Duplicate Reference Numbers:** If multiple GoldBank transactions share the same reference number (should not happen, but defensive coding), match the first occurrence and flag the duplicates for investigation.
- **Settlement File Format Change:** If the settlement file format changes unexpectedly (switch upgrade), the parser will fail. Log the parsing error, mark reconciliation as failed, and alert operations. The parser format is configurable per switch to accommodate changes.
- **Time Zone / Cutoff Mismatch:** Different switches may use different cutoff times and time zones. The reconciliation window must be configured per switch (cutoff time + timezone). Transactions near the cutoff boundary may appear in the next day's file — handle gracefully by checking the previous and next day if a transaction is unmatched.
- **Large Transaction Volume:** On high-volume days, reconciliation may involve hundreds of thousands of transactions. Use batch processing and streaming where possible. The matching algorithm should be O(n) using hash-based lookups, not O(n^2) nested loops.
- **Partial Settlement File:** If the settlement file is truncated (partial download), detect the truncation (record count mismatch vs. trailer, unexpected EOF), mark reconciliation as failed, and retry the download.
- **Already Reconciled:** If reconciliation is triggered for a date that has already been successfully reconciled, return "already_completed" and do not re-run unless explicitly forced.
- **Cross-Day Transactions:** Transactions initiated just before the cutoff but completed after may appear differently in the GoldBank log vs. the switch file. Use the switch timestamp (not GoldBank's timestamp) for date assignment.

---

## Dependencies

**Prerequisite Stories:**
- STORY-043: Outbound Transaction Routing — outbound transactions populate the `switch_messages` table
- STORY-044: Inbound Transaction Processing — inbound transactions populate the `switch_messages` table
- STORY-041: ISO 20022 Adapter — camt.053 parsing for settlement statements (if switch uses ISO 20022)

**Blocked Stories:**
- None directly. Reconciliation is a terminal process in the switch integration chain.

**External Dependencies:**
- National switch settlement file delivery mechanism (SFTP server, API endpoint, or camt.053 message)
- Settlement file format specification from the switch operator
- SFTP credentials or API keys for settlement file retrieval
- Scheduler infrastructure (Wolverine scheduled commands or Hangfire)

---

## Definition of Done

- [ ] Code implemented and committed
- [ ] Unit tests written and passing (>=80% coverage) — reconciliation engine tested with known matched/mismatched/unmatched scenarios
- [ ] Integration tests passing with sample settlement files (CSV and camt.053)
- [ ] Scheduled job tested: triggers at configured time, runs successfully
- [ ] All four categories tested: matched, mismatched, unmatched_local, unmatched_remote
- [ ] Net settlement calculation verified with test data
- [ ] Mismatch event publishing verified — operations team notified
- [ ] Settlement file retrieval tested (SFTP mock or local file)
- [ ] Retry logic tested for failed reconciliation runs
- [ ] Manual reconciliation trigger tested via admin API
- [ ] Code reviewed and approved
- [ ] Documentation updated (reconciliation schedule, settlement file format, mismatch resolution workflow)
- [ ] Acceptance criteria validated
- [ ] Deployed to staging

---

## Progress Tracking

**Status History:**
- 2026-02-24: Created

**Actual Effort:** TBD

---

**This story was created using BMAD Method v6 - Phase 4 (Implementation)**
