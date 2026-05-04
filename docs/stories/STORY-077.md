# STORY-077: CardTransactions Module Scaffolding & Domain Model

**Epic:** EPIC-015 Card Transaction Processing
**Priority:** Must Have
**Story Points:** 8
**Status:** Not Started
**Assigned To:** Unassigned
**Created:** 2026-03-15
**Sprint:** 9

---

## User Story

As a **developer**
I want to **scaffold the CardTransactions module with domain entities, gRPC service definition, and database schema**
So that **the team can implement card transaction processing handlers in subsequent stories**

---

## Description

### Background

The bank's core system needs to process card transactions received from the switch (GoldBank.Switching) that sits between the bank and the national payments network. Transactions arrive in ISO 20022 format and are translated by the switch into gRPC calls to Core Banking. This module handles the core banking side: account validation, fund movement, balance/statement enquiries, and response generation.

The CardTransactions module is the issuer-side processor — it handles transactions where the bank's clients are using their cards at merchants (on-us or off-us) or receiving deposits. The switch has already been built (Sprint 5, EPIC-008) and communicates with Core Banking via gRPC. This module provides the gRPC endpoints and business logic that the switch calls.

### Scope

**In scope:**
- Module folder structure under `server/GoldBank.Core/Modules/CardTransactions/`
- Domain entities: `CardTransaction`, `CardTransactionType` enum
- gRPC proto definition: `card_transaction_service.proto` with RPCs for all 6 transaction types
- Database schema and EF Core entity configuration
- Command and handler stubs for each transaction type
- Validator base class for common card transaction validation (account exists, active, currency match)
- Card transaction response model with ISO 20022 status codes

**Out of scope:**
- Individual transaction type business logic (STORY-078 through STORY-083)
- Switch-side changes (existing switch routes to this module)
- Fraud detection on card transactions (future enhancement)
- Card management (issuance, blocking, PIN management)

### Module Structure

```
server/GoldBank.Core/Modules/CardTransactions/
├── Application/
│   ├── Commands/
│   │   ├── ProcessPurchaseCommand.cs
│   │   ├── ProcessDepositCommand.cs
│   │   ├── BalanceEnquiryCommand.cs
│   │   └── StatementEnquiryCommand.cs
│   ├── Handlers/
│   │   ├── ProcessPurchaseHandler.cs
│   │   ├── ProcessDepositHandler.cs
│   │   ├── BalanceEnquiryHandler.cs
│   │   ├── StatementEnquiryHandler.cs
│   │   └── CardTransactionResult.cs
│   └── Validators/
│       └── CardTransactionValidator.cs
├── Domain/
│   ├── Entities/
│   │   └── CardTransaction.cs
│   └── Enums/
│       └── CardTransactionType.cs
├── Grpc/
│   └── CardTransactionGrpcService.cs
└── Infrastructure/
    └── Persistence/
        └── CardTransactionEntityConfiguration.cs
```

---

## Acceptance Criteria

- [ ] Module folder structure created matching the pattern of existing modules (Payments, Transfers, Accounts)
- [ ] `CardTransaction` domain entity created with fields: AccountId, MerchantAccountId (nullable), MerchantId, MerchantName, TransactionType, Amount, Fee, Currency, Status, ResponseCode, AuthorizationCode, Reference, RetrievalReference, Stan, TerminalId, ProcessingCode, SourceInstitution, AcquiringInstitution, BalanceAfter, TenantId, CompletedAt
- [ ] `CardTransactionType` enum created with values: OnUsPurchase, OffUsPurchase, OnUsDeposit, OffUsDeposit, BalanceEnquiry, StatementEnquiry
- [ ] `card_transaction_service.proto` created in `server/GoldBank.Protos/Protos/` with RPC methods: ProcessPurchase, ProcessDeposit, BalanceEnquiry, StatementEnquiry
- [ ] Proto messages include: card_holder_account, merchant_id, merchant_name, terminal_id, amount (Money type), processing_code, source_institution, acquiring_institution, stan, retrieval_reference, transaction_id
- [ ] Response proto includes: success, response_code, authorization_code, message, available_balance (Money), transaction_id
- [ ] EF Core entity configuration maps `CardTransaction` to `card_transactions` table in tenant schema
- [ ] Database indexes on: (tenant_id, created_at DESC), (account_id, created_at DESC), (reference), (retrieval_reference), (stan, source_institution)
- [ ] `CardTransactionValidator` validates: account exists, account active, currency matches account currency, amount is positive (for financial transactions)
- [ ] Solution builds successfully with the new module

---

## Technical Notes

### Components

| Component | Location | Purpose |
|-----------|----------|---------|
| `CardTransaction.cs` | `Modules/CardTransactions/Domain/Entities/` | Domain entity for card transaction records |
| `CardTransactionType.cs` | `Modules/CardTransactions/Domain/Enums/` | Enum for transaction types |
| `CardTransactionGrpcService.cs` | `Modules/CardTransactions/Grpc/` | gRPC service receiving calls from the switch |
| `CardTransactionValidator.cs` | `Modules/CardTransactions/Application/Validators/` | Common validation for all card transactions |
| `CardTransactionResult.cs` | `Modules/CardTransactions/Application/Handlers/` | Shared result model for handler responses |
| `CardTransactionEntityConfiguration.cs` | `Modules/CardTransactions/Infrastructure/Persistence/` | EF Core mapping |
| `card_transaction_service.proto` | `server/GoldBank.Protos/Protos/` | gRPC service definition |

### API / gRPC Endpoints

**card_transaction_service.proto:**

```protobuf
syntax = "proto3";

package goldbank.v1.cardtransactions;

import "google/protobuf/timestamp.proto";
import "Protos/common.proto";

service CardTransactionService {
  rpc ProcessPurchase (PurchaseRequest) returns (CardTransactionResponse);
  rpc ProcessDeposit (DepositRequest) returns (CardTransactionResponse);
  rpc BalanceEnquiry (BalanceEnquiryRequest) returns (BalanceEnquiryResponse);
  rpc StatementEnquiry (StatementEnquiryRequest) returns (StatementEnquiryResponse);
}

message PurchaseRequest {
  string transaction_id = 1;
  string card_holder_account = 2;
  string merchant_id = 3;
  string merchant_name = 4;
  string terminal_id = 5;
  goldbank.v1.common.Money amount = 6;
  string processing_code = 7;
  string source_institution = 8;
  string acquiring_institution = 9;
  string stan = 10;
  string retrieval_reference = 11;
  bool is_on_us = 12;
  string tenant_id = 13;
}

message DepositRequest {
  string transaction_id = 1;
  string card_holder_account = 2;
  string merchant_id = 3;
  string merchant_name = 4;
  string terminal_id = 5;
  goldbank.v1.common.Money amount = 6;
  string processing_code = 7;
  string source_institution = 8;
  string acquiring_institution = 9;
  string stan = 10;
  string retrieval_reference = 11;
  bool is_on_us = 12;
  string tenant_id = 13;
}

message BalanceEnquiryRequest {
  string transaction_id = 1;
  string card_holder_account = 2;
  string terminal_id = 3;
  string source_institution = 4;
  string stan = 5;
  string retrieval_reference = 6;
  string tenant_id = 7;
}

message StatementEnquiryRequest {
  string transaction_id = 1;
  string card_holder_account = 2;
  string terminal_id = 3;
  string source_institution = 4;
  string stan = 5;
  string retrieval_reference = 6;
  string tenant_id = 7;
  int32 max_records = 8;
}

message CardTransactionResponse {
  bool success = 1;
  string response_code = 2;
  string authorization_code = 3;
  string message = 4;
  goldbank.v1.common.Money available_balance = 5;
  string transaction_id = 6;
  google.protobuf.Timestamp processed_at = 7;
}

message BalanceEnquiryResponse {
  bool success = 1;
  string response_code = 2;
  string message = 3;
  goldbank.v1.common.Money available_balance = 4;
  goldbank.v1.common.Money ledger_balance = 5;
  string transaction_id = 6;
}

message StatementEntry {
  google.protobuf.Timestamp date = 1;
  string description = 2;
  goldbank.v1.common.Money amount = 3;
  string type = 4;
  string reference = 5;
  goldbank.v1.common.Money balance_after = 6;
}

message StatementEnquiryResponse {
  bool success = 1;
  string response_code = 2;
  string message = 3;
  repeated StatementEntry entries = 4;
  goldbank.v1.common.Money available_balance = 5;
  string transaction_id = 6;
}
```

### Database Changes

```sql
CREATE TABLE tenant_{slug}.card_transactions (
    id                    UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    account_id            UUID NOT NULL,
    merchant_account_id   UUID,
    merchant_id           VARCHAR(15),
    merchant_name         VARCHAR(200),
    transaction_type      VARCHAR(30) NOT NULL,
    amount                DECIMAL(18, 2) NOT NULL,
    fee                   DECIMAL(18, 2) NOT NULL DEFAULT 0,
    currency              VARCHAR(3) NOT NULL DEFAULT 'ZWG',
    status                VARCHAR(20) NOT NULL DEFAULT 'pending',
    response_code         VARCHAR(4),
    authorization_code    VARCHAR(12),
    reference             VARCHAR(50),
    retrieval_reference   VARCHAR(12),
    stan                  VARCHAR(12),
    terminal_id           VARCHAR(16),
    processing_code       VARCHAR(6),
    source_institution    VARCHAR(20),
    acquiring_institution VARCHAR(20),
    balance_after         DECIMAL(18, 2),
    tenant_id             VARCHAR(50) NOT NULL,
    created_at            TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    completed_at          TIMESTAMPTZ
);

CREATE INDEX idx_card_txn_tenant_time ON tenant_{slug}.card_transactions (tenant_id, created_at DESC);
CREATE INDEX idx_card_txn_account_time ON tenant_{slug}.card_transactions (account_id, created_at DESC);
CREATE INDEX idx_card_txn_reference ON tenant_{slug}.card_transactions (reference);
CREATE INDEX idx_card_txn_retrieval_ref ON tenant_{slug}.card_transactions (retrieval_reference);
CREATE INDEX idx_card_txn_stan_source ON tenant_{slug}.card_transactions (stan, source_institution);
```

### Security Considerations

- Only the switch (GoldBank.Switching) should call this gRPC service — enforce via mTLS or service-to-service auth tokens
- Account balance must never be exposed to off-us merchants; only return response codes
- All transactions must be logged with full audit trail
- Idempotency: duplicate STAN + source_institution combinations within a time window must return the original response

### Edge Cases

- **Duplicate transaction:** Same STAN + source_institution within 5 minutes — return original response (idempotent)
- **Account not found:** Return response code "14" (invalid card/account)
- **Account frozen/closed:** Return response code "78" (account blocked)
- **Currency mismatch:** Return response code "12" (invalid transaction)
- **System error:** Return response code "96" (system malfunction)

---

## Dependencies

**Prerequisite Stories:**
- STORY-042: Message Router & Canonical Format — canonical message structure used by the switch
- STORY-044: Inbound Transaction Processing — switch-side inbound handling that calls this module

**Blocked Stories:**
- STORY-078: On-Us Purchase Transaction Processing
- STORY-079: Off-Us Purchase Transaction Processing
- STORY-080: On-Us Deposit Transaction Processing
- STORY-081: Off-Us Deposit Transaction Processing
- STORY-082: Balance Enquiry Transaction
- STORY-083: Statement Enquiry Transaction

**External Dependencies:**
- None — builds on existing switch and core banking infrastructure

---

## Definition of Done

- [ ] Code implemented and committed
- [ ] Module folder structure matches existing module patterns
- [ ] Proto file compiles and generates C# code
- [ ] EF Core migration created for card_transactions table
- [ ] Unit tests for CardTransactionValidator (>=80% coverage)
- [ ] Solution builds successfully
- [ ] Code reviewed and approved
- [ ] Deployed to staging

---

## Progress Tracking

**Status History:**
- 2026-03-15: Created

**Actual Effort:** TBD

---

**This story was created using BMAD Method v6 - Phase 4 (Implementation)**
