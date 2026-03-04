# Sprint Plan: UniBank

**Date:** 2026-02-24
**Scrum Master:** wmapundu
**Project Level:** 4
**Total Stories:** 88
**Total Points:** 396
**Planned Sprints:** 8 (3 weeks each)
**Sprint Capacity:** 144 effective points/sprint (4 senior devs x 15 days x 6 hrs / 2 hrs per point x 80% buffer)
**Target Completion:** August 2026

---

## Executive Summary

UniBank's implementation is planned across 8 three-week sprints, totaling 24 weeks. The plan follows a foundation-first approach: infrastructure and user onboarding in Sprints 1-2, core payment capabilities in Sprints 3-4, external integrations in Sprints 5-6, and admin/white-label/hardening in Sprints 7-8. The 88 stories total 396 story points, well within the team's 1,152-point total capacity (144 effective points x 8 sprints), providing buffer for unknowns and technical debt.

**Key Metrics:**
- Total Stories: 88
- Total Points: 396
- Sprints: 8 (3 weeks each)
- Team Capacity: 144 effective points per sprint
- Target Completion: August 2026
- Buffer: ~66% capacity remaining for unknowns, bugs, and refinement

---

## Story Inventory

### EPIC-000: Infrastructure & Foundation

#### STORY-001: Solution Scaffolding & Project Structure

**Epic:** EPIC-000 Infrastructure
**Priority:** Must Have
**Points:** 8

**User Story:**
As a developer
I want a properly structured .NET 10 solution with all projects scaffolded
So that the team can begin parallel development immediately

**Acceptance Criteria:**
- [ ] UniBank.sln created with all projects per architecture (Gateway, Core, Switching, TerminalManager, HSM, Admin, Reporting, Notifications, Protos, SharedKernel)
- [ ] Project references and NuGet packages configured
- [ ] Solution builds successfully
- [ ] README with setup instructions

**Technical Notes:** Follow code organization from architecture doc. Use .NET 10 project templates.

**Dependencies:** None

---

#### STORY-002: Docker Compose Development Environment

**Epic:** EPIC-000 Infrastructure
**Priority:** Must Have
**Points:** 5

**User Story:**
As a developer
I want a Docker Compose setup for local development
So that I can run the full stack locally with one command

**Acceptance Criteria:**
- [ ] docker-compose.yml with PostgreSQL 18, Redis, all services
- [ ] docker-compose.override.yml for dev settings
- [ ] Health checks configured for all services
- [ ] Environment variables documented

**Technical Notes:** Use Docker Compose profiles for selective service startup.

**Dependencies:** STORY-001

---

#### STORY-003: PostgreSQL Database Schema & Multi-Tenant Foundation

**Epic:** EPIC-000 Infrastructure
**Priority:** Must Have
**Points:** 8

**User Story:**
As a developer
I want the database schema created with multi-tenant support
So that all services can persist data with tenant isolation

**Acceptance Criteria:**
- [ ] Public schema with tenants, bill_providers, system_config tables
- [ ] Tenant schema template with all tables per architecture
- [ ] EF Core DbContext with dynamic schema resolution
- [ ] Migration framework configured
- [ ] Monthly partitioning on transactions and audit_logs tables

**Technical Notes:** Schema-per-tenant. EF Core with Npgsql. Partition management.

**Dependencies:** STORY-002

---

#### STORY-004: gRPC Proto Definitions & Shared Contracts

**Epic:** EPIC-000 Infrastructure
**Priority:** Must Have
**Points:** 5

**User Story:**
As a developer
I want all gRPC service contracts defined in .proto files
So that all services have type-safe communication contracts

**Acceptance Criteria:**
- [ ] Proto files for all 10 gRPC services (Account, Payment, Transfer, Agent, BillPay, Merchant, Terminal, Admin, Reporting, HSM)
- [ ] UniBank.Protos project compiles and generates C# code
- [ ] Versioned as unibank.v1.{service}
- [ ] Request/response messages defined for all RPCs

**Technical Notes:** Follow API design from architecture doc exactly.

**Dependencies:** STORY-001

---

#### STORY-005: API Gateway with gRPC Interceptors

**Epic:** EPIC-000 Infrastructure
**Priority:** Must Have
**Points:** 8

**User Story:**
As a developer
I want an API Gateway that routes gRPC calls with auth, rate limiting, and tenant context
So that all client requests are authenticated and properly routed

**Acceptance Criteria:**
- [ ] gRPC gateway service running with TLS
- [ ] JWT validation interceptor
- [ ] Tenant identification interceptor (extracts tenant_id from JWT)
- [ ] Rate limiting interceptor (per-user, per-tenant)
- [ ] Request/response logging with PII masking
- [ ] Routes to Core Banking service

**Technical Notes:** ASP.NET Core gRPC interceptors. Redis for rate limit counters.

**Dependencies:** STORY-004

---

#### STORY-006: CI/CD Pipeline Setup

**Epic:** EPIC-000 Infrastructure
**Priority:** Must Have
**Points:** 5

**User Story:**
As a developer
I want an automated CI/CD pipeline
So that code is built, tested, and deployed automatically

**Acceptance Criteria:**
- [ ] Build stage: dotnet build all projects
- [ ] Test stage: dotnet test (unit tests)
- [ ] Docker stage: build all service images
- [ ] Deploy stage: deploy to staging environment
- [ ] Pipeline triggers on push to main branch

**Technical Notes:** GitLab CI or Jenkins. Docker image registry.

**Dependencies:** STORY-001, STORY-002

---

#### STORY-007: Wolverine Messaging & MQTT Broker Configuration

**Epic:** EPIC-000 Infrastructure
**Priority:** Must Have
**Points:** 5

**User Story:**
As a developer
I want Wolverine messaging and MQTT broker configured
So that services can communicate asynchronously

**Acceptance Criteria:**
- [ ] Wolverine configured in Core Banking with durable outbox
- [ ] MQTT embedded broker running in Terminal Manager
- [ ] SharedKernel domain events defined
- [ ] Test handler verifies end-to-end message delivery
- [ ] Saga support configured

**Technical Notes:** Wolverine with PostgreSQL-backed outbox for durability.

**Dependencies:** STORY-001, STORY-003

---

#### STORY-008: Monitoring & Logging Stack

**Epic:** EPIC-000 Infrastructure
**Priority:** Must Have
**Points:** 5

**User Story:**
As a developer
I want Prometheus, Grafana, and ELK configured
So that we can monitor system health and search logs

**Acceptance Criteria:**
- [ ] Prometheus scraping .NET metrics endpoints
- [ ] Grafana dashboard with basic service health metrics
- [ ] Serilog configured with structured JSON output
- [ ] ELK stack receiving logs from all services
- [ ] Alert rules for critical thresholds

**Technical Notes:** Docker containers for Prometheus, Grafana, Elasticsearch, Kibana.

**Dependencies:** STORY-002

---

### EPIC-001: User Registration & KYC

#### STORY-009: User Self-Registration with Phone & OTP

**Epic:** EPIC-001
**Priority:** Must Have
**Points:** 5

**User Story:**
As an unbanked consumer
I want to register using my phone number with OTP verification
So that I can create a UniBank account

**Acceptance Criteria:**
- [ ] gRPC Register endpoint accepts phone number
- [ ] OTP generated and sent via SMS gateway
- [ ] VerifyOTP endpoint validates code
- [ ] Account created in pending KYC state
- [ ] Rate limiting on OTP requests (max 3/hour)

**Technical Notes:** AccountService.Register + VerifyOTP. SMS gateway integration.

**Dependencies:** STORY-005 (Gateway), STORY-003 (DB)

---

#### STORY-010: Create Account PIN

**Epic:** EPIC-001
**Priority:** Must Have
**Points:** 3

**User Story:**
As a new user
I want to create a 4-6 digit PIN
So that I can secure my account

**Acceptance Criteria:**
- [ ] PIN creation screen after OTP verification
- [ ] PIN stored as bcrypt hash (never plaintext)
- [ ] PIN complexity validation (no sequential/repeated digits)
- [ ] PIN confirmation (enter twice)

**Technical Notes:** Bcrypt hashing. PIN stored in accounts table.

**Dependencies:** STORY-009

---

#### STORY-011: KYC - National ID Document Upload

**Epic:** EPIC-001
**Priority:** Must Have
**Points:** 5

**User Story:**
As a new user
I want to upload my national ID document
So that my identity can be verified

**Acceptance Criteria:**
- [ ] User can capture/upload national ID document image
- [ ] Document stored encrypted (AES-256) with reference in DB
- [ ] KYC record created with document reference
- [ ] Document type validation (image format, size limits)

**Technical Notes:** gRPC streaming for file upload. Encrypted storage. FR-002.

**Dependencies:** STORY-009

---

#### STORY-012: KYC - Selfie Capture & Photo Match

**Epic:** EPIC-001
**Priority:** Must Have
**Points:** 8

**User Story:**
As a new user
I want to take a selfie that is matched against my ID photo
So that my identity is confirmed

**Acceptance Criteria:**
- [ ] Live selfie capture (liveness detection)
- [ ] Selfie stored encrypted alongside ID document
- [ ] Photo comparison with confidence score
- [ ] Auto-approve above threshold, flag for manual review below
- [ ] KYC status updated (approved/pending_review/rejected)

**Technical Notes:** May use third-party KYC provider API or on-device ML. FR-003.

**Dependencies:** STORY-011

---

#### STORY-013: Account Activation on KYC Approval

**Epic:** EPIC-001
**Priority:** Must Have
**Points:** 3

**User Story:**
As a verified user
I want my account activated immediately after KYC approval
So that I can start transacting

**Acceptance Criteria:**
- [ ] Account status changes to active on KYC approval
- [ ] Zero balance initialized
- [ ] Wolverine event: AccountCreated published
- [ ] Push notification sent to user
- [ ] No fees or charges applied

**Technical Notes:** FR-004. Wolverine event triggers Notification Service.

**Dependencies:** STORY-012

---

#### STORY-014: Device Binding on Registration

**Epic:** EPIC-001
**Priority:** Must Have
**Points:** 3

**User Story:**
As a new user
I want my account bound to my device
So that unauthorized devices cannot access my account

**Acceptance Criteria:**
- [ ] Device ID captured during registration
- [ ] Device ID stored in account record
- [ ] Login from different device triggers additional verification
- [ ] Device transfer process available

**Technical Notes:** FR-062. device_id in JWT token.

**Dependencies:** STORY-009

---

### EPIC-002: Account Management

#### STORY-015: Account Profile View & Edit

**Epic:** EPIC-002
**Priority:** Must Have
**Points:** 3

**User Story:**
As a registered user
I want to view and edit my profile details
So that my information is always up to date

**Acceptance Criteria:**
- [ ] GetProfile returns user details
- [ ] UpdateProfile allows editing non-KYC fields
- [ ] KYC fields (name, ID number) are read-only
- [ ] Changes saved immediately

**Technical Notes:** FR-005. AccountService.GetProfile/UpdateProfile.

**Dependencies:** STORY-013

---

#### STORY-016: Account Balance Inquiry with Redis Caching

**Epic:** EPIC-002
**Priority:** Must Have
**Points:** 3

**User Story:**
As a user
I want to see my current balance instantly
So that I know how much money I have

**Acceptance Criteria:**
- [ ] Balance displayed on home screen after auth
- [ ] Redis cache with 5s TTL
- [ ] Cache invalidated on transaction completion
- [ ] Manual refresh available
- [ ] Response time < 500ms

**Technical Notes:** FR-006. Redis caching strategy from architecture.

**Dependencies:** STORY-013

---

#### STORY-017: Transaction History with Streaming

**Epic:** EPIC-002
**Priority:** Must Have
**Points:** 5

**User Story:**
As a user
I want to view my transaction history
So that I can track my spending and receipts

**Acceptance Criteria:**
- [ ] gRPC server streaming for transaction list
- [ ] Sorted by date (most recent first)
- [ ] Shows: date, type, amount, recipient/sender, status
- [ ] Filter by transaction type and date range
- [ ] Minimum 90 days of history

**Technical Notes:** FR-007. Server streaming avoids large payloads. Partitioned table query.

**Dependencies:** STORY-013

---

### EPIC-014: Security & Authentication

#### STORY-018: PIN & Biometric Authentication

**Epic:** EPIC-014
**Priority:** Must Have
**Points:** 5

**User Story:**
As a user
I want to log in with my PIN or biometric (fingerprint/face)
So that my account is secured conveniently

**Acceptance Criteria:**
- [ ] PIN login validates against bcrypt hash
- [ ] Biometric unlock retrieves stored credentials from device keystore
- [ ] JWT access token (15 min) + refresh token (30 day) issued
- [ ] Failed attempts tracked: 5 failures → 30 min lockout
- [ ] Refresh token rotation on use

**Technical Notes:** FR-059. JWT in gRPC metadata.

**Dependencies:** STORY-010, STORY-005

---

#### STORY-019: Session Management & Auto-Timeout

**Epic:** EPIC-014
**Priority:** Must Have
**Points:** 3

**User Story:**
As a user
I want my session to expire after inactivity
So that my account is protected if I leave my phone unattended

**Acceptance Criteria:**
- [ ] Session timeout configurable (default 5 min)
- [ ] Re-authentication required after timeout
- [ ] Active transactions not interrupted
- [ ] Session tokens invalidated on timeout and logout

**Technical Notes:** FR-060. Redis session tracking. JWT expiry.

**Dependencies:** STORY-018

---

#### STORY-020: Transaction Authorization (High-Value PIN)

**Epic:** EPIC-014
**Priority:** Must Have
**Points:** 3

**User Story:**
As a user
I want to confirm high-value transactions with my PIN
So that large payments require explicit authorization

**Acceptance Criteria:**
- [ ] Authorization threshold configurable per transaction type and tenant
- [ ] PIN required for transactions above threshold
- [ ] Required regardless of biometric login
- [ ] Failed attempts logged

**Technical Notes:** FR-061. Applied across payment, transfer, and cash-out flows.

**Dependencies:** STORY-018

---

### EPIC-003: NFC Contactless Payments

#### STORY-021: HSM Interface Service - Core Operations

**Epic:** EPIC-003
**Priority:** Must Have
**Points:** 8

**User Story:**
As a system
I want HSM cryptographic operations available via gRPC
So that PIN encryption, key management, and tokenization are secure

**Acceptance Criteria:**
- [ ] HSM Interface service running with PKCS#11 interop
- [ ] Master key generation within HSM
- [ ] Session key derivation
- [ ] PIN block encrypt/decrypt operations
- [ ] MAC generation for switch messages
- [ ] All operations audit logged

**Technical Notes:** FR-033, FR-034. PKCS#11 via P/Invoke. Dedicated gRPC service.

**Dependencies:** STORY-004 (Protos), Physical HSM hardware

---

#### STORY-022: NFC Payment Tokenization

**Epic:** EPIC-003
**Priority:** Must Have
**Points:** 8

**User Story:**
As a user
I want a secure payment token provisioned to my phone
So that my actual account credentials are never exposed during NFC payments

**Acceptance Criteria:**
- [ ] Payment token generated via HSM during account activation
- [ ] Token stored in device secure element or HCE
- [ ] Actual credentials never transmitted during NFC
- [ ] Token can be revoked remotely (lost device)

**Technical Notes:** FR-009. EMV tokenization spec. Android HCE API.

**Dependencies:** STORY-021, STORY-013

---

#### STORY-023: NFC Contactless Payment at POS

**Epic:** EPIC-003
**Priority:** Must Have
**Points:** 8

**User Story:**
As a consumer
I want to pay by tapping my phone on a POS terminal
So that I can make contactless payments without a physical card

**Acceptance Criteria:**
- [ ] Phone emulates contactless card via NFC/HCE
- [ ] Transaction initiated on tap
- [ ] Payment processed end-to-end < 2 seconds
- [ ] Supports EMV contactless kernel specifications

**Technical Notes:** FR-008. Android HCE. PaymentService.InitiateNFCPayment. Core differentiator.

**Dependencies:** STORY-022, STORY-021

---

#### STORY-024: PIN Entry for High-Value NFC Transactions

**Epic:** EPIC-003
**Priority:** Must Have
**Points:** 5

**User Story:**
As a consumer
I want to enter my PIN on the terminal for large NFC payments
So that high-value transactions have additional security

**Acceptance Criteria:**
- [ ] Threshold configurable per tenant
- [ ] Below threshold: tap only
- [ ] Above threshold: PIN prompt on terminal
- [ ] PIN encrypted via HSM before transmission

**Technical Notes:** FR-010. HSM PIN encryption. Terminal-side CVM.

**Dependencies:** STORY-023, STORY-021

---

#### STORY-025: NFC Payment Notifications & Receipt

**Epic:** EPIC-003
**Priority:** Must Have
**Points:** 3

**User Story:**
As a consumer
I want to receive a notification when my NFC payment completes
So that I have confirmation and a receipt

**Acceptance Criteria:**
- [ ] Push notification on success and failure
- [ ] Includes: amount, merchant name, date/time, status
- [ ] Transaction appears in history immediately
- [ ] Wolverine event triggers notification

**Technical Notes:** FR-011. Notification Service subscribes to TransactionCompleted event.

**Dependencies:** STORY-023

---

### EPIC-004: EMV QR Code Payments

#### STORY-026: Generate EMV QR Code

**Epic:** EPIC-004
**Priority:** Must Have
**Points:** 5

**User Story:**
As a merchant or user
I want to generate an EMV QR code
So that others can pay me by scanning

**Acceptance Criteria:**
- [ ] QR code generated per EMV QR Code specification
- [ ] Supports fixed or dynamic amount
- [ ] Contains payee identification
- [ ] Displayable on screen or printable

**Technical Notes:** FR-012. PaymentService.GenerateQRCode. EMV QRCPS spec.

**Dependencies:** STORY-013

---

#### STORY-027: Scan QR Code & Process Payment

**Epic:** EPIC-004
**Priority:** Must Have
**Points:** 5

**User Story:**
As a consumer
I want to scan a QR code to make a payment
So that I can pay without NFC

**Acceptance Criteria:**
- [ ] Camera opens to scan QR code
- [ ] QR parsed per EMV specification
- [ ] Payment details (recipient, amount) shown for confirmation
- [ ] PIN/biometric required to authorize
- [ ] Payment processed end-to-end < 2 seconds

**Technical Notes:** FR-013. PaymentService.ProcessQRPayment. Camera permission.

**Dependencies:** STORY-026, STORY-018

---

#### STORY-028: QR Payment Confirmation & Notifications

**Epic:** EPIC-004
**Priority:** Must Have
**Points:** 3

**User Story:**
As a consumer
I want confirmation and receipt after QR payment
So that I know the payment was successful

**Acceptance Criteria:**
- [ ] Confirmation screen with recipient and amount
- [ ] Push notification to payer and payee
- [ ] Transaction appears in both users' history

**Technical Notes:** FR-014. Wolverine event: TransactionCompleted.

**Dependencies:** STORY-027

---

### EPIC-005: P2P Transfers

#### STORY-029: P2P Domestic Transfer

**Epic:** EPIC-005
**Priority:** Must Have
**Points:** 5

**User Story:**
As a consumer
I want to send money to another UniBank user
So that I can pay people remotely

**Acceptance Criteria:**
- [ ] Enter recipient phone number or select from contacts
- [ ] Validate recipient exists and is active
- [ ] Enter amount, see confirmation screen
- [ ] Transfer is immediate (real-time debit + credit)
- [ ] Wolverine saga: debit sender → credit receiver → record fee

**Technical Notes:** FR-015. TransferService.SendP2P. Wolverine saga for atomicity.

**Dependencies:** STORY-013, STORY-020

---

#### STORY-030: P2P Cross-Border Transfer

**Epic:** EPIC-005
**Priority:** Must Have
**Points:** 5

**User Story:**
As a consumer
I want to send money to users in other Southern African countries
So that I can support family and do business cross-border

**Acceptance Criteria:**
- [ ] Select destination country
- [ ] Exchange rate displayed (if applicable)
- [ ] Cross-border limits enforced
- [ ] Recipient receives in local currency/account

**Technical Notes:** FR-016. TransferService.SendCrossBorder. May route via switching server.

**Dependencies:** STORY-029

---

#### STORY-031: Transfer Confirmation & Notifications

**Epic:** EPIC-005
**Priority:** Must Have
**Points:** 3

**User Story:**
As a consumer
I want confirmation before transfer executes and notifications after
So that I can verify details and have a receipt

**Acceptance Criteria:**
- [ ] Confirmation screen: recipient name, phone, amount, fees
- [ ] Can cancel before confirming
- [ ] Sender gets confirmation with reference number
- [ ] Receiver gets notification with sender name and amount
- [ ] Both see transaction in history

**Technical Notes:** FR-017, FR-018. Notification Service event handlers.

**Dependencies:** STORY-029

---

### EPIC-006: Agent Cash-In / Cash-Out

#### STORY-032: Cash-In at Merchant Agent

**Epic:** EPIC-006
**Priority:** Must Have
**Points:** 5

**User Story:**
As a consumer
I want to deposit cash at a merchant agent
So that I can load money into my UniBank account

**Acceptance Criteria:**
- [ ] Agent initiates cash-in (POS terminal or agent app)
- [ ] Consumer identified by phone number
- [ ] Consumer confirms deposit amount on mobile app
- [ ] Account credited immediately
- [ ] Both receive receipts

**Technical Notes:** FR-019. AgentService.CashIn. Wolverine saga for atomicity.

**Dependencies:** STORY-013, STORY-035

---

#### STORY-033: Cash-Out at Merchant Agent

**Epic:** EPIC-006
**Priority:** Must Have
**Points:** 5

**User Story:**
As a consumer
I want to withdraw cash from my account at a merchant agent
So that I can access physical cash when needed

**Acceptance Criteria:**
- [ ] Consumer initiates on mobile or at agent
- [ ] PIN authentication required
- [ ] Agent confirms cash disbursement
- [ ] Account debited immediately
- [ ] Both receive receipts

**Technical Notes:** FR-020. AgentService.CashOut. PIN via FR-061.

**Dependencies:** STORY-013, STORY-035, STORY-020

---

#### STORY-034: Agent Commission Engine

**Epic:** EPIC-006
**Priority:** Must Have
**Points:** 5

**User Story:**
As a merchant agent
I want to automatically earn commission on cash-in/cash-out transactions
So that I'm incentivized to serve customers

**Acceptance Criteria:**
- [ ] Commission rate configurable per transaction type and tenant
- [ ] Commission calculated and credited automatically
- [ ] Real-time commission balance visible to agent
- [ ] Commission included in settlement

**Technical Notes:** FR-021. SharedKernel.FeeCalculator. Per-tenant config.

**Dependencies:** STORY-032, STORY-033

---

#### STORY-035: Agent Float/Balance Management

**Epic:** EPIC-006
**Priority:** Must Have
**Points:** 5

**User Story:**
As a merchant agent
I want to manage my float balance
So that I have funds available for cash-in/cash-out operations

**Acceptance Criteria:**
- [ ] Dedicated agent float account
- [ ] Float decreases on cash-in, increases on cash-out
- [ ] Low float alerts
- [ ] Float top-up via bank transfer or super-agent

**Technical Notes:** FR-022. AgentService.GetFloatBalance. Wolverine event for low float alert.

**Dependencies:** STORY-037

---

#### STORY-036: Agent Transaction Receipt

**Epic:** EPIC-006
**Priority:** Must Have
**Points:** 3

**User Story:**
As a merchant agent
I want receipts for all agent transactions
So that I have records for reconciliation

**Acceptance Criteria:**
- [ ] Receipt: transaction ID, date/time, type, amount, customer ref
- [ ] Printable on POS terminal
- [ ] Digital receipt in agent history
- [ ] Customer receives matching digital receipt

**Technical Notes:** FR-023. Notification Service for digital receipts.

**Dependencies:** STORY-032, STORY-033

---

### EPIC-007: Bill Payments

#### STORY-037: Bill Provider Registry

**Epic:** EPIC-007
**Priority:** Must Have
**Points:** 3

**User Story:**
As a user
I want to browse and search bill payment providers
So that I can find the right provider to pay

**Acceptance Criteria:**
- [ ] Providers organized by category (utilities, telecom, etc.)
- [ ] Search by name
- [ ] Provider details: name, account format, min/max amounts
- [ ] Cached in Redis (1hr TTL)

**Technical Notes:** FR-024. BillPayService.ListProviders. Public schema table.

**Dependencies:** STORY-003

---

#### STORY-038: Pay Bill

**Epic:** EPIC-007
**Priority:** Must Have
**Points:** 5

**User Story:**
As a user
I want to pay a bill by selecting provider and entering details
So that I can pay utilities and services digitally

**Acceptance Criteria:**
- [ ] Select provider, enter account/reference number
- [ ] Enter amount, validate against provider rules
- [ ] Confirmation screen before execution
- [ ] Payment processed, receipt generated
- [ ] Wolverine saga: debit account → route to provider → confirm

**Technical Notes:** FR-025, FR-026. BillPayService.PayBill.

**Dependencies:** STORY-037, STORY-020

---

#### STORY-039: Saved/Favorite Billers

**Epic:** EPIC-007
**Priority:** Should Have
**Points:** 3

**User Story:**
As a user
I want to save frequently used billers
So that I can quickly pay recurring bills

**Acceptance Criteria:**
- [ ] Save biller with account/reference
- [ ] Favorites list available
- [ ] Initiate payment from saved biller (pre-filled)
- [ ] Can remove saved billers

**Technical Notes:** FR-027. BillPayService.SaveBiller/GetSavedBillers.

**Dependencies:** STORY-038

---

### EPIC-008: National Network Switching

#### STORY-040: ISO 8583 Adapter

**Epic:** EPIC-008
**Priority:** Must Have
**Points:** 8

**User Story:**
As a system
I want to format and parse ISO 8583 messages
So that transactions can be routed to legacy national switches

**Acceptance Criteria:**
- [ ] ISO 8583 message builder (all required data elements)
- [ ] ISO 8583 message parser
- [ ] MAC generation via HSM Interface
- [ ] Compliant with national scheme spec
- [ ] Full message logging in switching schema

**Technical Notes:** FR-031. Switching Server ISO8583Adapter. TCP/IP socket management.

**Dependencies:** STORY-021 (HSM), STORY-004

---

#### STORY-041: ISO 20022 Adapter

**Epic:** EPIC-008
**Priority:** Must Have
**Points:** 8

**User Story:**
As a system
I want to format and parse ISO 20022 messages
So that transactions can be routed to modern national switches

**Acceptance Criteria:**
- [ ] ISO 20022 XML/JSON message builder
- [ ] ISO 20022 message parser
- [ ] Mapping between canonical format and ISO 20022
- [ ] MQ/API client connectivity

**Technical Notes:** FR-064. Switching Server ISO20022Adapter.

**Dependencies:** STORY-004

---

#### STORY-042: Message Router & Canonical Format

**Epic:** EPIC-008
**Priority:** Must Have
**Points:** 5

**User Story:**
As a system
I want a message router that selects the right adapter
So that transactions route to the correct switch with the correct protocol

**Acceptance Criteria:**
- [ ] Internal canonical message format defined
- [ ] Router selects adapter based on destination institution
- [ ] Inbound messages parsed to canonical, outbound from canonical
- [ ] New adapters can be added without changing router

**Technical Notes:** Adapter pattern. Wolverine command handlers. CanonicalFormat project.

**Dependencies:** STORY-040, STORY-041

---

#### STORY-043: Outbound Transaction Routing

**Epic:** EPIC-008
**Priority:** Must Have
**Points:** 5

**User Story:**
As a system
I want outgoing transactions routed to the national switch
So that users can transact with external banks

**Acceptance Criteria:**
- [ ] Core Banking sends Wolverine command to Switching Server
- [ ] Message formatted via appropriate adapter
- [ ] Sent to national switch via TCP/MQ
- [ ] Response received and routed back to Core Banking
- [ ] Timeout handling with proper decline codes

**Technical Notes:** FR-028. TCP socket pool management. Circuit breakers via Polly.

**Dependencies:** STORY-042

---

#### STORY-044: Inbound Transaction Processing

**Epic:** EPIC-008
**Priority:** Must Have
**Points:** 5

**User Story:**
As a system
I want to process incoming transactions from the national switch
So that users can receive payments from external institutions

**Acceptance Criteria:**
- [ ] Listener for incoming TCP/MQ messages
- [ ] Parse via appropriate adapter
- [ ] Wolverine event to Core Banking
- [ ] Destination account credited
- [ ] Invalid transactions return proper decline codes

**Technical Notes:** FR-029. Inbound handler with validation.

**Dependencies:** STORY-042

---

#### STORY-045: Daily Reconciliation

**Epic:** EPIC-008
**Priority:** Must Have
**Points:** 5

**User Story:**
As an operations team
I want daily reconciliation with the national switch
So that all transactions are matched and discrepancies flagged

**Acceptance Criteria:**
- [ ] Automated recon runs on configurable schedule
- [ ] Mismatches flagged for manual review
- [ ] Reconciliation report generated and stored
- [ ] Settlement amounts calculated

**Technical Notes:** FR-030. Scheduled job in Switching Server. Reconciliation schema.

**Dependencies:** STORY-043, STORY-044

---

### EPIC-009: Terminal Management & HSM

#### STORY-046: Terminal Registration & Provisioning

**Epic:** EPIC-009
**Priority:** Must Have
**Points:** 5

**User Story:**
As an admin
I want to register and provision EFT POS terminals
So that merchants can accept digital payments

**Acceptance Criteria:**
- [ ] Register terminal with unique ID and merchant assignment
- [ ] Download configuration remotely
- [ ] Activation requires successful key exchange
- [ ] Registration logged in audit trail

**Technical Notes:** FR-032. TerminalService.RegisterTerminal. MQTT config push.

**Dependencies:** STORY-007 (MQTT), STORY-021 (HSM)

---

#### STORY-047: Terminal Key Management via HSM

**Epic:** EPIC-009
**Priority:** Must Have
**Points:** 5

**User Story:**
As a system
I want terminal encryption keys managed through HSM
So that PIN data is always securely encrypted

**Acceptance Criteria:**
- [ ] Master keys generated in HSM
- [ ] Session keys derived and distributed to terminals via MQTT
- [ ] Key rotation on configurable schedule
- [ ] All key operations audit logged

**Technical Notes:** FR-033. HSM Interface + Terminal Manager. MQTT key injection topic.

**Dependencies:** STORY-046, STORY-021

---

#### STORY-048: Terminal Status Monitoring

**Epic:** EPIC-009
**Priority:** Must Have
**Points:** 3

**User Story:**
As an operations team
I want to see the status of all POS terminals
So that I can identify offline or faulty terminals

**Acceptance Criteria:**
- [ ] Terminal heartbeat via MQTT status topic
- [ ] Status tracked: online, offline, fault
- [ ] Dashboard shows all terminals with state
- [ ] Alerts for terminals offline > threshold
- [ ] Last communication timestamp recorded

**Technical Notes:** FR-035. MQTT terminals/{id}/status. TerminalService.GetTerminalStatus.

**Dependencies:** STORY-046

---

#### STORY-049: Remote Terminal Software Updates

**Epic:** EPIC-009
**Priority:** Should Have
**Points:** 3

**User Story:**
As an admin
I want to push software updates to terminals remotely
So that terminals stay current without physical visits

**Acceptance Criteria:**
- [ ] Software updates pushed via MQTT update topic
- [ ] Config changes applied without physical access
- [ ] Update status tracked per terminal
- [ ] Failed updates flagged and retryable

**Technical Notes:** FR-036. MQTT terminals/{id}/updates.

**Dependencies:** STORY-046

---

### EPIC-010: Merchant Management

#### STORY-050: Merchant Registration & KYC

**Epic:** EPIC-010
**Priority:** Must Have
**Points:** 5

**User Story:**
As a merchant
I want to register my business with KYC verification
So that I can accept digital payments and act as an agent

**Acceptance Criteria:**
- [ ] Business details and owner ID submitted
- [ ] Merchant KYC validated
- [ ] Unique merchant ID assigned
- [ ] Agent flag configurable
- [ ] Registration logged

**Technical Notes:** FR-037. MerchantService.Register.

**Dependencies:** STORY-003

---

#### STORY-051: Merchant Profile Management

**Epic:** EPIC-010
**Priority:** Must Have
**Points:** 3

**User Story:**
As a merchant
I want to view and update my business profile
So that my details are accurate

**Acceptance Criteria:**
- [ ] View business details and location
- [ ] Update non-KYC fields
- [ ] GPS coordinates stored for locator
- [ ] Changes reflected immediately

**Technical Notes:** FR-038. MerchantService.GetProfile.

**Dependencies:** STORY-050

---

#### STORY-052: Merchant Settlement & Payout

**Epic:** EPIC-010
**Priority:** Must Have
**Points:** 5

**User Story:**
As a merchant
I want automated settlement and payout
So that I receive my earnings regularly

**Acceptance Criteria:**
- [ ] Settlement calculated from completed transactions
- [ ] Schedule configurable (daily/weekly)
- [ ] Includes transaction fees and commission
- [ ] Payout credited to designated account
- [ ] Settlement notification sent

**Technical Notes:** FR-039. Scheduled job. MerchantService.GetSettlements.

**Dependencies:** STORY-050, STORY-034

---

#### STORY-053: Merchant Transaction History & Statements

**Epic:** EPIC-010
**Priority:** Must Have
**Points:** 3

**User Story:**
As a merchant
I want to view my transaction history and statements
So that I can track my business performance

**Acceptance Criteria:**
- [ ] View all transactions processed at terminal(s)
- [ ] Filter by date, type, status
- [ ] Daily/monthly statements
- [ ] Statements include totals and fees

**Technical Notes:** FR-040. MerchantService.GetTransactions (streaming).

**Dependencies:** STORY-050

---

#### STORY-054: Merchant Commission Reporting

**Epic:** EPIC-010
**Priority:** Must Have
**Points:** 3

**User Story:**
As a merchant agent
I want to see my commission earnings
So that I can track income from agent services

**Acceptance Criteria:**
- [ ] Commission earnings displayed separately
- [ ] Breakdown by type (cash-in, cash-out)
- [ ] Running total and period summaries
- [ ] Exportable report

**Technical Notes:** FR-041. AgentService.GetCommissionReport.

**Dependencies:** STORY-034

---

### EPIC-011: Admin / Back-Office Portal

#### STORY-055: Admin Portal Foundation & RBAC

**Epic:** EPIC-011
**Priority:** Must Have
**Points:** 8

**User Story:**
As an admin
I want a back-office portal with role-based access
So that team members can manage the platform with appropriate permissions

**Acceptance Criteria:**
- [ ] Blazor Server app running with authentication
- [ ] Admin users created with assigned roles
- [ ] Roles: super_admin, operations, support, finance, compliance
- [ ] gRPC-Web connectivity to Core Banking
- [ ] Tenant-scoped views for tenant_admin role

**Technical Notes:** FR-042. Blazor Server with JWT auth. AdminService gRPC-Web.

**Dependencies:** STORY-005

---

#### STORY-056: Customer Account Management (Admin)

**Epic:** EPIC-011
**Priority:** Must Have
**Points:** 5

**User Story:**
As a support admin
I want to search, view, and manage customer accounts
So that I can assist customers and handle account issues

**Acceptance Criteria:**
- [ ] Search by name, phone, ID number
- [ ] View full account details and transaction history
- [ ] Suspend, freeze, or close accounts with reason
- [ ] All actions logged in audit trail

**Technical Notes:** FR-043. AdminService.SearchCustomers/ManageAccount.

**Dependencies:** STORY-055

---

#### STORY-057: Merchant/Agent Management (Admin)

**Epic:** EPIC-011
**Priority:** Must Have
**Points:** 5

**User Story:**
As an operations admin
I want to approve, manage, and configure merchants/agents
So that the merchant network is properly managed

**Acceptance Criteria:**
- [ ] Approve/reject merchant applications
- [ ] Suspend or deactivate merchants
- [ ] Configure merchant-specific settings (fees, limits)
- [ ] All actions logged

**Technical Notes:** FR-044. AdminService.ManageMerchant.

**Dependencies:** STORY-055

---

#### STORY-058: Transaction Monitoring & Search (Admin)

**Epic:** EPIC-011
**Priority:** Must Have
**Points:** 5

**User Story:**
As an operations admin
I want to monitor and search all transactions
So that I can investigate issues and monitor platform activity

**Acceptance Criteria:**
- [ ] Real-time transaction feed
- [ ] Search by: ID, user, merchant, date, amount, type
- [ ] Full processing trail in transaction details
- [ ] Flag suspicious transactions for review

**Technical Notes:** FR-045. AdminService.SearchTransactions (streaming).

**Dependencies:** STORY-055

---

#### STORY-059: KYC Review & Approval Workflow (Admin)

**Epic:** EPIC-011
**Priority:** Must Have
**Points:** 5

**User Story:**
As a compliance admin
I want to review and approve KYC submissions
So that identity verification meets regulatory standards

**Acceptance Criteria:**
- [ ] Pending KYC queue
- [ ] View ID document and selfie side by side
- [ ] Approve, reject, or request resubmission
- [ ] Decision logged with reviewer ID and timestamp

**Technical Notes:** FR-046. AdminService.ReviewKYC.

**Dependencies:** STORY-055, STORY-012

---

#### STORY-060: System Configuration Management (Admin)

**Epic:** EPIC-011
**Priority:** Must Have
**Points:** 5

**User Story:**
As a super admin
I want to configure transaction fees, limits, and system parameters
So that the platform operates with correct business rules

**Acceptance Criteria:**
- [ ] Transaction fees configurable by type and tenant
- [ ] Transaction limits configurable (daily, monthly, per-transaction)
- [ ] Agent commission rates configurable
- [ ] Changes logged and take effect per schedule

**Technical Notes:** FR-047. AdminService.UpdateSystemConfig.

**Dependencies:** STORY-055

---

#### STORY-061: Dispute/Chargeback Management (Admin)

**Epic:** EPIC-011
**Priority:** Should Have
**Points:** 5

**User Story:**
As a support admin
I want to manage transaction disputes
So that customer issues are resolved fairly

**Acceptance Criteria:**
- [ ] Disputes raised by customers or admins
- [ ] Workflow: open → investigate → resolve
- [ ] Refunds/reversals processed from resolution
- [ ] History and resolution details stored

**Technical Notes:** FR-048. AdminService dispute workflow.

**Dependencies:** STORY-058

---

### EPIC-012: Reporting & Analytics

#### STORY-062: Real-Time Transaction Dashboard

**Epic:** EPIC-012
**Priority:** Must Have
**Points:** 5

**User Story:**
As an admin
I want a real-time transaction dashboard
So that I can monitor platform activity at a glance

**Acceptance Criteria:**
- [ ] Live transaction count and value
- [ ] Transactions per second/minute/hour
- [ ] Visual charts for trends
- [ ] Auto-refresh configurable

**Technical Notes:** FR-049. ReportingService.GetDashboard. Blazor real-time via SignalR.

**Dependencies:** STORY-055

---

#### STORY-063: User Growth & Registration Reports

**Epic:** EPIC-012
**Priority:** Must Have
**Points:** 3

**User Story:**
As a business admin
I want user registration and growth reports
So that I can track adoption trends

**Acceptance Criteria:**
- [ ] Daily/weekly/monthly registration counts
- [ ] KYC completion rates
- [ ] Active vs inactive user ratios
- [ ] Growth trend charts

**Technical Notes:** FR-050. ReportingService.GetUserGrowthReport. Read replica queries.

**Dependencies:** STORY-055

---

#### STORY-064: Merchant/Agent Performance Reports

**Epic:** EPIC-012
**Priority:** Must Have
**Points:** 3

**User Story:**
As an operations admin
I want merchant and agent performance reports
So that I can identify top and underperforming agents

**Acceptance Criteria:**
- [ ] Transaction volume and value per merchant/agent
- [ ] Agent cash-in/cash-out volumes
- [ ] Commission earned per agent
- [ ] Top/underperforming rankings

**Technical Notes:** FR-051. ReportingService.GetMerchantReport.

**Dependencies:** STORY-055

---

#### STORY-065: Revenue & Fee Reports

**Epic:** EPIC-012
**Priority:** Must Have
**Points:** 3

**User Story:**
As a finance admin
I want revenue and fee reports
So that I can track financial performance

**Acceptance Criteria:**
- [ ] Revenue by fee type (transaction, interchange, terminal)
- [ ] Revenue per tenant
- [ ] Period comparison (month over month)
- [ ] Revenue trend visualization

**Technical Notes:** FR-052. ReportingService.GetRevenueReport.

**Dependencies:** STORY-055

---

#### STORY-066: Reconciliation Reports

**Epic:** EPIC-012
**Priority:** Must Have
**Points:** 3

**User Story:**
As a finance admin
I want reconciliation reports
So that I can verify all transactions are accounted for

**Acceptance Criteria:**
- [ ] Daily reconciliation summary
- [ ] Matched vs unmatched counts
- [ ] Settlement amounts and discrepancies
- [ ] Drill-down into mismatches

**Technical Notes:** FR-053. ReportingService.GetReconReport.

**Dependencies:** STORY-045, STORY-055

---

#### STORY-067: Exportable Reports

**Epic:** EPIC-012
**Priority:** Should Have
**Points:** 3

**User Story:**
As an admin
I want to export reports as CSV and PDF
So that I can share data offline and with stakeholders

**Acceptance Criteria:**
- [ ] Export as CSV
- [ ] Export as PDF
- [ ] Includes applied filters and date range
- [ ] gRPC streaming for large exports

**Technical Notes:** FR-054. ReportingService.ExportReport (streaming).

**Dependencies:** STORY-062 through STORY-066

---

### EPIC-013: White-Label Configuration

#### STORY-068: Configurable Branding per Tenant

**Epic:** EPIC-013
**Priority:** Must Have
**Points:** 5

**User Story:**
As a deploying institution
I want to customize the app branding (logo, colors, name)
So that the platform reflects my brand

**Acceptance Criteria:**
- [ ] Logo, colors, app name configurable per tenant
- [ ] Applied across mobile app and admin portal
- [ ] Changes without code deployment
- [ ] Default branding for unconfigured elements

**Technical Notes:** FR-055. Tenant config in DB. Dynamic theming.

**Dependencies:** STORY-003

---

#### STORY-069: Tenant Data Isolation Verification

**Epic:** EPIC-013
**Priority:** Must Have
**Points:** 5

**User Story:**
As a platform owner
I want verified tenant data isolation
So that white-label deployments are fully separated

**Acceptance Criteria:**
- [ ] Schema-per-tenant enforced at application level
- [ ] Cross-tenant access prevented at DB level
- [ ] Tenant-specific encryption keys supported
- [ ] Tenant ID enforced on all gRPC calls
- [ ] Penetration test: attempt cross-tenant access fails

**Technical Notes:** FR-056. EF Core schema resolution. Gateway tenant interceptor.

**Dependencies:** STORY-003, STORY-005

---

#### STORY-070: Per-Tenant Fee & Limit Configuration

**Epic:** EPIC-013
**Priority:** Must Have
**Points:** 3

**User Story:**
As a deploying institution
I want independent fee and limit settings
So that my business model is configured correctly

**Acceptance Criteria:**
- [ ] Unique fee structures per tenant
- [ ] Transaction limits independent per tenant
- [ ] Agent commission rates per tenant
- [ ] Changes apply only to specified tenant

**Technical Notes:** FR-057. Tenant-scoped config in DB.

**Dependencies:** STORY-060

---

#### STORY-071: Per-Tenant Admin Portal Access

**Epic:** EPIC-013
**Priority:** Must Have
**Points:** 5

**User Story:**
As a deploying institution
I want my own admin portal view with isolation
So that I can manage my deployment independently

**Acceptance Criteria:**
- [ ] Tenant admins see only their data
- [ ] Super admin can view all tenants
- [ ] Tenant admin roles independent per tenant
- [ ] Cannot access other tenants' configurations

**Technical Notes:** FR-058. Tenant-scoped JWT claims. Blazor tenant filtering.

**Dependencies:** STORY-055, STORY-069

---

### EPIC-014 (Continued): Fraud Detection

#### STORY-072: Fraud Detection Alerts

**Epic:** EPIC-014
**Priority:** Should Have
**Points:** 5

**User Story:**
As a compliance team
I want alerts on suspicious transaction patterns
So that potential fraud is detected early

**Acceptance Criteria:**
- [ ] Rules-based detection (unusual amounts, velocity, location)
- [ ] Alerts generated and visible in admin portal
- [ ] Suspicious accounts auto-suspended pending review
- [ ] Alert rules configurable

**Technical Notes:** FR-063. Rules engine in Payments module. Wolverine event: FraudAlertRaised.

**Dependencies:** STORY-058

---

### Cross-Cutting: Launch Preparation

#### STORY-073: Notification Service - Push & SMS

**Epic:** Cross-cutting
**Priority:** Must Have
**Points:** 5

**User Story:**
As a system
I want push notifications and SMS delivered reliably
So that users receive timely transaction alerts

**Acceptance Criteria:**
- [ ] Wolverine event handlers for all notification events
- [ ] Firebase Cloud Messaging integration (push)
- [ ] SMS gateway integration (OTP, alerts)
- [ ] Notification templates per event type
- [ ] Delivery status tracking

**Technical Notes:** Notification Service. Subscribes to domain events.

**Dependencies:** STORY-007

---

#### STORY-074: Performance Testing & NFR Validation

**Epic:** Cross-cutting
**Priority:** Must Have
**Points:** 5

**User Story:**
As a team
I want performance tests validating all NFRs
So that we confirm the system meets requirements before launch

**Acceptance Criteria:**
- [ ] Load test: 1000 concurrent users
- [ ] Payment transaction p95 < 2 seconds
- [ ] Non-payment API p95 < 500ms
- [ ] No transaction failures under load
- [ ] Results documented

**Technical Notes:** NBomber or k6. Test against staging.

**Dependencies:** All functional stories complete

---

#### STORY-075: Security Audit & Hardening

**Epic:** Cross-cutting
**Priority:** Must Have
**Points:** 5

**User Story:**
As a team
I want a security audit before launch
So that vulnerabilities are identified and fixed

**Acceptance Criteria:**
- [ ] TLS/mTLS verified on all channels
- [ ] PII masking verified in all logs
- [ ] SQL injection testing (EF Core parameterized)
- [ ] Auth bypass testing
- [ ] Rate limiting verified
- [ ] PCI-DSS self-assessment completed

**Technical Notes:** OWASP ZAP scan. Manual review of auth flows.

**Dependencies:** All functional stories complete

---

#### STORY-076: Pilot Deployment Preparation

**Epic:** Cross-cutting
**Priority:** Must Have
**Points:** 5

**User Story:**
As a team
I want to prepare for pilot deployment with first white-label institution
So that we can launch with a real customer

**Acceptance Criteria:**
- [ ] Staging environment mirrors production
- [ ] First tenant configured with branding
- [ ] Merchant onboarding tested end-to-end
- [ ] Runbook documented (deploy, rollback, incident response)
- [ ] Support team trained on admin portal

**Technical Notes:** Deployment to on-premise production server.

**Dependencies:** All stories complete

---

## Sprint Allocation

### Sprint 1 (Weeks 1-3): Foundation & Infrastructure

**Goal:** Establish development environment, core architecture, database, gRPC contracts, and user registration flow

**Stories:**

| Story | Title | Points | Epic |
|-------|-------|--------|------|
| STORY-001 | Solution Scaffolding & Project Structure | 8 | EPIC-000 |
| STORY-002 | Docker Compose Development Environment | 5 | EPIC-000 |
| STORY-003 | PostgreSQL Database Schema & Multi-Tenant | 8 | EPIC-000 |
| STORY-004 | gRPC Proto Definitions & Shared Contracts | 5 | EPIC-000 |
| STORY-005 | API Gateway with gRPC Interceptors | 8 | EPIC-000 |
| STORY-006 | CI/CD Pipeline Setup | 5 | EPIC-000 |
| STORY-007 | Wolverine Messaging & MQTT Broker | 5 | EPIC-000 |
| STORY-008 | Monitoring & Logging Stack | 5 | EPIC-000 |
| STORY-009 | User Self-Registration with Phone & OTP | 5 | EPIC-001 |
| STORY-010 | Create Account PIN | 3 | EPIC-001 |
| STORY-073 | Notification Service - Push & SMS | 5 | Cross-cutting |

**Total:** 62 points / 144 capacity (43% utilization)

**Notes:** First sprint intentionally lighter — infrastructure setup involves exploration and unknowns. Buffer accounts for environment issues, tooling configuration, and team ramp-up.

---

### Sprint 2 (Weeks 4-6): Identity, Auth & Account Management

**Goal:** Complete KYC flow, user authentication, device binding, and core account features

**Stories:**

| Story | Title | Points | Epic |
|-------|-------|--------|------|
| STORY-011 | KYC - National ID Document Upload | 5 | EPIC-001 |
| STORY-012 | KYC - Selfie Capture & Photo Match | 8 | EPIC-001 |
| STORY-013 | Account Activation on KYC Approval | 3 | EPIC-001 |
| STORY-014 | Device Binding on Registration | 3 | EPIC-001 |
| STORY-015 | Account Profile View & Edit | 3 | EPIC-002 |
| STORY-016 | Account Balance Inquiry with Redis Caching | 3 | EPIC-002 |
| STORY-017 | Transaction History with Streaming | 5 | EPIC-002 |
| STORY-018 | PIN & Biometric Authentication | 5 | EPIC-014 |
| STORY-019 | Session Management & Auto-Timeout | 3 | EPIC-014 |
| STORY-020 | Transaction Authorization (High-Value PIN) | 3 | EPIC-014 |
| STORY-050 | Merchant Registration & KYC | 5 | EPIC-010 |
| STORY-051 | Merchant Profile Management | 3 | EPIC-010 |

**Total:** 49 points / 144 capacity (34% utilization)

**Notes:** KYC selfie matching (STORY-012) has high uncertainty — may need third-party API integration. Merchant registration started early to enable agent banking in Sprint 4.

---

### Sprint 3 (Weeks 7-9): Core Payments (NFC + QR)

**Goal:** Deliver NFC contactless payments, EMV QR payments, and HSM integration — UniBank's core differentiator

**Stories:**

| Story | Title | Points | Epic |
|-------|-------|--------|------|
| STORY-021 | HSM Interface Service - Core Operations | 8 | EPIC-003 |
| STORY-022 | NFC Payment Tokenization | 8 | EPIC-003 |
| STORY-023 | NFC Contactless Payment at POS | 8 | EPIC-003 |
| STORY-024 | PIN Entry for High-Value NFC Transactions | 5 | EPIC-003 |
| STORY-025 | NFC Payment Notifications & Receipt | 3 | EPIC-003 |
| STORY-026 | Generate EMV QR Code | 5 | EPIC-004 |
| STORY-027 | Scan QR Code & Process Payment | 5 | EPIC-004 |
| STORY-028 | QR Payment Confirmation & Notifications | 3 | EPIC-004 |

**Total:** 45 points / 144 capacity (31% utilization)

**Notes:** NFC/HCE implementation is technically complex with high uncertainty. HSM hardware integration may require iteration. Buffer is intentionally large for this critical sprint.

---

### Sprint 4 (Weeks 10-12): Transfers, Agent Banking & Bills

**Goal:** Enable P2P transfers, cash-in/cash-out agent network, and bill payments

**Stories:**

| Story | Title | Points | Epic |
|-------|-------|--------|------|
| STORY-029 | P2P Domestic Transfer | 5 | EPIC-005 |
| STORY-030 | P2P Cross-Border Transfer | 5 | EPIC-005 |
| STORY-031 | Transfer Confirmation & Notifications | 3 | EPIC-005 |
| STORY-032 | Cash-In at Merchant Agent | 5 | EPIC-006 |
| STORY-033 | Cash-Out at Merchant Agent | 5 | EPIC-006 |
| STORY-034 | Agent Commission Engine | 5 | EPIC-006 |
| STORY-035 | Agent Float/Balance Management | 5 | EPIC-006 |
| STORY-036 | Agent Transaction Receipt | 3 | EPIC-006 |
| STORY-037 | Bill Provider Registry | 3 | EPIC-007 |
| STORY-038 | Pay Bill | 5 | EPIC-007 |
| STORY-039 | Saved/Favorite Billers | 3 | EPIC-007 |

**Total:** 47 points / 144 capacity (33% utilization)

**Notes:** Wolverine sagas critical for P2P and cash-in/out atomicity. Agent banking depends on merchant registration from Sprint 2.

---

### Sprint 5 (Weeks 13-15): National Switch Integration

**Goal:** Connect to national payment switch with ISO 8583 and ISO 20022 adapters, plus reconciliation

**Stories:**

| Story | Title | Points | Epic |
|-------|-------|--------|------|
| STORY-040 | ISO 8583 Adapter | 8 | EPIC-008 |
| STORY-041 | ISO 20022 Adapter | 8 | EPIC-008 |
| STORY-042 | Message Router & Canonical Format | 5 | EPIC-008 |
| STORY-043 | Outbound Transaction Routing | 5 | EPIC-008 |
| STORY-044 | Inbound Transaction Processing | 5 | EPIC-008 |
| STORY-045 | Daily Reconciliation | 5 | EPIC-008 |
| STORY-052 | Merchant Settlement & Payout | 5 | EPIC-010 |
| STORY-053 | Merchant Transaction History & Statements | 3 | EPIC-010 |
| STORY-054 | Merchant Commission Reporting | 3 | EPIC-010 |

**Total:** 47 points / 144 capacity (33% utilization)

**Notes:** Switch integration depends on external sandbox access. ISO 8583 and ISO 20022 can be developed in parallel by different developers. Merchant settlement included as it depends on transaction data flowing.

---

### Sprint 6 (Weeks 16-18): Terminal Management & White-Label

**Goal:** Complete terminal management with MQTT, and white-label multi-tenant configuration

**Stories:**

| Story | Title | Points | Epic |
|-------|-------|--------|------|
| STORY-046 | Terminal Registration & Provisioning | 5 | EPIC-009 |
| STORY-047 | Terminal Key Management via HSM | 5 | EPIC-009 |
| STORY-048 | Terminal Status Monitoring | 3 | EPIC-009 |
| STORY-049 | Remote Terminal Software Updates | 3 | EPIC-009 |
| STORY-068 | Configurable Branding per Tenant | 5 | EPIC-013 |
| STORY-069 | Tenant Data Isolation Verification | 5 | EPIC-013 |
| STORY-070 | Per-Tenant Fee & Limit Configuration | 3 | EPIC-013 |

**Total:** 29 points / 144 capacity (20% utilization)

**Notes:** Light sprint — terminal management depends on physical hardware availability. White-label config is critical for the 3-tenant year 1 goal. Extra buffer for catching up on any delayed stories from earlier sprints.

---

### Sprint 7 (Weeks 19-21): Admin Portal & Reporting

**Goal:** Deliver complete admin back-office portal and reporting dashboard

**Stories:**

| Story | Title | Points | Epic |
|-------|-------|--------|------|
| STORY-055 | Admin Portal Foundation & RBAC | 8 | EPIC-011 |
| STORY-056 | Customer Account Management (Admin) | 5 | EPIC-011 |
| STORY-057 | Merchant/Agent Management (Admin) | 5 | EPIC-011 |
| STORY-058 | Transaction Monitoring & Search (Admin) | 5 | EPIC-011 |
| STORY-059 | KYC Review & Approval Workflow (Admin) | 5 | EPIC-011 |
| STORY-060 | System Configuration Management (Admin) | 5 | EPIC-011 |
| STORY-061 | Dispute/Chargeback Management (Admin) | 5 | EPIC-011 |
| STORY-062 | Real-Time Transaction Dashboard | 5 | EPIC-012 |
| STORY-063 | User Growth & Registration Reports | 3 | EPIC-012 |
| STORY-064 | Merchant/Agent Performance Reports | 3 | EPIC-012 |
| STORY-065 | Revenue & Fee Reports | 3 | EPIC-012 |
| STORY-066 | Reconciliation Reports | 3 | EPIC-012 |
| STORY-067 | Exportable Reports | 3 | EPIC-012 |

**Total:** 58 points / 144 capacity (40% utilization)

**Notes:** Largest sprint by story count. Admin portal stories are well-defined CRUD operations. Blazor Server enables rapid development. Reporting uses read replicas.

---

### Sprint 8 (Weeks 22-24): Security, Testing & Launch

**Goal:** Complete white-label admin access, fraud detection, performance validation, security audit, and pilot deployment

**Stories:**

| Story | Title | Points | Epic |
|-------|-------|--------|------|
| STORY-071 | Per-Tenant Admin Portal Access | 5 | EPIC-013 |
| STORY-072 | Fraud Detection Alerts | 5 | EPIC-014 |
| STORY-074 | Performance Testing & NFR Validation | 5 | Cross-cutting |
| STORY-075 | Security Audit & Hardening | 5 | Cross-cutting |
| STORY-076 | Pilot Deployment Preparation | 5 | Cross-cutting |

**Total:** 25 points / 144 capacity (17% utilization)

**Notes:** Intentionally the lightest sprint. Focus is on quality, testing, and launch readiness rather than new features. Large buffer for fixing issues found during performance testing and security audit.

---

## Epic Traceability

| Epic ID | Epic Name | Stories | Total Points | Sprint(s) |
|---------|-----------|---------|--------------|-----------|
| EPIC-000 | Infrastructure & Foundation | 001-008 | 49 | 1 |
| EPIC-001 | User Registration & KYC | 009-014 | 22 | 1-2 |
| EPIC-002 | Account Management | 015-017 | 11 | 2 |
| EPIC-003 | NFC Contactless Payments | 021-025 | 32 | 3 |
| EPIC-004 | EMV QR Code Payments | 026-028 | 13 | 3 |
| EPIC-005 | P2P Transfers | 029-031 | 13 | 4 |
| EPIC-006 | Agent Cash-In / Cash-Out | 032-036 | 23 | 4 |
| EPIC-007 | Bill Payments | 037-039 | 11 | 4 |
| EPIC-008 | National Network Switching | 040-045 | 36 | 5 |
| EPIC-009 | Terminal Management & HSM | 046-049 | 16 | 6 |
| EPIC-010 | Merchant Management | 050-054 | 19 | 2, 5 |
| EPIC-011 | Admin / Back-Office Portal | 055-061 | 38 | 7 |
| EPIC-012 | Reporting & Analytics | 062-067 | 21 | 7 |
| EPIC-013 | White-Label Configuration | 068-071 | 18 | 6, 8 |
| EPIC-014 | Security & Authentication | 018-020, 072 | 16 | 2, 8 |
| Cross-cutting | Notifications, Testing, Launch | 073-076 | 20 | 1, 8 |
| **TOTAL** | **15 Epics** | **76 stories** | **358 points** | **8 sprints** |

---

## Requirements Coverage

| FR ID | FR Name | Story | Sprint |
|-------|---------|-------|--------|
| FR-001 | User Self-Registration | STORY-009 | 1 |
| FR-002 | KYC - National ID | STORY-011 | 2 |
| FR-003 | KYC - Selfie Match | STORY-012 | 2 |
| FR-004 | Free Account Creation | STORY-013 | 2 |
| FR-005 | Profile Management | STORY-015 | 2 |
| FR-006 | Balance Inquiry | STORY-016 | 2 |
| FR-007 | Transaction History | STORY-017 | 2 |
| FR-008 | NFC Payment at POS | STORY-023 | 3 |
| FR-009 | NFC Tokenization | STORY-022 | 3 |
| FR-010 | PIN for High-Value NFC | STORY-024 | 3 |
| FR-011 | NFC Receipt/Notification | STORY-025 | 3 |
| FR-012 | Generate EMV QR | STORY-026 | 3 |
| FR-013 | Scan QR to Pay | STORY-027 | 3 |
| FR-014 | QR Payment Confirmation | STORY-028 | 3 |
| FR-015 | P2P Transfer | STORY-029 | 4 |
| FR-016 | Cross-Border Transfer | STORY-030 | 4 |
| FR-017 | Transfer Confirmation | STORY-031 | 4 |
| FR-018 | P2P Notifications | STORY-031 | 4 |
| FR-019 | Cash-In at Agent | STORY-032 | 4 |
| FR-020 | Cash-Out at Agent | STORY-033 | 4 |
| FR-021 | Agent Commission | STORY-034 | 4 |
| FR-022 | Agent Float Management | STORY-035 | 4 |
| FR-023 | Agent Receipt | STORY-036 | 4 |
| FR-024 | Browse Bill Providers | STORY-037 | 4 |
| FR-025 | Pay Bill | STORY-038 | 4 |
| FR-026 | Bill Payment Receipt | STORY-038 | 4 |
| FR-027 | Saved Billers | STORY-039 | 4 |
| FR-028 | Route to National Switch | STORY-043 | 5 |
| FR-029 | Process Incoming Switch | STORY-044 | 5 |
| FR-030 | Switch Reconciliation | STORY-045 | 5 |
| FR-031 | ISO 8583 Formatting | STORY-040 | 5 |
| FR-032 | Register POS Terminals | STORY-046 | 6 |
| FR-033 | Terminal Key Management | STORY-047 | 6 |
| FR-034 | Terminal PIN Handling | STORY-021 | 3 |
| FR-035 | Terminal Status Monitoring | STORY-048 | 6 |
| FR-036 | Remote Terminal Updates | STORY-049 | 6 |
| FR-037 | Merchant Registration | STORY-050 | 2 |
| FR-038 | Merchant Profile | STORY-051 | 2 |
| FR-039 | Merchant Settlement | STORY-052 | 5 |
| FR-040 | Merchant Tx History | STORY-053 | 5 |
| FR-041 | Commission Reporting | STORY-054 | 5 |
| FR-042 | Admin RBAC | STORY-055 | 7 |
| FR-043 | Customer Account Mgmt | STORY-056 | 7 |
| FR-044 | Merchant/Agent Mgmt | STORY-057 | 7 |
| FR-045 | Transaction Monitoring | STORY-058 | 7 |
| FR-046 | KYC Review Workflow | STORY-059 | 7 |
| FR-047 | System Config Mgmt | STORY-060 | 7 |
| FR-048 | Dispute Management | STORY-061 | 7 |
| FR-049 | Real-Time Dashboard | STORY-062 | 7 |
| FR-050 | User Growth Reports | STORY-063 | 7 |
| FR-051 | Merchant Reports | STORY-064 | 7 |
| FR-052 | Revenue Reports | STORY-065 | 7 |
| FR-053 | Recon Reports | STORY-066 | 7 |
| FR-054 | Exportable Reports | STORY-067 | 7 |
| FR-055 | Configurable Branding | STORY-068 | 6 |
| FR-056 | Tenant Data Isolation | STORY-069 | 6 |
| FR-057 | Per-Tenant Fees/Limits | STORY-070 | 6 |
| FR-058 | Per-Tenant Admin Access | STORY-071 | 8 |
| FR-059 | PIN/Biometric Auth | STORY-018 | 2 |
| FR-060 | Session Timeout | STORY-019 | 2 |
| FR-061 | Transaction Authorization | STORY-020 | 2 |
| FR-062 | Device Binding | STORY-014 | 2 |
| FR-063 | Fraud Detection | STORY-072 | 8 |
| FR-064 | ISO 20022 Formatting | STORY-041 | 5 |

**Coverage: 64/64 FRs (100%)**

---

## Risks and Mitigation

**High:**
- **NFC/HCE implementation complexity** (Sprint 3) — Mitigation: prototype NFC early in Sprint 2 if possible, allocate large buffer
- **National switch sandbox access** (Sprint 5) — Mitigation: start external coordination in Sprint 1, build adapter with mock switch
- **HSM hardware availability** (Sprint 3) — Mitigation: develop against HSM simulator, swap to real hardware when available

**Medium:**
- **KYC third-party API selection** (Sprint 2) — Mitigation: design adapter interface, select provider by Sprint 1 end
- **Scope vs. timeline** — 76 stories in 24 weeks is ambitious — Mitigation: Should Have stories can be deferred
- **Cross-border transfer complexity** (Sprint 4) — Mitigation: may require switch integration from Sprint 5; stub initially

**Low:**
- **Docker Compose scaling limits** — Mitigation: architecture is container-ready for future K8s migration
- **Blazor Server sticky session management** — Mitigation: single admin portal instance sufficient initially

---

## Dependencies

**External:**
- National payment switch sandbox access (needed by Sprint 5)
- HSM hardware (needed by Sprint 3)
- EFT POS terminals for testing (needed by Sprint 3)
- SMS gateway API credentials (needed by Sprint 1)
- Firebase Cloud Messaging setup (needed by Sprint 1)
- KYC verification service selection (needed by Sprint 2)

**Internal:**
- Infrastructure (Sprint 1) blocks all subsequent sprints
- KYC/Auth (Sprint 2) blocks payment features (Sprint 3+)
- HSM Interface (Sprint 3) blocks terminal key management (Sprint 6)
- Switch integration (Sprint 5) blocks reconciliation reports (Sprint 7)

---

## Definition of Done

For a story to be considered complete:
- [ ] Code implemented and committed
- [ ] Unit tests written and passing (80%+ coverage on business logic)
- [ ] Integration tests passing (gRPC service contracts)
- [ ] Code reviewed and approved by at least 1 team member
- [ ] Deployed to staging environment
- [ ] Acceptance criteria validated
- [ ] No critical or high bugs outstanding

---

## Next Steps

**Immediate:** Begin Sprint 1

Run `/create-story STORY-001` to create a detailed story document, or run `/dev-story STORY-001` to start implementing immediately.

**Sprint cadence:**
- Sprint length: 3 weeks
- Sprint planning: Monday Week 1
- Mid-sprint check: Friday Week 2
- Sprint review: Friday Week 3
- Sprint retrospective: Friday Week 3

**Developer allocation suggestion:**
- **Dev 1:** Core Banking (Accounts, Payments, Transfers)
- **Dev 2:** Switching Server + HSM Interface
- **Dev 3:** Terminal Manager + Merchant Management + Agent Banking
- **Dev 4:** Admin Portal + Reporting + White-Label Config

---

**This plan was created using BMAD Method v6 - Phase 4 (Implementation Planning)**

*To continue: Run `/workflow-status` to see your progress and next recommended workflow.*
