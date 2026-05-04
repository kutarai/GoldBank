# System Architecture: GoldBank

**Date:** 2026-02-24
**Architect:** wmapundu
**Version:** 1.0
**Project Type:** other (multi-component banking suite)
**Project Level:** 4
**Status:** Draft

---

## Document Overview

This document defines the system architecture for GoldBank. It provides the technical blueprint for implementation, addressing all functional and non-functional requirements from the PRD.

**Related Documents:**
- Product Requirements Document: docs/prd-goldbank-2026-02-24.md
- Product Brief: docs/product-brief-goldbank-2026-02-24.md

---

## Executive Summary

GoldBank is architected as a **modular monolith with dedicated satellite services**, built on **.NET 10** with **PostgreSQL 18**, communicating via **gRPC** for speed and **Wolverine + MQTT** for async messaging. The architecture is designed for on-premise deployment using Docker containers, supporting multi-tenant white-label deployments with schema-per-tenant data isolation. Satellite services handle PCI-sensitive operations (switching, HSM, terminal management) with network segmentation, while the core banking monolith manages all business logic through well-defined internal modules.

---

## Architectural Drivers

These requirements heavily influence architectural decisions:

1. **NFR-001: Payment transaction < 2 seconds** — Requires gRPC binary protocol, optimized transaction pipeline, Redis caching, connection pooling
2. **NFR-002: 1,000 concurrent users** — Requires stateless services, connection pooling, async processing via Wolverine
3. **NFR-004/005: TLS + AES-256 encryption** — Requires encryption at every layer, TLS 1.3 on all channels, mTLS server-to-server
4. **NFR-006: PCI-DSS compliance** — Requires network segmentation, isolated switching schema, tokenized card data
5. **NFR-007: HSM-based key management** — Requires dedicated HSM Interface Service with PKCS#11 interop
6. **NFR-011: 99.9% uptime** — Requires redundancy, health monitoring, Docker restart policies, failover
7. **NFR-013: Transaction atomicity** — Requires ACID-compliant PostgreSQL, EF Core transactions, Wolverine outbox pattern
8. **NFR-017/018: ISO 8583/20022 + EMV compliance** — Requires adapter pattern in switching server, standards-compliant message handling

---

## System Overview

### High-Level Architecture

The system consists of 9 major components organized in a modular monolith with satellite services pattern:

1. **API Gateway** — Single entry point with gRPC interceptors for auth, routing, rate limiting
2. **Core Banking Service** — Modular monolith handling accounts, payments, transfers, bills, merchants, multi-tenancy
3. **Switching Server** — Satellite service for ISO 8583/20022 national payment switch integration
4. **Terminal Manager** — Satellite service managing EFT POS terminals via MQTT
5. **HSM Interface Service** — Satellite service for cryptographic operations via PKCS#11
6. **Admin Portal** — Blazor Server web application for back-office management
7. **Reporting Engine** — Analytics, dashboards, and report generation
8. **Notification Service** — Push notifications, SMS, and transaction alerts
9. **Message Bus** — Wolverine + MQTT embedded broker for async communication

### Architecture Diagram

```
                          ┌─────────────────────┐
                          │     Mobile App       │
                          │  (KMP / Android+iOS) │
                          └──────────┬──────────┘
                                     │ gRPC/TLS
                          ┌──────────▼──────────┐
                          │    API Gateway       │
                          │  (.NET 10 + gRPC     │
                          │   Interceptors)      │
                          │  • JWT Auth          │
                          │  • Tenant Routing    │
                          │  • Rate Limiting     │
                          └──────────┬──────────┘
                                     │ gRPC (internal)
              ┌──────────────────────┼──────────────────────┐
              │                      │                      │
    ┌─────────▼──────────┐ ┌────────▼─────────┐  ┌─────────▼──────────┐
    │  Core Banking       │ │  Admin Portal    │  │  Notification      │
    │  Service            │ │  (Blazor Server) │  │  Service           │
    │  (.NET 10)          │ │  (.NET 10)       │  │  (.NET 10)         │
    │  ┌────────────────┐ │ └────────┬─────────┘  └──────────────────┘
    │  │ Accounts       │ │          │ gRPC-Web
    │  │ Payments       │ │   ┌──────▼────────┐
    │  │ Transfers      │ │   │  Reporting    │
    │  │ AgentBanking   │ │   │  Engine       │
    │  │ BillPay        │ │   │  (.NET 10)    │
    │  │ Merchants      │ │   └───────────────┘
    │  │ MultiTenant    │ │
    │  └────────────────┘ │
    └─────────┬───────────┘
              │ Wolverine Commands/Events
    ┌─────────▼─────────────────────────────────────────┐
    │           Wolverine + MQTT Embedded Broker          │
    └────┬──────────────────┬───────────────┬────────────┘
         │                  │               │
┌────────▼─────────┐ ┌─────▼────────┐ ┌────▼──────────────┐
│ Switching Server │ │  Terminal    │ │  HSM Interface    │
│ (.NET 10)        │ │  Manager    │ │  Service          │
│ ┌──────────────┐ │ │  (.NET 10)  │ │  (.NET 10)        │
│ │ ISO 8583     │ │ │             │ │  PKCS#11 Interop  │
│ │ ISO 20022    │ │ │  MQTT ↕     │ └────┬──────────────┘
│ │ Adapters     │ │ │  Terminals  │      │
│ └──────────────┘ │ └─────┬──────┘      │
└────────┬─────────┘       │         Hardware
         │             EFT POS       Security
    National           Terminals     Module
    Switch
```

### Architectural Pattern

**Pattern:** Modular Monolith with Satellite Services

**Rationale:**
- **Modular Monolith** for core banking — manageable for a 4-developer team, clear module boundaries, single deployment unit, shared transaction context for ACID operations
- **Satellite Services** for switching, terminal management, and HSM — PCI-DSS network segmentation, hardware-bound (HSM), dedicated network connections (national switch), independent lifecycle
- **Not microservices** — too complex operationally for 4 developers, distributed transactions would add latency to the payment critical path
- **Not pure monolith** — PCI compliance requires isolation of payment processing components

---

## Technology Stack

### Mobile App

**Choice:** Kotlin Multiplatform (KMP) + Jetpack Compose (Android) / SwiftUI (iOS)

**Rationale:** KMP enables shared business logic between Android and iOS while allowing platform-native UIs. Android-first deployment with iOS following. Kotlin gRPC client for server communication.

**Trade-offs:** KMP ecosystem is maturing but not as established as Flutter/React Native. Gain: native performance and NFC access. Lose: some shared UI code.

### Backend

**Choice:** C# / ASP.NET Core (.NET 10)

**Rationale:** Enterprise-grade framework with excellent gRPC support, strong typing, high performance (Kestrel), mature ecosystem for banking (encryption, security libraries). Team expertise in C#/.NET.

**Trade-offs:** Gain: developer productivity, type safety, rich ecosystem. Lose: slightly higher memory footprint than Go.

### Communication

**Choice:** gRPC with Protocol Buffers

**Rationale:** Binary serialization is 5-10x faster than JSON REST. Strong service contracts via .proto files. HTTP/2 multiplexing. Native .NET support. Streaming support for large result sets.

**Trade-offs:** Gain: speed, strong contracts, streaming. Lose: less human-readable than REST, requires gRPC-Web adapter for browser (Blazor).

### Database

**Choice:** PostgreSQL 18

**Rationale:** ACID-compliant, free licensing (critical for on-premise), excellent JSON support for flexible config, table partitioning for high-volume transaction tables, mature replication for read replicas.

**Trade-offs:** Gain: zero licensing cost, proven reliability, excellent .NET support via Npgsql. Lose: some enterprise tooling vs. SQL Server.

### Messaging

**Choice:** Wolverine + MQTT Embedded Broker

**Rationale:** Wolverine provides command/event handling, saga orchestration for multi-step transactions, durable outbox pattern for guaranteed delivery. MQTT is lightweight and ideal for POS terminal communication with intermittent connectivity.

**Trade-offs:** Gain: built-in saga support, durable outbox, .NET-native, lightweight terminal protocol. Lose: smaller community than RabbitMQ/Kafka.

### Caching

**Choice:** Redis

**Rationale:** In-memory key-value store for hot data caching (balances, tenant config), session state management, and pub/sub for real-time updates. Excellent .NET client support.

### Admin Portal

**Choice:** Blazor Server (.NET 10)

**Rationale:** Full-stack C# — no JavaScript framework needed. Server-side rendering with real-time updates via SignalR. Same team can build frontend and backend. gRPC-Web for backend communication.

**Trade-offs:** Gain: single language stack, real-time dashboards. Lose: requires persistent SignalR connection, sticky sessions.

### Containerization

**Choice:** Docker + Docker Compose

**Rationale:** On-premise deployment with reproducible environments. Docker Compose orchestrates all services. Simpler than Kubernetes for a 4-person team.

### Monitoring & Logging

**Choice:** Prometheus + Grafana (metrics), Serilog + ELK Stack (logging)

**Rationale:** Open source, on-premise compatible. Prometheus scrapes .NET metrics endpoints. Grafana provides dashboards and alerting. Serilog provides structured logging with PII masking. ELK enables centralized log search.

### CI/CD

**Choice:** GitLab CI or Jenkins

**Rationale:** On-premise compatible CI/CD. Automated build, test, containerize, deploy pipeline.

---

## System Components

### Component 1: API Gateway

**Purpose:** Single entry point for all client requests (mobile app, admin portal)

**Responsibilities:**
- gRPC request routing to backend services
- JWT token validation via interceptors
- Tenant identification and context injection
- Rate limiting (per-user, per-tenant)
- Request/response logging (with PII masking)
- API versioning

**Interfaces:**
- gRPC/TLS (port 443) — mobile app
- gRPC-Web (port 443) — Blazor admin portal

**Dependencies:**
- Core Banking Service (routing target)
- Redis (rate limiting counters, session cache)

**FRs Addressed:** All client-facing FRs route through gateway

---

### Component 2: Core Banking Service (Modular Monolith)

**Purpose:** All core business logic and transaction processing

**Responsibilities:**
- User registration, KYC, account management
- Payment processing (NFC, QR)
- P2P transfers (domestic, cross-border)
- Agent banking (cash-in/out, float, commission)
- Bill payments
- Merchant management and settlement
- Multi-tenant configuration and data isolation
- Fee calculation engine
- Transaction orchestration

**Module Structure:**

```
Core Banking Service
├── Modules/
│   ├── Accounts/           (FR-001 to FR-007)
│   │   ├── Registration
│   │   ├── KYC
│   │   └── Profile & Balance
│   ├── Payments/           (FR-008 to FR-014)
│   │   ├── NFC Processing
│   │   ├── QR Code Processing
│   │   └── Payment Authorization
│   ├── Transfers/          (FR-015 to FR-018)
│   │   ├── P2P Domestic
│   │   └── P2P Cross-Border
│   ├── AgentBanking/       (FR-019 to FR-023)
│   │   ├── CashIn / CashOut
│   │   ├── Float Management
│   │   └── Commission Engine
│   ├── BillPay/            (FR-024 to FR-027)
│   │   ├── Provider Registry
│   │   └── Bill Processing
│   ├── Merchants/          (FR-037 to FR-041)
│   │   ├── Onboarding
│   │   ├── Settlement
│   │   └── Reporting
│   └── MultiTenant/        (FR-055 to FR-058)
│       ├── Tenant Config
│       ├── Branding
│       └── Data Isolation (schema routing)
├── SharedKernel/
│   ├── Domain Events
│   ├── Transaction Engine (ACID)
│   └── Fee Calculator
└── Infrastructure/
    ├── Persistence (EF Core + PostgreSQL 18)
    ├── Wolverine Handlers (commands/events)
    └── Redis Cache
```

**Interfaces:**
- gRPC services (called via gateway)
- Wolverine commands/events (internal async)

**Dependencies:**
- PostgreSQL 18 (persistence)
- Redis (caching)
- Wolverine (messaging)
- Switching Server (via Wolverine commands)
- HSM Interface (via gRPC for crypto operations)
- Notification Service (via Wolverine events)

---

### Component 3: Switching Server (Satellite)

**Purpose:** Route transactions to/from national payment switch(es)

**Responsibilities:**
- Outgoing transaction routing to national switch
- Incoming transaction processing from national switch
- ISO 8583 message formatting and parsing
- ISO 20022 message formatting and parsing
- Adapter pattern for multi-protocol support
- Internal canonical message format translation
- Daily reconciliation
- MAC generation coordination (via HSM)

**Structure:**

```
Switching Server
├── MessageRouter/
│   ├── InboundHandler     (incoming from national switch)
│   └── OutboundHandler    (outgoing to national switch)
├── Adapters/
│   ├── ISO8583Adapter     (FR-031)
│   └── ISO20022Adapter    (FR-064)
├── CanonicalFormat/
│   └── InternalMessage    (unified internal representation)
├── Reconciliation/        (FR-030)
│   └── DailyRecon
└── ConnectionManager/
    ├── TCP/IP Socket Pool (ISO 8583)
    └── MQ/API Client      (ISO 20022)
```

**Interfaces:**
- Wolverine commands from Core Banking
- TCP/IP sockets to national switch (ISO 8583)
- MQ/API to national switch (ISO 20022)
- gRPC to HSM Interface (MAC generation)

**Dependencies:**
- National payment switch (external)
- HSM Interface Service (MAC generation)
- PostgreSQL (switching schema — isolated)

**FRs Addressed:** FR-028, FR-029, FR-030, FR-031, FR-064

---

### Component 4: Terminal Manager (Satellite)

**Purpose:** Manage EFT POS terminal lifecycle, configuration, and monitoring

**Responsibilities:**
- Terminal registration and provisioning
- Remote configuration and key distribution
- Terminal status monitoring (online/offline/fault)
- Remote software updates
- MQTT broker for terminal communication

**MQTT Topics:**
- `terminals/{terminalId}/status` — heartbeat and status updates
- `terminals/{terminalId}/config` — configuration push
- `terminals/{terminalId}/keys` — key injection commands
- `terminals/{terminalId}/updates` — software update delivery

**Interfaces:**
- MQTT broker (terminal communication)
- Wolverine events (internal)
- gRPC to HSM Interface (key distribution)

**Dependencies:**
- HSM Interface (key generation and distribution)
- PostgreSQL (terminal registry)
- MQTT embedded broker

**FRs Addressed:** FR-032, FR-035, FR-036

---

### Component 5: HSM Interface Service (Satellite)

**Purpose:** Cryptographic operations — PIN handling, key management, MAC generation, token generation

**Responsibilities:**
- Master key generation and storage (within HSM)
- Session key derivation and distribution
- PIN block encryption/decryption
- PIN translation for switch forwarding
- MAC generation for switch messages
- NFC payment token generation
- Key rotation on configurable schedule
- Audit logging of all crypto operations

**Interfaces:**
- gRPC (internal only — never exposed externally)
- PKCS#11 interop to physical HSM hardware

**Dependencies:**
- Physical HSM hardware

**FRs Addressed:** FR-009, FR-010, FR-033, FR-034

---

### Component 6: Admin Portal

**Purpose:** Back-office web application for operations, support, compliance, and finance teams

**Responsibilities:**
- Admin user management with RBAC
- Customer account management (view, suspend, freeze, close)
- Merchant/agent management (approve, suspend, configure)
- Transaction monitoring and search
- KYC review and approval workflow
- System configuration management (fees, limits)
- Dispute/chargeback management

**Interfaces:**
- HTTPS web UI (Blazor Server)
- gRPC-Web to Core Banking and Reporting services

**Dependencies:**
- Core Banking Service (data operations)
- Reporting Engine (dashboards and reports)

**FRs Addressed:** FR-042 to FR-048

---

### Component 7: Reporting Engine

**Purpose:** Analytics, dashboards, report generation, and data export

**Responsibilities:**
- Real-time transaction dashboard
- User registration and growth reports
- Merchant/agent performance reports
- Revenue and fee reports
- Reconciliation reports
- Report export (CSV, PDF)

**Interfaces:**
- gRPC service (consumed by Admin Portal)

**Dependencies:**
- PostgreSQL read replica (heavy queries don't impact production)
- Redis (cached aggregations)

**FRs Addressed:** FR-049 to FR-054

---

### Component 8: Notification Service

**Purpose:** Push notifications, SMS, and transaction alerts

**Responsibilities:**
- Subscribe to Wolverine events (TransactionCompleted, KYCApproved, etc.)
- Send push notifications via Firebase Cloud Messaging
- Send SMS via SMS gateway
- Transaction receipts and alerts
- Low float warnings for agents

**Interfaces:**
- Wolverine event handlers (subscribes to domain events)
- Firebase Cloud Messaging (external)
- SMS gateway (external)

**Dependencies:**
- Wolverine (event source)
- Firebase Cloud Messaging
- SMS gateway provider

**FRs Addressed:** FR-011, FR-014, FR-018, FR-023, FR-026

---

## Data Architecture

### Data Model

**Core Entities:**

```
Tenant (id, name, branding_config, fee_config, status, created_at)
├── Has many: Accounts, Merchants, AdminUsers
│
Account (id, tenant_id, phone, pin_hash, balance, status, kyc_status, device_id, created_at)
├── Belongs to: Tenant
├── Has many: Transactions, KYCRecords
│
KYCRecord (id, account_id, id_document_ref, selfie_ref, status, reviewer_id, reviewed_at)
├── Belongs to: Account
│
Transaction (id, tenant_id, type, amount, fee, commission, source_account_id, dest_account_id,
             merchant_id, reference, status, created_at)
├── Belongs to: Account(s), Merchant (optional)
│
Merchant (id, tenant_id, business_name, owner_name, location, gps_lat, gps_lon,
          agent_status, float_balance, kyc_status, status, created_at)
├── Belongs to: Tenant
├── Has many: Terminals, Settlements, Transactions
│
Terminal (id, merchant_id, terminal_id, model, status, last_seen, config, created_at)
├── Belongs to: Merchant
│
Settlement (id, merchant_id, period_start, period_end, transaction_total, fee_total,
            commission_total, net_amount, status, created_at)
├── Belongs to: Merchant
│
BillProvider (id, name, category, account_format, min_amount, max_amount, status)
├── Has many: BillPayments
│
AdminUser (id, tenant_id, email, password_hash, role, status, last_login, created_at)
├── Belongs to: Tenant
│
AuditLog (id, tenant_id, actor_id, actor_type, action, entity_type, entity_id,
          details_json, ip_address, timestamp)
├── Immutable, append-only
```

### Database Design

**Schema Strategy: Schema-per-tenant for data isolation**

```
PostgreSQL 18
├── public schema (shared)
│   ├── tenants                    -- tenant registry
│   ├── bill_providers             -- shared bill provider catalog
│   └── system_config              -- global system parameters
│
├── tenant_{id} schema (per tenant)
│   ├── accounts                   -- user accounts
│   ├── kyc_records                -- KYC submissions and reviews
│   ├── transactions               -- PARTITIONED BY RANGE (created_at) monthly
│   ├── merchants                  -- merchant/agent records
│   ├── terminals                  -- POS terminal registry
│   ├── settlements                -- merchant settlement records
│   ├── saved_billers              -- user saved bill providers
│   ├── admin_users                -- tenant admin users
│   └── audit_logs                 -- PARTITIONED BY RANGE (timestamp) monthly
│
└── switching schema (PCI-isolated)
    ├── switch_transactions        -- switch message log
    ├── reconciliation_records     -- daily recon results
    └── message_logs               -- raw ISO 8583/20022 message archive
```

**Key Indexes:**
- `accounts`: (tenant_id, phone) UNIQUE, (tenant_id, kyc_status)
- `transactions`: (tenant_id, created_at), (source_account_id, created_at), (merchant_id, created_at)
- `merchants`: (tenant_id, agent_status), (tenant_id, location)
- `terminals`: (terminal_id) UNIQUE, (merchant_id, status)
- `audit_logs`: (tenant_id, timestamp), (actor_id, timestamp)

**Partitioning:**
- `transactions` — monthly range partitions on `created_at`
- `audit_logs` — monthly range partitions on `timestamp`
- Old partitions can be archived to cold storage after retention period

### Data Flow

**Write Path (Transaction):**
```
Mobile App
  → gRPC/TLS → API Gateway (JWT validation, tenant routing)
    → gRPC → Core Banking (business logic, fee calc, balance check)
      → EF Core → PostgreSQL (ACID transaction: debit + credit + fee + log)
      → Wolverine Event: TransactionCompleted
        → Notification Service (push notification)
        → Reporting Engine (async aggregation update)
        → Switching Server (if external transaction)
```

**Read Path (Balance Inquiry):**
```
Mobile App
  → gRPC/TLS → API Gateway
    → gRPC → Core Banking
      → Redis Cache (hit?) → Return cached balance
      → Cache miss → PostgreSQL → Update Redis (5s TTL) → Return
```

**Switching Path:**
```
Outbound:
Core Banking → Wolverine Command → Switching Server
  → Adapter (ISO 8583 or ISO 20022) → National Switch
  → Response → Wolverine Event → Core Banking → Update transaction status

Inbound:
National Switch → Switching Server → Parse message
  → Wolverine Event → Core Banking → Credit account → Notification
```

---

## API Design

### API Architecture

| Aspect | Design |
|--------|--------|
| **Protocol** | gRPC with Protocol Buffers |
| **Versioning** | Package-level in proto (e.g., `goldbank.v1.accounts`) |
| **Authentication** | JWT Bearer tokens in gRPC metadata |
| **Authorization** | Role-based claims in JWT |
| **Streaming** | Server streaming for transaction history and report export |
| **Error handling** | gRPC status codes with detailed error metadata |
| **Contracts** | Shared `.proto` files in GoldBank.Protos project |

### Endpoints

**Account Services:**
```protobuf
service AccountService {
  rpc Register(RegisterRequest) returns (RegisterResponse);
  rpc VerifyOTP(VerifyOTPRequest) returns (VerifyOTPResponse);
  rpc SubmitKYC(SubmitKYCRequest) returns (KYCResponse);
  rpc GetProfile(GetProfileRequest) returns (ProfileResponse);
  rpc UpdateProfile(UpdateProfileRequest) returns (ProfileResponse);
  rpc GetBalance(GetBalanceRequest) returns (BalanceResponse);
  rpc GetTransactionHistory(HistoryRequest) returns (stream TransactionResponse);
}
```

**Payment Services:**
```protobuf
service PaymentService {
  rpc InitiateNFCPayment(NFCPaymentRequest) returns (PaymentResponse);
  rpc GenerateQRCode(QRCodeRequest) returns (QRCodeResponse);
  rpc ProcessQRPayment(QRPaymentRequest) returns (PaymentResponse);
  rpc AuthorizeTransaction(AuthorizeRequest) returns (AuthorizeResponse);
}
```

**Transfer Services:**
```protobuf
service TransferService {
  rpc SendP2P(P2PTransferRequest) returns (TransferResponse);
  rpc SendCrossBorder(CrossBorderRequest) returns (TransferResponse);
  rpc ValidateRecipient(ValidateRecipientRequest) returns (RecipientResponse);
}
```

**Agent Banking Services:**
```protobuf
service AgentService {
  rpc CashIn(CashInRequest) returns (AgentTransactionResponse);
  rpc CashOut(CashOutRequest) returns (AgentTransactionResponse);
  rpc GetFloatBalance(FloatBalanceRequest) returns (BalanceResponse);
  rpc GetCommissionReport(CommissionRequest) returns (CommissionResponse);
}
```

**Bill Payment Services:**
```protobuf
service BillPayService {
  rpc ListProviders(ListProvidersRequest) returns (ProvidersResponse);
  rpc PayBill(PayBillRequest) returns (PaymentResponse);
  rpc GetSavedBillers(SavedBillersRequest) returns (SavedBillersResponse);
  rpc SaveBiller(SaveBillerRequest) returns (SaveBillerResponse);
}
```

**Merchant Services:**
```protobuf
service MerchantService {
  rpc Register(MerchantRegisterRequest) returns (MerchantResponse);
  rpc GetProfile(MerchantProfileRequest) returns (MerchantResponse);
  rpc GetTransactions(MerchantTxnRequest) returns (stream TransactionResponse);
  rpc GetSettlements(SettlementRequest) returns (SettlementResponse);
}
```

**Terminal Services:**
```protobuf
service TerminalService {
  rpc RegisterTerminal(TerminalRegisterRequest) returns (TerminalResponse);
  rpc GetTerminalStatus(TerminalStatusRequest) returns (TerminalStatusResponse);
  rpc PushConfig(TerminalConfigRequest) returns (ConfigResponse);
  rpc RequestKeyInjection(KeyInjectionRequest) returns (KeyResponse);
}
```

**Admin Services:**
```protobuf
service AdminService {
  rpc SearchCustomers(SearchRequest) returns (CustomerListResponse);
  rpc ManageAccount(AccountActionRequest) returns (AccountActionResponse);
  rpc ReviewKYC(KYCReviewRequest) returns (KYCReviewResponse);
  rpc ManageMerchant(MerchantActionRequest) returns (MerchantActionResponse);
  rpc SearchTransactions(TxnSearchRequest) returns (stream TransactionResponse);
  rpc UpdateSystemConfig(ConfigUpdateRequest) returns (ConfigResponse);
}
```

**Reporting Services:**
```protobuf
service ReportingService {
  rpc GetDashboard(DashboardRequest) returns (DashboardResponse);
  rpc GetUserGrowthReport(ReportRequest) returns (ReportResponse);
  rpc GetMerchantReport(ReportRequest) returns (ReportResponse);
  rpc GetRevenueReport(ReportRequest) returns (ReportResponse);
  rpc GetReconReport(ReconRequest) returns (ReconResponse);
  rpc ExportReport(ExportRequest) returns (stream ExportChunk);
}
```

### Authentication & Authorization

**Authentication Flow:**
```
Mobile Login:
Phone + PIN → gRPC/TLS → Gateway → Core Banking
                                        │
                                        ├── Validate PIN (bcrypt compare)
                                        ├── Check device binding (device_id match)
                                        ├── Generate JWT access token (15 min expiry)
                                        ├── Generate refresh token (30 day expiry, rotated)
                                        └── Return tokens in response

Biometric Login:
Device biometric → Unlock local keystore → Retrieve stored credentials → Same flow

Token Refresh:
Refresh token → Gateway → Core Banking → Validate + Rotate → New access + refresh tokens
```

**JWT Token Contents:**
```json
{
  "sub": "user_id",
  "tenant_id": "tenant_uuid",
  "role": "consumer|merchant|agent|admin",
  "device_id": "device_uuid",
  "permissions": ["payments", "transfers", "bills"],
  "exp": 1740000000,
  "iss": "goldbank-gateway"
}
```

**Role-Based Access Control:**
```
Roles:
├── consumer      → Account, payments, transfers, bills
├── merchant      → Merchant portal, transaction history, settlements
├── agent         → Cash-in/out, float management, commission
├── admin
│   ├── super_admin    → Full system access
│   ├── operations     → Transaction monitoring, merchant management
│   ├── support        → Customer account management, disputes
│   ├── finance        → Settlements, reconciliation, revenue reports
│   └── compliance     → KYC review, audit logs, fraud alerts
└── tenant_admin  → Tenant-scoped admin (white-label institution)
```

**Server-to-Server Authentication:**
- mTLS with certificate-based authentication between all internal services
- Certificates managed and rotated on schedule

---

## Non-Functional Requirements Coverage

### NFR-001: Payment Transaction Performance

**Requirement:** Payment transaction end-to-end response time < 2 seconds for 95% of requests

**Architecture Solution:**
- gRPC binary protocol (5-10x faster than JSON REST)
- Redis caching eliminates redundant DB lookups during transaction flow
- Connection pooling (Npgsql + gRPC channel reuse)
- Optimized transaction pipeline — minimal service hops for payment critical path
- Wolverine for async post-transaction work (notifications, reporting) — off the hot path

**Validation:** Monitor p95 latency via Prometheus; load test with NBomber targeting 1000 TPS

---

### NFR-002: Concurrent User Capacity

**Requirement:** System must support 1,000 concurrent users

**Architecture Solution:**
- Stateless Core Banking service (horizontal scaling via Docker replicas)
- gRPC HTTP/2 multiplexing (many concurrent requests per connection)
- Connection pooling to PostgreSQL (Npgsql pool)
- Async processing via Wolverine (non-blocking operations)

**Validation:** Load test with k6/NBomber simulating 1000 concurrent gRPC sessions

---

### NFR-003: Non-Payment API Performance

**Requirement:** Non-payment API response < 500ms for 95% of requests

**Architecture Solution:**
- Redis caching: balance (5s TTL), tenant config (5min TTL), bill providers (1hr TTL)
- PostgreSQL indexing on common query paths
- gRPC streaming for large result sets (avoids payload size issues)

**Validation:** Monitor p95 latency per endpoint in Grafana

---

### NFR-004: Data in Transit Encryption

**Requirement:** All data encrypted via TLS 1.2+

**Architecture Solution:**
- TLS 1.3 on all external gRPC channels (mobile → gateway)
- mTLS on all internal gRPC channels (service-to-service)
- Certificate pinning in mobile app
- No plaintext communication anywhere in the system

**Validation:** TLS scanner verification, certificate expiry monitoring

---

### NFR-005: Data at Rest Encryption

**Requirement:** All sensitive data encrypted using AES-256

**Architecture Solution:**
- PostgreSQL Transparent Data Encryption (TDE) with AES-256
- KYC documents (ID images, selfies) encrypted with AES-256, keys stored in HSM
- Redis configured with encrypted persistence
- Backup files encrypted at rest

**Validation:** Encryption audit, verify no plaintext sensitive data in storage

---

### NFR-006: PCI-DSS Compliance

**Requirement:** PCI-DSS compliance for payment data handling

**Architecture Solution:**
- Network segmentation: switching schema isolated from general data
- Switching Server runs in dedicated Docker network segment
- Card/token data never stored in plaintext
- HSM-based key management (no software key storage)
- Audit logging of all access to payment data
- Role-based access restricts payment data visibility

**Validation:** PCI-DSS self-assessment questionnaire, penetration testing

---

### NFR-007: HSM-Based Key Management

**Requirement:** All cryptographic key management uses HSM

**Architecture Solution:**
- Dedicated HSM Interface Service with PKCS#11 interop
- Master keys generated and stored exclusively within HSM boundary
- Session keys derived via HSM
- PIN encryption/decryption within HSM
- All key operations audited

**Validation:** HSM audit log review, verify no keys exist outside HSM

---

### NFR-008: Audit Logging

**Requirement:** Immutable audit trail for all financial transactions and admin actions

**Architecture Solution:**
- Append-only `audit_logs` table with monthly partitioning
- Database-level write-only permissions (no UPDATE/DELETE on audit tables)
- Structured logging via Serilog with correlation IDs
- 7-year retention with partition archival to cold storage
- PII masking in application logs (Serilog enrichers)

**Validation:** Attempt to modify audit records (should fail), retention policy verification

---

### NFR-009: 500K User Capacity

**Requirement:** Support 500,000 registered users in year 1

**Architecture Solution:**
- Schema-per-tenant scales independently per deployment
- Transaction table partitioning handles volume growth
- PostgreSQL with proper indexing handles millions of records efficiently
- KYC document storage on encrypted file system with reference in DB

**Validation:** Load test with synthetic 500K user dataset

---

### NFR-010: Horizontal Scalability

**Requirement:** Architecture supports horizontal scaling

**Architecture Solution:**
- Stateless services (Gateway, Core Banking, Notifications) run as Docker replicas
- Redis for shared state (session, cache) enables stateless services
- Docker Compose scale command for adding instances
- PostgreSQL read replicas for reporting workload

**Validation:** Scale to 2+ replicas, verify load distribution

---

### NFR-011: System Availability (99.9%)

**Requirement:** 99.9% uptime (~8.7 hours downtime/year)

**Architecture Solution:**
- Docker restart policies (always restart on failure)
- Health check endpoints on all services (gRPC health protocol)
- Prometheus monitoring with Grafana alerting
- Redundant instances for critical services (Gateway, Core Banking)
- PostgreSQL streaming replication for database failover

**Validation:** Uptime monitoring dashboard, incident tracking

---

### NFR-012: Database Backup and Recovery

**Requirement:** Daily backups with point-in-time recovery, RPO 1hr, RTO 4hr

**Architecture Solution:**
- PostgreSQL WAL (Write-Ahead Log) archiving for continuous backup
- Daily full backups via pg_basebackup
- WAL archiving enables point-in-time recovery to any second
- Encrypted backup storage
- Monthly restore testing

**Validation:** Monthly restore drill, verify RPO/RTO targets

---

### NFR-013: Transaction Atomicity

**Requirement:** All financial transactions are atomic

**Architecture Solution:**
- PostgreSQL ACID transactions via EF Core transaction scope
- Single database for core banking (no distributed transactions on critical path)
- Wolverine outbox pattern for cross-service consistency (debit account → send to switch)
- Wolverine sagas for multi-step operations (P2P: debit sender → credit receiver)
- Idempotency keys prevent duplicate processing

**Validation:** Chaos testing — kill service mid-transaction, verify no partial state

---

### NFR-014: Multi-Language Support

**Requirement:** English + at least one regional language

**Architecture Solution:**
- Mobile app: Android/iOS resource files for localized strings
- Tenant-configurable default language
- Admin portal: Blazor localization middleware

**Validation:** Switch language, verify all UI strings translate correctly

---

### NFR-015: Onboarding Under 10 Minutes

**Requirement:** Registration to first transaction in < 10 minutes

**Architecture Solution:**
- Streamlined gRPC registration flow (phone → OTP → PIN → KYC → active)
- KYC auto-approval for clear matches (manual review only for edge cases)
- Account immediately active upon KYC pass
- First cash-in available immediately

**Validation:** Time end-to-end onboarding flow in testing

---

### NFR-016: Mobile Platform Support

**Requirement:** Android 8.0+ and iOS 14+

**Architecture Solution:**
- KMP shared business logic (gRPC client, domain models, crypto)
- Android: Jetpack Compose UI, NFC via Android HCE API (API 26+)
- iOS: SwiftUI, NFC via Core NFC framework (iOS 14+)
- gRPC client via grpc-kotlin (Android) and grpc-swift (iOS)

**Validation:** Test on minimum supported OS versions

---

### NFR-017: ISO 8583 Compliance

**Requirement:** National switch communication via ISO 8583

**Architecture Solution:**
- Dedicated ISO 8583 adapter in Switching Server
- TCP/IP socket connection management with keep-alive
- Message builder/parser compliant with national scheme specification
- MAC generation via HSM Interface
- Full message logging in switching schema

**Validation:** Scheme certification testing, sandbox validation

---

### NFR-018: EMV Contactless Compliance

**Requirement:** NFC payments comply with EMV contactless specs

**Architecture Solution:**
- EMV contactless kernel implementation in mobile app (HCE)
- Payment tokenization via HSM
- EMV QR Code generation per EMV QR specification
- Terminal-side EMV processing on certified POS terminals

**Validation:** EMV test tool certification, terminal certification

---

## Security Architecture

### Authentication

| Aspect | Design |
|--------|--------|
| **Consumer auth** | Phone + PIN/biometric → JWT (15 min access + 30 day refresh) |
| **Admin auth** | Email + password → JWT with admin role claims |
| **Service-to-service** | mTLS with certificate-based authentication |
| **Token storage** | Mobile: encrypted keystore. Server: Redis with encryption |
| **Failed attempts** | 5 failures → 30 min lockout, alert generated |
| **Device binding** | device_id in JWT must match registered device (FR-062) |

### Authorization

| Role | Access Scope |
|------|-------------|
| **consumer** | Own account, payments, transfers, bills |
| **merchant** | Own merchant profile, transactions, settlements |
| **agent** | Cash-in/out operations, float, commission |
| **super_admin** | Full system access across all tenants |
| **operations** | Transaction monitoring, merchant management |
| **support** | Customer account management, disputes |
| **finance** | Settlements, reconciliation, revenue reports |
| **compliance** | KYC review, audit logs, fraud alerts |
| **tenant_admin** | Tenant-scoped admin (own institution only) |

**Enforcement:** gRPC interceptor validates JWT claims against required role for each RPC method.

### Data Encryption

| Layer | Mechanism |
|-------|-----------|
| **Transit** | TLS 1.3 on all gRPC channels, mTLS server-to-server |
| **At rest (DB)** | PostgreSQL TDE with AES-256 |
| **KYC documents** | AES-256 encrypted file storage, keys managed by HSM |
| **PIN storage** | Bcrypt hashed, never stored in plaintext |
| **Payment tokens** | Generated and managed via HSM, tokenized on device |
| **Switch messages** | MAC generated via HSM per scheme requirements |
| **Backups** | AES-256 encrypted backup files |

### Security Best Practices

- **Input validation:** Proto validators on all gRPC message fields
- **Rate limiting:** Per-user and per-tenant at gateway level
- **Failed auth lockout:** 5 attempts → 30 min lock, alert to compliance
- **SQL injection prevention:** EF Core parameterized queries (no raw SQL)
- **PII masking:** Serilog enrichers mask card numbers, PINs, phone numbers in logs
- **Security headers:** CSP, HSTS, X-Frame-Options on Blazor admin portal
- **Dependency scanning:** Automated NuGet vulnerability scanning in CI pipeline
- **Secret management:** Environment variables or vault for connection strings, API keys

---

## Scalability & Performance

### Scaling Strategy

**On-Premise Docker Compose Scaling:**

| Service | Replicas | Scaling Mode |
|---------|----------|-------------|
| API Gateway | 2 | Active-active, load balanced |
| Core Banking | 2 | Stateless, load balanced |
| Switching Server | 2 | Active-passive (connection management) |
| Terminal Manager | 1 | Single instance (MQTT broker) |
| HSM Interface | 1 | Bound to physical HSM hardware |
| Notification Service | 2 | Stateless worker |
| Reporting Engine | 1 | Read-heavy, uses DB read replica |
| Admin Portal | 1 | Blazor Server with sticky sessions |
| PostgreSQL | 1 primary + 1 read replica | Streaming replication |
| Redis | 1 primary + 1 replica | Sentinel for failover |

### Performance Optimization

| Technique | Application |
|-----------|------------|
| **gRPC binary protocol** | 5-10x faster serialization than JSON |
| **Redis caching** | Balance (5s TTL), tenant config (5min TTL), bill providers (1hr TTL) |
| **Connection pooling** | EF Core + Npgsql pooling, gRPC channel reuse |
| **Async processing** | Wolverine for notifications, recon, settlement — off transaction hot path |
| **DB indexing** | Composite indexes on high-query paths |
| **Table partitioning** | Monthly partitions on transactions and audit logs |
| **gRPC streaming** | Server streaming for transaction history and report export |
| **HTTP/2 multiplexing** | Multiple concurrent gRPC calls over single connection |

### Caching Strategy

| Data | Cache Location | TTL | Invalidation |
|------|---------------|-----|-------------|
| Account balance | Redis | 5 seconds | On transaction completion |
| Tenant configuration | Redis | 5 minutes | On config update event |
| Bill provider list | Redis | 1 hour | On provider update |
| Session tokens | Redis | 15 minutes | On logout/expire |
| Rate limit counters | Redis | 1 minute sliding | Auto-expire |

### Load Balancing

- **Docker internal load balancing** for service replicas
- **Nginx reverse proxy** as external entry point (TLS termination, load distribution)
- **Algorithm:** Round-robin for stateless services, sticky sessions for Blazor
- **Health checks:** gRPC health protocol, Docker healthcheck directive

---

## Reliability & Availability

### High Availability Design

- **No single points of failure** for critical path (Gateway, Core Banking have 2+ replicas)
- **Docker restart policies:** `restart: always` on all services
- **PostgreSQL streaming replication:** automatic failover to read replica if primary fails
- **Redis Sentinel:** automatic failover for cache layer
- **Circuit breakers:** Polly circuit breakers on national switch connection (prevent cascade)
- **Wolverine durable outbox:** messages survive service restarts

### Disaster Recovery

| Metric | Target |
|--------|--------|
| **RPO (Recovery Point Objective)** | 1 hour maximum data loss |
| **RTO (Recovery Time Objective)** | 4 hours maximum downtime |
| **Backup frequency** | Daily full + continuous WAL archiving |
| **Backup storage** | Encrypted, off-site (separate physical location) |
| **Restore testing** | Monthly |

### Backup Strategy

- **PostgreSQL:** pg_basebackup daily + continuous WAL archiving
- **Redis:** RDB snapshots + AOF for point-in-time recovery
- **KYC documents:** Daily backup of encrypted file storage
- **Configuration:** Git-versioned Docker Compose and environment configs
- **HSM keys:** HSM-native backup procedures (secure key export to backup HSM)

### Monitoring & Alerting

**Prometheus + Grafana Stack:**

| Metric | Alert Threshold | Severity |
|--------|----------------|----------|
| Transaction latency p95 | > 2 seconds | Critical |
| Error rate | > 1% | Critical |
| Service health | Any service down > 30s | Critical |
| PostgreSQL connections | > 80% pool capacity | Warning |
| PostgreSQL replication lag | > 10 seconds | Warning |
| Redis memory | > 80% allocated | Warning |
| Disk space | > 85% used | Warning |
| Failed auth attempts | > 50/min | Warning (potential brute force) |
| Certificate expiry | < 30 days | Warning |

**Logging (Serilog + ELK):**
- Structured JSON logging with correlation IDs across all services
- Centralized in Elasticsearch, searchable via Kibana
- PII masking via Serilog destructuring policies
- Log retention: 90 days online, 1 year archived

---

## Integration Architecture

### External Integrations

| System | Protocol | Purpose |
|--------|----------|---------|
| **National Payment Switch** | ISO 8583 (TCP/IP), ISO 20022 (MQ/API) | Transaction routing, authorization |
| **HSM Hardware** | PKCS#11 | Cryptographic operations |
| **Firebase Cloud Messaging** | HTTPS REST | Push notifications |
| **SMS Gateway** | HTTPS REST/SMPP | SMS notifications, OTP delivery |
| **KYC Verification Service** | HTTPS REST | National ID validation (TBD) |

### Internal Integrations

| Source | Target | Protocol | Pattern |
|--------|--------|----------|---------|
| API Gateway | Core Banking | gRPC | Request-response |
| API Gateway | Admin Portal | gRPC-Web | Request-response |
| Core Banking | Switching Server | Wolverine | Command (async) |
| Core Banking | HSM Interface | gRPC | Request-response (sync) |
| Core Banking | Notification Service | Wolverine | Event (async) |
| Core Banking | Reporting Engine | Wolverine | Event (async) |
| Terminal Manager | POS Terminals | MQTT | Pub/sub |
| Terminal Manager | HSM Interface | gRPC | Request-response |
| Admin Portal | Reporting Engine | gRPC | Request-response |

### Message/Event Architecture

**Wolverine Domain Events:**

| Event | Publisher | Subscribers |
|-------|-----------|------------|
| `AccountCreated` | Accounts module | Notification Service |
| `KYCSubmitted` | Accounts module | Admin Portal (queue for review) |
| `KYCApproved` | Accounts module | Notification Service |
| `TransactionCompleted` | Payments/Transfers | Notification Service, Reporting Engine |
| `TransactionFailed` | Payments/Transfers | Notification Service |
| `CashInCompleted` | AgentBanking module | Notification Service, Reporting Engine |
| `CashOutCompleted` | AgentBanking module | Notification Service, Reporting Engine |
| `SettlementProcessed` | Merchants module | Notification Service |
| `SwitchTransactionRouted` | Core Banking | Switching Server |
| `SwitchResponseReceived` | Switching Server | Core Banking |
| `TerminalStatusChanged` | Terminal Manager | Admin Portal (monitoring) |
| `FraudAlertRaised` | Payments module | Admin Portal (compliance) |

**Wolverine Sagas (Multi-Step Transactions):**

| Saga | Steps | Compensation |
|------|-------|-------------|
| **P2P Transfer** | 1. Debit sender → 2. Credit receiver → 3. Record fee → 4. Notify | Reverse debit on failure |
| **NFC Payment** | 1. Validate token → 2. Authorize via HSM → 3. Debit account → 4. Route to switch → 5. Notify | Reverse debit if switch declines |
| **Cash-In** | 1. Validate agent float → 2. Credit customer → 3. Debit agent float → 4. Calculate commission → 5. Notify | Reverse credit on failure |
| **Bill Payment** | 1. Debit account → 2. Route to bill provider → 3. Confirm payment → 4. Notify | Reverse debit if provider rejects |

---

## Development Architecture

### Code Organization

```
GoldBank.sln
├── src/
│   ├── GoldBank.Protos/                  # Shared .proto files (service contracts)
│   ├── GoldBank.SharedKernel/            # Domain events, value objects, common types
│   ├── GoldBank.Gateway/                 # API Gateway + gRPC interceptors
│   ├── GoldBank.Core/                    # Core Banking modular monolith
│   │   ├── Modules/
│   │   │   ├── Accounts/
│   │   │   │   ├── Domain/             # Entities, value objects
│   │   │   │   ├── Application/        # Use cases, handlers
│   │   │   │   ├── Infrastructure/     # Repositories, external calls
│   │   │   │   └── Grpc/              # gRPC service implementation
│   │   │   ├── Payments/
│   │   │   ├── Transfers/
│   │   │   ├── AgentBanking/
│   │   │   ├── BillPay/
│   │   │   ├── Merchants/
│   │   │   └── MultiTenant/
│   │   ├── SharedKernel/               # Cross-module shared logic
│   │   │   ├── TransactionEngine/
│   │   │   └── FeeCalculator/
│   │   └── Infrastructure/
│   │       ├── Persistence/            # EF Core DbContext, migrations
│   │       ├── Caching/                # Redis integration
│   │       └── Messaging/              # Wolverine configuration
│   ├── GoldBank.Switching/               # Switching Server
│   │   ├── Adapters/
│   │   │   ├── ISO8583/
│   │   │   └── ISO20022/
│   │   ├── CanonicalFormat/
│   │   ├── Reconciliation/
│   │   └── ConnectionManager/
│   ├── GoldBank.TerminalManager/         # Terminal Manager + MQTT
│   ├── GoldBank.HSM/                     # HSM Interface (PKCS#11)
│   ├── GoldBank.Admin/                   # Blazor Server Admin Portal
│   ├── GoldBank.Reporting/               # Reporting Engine
│   ├── GoldBank.Notifications/           # Notification Service
│   └── GoldBank.Mobile/                  # KMP Mobile App (separate repo possible)
├── tests/
│   ├── GoldBank.Core.Tests/              # Unit tests for core business logic
│   ├── GoldBank.Switching.Tests/         # Switching adapter tests
│   ├── GoldBank.Integration.Tests/       # gRPC service integration tests
│   └── GoldBank.E2E.Tests/              # End-to-end flow tests
├── docker/
│   ├── docker-compose.yml               # Production compose
│   ├── docker-compose.override.yml      # Development overrides
│   ├── docker-compose.test.yml          # Test environment
│   └── Dockerfile.{service}             # Per-service Dockerfiles
└── docs/                                # Architecture, PRD, product brief
```

### Module Structure

Each module within Core Banking follows clean architecture:

```
Module/
├── Domain/
│   ├── Entities/          # Aggregate roots, entities
│   ├── ValueObjects/      # Immutable value types
│   ├── Events/            # Domain events
│   └── Interfaces/        # Repository interfaces
├── Application/
│   ├── Commands/          # Wolverine command handlers
│   ├── Queries/           # Read-side handlers
│   ├── Validators/        # FluentValidation rules
│   └── DTOs/              # Data transfer objects
├── Infrastructure/
│   ├── Repositories/      # EF Core implementations
│   └── ExternalServices/  # External API clients
└── Grpc/
    └── ServiceImpl.cs     # gRPC service implementation
```

### Testing Strategy

| Level | Coverage Target | Tools | Focus |
|-------|----------------|-------|-------|
| **Unit tests** | 80%+ business logic | xUnit, NSubstitute, FluentAssertions | Domain logic, fee calculations, validation |
| **Integration tests** | All gRPC contracts | TestContainers (PostgreSQL, Redis) | Service endpoints, DB operations |
| **E2E tests** | Critical user flows | gRPC client test harness | Onboarding, payment, cash-in, P2P |
| **Performance tests** | NFR validation | NBomber | Latency, throughput, concurrent users |
| **Security tests** | OWASP top 10 | OWASP ZAP, manual review | Auth bypass, injection, data exposure |

### CI/CD Pipeline

```
┌──────┐    ┌──────┐    ┌─────────────┐    ┌──────────┐    ┌─────────┐    ┌────────────┐
│ Push │───>│Build │───>│ Unit Tests  │───>│ Docker   │───>│ Deploy  │───>│ E2E Tests  │
│      │    │      │    │ Integration │    │ Build    │    │ Staging │    │            │
└──────┘    └──────┘    └─────────────┘    └──────────┘    └─────────┘    └─────┬──────┘
                                                                                │ Pass
                                                                          ┌─────▼──────┐
                                                                          │  Deploy    │
                                                                          │ Production │
                                                                          └────────────┘

Stages:
1. Build:        dotnet build (all projects)
2. Test:         dotnet test (unit + integration with TestContainers)
3. Containerize: Docker build per service
4. Stage:        Deploy to staging environment
5. Verify:       Run E2E test suite against staging
6. Production:   Rolling deployment (one service at a time)
```

---

## Deployment Architecture

### Environments

| Environment | Purpose | Infrastructure |
|-------------|---------|---------------|
| **Development** | Local development | Docker Compose on developer machine |
| **Staging** | Integration testing, UAT | On-premise server (mirrors production) |
| **Production** | Live system | On-premise server cluster |

### Deployment Strategy

- **Rolling deployment:** Update one service at a time, verify health before proceeding
- **Docker Compose:** `docker compose up -d --no-deps {service}` for targeted deployment
- **Database migrations:** EF Core migrations applied during Core Banking startup (with rollback plan)
- **Zero-downtime:** Gateway routes to healthy instances during rolling update
- **Rollback:** Keep previous Docker image tagged, `docker compose up -d --no-deps {service}` with previous tag

### Infrastructure as Code

- **Docker Compose files:** Version-controlled, parameterized with `.env` files
- **Environment configuration:** Per-environment `.env` files (dev, staging, prod)
- **Database migrations:** EF Core migrations committed to source control
- **Monitoring config:** Prometheus rules and Grafana dashboards version-controlled
- **Secret management:** Environment variables for sensitive config (DB passwords, API keys, certificates)

---

## Requirements Traceability

### Functional Requirements Coverage

| FR ID | FR Name | Component(s) | Notes |
|-------|---------|--------------|-------|
| FR-001 | User Self-Registration | Core Banking (Accounts) | gRPC AccountService.Register |
| FR-002 | KYC - National ID | Core Banking (Accounts) | AccountService.SubmitKYC |
| FR-003 | KYC - Selfie Match | Core Banking (Accounts) | AccountService.SubmitKYC |
| FR-004 | Free Account Creation | Core Banking (Accounts) | Auto-activate on KYC pass |
| FR-005 | Profile Management | Core Banking (Accounts) | AccountService.GetProfile/Update |
| FR-006 | Balance Inquiry | Core Banking (Accounts) + Redis | AccountService.GetBalance |
| FR-007 | Transaction History | Core Banking (Accounts) | AccountService.GetTransactionHistory (streaming) |
| FR-008 | NFC Payment at POS | Core Banking (Payments) + HSM | PaymentService.InitiateNFCPayment |
| FR-009 | NFC Tokenization | HSM Interface | Token generation via PKCS#11 |
| FR-010 | PIN for High-Value NFC | HSM Interface + Terminal | PIN encryption via HSM |
| FR-011 | NFC Receipt/Notification | Notification Service | Wolverine event: TransactionCompleted |
| FR-012 | Generate EMV QR | Core Banking (Payments) | PaymentService.GenerateQRCode |
| FR-013 | Scan QR to Pay | Core Banking (Payments) | PaymentService.ProcessQRPayment |
| FR-014 | QR Payment Confirmation | Notification Service | Wolverine event: TransactionCompleted |
| FR-015 | P2P Transfer | Core Banking (Transfers) | TransferService.SendP2P |
| FR-016 | Cross-Border Transfer | Core Banking (Transfers) | TransferService.SendCrossBorder |
| FR-017 | Transfer Confirmation | Core Banking (Transfers) | Confirmation in gRPC response |
| FR-018 | P2P Notifications | Notification Service | Wolverine event: TransactionCompleted |
| FR-019 | Cash-In at Agent | Core Banking (AgentBanking) | AgentService.CashIn |
| FR-020 | Cash-Out at Agent | Core Banking (AgentBanking) | AgentService.CashOut |
| FR-021 | Agent Commission | Core Banking (AgentBanking) | SharedKernel.FeeCalculator |
| FR-022 | Agent Float Management | Core Banking (AgentBanking) | AgentService.GetFloatBalance |
| FR-023 | Agent Receipt | Notification Service | Wolverine event: CashIn/OutCompleted |
| FR-024 | Browse Bill Providers | Core Banking (BillPay) | BillPayService.ListProviders |
| FR-025 | Pay Bill | Core Banking (BillPay) | BillPayService.PayBill |
| FR-026 | Bill Payment Receipt | Notification Service | Wolverine event: TransactionCompleted |
| FR-027 | Saved Billers | Core Banking (BillPay) | BillPayService.SaveBiller |
| FR-028 | Route to National Switch | Switching Server | Wolverine command: RouteTransaction |
| FR-029 | Process Incoming Switch | Switching Server | TCP/MQ listener → Wolverine event |
| FR-030 | Switch Reconciliation | Switching Server (Reconciliation) | Scheduled daily job |
| FR-031 | ISO 8583 Formatting | Switching Server (ISO8583Adapter) | Adapter pattern |
| FR-032 | Register POS Terminals | Terminal Manager | TerminalService.RegisterTerminal |
| FR-033 | Terminal Key Management | HSM Interface + Terminal Manager | MQTT key injection |
| FR-034 | Terminal PIN Handling | HSM Interface | gRPC crypto operations |
| FR-035 | Terminal Status Monitoring | Terminal Manager | MQTT heartbeat topics |
| FR-036 | Remote Terminal Updates | Terminal Manager | MQTT update topics |
| FR-037 | Merchant Registration | Core Banking (Merchants) | MerchantService.Register |
| FR-038 | Merchant Profile | Core Banking (Merchants) | MerchantService.GetProfile |
| FR-039 | Merchant Settlement | Core Banking (Merchants) | Scheduled settlement job |
| FR-040 | Merchant Tx History | Core Banking (Merchants) | MerchantService.GetTransactions |
| FR-041 | Commission Reporting | Core Banking (Merchants) | AgentService.GetCommissionReport |
| FR-042 | Admin RBAC | Admin Portal + Core Banking | AdminService with role interceptors |
| FR-043 | Customer Account Mgmt | Admin Portal | AdminService.SearchCustomers/ManageAccount |
| FR-044 | Merchant/Agent Mgmt | Admin Portal | AdminService.ManageMerchant |
| FR-045 | Transaction Monitoring | Admin Portal | AdminService.SearchTransactions |
| FR-046 | KYC Review Workflow | Admin Portal | AdminService.ReviewKYC |
| FR-047 | System Config Mgmt | Admin Portal | AdminService.UpdateSystemConfig |
| FR-048 | Dispute Management | Admin Portal | AdminService (dispute workflow) |
| FR-049 | Real-Time Dashboard | Reporting Engine | ReportingService.GetDashboard |
| FR-050 | User Growth Reports | Reporting Engine | ReportingService.GetUserGrowthReport |
| FR-051 | Merchant Reports | Reporting Engine | ReportingService.GetMerchantReport |
| FR-052 | Revenue Reports | Reporting Engine | ReportingService.GetRevenueReport |
| FR-053 | Reconciliation Reports | Reporting Engine | ReportingService.GetReconReport |
| FR-054 | Exportable Reports | Reporting Engine | ReportingService.ExportReport (streaming) |
| FR-055 | Configurable Branding | Core Banking (MultiTenant) | Tenant config in DB |
| FR-056 | Tenant Data Isolation | Core Banking (MultiTenant) | Schema-per-tenant |
| FR-057 | Per-Tenant Fees/Limits | Core Banking (MultiTenant) | Tenant-scoped fee config |
| FR-058 | Per-Tenant Admin Access | Admin Portal + Core Banking | Tenant-scoped JWT claims |
| FR-059 | PIN/Biometric Auth | Core Banking (Accounts) + Mobile | JWT issuance on auth |
| FR-060 | Session Timeout | API Gateway | JWT expiry + Redis session |
| FR-061 | Transaction Authorization | Core Banking (Payments) | PIN verification on high-value |
| FR-062 | Device Binding | Core Banking (Accounts) | device_id in JWT + DB |
| FR-063 | Fraud Detection | Core Banking (Payments) | Rules engine + alerts |
| FR-064 | ISO 20022 Formatting | Switching Server (ISO20022Adapter) | Adapter pattern |

**Coverage: 64/64 FRs (100%)**

### Non-Functional Requirements Coverage

| NFR ID | NFR Name | Solution | Validation |
|--------|----------|----------|------------|
| NFR-001 | Payment < 2s | gRPC, Redis, connection pooling, async offload | p95 latency monitoring |
| NFR-002 | 1,000 concurrent | Stateless services, HTTP/2, connection pooling | Load testing |
| NFR-003 | API < 500ms | Redis caching, DB indexing, gRPC streaming | p95 latency monitoring |
| NFR-004 | TLS in transit | TLS 1.3, mTLS, cert pinning | TLS scanning |
| NFR-005 | AES-256 at rest | PostgreSQL TDE, encrypted file storage, HSM keys | Encryption audit |
| NFR-006 | PCI-DSS | Network segmentation, isolated schema, HSM | PCI self-assessment |
| NFR-007 | HSM keys | Dedicated HSM Interface, PKCS#11 | HSM audit |
| NFR-008 | Audit logging | Append-only table, partitioned, 7yr retention | Modification attempt test |
| NFR-009 | 500K users | Schema-per-tenant, partitioned tables | Load test with synthetic data |
| NFR-010 | Horizontal scale | Stateless services, Docker replicas, Redis | Scale test |
| NFR-011 | 99.9% uptime | Redundancy, restart policies, health checks | Uptime monitoring |
| NFR-012 | Backup/recovery | WAL archiving, daily backup, RPO 1hr/RTO 4hr | Monthly restore drill |
| NFR-013 | Atomicity | ACID, Wolverine outbox, sagas, idempotency | Chaos testing |
| NFR-014 | Multi-language | Resource files, tenant-configurable | Language switch test |
| NFR-015 | Onboarding < 10min | Streamlined flow, auto KYC | Timed flow test |
| NFR-016 | Android 8+/iOS 14+ | KMP, platform-specific NFC | Min OS testing |
| NFR-017 | ISO 8583 | Dedicated adapter, scheme-compliant | Scheme certification |
| NFR-018 | EMV compliance | EMV kernel, tokenization, QR spec | EMV test tools |

**Coverage: 18/18 NFRs (100%)**

---

## Trade-offs & Decision Log

### Decision 1: Modular Monolith vs. Microservices

**Choice:** Modular Monolith with Satellite Services

**Trade-off:**
- Gain: Simpler deployment, shared transaction context (ACID), manageable for 4 developers, faster development
- Lose: All modules deploy together, less independent scaling per module

**Rationale:** 4 developers cannot effectively manage microservices operational complexity. Modular monolith gives clean boundaries without distributed system overhead. Satellites isolate only what PCI and hardware requirements demand.

### Decision 2: gRPC vs. REST

**Choice:** gRPC with Protocol Buffers

**Trade-off:**
- Gain: 5-10x faster serialization, strong contracts, streaming, HTTP/2 multiplexing
- Lose: Less human-readable debugging, requires gRPC-Web for browser, smaller tooling ecosystem

**Rationale:** Payment transaction speed is a critical driver. gRPC's performance advantage directly addresses NFR-001 (< 2s). Strong contracts via .proto files prevent integration issues across the team.

### Decision 3: Schema-per-Tenant vs. Row-Level Isolation

**Choice:** Schema-per-tenant

**Trade-off:**
- Gain: Strong data isolation (FR-056), easy tenant provisioning/deprovisioning, independent backup per tenant possible
- Lose: More complex EF Core configuration (dynamic schema resolution), schema migrations apply to each tenant

**Rationale:** For a white-label banking platform, strong data isolation is non-negotiable. Schema-per-tenant provides the clearest separation with minimal risk of cross-tenant data leakage.

### Decision 4: Wolverine + MQTT vs. RabbitMQ/Kafka

**Choice:** Wolverine + MQTT Embedded Broker

**Trade-off:**
- Gain: .NET-native saga support, durable outbox built-in, MQTT ideal for terminal IoT communication, simpler infrastructure
- Lose: Smaller community than RabbitMQ, less tooling than Kafka

**Rationale:** Wolverine's saga orchestration is critical for multi-step financial transactions (P2P, payments). MQTT is purpose-built for terminal communication patterns. Combined infrastructure is simpler than running RabbitMQ/Kafka alongside.

### Decision 5: PostgreSQL vs. SQL Server

**Choice:** PostgreSQL 18

**Trade-off:**
- Gain: Zero licensing cost for on-premise, excellent partitioning, strong .NET support via Npgsql
- Lose: Some enterprise tooling and integration advantages of SQL Server with .NET

**Rationale:** Licensing cost is significant for on-premise multi-tenant deployment. PostgreSQL's native partitioning handles transaction volume effectively. Npgsql is mature and well-maintained.

---

## Open Issues & Risks

1. **National switch integration protocol:** Specific switch(es) and their exact ISO 8583/20022 message specifications TBD. Adapter pattern mitigates this — new adapters can be added without core changes.

2. **KYC verification provider:** Third-party service for national ID validation not yet selected. Architecture accommodates any REST/gRPC API provider behind an adapter.

3. **NFC HCE implementation complexity:** Android Host Card Emulation requires careful implementation for payment tokenization. Early prototyping recommended.

4. **HSM hardware selection:** Specific HSM model and PKCS#11 compatibility must be validated. HSM Interface service abstracts this behind a clean interface.

5. **Scope vs. timeline risk:** 64 FRs, 14 epics, ~73-100 stories with 4 developers in 6 months is ambitious. Sprint planning must ruthlessly prioritize MVP features.

---

## Assumptions & Constraints

**Assumptions:**
- NFC-capable smartphones are widespread among target users in Southern Africa
- Sponsoring merchant bank will maintain regulatory approval
- National payment switch connectivity and sandbox will be available for integration testing
- Merchants will adopt low-cost terminals due to agent commission incentive
- On-premise infrastructure will be provisioned before development milestones
- KYC verification service will be accessible via API
- .NET 10 will be available and stable for production use

**Constraints:**
- On-premise deployment (no cloud services)
- 4 senior developers
- 6-month timeline to launch
- Physical HSM hardware dependency
- National switch connectivity dependency
- Device compliance already handled

---

## Future Considerations

- **Lending/credit products:** Core Banking Accounts module can be extended with a Lending module. Transaction history provides credit scoring data.
- **Savings accounts:** New module in Core Banking with interest calculation engine.
- **Kubernetes migration:** When scale demands exceed Docker Compose, architecture is container-ready for K8s.
- **Event sourcing:** Transaction engine could migrate to event sourcing for complete audit trail and temporal queries.
- **API marketplace:** gRPC services could expose a REST gateway for third-party developer access.

---

## Approval & Sign-off

**Review Status:**
- [ ] Technical Lead
- [ ] Product Owner
- [ ] Security Architect (if applicable)
- [ ] DevOps Lead

---

## Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 2026-02-24 | wmapundu | Initial architecture |

---

## Next Steps

### Phase 4: Sprint Planning & Implementation

Run `/sprint-planning` to:
- Break epics into detailed user stories
- Estimate story complexity
- Plan sprint iterations
- Begin implementation following this architectural blueprint

**Key Implementation Principles:**
1. Follow component boundaries defined in this document
2. Implement NFR solutions as specified
3. Use technology stack as defined
4. Follow gRPC service contracts exactly
5. Adhere to security and performance guidelines

---

**This document was created using BMAD Method v6 - Phase 3 (Solutioning)**

*To continue: Run `/workflow-status` to see your progress and next recommended workflow.*

---

## Appendix A: Technology Evaluation Matrix

| Category | Choice | Alternative 1 | Alternative 2 | Decision Rationale |
|----------|--------|---------------|---------------|-------------------|
| Backend | .NET 10 | Java/Spring | Go | Team expertise, enterprise banking ecosystem |
| Database | PostgreSQL 18 | SQL Server | Oracle | Zero licensing, excellent partitioning, ACID |
| Mobile | KMP | Flutter | React Native | Native NFC access, Android-first, Kotlin synergy |
| Messaging | Wolverine + MQTT | RabbitMQ | Kafka | .NET-native sagas, MQTT for terminals |
| Communication | gRPC | REST | GraphQL | Binary speed, strong contracts, streaming |
| Admin UI | Blazor Server | React | Angular | Full-stack C#, no JS dependency |
| Cache | Redis | Memcached | In-process | Persistence, pub/sub, data structures |
| Monitoring | Prometheus + Grafana | Datadog | New Relic | Open source, on-premise compatible |
| Logging | Serilog + ELK | NLog + Seq | log4net | Structured logging, centralized search |

---

## Appendix B: Capacity Planning

**Year 1 Targets:**
- 500,000 registered users
- 10,000 merchants
- 5,000,000 transactions/month
- 3 white-label tenants

**Database Sizing (per tenant):**

| Table | Rows/Month | Row Size (est.) | Monthly Growth | Year 1 Total |
|-------|-----------|----------------|----------------|-------------|
| accounts | ~167K new | ~500 bytes | ~83 MB | ~1 GB |
| transactions | ~1.67M | ~300 bytes | ~500 MB | ~6 GB |
| audit_logs | ~3M | ~200 bytes | ~600 MB | ~7.2 GB |
| kyc_records | ~167K | ~200 bytes + doc refs | ~33 MB | ~400 MB |

**Estimated total DB size per tenant (Year 1):** ~15 GB
**Estimated total across 3 tenants:** ~45 GB + switching schema + indexes

**KYC Document Storage:** ~500K users x ~2 MB (ID + selfie) = ~1 TB encrypted file storage

**Infrastructure Recommendations:**
- Database server: 32 GB RAM, 8 cores, 500 GB NVMe SSD (primary + replica)
- Application server: 16 GB RAM, 8 cores (Docker host for all services)
- Redis: 4 GB RAM dedicated
- HSM: Per vendor specifications
- Total on-premise: 2-3 physical servers + HSM + network equipment

---

## Appendix C: Cost Estimation

**Software Licensing (Annual):**

| Item | Cost |
|------|------|
| .NET 10 | Free (MIT license) |
| PostgreSQL 18 | Free (PostgreSQL license) |
| Redis | Free (BSD license) |
| Docker | Free (Docker Engine CE) |
| Prometheus + Grafana | Free (Apache 2.0) |
| ELK Stack | Free (Elastic license) |
| Wolverine | Free (MIT license) |
| **Total software licensing** | **$0** |

**Infrastructure (Estimated):**

| Item | One-time | Annual |
|------|----------|--------|
| Application servers (2x) | $10K-15K | Maintenance |
| Database servers (2x) | $15K-20K | Maintenance |
| HSM hardware | $15K-30K | Support contract |
| Network equipment | $5K-10K | Maintenance |
| EFT POS terminals (pilot) | $5K-10K | Per unit cost |
| **Total infrastructure** | **$50K-85K** | **+ support/maintenance** |

**External Services (Monthly):**

| Item | Estimated Cost |
|------|---------------|
| Firebase Cloud Messaging | Free tier (likely sufficient) |
| SMS Gateway | Volume-based ($0.01-0.05/SMS) |
| KYC Verification Service | Per-verification fee (TBD) |
| **Total external services** | **Volume-dependent** |
