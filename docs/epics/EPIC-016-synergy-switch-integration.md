# EPIC-016: SynergySwitch Payment Switch Integration

**Created:** 2026-03-22
**Priority:** Must Have
**Status:** Not Started
**Estimated Points:** 47
**Estimated Stories:** 9

---

## Overview

Integrate the SynergySwitch payment switch into the UniBank platform as the EFT transaction gateway between POS terminals/ATMs and the core banking system. The switch supports two inbound protocols: ISO 8583 over TCP (from the national payment network / Zimswitch) and ISO 20022 over gRPC (from modern POS terminals). Both paths translate to gRPC calls against UniBank Core Banking's CardTransactionService.

SynergySwitch is a standalone .NET 10 application that has been moved into `switch/` within the UniBank monorepo. It already has:
- **ISO 8583** message parsing/building (NetCore8583) over persistent TCP connections
- **ISO 20022** AcceptorAuthorisationRequest/Response via gRPC (PaymentService)
- High-throughput TCP connection pooling (10k+ concurrent, STAN-multiplexed)
- BIN-based multi-gateway routing with longest-prefix matching
- Gateway protocol selection: ISO 8583 (TCP) or ISO 20022 (gRPC) per gateway
- gRPC services for terminal communication (PaymentService, TerminalManagementService)
- gRPC client for bank communication (Iso20022GrpcClient → CardTransactionService)
- Razor Pages admin dashboard
- Prometheus metrics and Grafana dashboards
- EMVco QR payment and mobile money payment support
- Podman containerization

## Goal

Complete the end-to-end integration so that card transactions flow through either path to core banking:

```
Path A (National Network / Legacy):
  Zimswitch → SynergySwitch (ISO 8583 ↔ gRPC) → UniBank Gateway → CardTransactions Module

Path B (Modern POS Terminals):
  POS Terminal → SynergySwitch (ISO 20022 ↔ gRPC) → UniBank Gateway → CardTransactions Module
```

Both paths converge at the same gRPC `CardTransactionService` endpoints. Responses flow back through the same path in reverse.

## Dependencies

- EPIC-015: Card Transaction Processing (Sprint 9) — provides the `CardTransactionService` gRPC endpoints that the switch calls
- EPIC-009: Terminal Management & HSM — terminal provisioning infrastructure

## Architecture

```
  ┌─────────────────────┐         ┌─────────────────────────┐
  │   National Network  │         │  POS Terminal / ATM     │
  │   (Zimswitch)       │         │  (Modern ISO 20022)     │
  └─────────┬───────────┘         └────────────┬────────────┘
            │ ISO 8583 / TCP                    │ ISO 20022 / gRPC
            │                                   │
            │           ┌───────────────────────┴──────────────┐
            │           │          SynergySwitch               │
            │           │                                      │
            │           │  ┌────────────────────────────────┐  │
            └───────────│──│ BankTcpClient (ISO 8583 pool)  │  │
                        │  └────────────┬───────────────────┘  │
                        │               │                      │
                        │  ┌────────────┴───────────────────┐  │
                        │  │ PaymentGrpcService (ISO 20022) │←─│── POS Terminal (gRPC)
                        │  │ TerminalMgmtGrpcService        │  │
                        │  └────────────┬───────────────────┘  │
                        │               │                      │
                        │  ┌────────────┴───────────────────┐  │
                        │  │ AuthorisationProcessor         │  │
                        │  │ GatewayManager (BIN routing)   │  │
                        │  │ On-Us detection                │  │
                        │  └────────────┬───────────────────┘  │
                        │               │ gRPC                 │
                        │  ┌────────────┴───────────────────┐  │
                        │  │ Iso20022GrpcClient             │──│──→ UniBank Gateway :1111
                        │  └────────────────────────────────┘  │    (CardTransactionService)
                        │                                      │
                        │  PostgreSQL: synergy_switch           │
                        │  Web Dashboard + Prometheus: :8080    │
                        └──────────────────────────────────────┘
```

## Stories

### STORY-084: Switch Proto Alignment & gRPC Client Configuration
**Points:** 5 | **Priority:** Must Have

As a **developer**, I want the switch's gRPC client to call UniBank's CardTransactionService with matching proto definitions, so that the switch can authorize card transactions against core banking.

**Acceptance Criteria:**
- Switch's `bank/card_transaction_service.proto` aligns with UniBank's `card_transaction_service.proto` (all 4 RPCs: ProcessPurchase, ProcessDeposit, BalanceEnquiry, StatementEnquiry)
- `Iso20022GrpcClient` is configured to connect to `gateway:1111` (container name) or configurable via environment variable
- Proto includes `is_on_us` field for balance and statement enquiry requests
- gRPC channel uses insecure credentials in development, TLS in production
- Health check verifies gRPC connectivity to gateway on startup

**Dependencies:** STORY-077 (CardTransactions scaffolding)

---

### STORY-085: On-Us Transaction Detection & Routing
**Points:** 5 | **Priority:** Must Have

As a **switch operator**, I want the switch to detect on-us transactions (both cardholder and merchant are UniBank clients) and route them directly to core banking without going through the national network, so that on-us transactions are faster and cheaper.

**Acceptance Criteria:**
- Switch identifies on-us transactions by checking if the PAN's BIN matches a configured "own BIN" list
- On-us transactions bypass the ISO 8583 TCP path and go directly to `Iso20022GrpcClient`
- `is_on_us = true` is set on gRPC requests for on-us transactions
- Off-us transactions continue through normal ISO 8583 → bank TCP → response path
- On-us routing is configurable per gateway (own BIN prefixes stored in gateway config)
- Transaction logs record whether the transaction was on-us or off-us

**Dependencies:** STORY-084

---

### STORY-086: Inbound Message ↔ gRPC Transaction Mapping (ISO 8583 & ISO 20022)
**Points:** 8 | **Priority:** Must Have

As a **developer**, I want the switch to translate both ISO 8583 (from national network) and ISO 20022 (from modern POS terminals) into gRPC CardTransactionService calls, so that both inbound paths are processed by core banking.

**Acceptance Criteria:**

**ISO 8583 → gRPC mapping:**
- Field mapping: field 2 (PAN) → card_holder_account, field 3 (processing code) → transaction type routing, field 4 (amount) → amount, field 11 (STAN) → stan, field 32 (acquiring institution) → acquiring_institution, field 37 (RRN) → retrieval_reference, field 41 (terminal ID) → terminal_id, field 42 (merchant ID) → merchant_id, field 43 (merchant name) → merchant_name
- Processing code mapping: "00xxxx" = purchase, "01xxxx" = cash withdrawal, "20xxxx" = deposit, "30xxxx" = balance enquiry, "40xxxx" = statement
- gRPC response mapped back to ISO 8583: response_code → field 39, authorization_code → field 38, available_balance → field 54
- EMV ICC data (field 55) parsed and preserved through the flow

**ISO 20022 → gRPC mapping:**
- `AcceptorAuthorisationRequest` fields mapped: environment.card_holder_account → card_holder_account, environment.merchant_id → merchant_id, transaction.amount → amount, transaction.transaction_type ("CRDP" = purchase, "CSHW" = withdrawal, "CDPT" = deposit) → gRPC RPC selection, transaction.transaction_reference → retrieval_reference
- `AcceptorAuthorisationResponse` built from gRPC response: authorization_code, result (approved/declined), display_message
- EMV ICC data (`icc_related_data` BER-TLV) parsed via `EmvTlvParser` and preserved

**Common:**
- Both paths converge at `AuthorisationProcessor` which calls `Iso20022GrpcClient`
- Missing fields default to safe values, invalid data returns response code "30" (format error)
- Transaction logs record the inbound protocol (ISO 8583 or ISO 20022)

**Dependencies:** STORY-084

---

### STORY-087: Switch Database Migration & Shared PostgreSQL
**Points:** 3 | **Priority:** Must Have

As a **developer**, I want the switch to use the shared PostgreSQL instance with its own database, so that the platform runs on a single database server in development.

**Acceptance Criteria:**
- `synergy_switch` database is auto-created during postgres container init (via `000_create_databases.sql`)
- Switch migrator runs against the shared postgres using connection string from environment
- All 5 existing migrations apply cleanly
- Switch and UniBank databases are fully isolated (separate databases, shared server)
- Connection string is configurable via `ConnectionStrings__SwitchDb` environment variable

**Dependencies:** None

---

### STORY-088: End-to-End Purchase Transaction Flow
**Points:** 8 | **Priority:** Must Have

As a **bank client**, I want my card purchase at a POS terminal to flow through the switch to core banking and back, so that my account is debited and the merchant is paid.

**Acceptance Criteria:**
- POS terminal sends `AcceptorAuthorisationRequest` via gRPC to switch
- Switch's `AuthorisationProcessor` builds ISO 8583 0200 or routes via gRPC (on-us)
- Core banking `ProcessPurchaseHandler` validates account, debits client, credits merchant (on-us) or suspense (off-us)
- Response flows back: core banking → switch → terminal with auth code or decline reason
- Transaction logged in both switch (`TransactionLogEntity`) and core banking (`CardTransaction`)
- Response time < 3 seconds for on-us, < 10 seconds for off-us (through national network)
- Idempotency: duplicate STAN returns original response from both switch and core banking

**Dependencies:** STORY-085, STORY-086

---

### STORY-089: End-to-End Deposit & Enquiry Flows
**Points:** 5 | **Priority:** Must Have

As a **bank client**, I want to make deposits, check my balance, and request mini-statements at POS terminals through the switch, so that all card transaction types are supported.

**Acceptance Criteria:**
- Deposit flow: terminal → switch → ProcessDeposit → merchant debited (on-us) or suspense debited (off-us) → client credited
- Balance enquiry flow: terminal → switch → BalanceEnquiry → available balance returned (ledger balance only for on-us)
- Statement enquiry flow: terminal → switch → StatementEnquiry → recent transactions returned (sanitized for off-us)
- All three flows logged and idempotent
- Processing code routing correctly identifies each transaction type

**Dependencies:** STORY-088

---

### STORY-090: Switch Monitoring & Alerting Integration
**Points:** 3 | **Priority:** Should Have

As an **operations engineer**, I want the switch's metrics and alerts to be visible in the unified Prometheus/Grafana stack, so that I can monitor the entire platform from one place.

**Acceptance Criteria:**
- Prometheus scrapes switch metrics from `switch:8080/metrics`
- Switch Grafana dashboard imported into the shared Grafana instance
- Alert rules for gateway connectivity, transaction error rates, and latency are active
- Switch metrics visible alongside core banking metrics in unified dashboards
- Key metrics: `switch_transactions_total`, `switch_transaction_duration_seconds`, `switch_gateway_connectivity_state`

**Dependencies:** None

---

### STORY-091: Switch Admin Dashboard Access
**Points:** 3 | **Priority:** Should Have

As a **switch operator**, I want to access the switch admin dashboard to manage gateways, terminals, and view transaction logs, so that I can operate the switch without CLI access.

**Acceptance Criteria:**
- Razor Pages dashboard accessible at `http://localhost:${SWITCH_WEB_PORT}`
- Dashboard shows: live transaction volume, gateway connectivity status, terminal count
- Gateway management: add/edit/disable gateways, configure BIN routes, view audit logs
- Terminal management: view registered terminals, heartbeat status
- Transaction viewer: search by RRN, STAN, terminal ID, date range, response code

**Dependencies:** STORY-087

---

### STORY-092: Gateway Failover & Offline Mode
**Points:** 7 | **Priority:** Should Have

As a **switch operator**, I want the switch to fail over between gateways and operate in offline mode when all gateways are down, so that transactions are not lost during outages.

**Acceptance Criteria:**
- If primary gateway's TCP pool is unhealthy, traffic routes to next gateway by priority
- Gateway health monitored every 3 seconds via sign-on (0810) messages
- If all gateways are down and `OfflineMode = true`, switch approves transactions locally with a configurable floor limit
- Offline-approved transactions are queued and forwarded when connectivity restores (store-and-forward)
- Gateway state changes (up/down/failover) logged and alerted via Prometheus
- Connection pool auto-recovers: failed connections replaced, sign-on re-attempted

**Dependencies:** STORY-088

---

## Epic Summary

| Story | Title | Points | Priority |
|-------|-------|--------|----------|
| STORY-084 | Switch Proto Alignment & gRPC Client Configuration | 5 | Must Have |
| STORY-085 | On-Us Transaction Detection & Routing | 5 | Must Have |
| STORY-086 | Inbound Message ↔ gRPC Mapping (ISO 8583 & ISO 20022) | 8 | Must Have |
| STORY-087 | Switch Database Migration & Shared PostgreSQL | 3 | Must Have |
| STORY-088 | End-to-End Purchase Transaction Flow | 8 | Must Have |
| STORY-089 | End-to-End Deposit & Enquiry Flows | 5 | Must Have |
| STORY-090 | Switch Monitoring & Alerting Integration | 3 | Should Have |
| STORY-091 | Switch Admin Dashboard Access | 3 | Should Have |
| STORY-092 | Gateway Failover & Offline Mode | 7 | Should Have |
| **Total** | | **47** | |

## Recommended Sprint Allocation

**Sprint 10 (Must Have — 34 points):**
- STORY-087: Switch Database Migration (3 pts) — foundation
- STORY-084: Proto Alignment & gRPC Client (5 pts) — enables all flows
- STORY-085: On-Us Detection & Routing (5 pts)
- STORY-086: ISO 8583 ↔ gRPC Mapping (8 pts)
- STORY-088: E2E Purchase Flow (8 pts)
- STORY-089: E2E Deposit & Enquiry Flows (5 pts)

**Sprint 10 (Should Have — 13 points):**
- STORY-090: Monitoring Integration (3 pts)
- STORY-091: Admin Dashboard (3 pts)
- STORY-092: Gateway Failover & Offline Mode (7 pts)

---

**This epic was created using BMAD Method v6 - Phase 4 (Implementation)**
