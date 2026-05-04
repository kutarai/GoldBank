# Product Requirements Document: GoldBank

**Date:** 2026-02-24
**Author:** wmapundu
**Version:** 1.0
**Project Type:** other (multi-component banking suite)
**Project Level:** 4
**Status:** Draft

---

## Document Overview

This Product Requirements Document (PRD) defines the functional and non-functional requirements for GoldBank. It serves as the source of truth for what will be built and provides traceability from requirements through implementation.

**Related Documents:**
- Product Brief: docs/product-brief-goldbank-2026-02-24.md

---

## Executive Summary

GoldBank is a white-label banking platform targeting the Southern African region, comprising a mobile wallet app, account management backend, national network switching server, and EFT POS terminal manager with HSM. Its key differentiator is NFC-based contactless payments that turn capable smartphones into virtual payment cards, enabling card-present transactions without physical cards. The platform serves the unbanked mass market with zero-cost accounts while creating a self-sustaining merchant agent ecosystem.

---

## Product Goals

### Business Objectives

- Onboard 500,000 consumers in year 1
- Build a merchant agent network of 10,000 in year 1
- Reach 5 million transactions per month
- Deploy 3 white-label organisations in the first year

### Success Metrics

- Monthly active users (MAU)
- Monthly transaction volume
- Merchant network growth
- Net revenue of at least 5c per transaction average

---

## Functional Requirements

Functional Requirements (FRs) define **what** the system does - specific features and behaviors.

Each requirement includes:
- **ID**: Unique identifier (FR-001, FR-002, etc.)
- **Priority**: Must Have / Should Have / Could Have / Won't Have (MoSCoW)
- **Description**: What the system should do
- **Acceptance Criteria**: How to verify it's complete

---

### Feature Area 1: User Account Management

#### FR-001: User Self-Registration

**Priority:** Must Have

**Description:**
User can register for a new account via the mobile app using their phone number.

**Acceptance Criteria:**
- [ ] User can download app and initiate registration
- [ ] User provides phone number and receives OTP for verification
- [ ] User creates a PIN for account access
- [ ] Account is created in pending state until KYC is completed

**Dependencies:** None

---

#### FR-002: KYC - National ID Verification

**Priority:** Must Have

**Description:**
System verifies user identity by capturing and validating a national ID document.

**Acceptance Criteria:**
- [ ] User can upload or scan national ID document
- [ ] System validates ID document authenticity
- [ ] System extracts identity data from document
- [ ] Account is linked to verified national ID

**Dependencies:** FR-001

---

#### FR-003: KYC - Selfie/Photo Match

**Priority:** Must Have

**Description:**
System performs facial recognition matching between user selfie and national ID photo.

**Acceptance Criteria:**
- [ ] User can capture a live selfie during KYC process
- [ ] System compares selfie against ID document photo
- [ ] System provides pass/fail result with confidence score
- [ ] Failed matches are flagged for manual review

**Dependencies:** FR-002

---

#### FR-004: Free Account Creation

**Priority:** Must Have

**Description:**
Upon successful KYC, a fully functional account is created with zero fees.

**Acceptance Criteria:**
- [ ] Account is activated automatically upon KYC approval
- [ ] No monthly fees, maintenance charges, or card issuance costs applied
- [ ] User receives confirmation notification
- [ ] Account is immediately usable for transactions

**Dependencies:** FR-003

---

#### FR-005: Account Profile Management

**Priority:** Must Have

**Description:**
User can view and edit their personal profile details.

**Acceptance Criteria:**
- [ ] User can view their profile information
- [ ] User can update non-KYC fields (e.g., email, address)
- [ ] KYC fields (name, ID number) are read-only after verification
- [ ] Changes are saved and reflected immediately

**Dependencies:** FR-004

---

#### FR-006: Account Balance Inquiry

**Priority:** Must Have

**Description:**
User can view their current account balance at any time.

**Acceptance Criteria:**
- [ ] Balance is displayed on the home screen after authentication
- [ ] Balance reflects all completed transactions in real-time
- [ ] Balance can be refreshed manually

**Dependencies:** FR-004

---

#### FR-007: Transaction History / Mini-Statement

**Priority:** Must Have

**Description:**
User can view a list of past transactions with details.

**Acceptance Criteria:**
- [ ] User can view transaction history sorted by date (most recent first)
- [ ] Each transaction shows: date, type, amount, recipient/sender, status
- [ ] User can filter by transaction type and date range
- [ ] Minimum 90 days of history available

**Dependencies:** FR-004

---

### Feature Area 2: NFC Contactless Payments

#### FR-008: NFC Contactless Payment at POS

**Priority:** Must Have

**Description:**
User can make contactless payments by tapping their NFC-enabled phone on an EFT POS terminal.

**Acceptance Criteria:**
- [ ] Phone emulates a contactless payment card via NFC
- [ ] Transaction is initiated when phone is tapped on POS terminal
- [ ] Payment is processed within 2 seconds
- [ ] User receives confirmation notification on completion

**Dependencies:** FR-004, FR-009

---

#### FR-009: NFC Payment Tokenization

**Priority:** Must Have

**Description:**
Payment credentials are securely tokenized on the device for NFC transactions.

**Acceptance Criteria:**
- [ ] Payment token is provisioned to device during account setup
- [ ] Actual account credentials are never exposed during NFC transactions
- [ ] Tokens are stored securely in device secure element or HCE
- [ ] Tokens can be revoked remotely if device is lost/stolen

**Dependencies:** FR-004

---

#### FR-010: PIN Entry for High-Value NFC Transactions

**Priority:** Must Have

**Description:**
Transactions above a configurable threshold require PIN entry for authorization.

**Acceptance Criteria:**
- [ ] Threshold amount is configurable per tenant
- [ ] Transactions below threshold proceed with tap only
- [ ] Transactions above threshold prompt for PIN on the terminal
- [ ] PIN is encrypted via HSM before transmission

**Dependencies:** FR-008, FR-033

---

#### FR-011: NFC Payment Receipt/Notification

**Priority:** Must Have

**Description:**
User receives immediate notification upon NFC transaction completion.

**Acceptance Criteria:**
- [ ] Push notification sent to user's device on transaction completion
- [ ] Notification includes: amount, merchant name, date/time, status
- [ ] Transaction appears in transaction history immediately
- [ ] Both successful and failed transactions generate notifications

**Dependencies:** FR-008

---

### Feature Area 3: EMV QR Code Payments

#### FR-012: Generate EMV QR Code for Receiving Payments

**Priority:** Must Have

**Description:**
Merchants and users can generate an EMV-compliant QR code to receive payments.

**Acceptance Criteria:**
- [ ] QR code is generated per EMV QR Code specification
- [ ] QR code can include fixed or dynamic amount
- [ ] QR code contains payee identification
- [ ] QR code can be displayed on screen or printed

**Dependencies:** FR-004

---

#### FR-013: Scan EMV QR Code to Pay

**Priority:** Must Have

**Description:**
User can scan an EMV QR code using the mobile app camera to initiate a payment.

**Acceptance Criteria:**
- [ ] App opens camera to scan QR code
- [ ] QR code is parsed per EMV specification
- [ ] Payment details (recipient, amount) are displayed for confirmation
- [ ] User confirms and authorizes payment with PIN/biometric

**Dependencies:** FR-004

---

#### FR-014: QR Code Payment Confirmation

**Priority:** Must Have

**Description:**
System displays payment details for user confirmation before executing QR code payment.

**Acceptance Criteria:**
- [ ] Recipient name and amount are clearly displayed
- [ ] User must explicitly confirm before payment executes
- [ ] Confirmation receipt is shown after successful payment
- [ ] Both payer and payee receive transaction notifications

**Dependencies:** FR-013

---

### Feature Area 4: P2P Transfers

#### FR-015: Send Money to GoldBank User

**Priority:** Must Have

**Description:**
User can send money to another GoldBank user using their phone number.

**Acceptance Criteria:**
- [ ] User can enter recipient phone number or select from contacts
- [ ] System validates recipient exists and is active
- [ ] User enters amount and confirms transfer
- [ ] Transfer is immediate and reflected in both accounts

**Dependencies:** FR-004

---

#### FR-016: Cross-Border P2P Transfer

**Priority:** Must Have

**Description:**
User can send money to users in other Southern African countries.

**Acceptance Criteria:**
- [ ] User can select destination country
- [ ] Exchange rate is displayed before confirmation (if applicable)
- [ ] Transfer complies with cross-border transaction limits
- [ ] Recipient receives funds in their local currency/account

**Dependencies:** FR-015

---

#### FR-017: Transfer Confirmation Screen

**Priority:** Must Have

**Description:**
System displays transfer details for user confirmation before executing.

**Acceptance Criteria:**
- [ ] Recipient name, phone number, and amount are clearly displayed
- [ ] Any applicable fees are shown
- [ ] User must explicitly confirm before transfer executes
- [ ] Transfer can be cancelled before confirmation

**Dependencies:** FR-015

---

#### FR-018: P2P Transfer Notifications

**Priority:** Must Have

**Description:**
Both sender and receiver receive notifications on P2P transfer completion.

**Acceptance Criteria:**
- [ ] Sender receives confirmation notification with reference number
- [ ] Receiver receives notification with sender name and amount
- [ ] Notifications are sent via push notification
- [ ] Transaction appears in both users' transaction history

**Dependencies:** FR-015

---

### Feature Area 5: Cash-In / Cash-Out (Agent Network)

#### FR-019: Cash-In at Agent

**Priority:** Must Have

**Description:**
Consumer can deposit cash at a merchant agent and have their account credited.

**Acceptance Criteria:**
- [ ] Agent initiates cash-in transaction on POS terminal or agent app
- [ ] Consumer is identified by phone number or account number
- [ ] Consumer confirms the deposit amount
- [ ] Account is credited immediately upon confirmation
- [ ] Both agent and consumer receive transaction receipts

**Dependencies:** FR-004, FR-022

---

#### FR-020: Cash-Out at Agent

**Priority:** Must Have

**Description:**
Consumer can withdraw cash from their account at a merchant agent.

**Acceptance Criteria:**
- [ ] Consumer initiates cash-out request on mobile app or at agent
- [ ] Consumer authenticates with PIN
- [ ] Agent confirms cash disbursement
- [ ] Account is debited immediately
- [ ] Both agent and consumer receive transaction receipts

**Dependencies:** FR-004, FR-022

---

#### FR-021: Agent Commission Calculation

**Priority:** Must Have

**Description:**
System automatically calculates and credits commission to agents on cash-in/cash-out transactions.

**Acceptance Criteria:**
- [ ] Commission rate is configurable per transaction type and tenant
- [ ] Commission is calculated and credited automatically
- [ ] Agent can view commission earnings in real-time
- [ ] Commission is included in agent settlement

**Dependencies:** FR-019, FR-020

---

#### FR-022: Agent Float/Balance Management

**Priority:** Must Have

**Description:**
Agents maintain a float balance used for cash-in/cash-out operations.

**Acceptance Criteria:**
- [ ] Agent has a dedicated float account
- [ ] Float balance decreases on cash-in (agent gives cash, credits customer)
- [ ] Float balance increases on cash-out (agent receives cash, debits customer)
- [ ] Agent receives alerts when float balance is low
- [ ] Agent can top up float via bank transfer or super-agent

**Dependencies:** FR-037

---

#### FR-023: Agent Transaction Receipt

**Priority:** Must Have

**Description:**
Receipts are generated for all agent transactions.

**Acceptance Criteria:**
- [ ] Receipt includes: transaction ID, date/time, type, amount, customer reference
- [ ] Receipt can be printed on POS terminal
- [ ] Digital receipt is also available in agent transaction history
- [ ] Customer receives matching digital receipt on mobile app

**Dependencies:** FR-019, FR-020

---

### Feature Area 6: Bill Payments

#### FR-024: Browse Bill Payment Providers

**Priority:** Must Have

**Description:**
User can browse and search available bill payment providers.

**Acceptance Criteria:**
- [ ] Bill providers are organized by category (utilities, telecom, etc.)
- [ ] User can search providers by name
- [ ] Provider details include: name, account format, minimum/maximum amounts

**Dependencies:** FR-004

---

#### FR-025: Pay Bill

**Priority:** Must Have

**Description:**
User can pay a bill by selecting provider, entering account/reference number, and amount.

**Acceptance Criteria:**
- [ ] User selects provider and enters account/reference number
- [ ] User enters payment amount
- [ ] System validates account format and amount against provider rules
- [ ] Payment is processed and confirmation is displayed

**Dependencies:** FR-024

---

#### FR-026: Bill Payment Confirmation and Receipt

**Priority:** Must Have

**Description:**
System provides confirmation and receipt for completed bill payments.

**Acceptance Criteria:**
- [ ] Confirmation screen shows: provider, account, amount, reference number
- [ ] Receipt is stored in transaction history
- [ ] Push notification is sent on completion

**Dependencies:** FR-025

---

#### FR-027: Saved/Favorite Billers

**Priority:** Should Have

**Description:**
User can save frequently used billers for quick repeat payments.

**Acceptance Criteria:**
- [ ] User can save a biller with account/reference details
- [ ] Saved billers appear in a favorites list
- [ ] User can initiate payment from saved biller with pre-filled details
- [ ] User can remove saved billers

**Dependencies:** FR-025

---

### Feature Area 7: National Network Switching

#### FR-028: Route Outgoing Transactions to National Switch

**Priority:** Must Have

**Description:**
System routes outgoing payment transactions to the national payment switch for processing.

**Acceptance Criteria:**
- [ ] Transactions to external banks/institutions are routed via national switch
- [ ] Message formatting complies with national scheme standards
- [ ] Routing is based on destination institution identifier
- [ ] Failed routing attempts are logged and retried per configuration

**Dependencies:** FR-031

---

#### FR-029: Process Incoming Transactions from National Switch

**Priority:** Must Have

**Description:**
System receives and processes incoming transactions from the national payment switch.

**Acceptance Criteria:**
- [ ] System listens for incoming transaction messages
- [ ] Incoming transactions are validated and parsed
- [ ] Valid transactions credit the destination account
- [ ] Invalid transactions return appropriate decline codes

**Dependencies:** FR-031

---

#### FR-030: Transaction Reconciliation with National Switch

**Priority:** Must Have

**Description:**
System performs daily reconciliation of transactions with the national payment switch.

**Acceptance Criteria:**
- [ ] Automated reconciliation runs on configurable schedule
- [ ] Mismatches are flagged for manual review
- [ ] Reconciliation report is generated and stored
- [ ] Settlement amounts are calculated accurately

**Dependencies:** FR-028, FR-029

---

#### FR-031: ISO 8583 Message Formatting

**Priority:** Must Have

**Description:**
All switch transactions use ISO 8583 message format per national scheme specifications.

**Acceptance Criteria:**
- [ ] Messages comply with ISO 8583 standard
- [ ] All required data elements are populated correctly
- [ ] Message authentication codes (MAC) are generated per scheme rules
- [ ] Both request and response messages are handled correctly

**Dependencies:** None

---

### Feature Area 8: Terminal Management + HSM

#### FR-032: Register and Provision EFT POS Terminals

**Priority:** Must Have

**Description:**
Administrators can register new EFT POS terminals and provision them for use.

**Acceptance Criteria:**
- [ ] Terminal can be registered with unique terminal ID and merchant assignment
- [ ] Terminal configuration (parameters, keys) can be downloaded remotely
- [ ] Terminal activation requires successful key exchange
- [ ] Terminal registration is logged in audit trail

**Dependencies:** FR-037

---

#### FR-033: Remote Terminal Key Management via HSM

**Priority:** Must Have

**Description:**
HSM manages cryptographic keys for terminal PIN encryption and decryption.

**Acceptance Criteria:**
- [ ] Master keys are generated and stored securely in HSM
- [ ] Session keys are derived and distributed to terminals
- [ ] Key rotation is supported on configurable schedule
- [ ] All key operations are logged in audit trail

**Dependencies:** None

---

#### FR-034: Terminal PIN Encryption/Decryption via HSM

**Priority:** Must Have

**Description:**
HSM handles PIN block encryption and decryption for terminal transactions.

**Acceptance Criteria:**
- [ ] PIN blocks are encrypted at the terminal using session keys
- [ ] HSM decrypts PIN blocks for transaction authorization
- [ ] PIN translation is supported for switch forwarding
- [ ] PIN verification results are returned to requesting system

**Dependencies:** FR-033

---

#### FR-035: Terminal Status Monitoring

**Priority:** Must Have

**Description:**
System monitors the status of all registered EFT POS terminals.

**Acceptance Criteria:**
- [ ] Terminal status is tracked: online, offline, fault
- [ ] Status dashboard shows all terminals with current state
- [ ] Alerts are generated for terminals that go offline
- [ ] Last communication timestamp is recorded per terminal

**Dependencies:** FR-032

---

#### FR-036: Remote Terminal Software/Configuration Updates

**Priority:** Should Have

**Description:**
Terminal software and configuration can be updated remotely.

**Acceptance Criteria:**
- [ ] Software updates can be pushed to terminals remotely
- [ ] Configuration changes are applied without physical access
- [ ] Update status is tracked per terminal
- [ ] Failed updates are flagged and can be retried

**Dependencies:** FR-032

---

### Feature Area 9: Merchant Management

#### FR-037: Merchant Registration and Onboarding

**Priority:** Must Have

**Description:**
Merchants can be registered and onboarded with KYC verification.

**Acceptance Criteria:**
- [ ] Merchant provides business details and owner identification
- [ ] Merchant KYC is validated (ID, business registration)
- [ ] Merchant is assigned a unique merchant ID
- [ ] Merchant is configured as agent if applicable

**Dependencies:** None

---

#### FR-038: Merchant Profile Management

**Priority:** Must Have

**Description:**
Merchants can view and update their business profile details.

**Acceptance Criteria:**
- [ ] Merchant can view business details and location
- [ ] Merchant can update non-KYC fields
- [ ] Location/GPS coordinates stored for merchant locator
- [ ] Changes are reflected immediately

**Dependencies:** FR-037

---

#### FR-039: Merchant Settlement and Payout

**Priority:** Must Have

**Description:**
System processes merchant settlements and payouts on a configurable schedule.

**Acceptance Criteria:**
- [ ] Settlement is calculated based on completed transactions
- [ ] Settlement schedule is configurable (daily, weekly)
- [ ] Settlement includes transaction fees and commission
- [ ] Payout is credited to merchant's designated account

**Dependencies:** FR-037

---

#### FR-040: Merchant Transaction History

**Priority:** Must Have

**Description:**
Merchants can view their transaction history and statements.

**Acceptance Criteria:**
- [ ] Merchant can view all transactions processed at their terminal(s)
- [ ] Transactions are filterable by date, type, and status
- [ ] Daily/monthly statements can be generated
- [ ] Statements include transaction totals and fees

**Dependencies:** FR-037

---

#### FR-041: Merchant Commission Reporting

**Priority:** Must Have

**Description:**
Merchants can view their agent commission earnings.

**Acceptance Criteria:**
- [ ] Commission earnings are displayed separately from transaction revenue
- [ ] Commission is broken down by transaction type (cash-in, cash-out)
- [ ] Running total and period summaries are available
- [ ] Commission report can be exported

**Dependencies:** FR-021, FR-037

---

### Feature Area 10: Admin / Back-Office Portal

#### FR-042: Admin User Management with RBAC

**Priority:** Must Have

**Description:**
Admin portal supports multiple users with role-based access control.

**Acceptance Criteria:**
- [ ] Admin users can be created with assigned roles
- [ ] Roles define permissions for portal features
- [ ] Standard roles: super admin, operations, support, finance, compliance
- [ ] Role assignments can be modified by super admin

**Dependencies:** None

---

#### FR-043: Customer Account Management

**Priority:** Must Have

**Description:**
Admins can view and manage customer accounts.

**Acceptance Criteria:**
- [ ] Admin can search customers by name, phone, ID number
- [ ] Admin can view full account details and transaction history
- [ ] Admin can suspend, freeze, or close accounts with reason
- [ ] All admin actions on accounts are logged in audit trail

**Dependencies:** FR-042

---

#### FR-044: Merchant/Agent Management

**Priority:** Must Have

**Description:**
Admins can manage merchant and agent accounts.

**Acceptance Criteria:**
- [ ] Admin can approve or reject merchant applications
- [ ] Admin can suspend or deactivate merchants/agents
- [ ] Admin can configure merchant-specific settings (fees, limits)
- [ ] All admin actions are logged in audit trail

**Dependencies:** FR-042

---

#### FR-045: Transaction Monitoring and Search

**Priority:** Must Have

**Description:**
Admins can monitor and search all transactions across the platform.

**Acceptance Criteria:**
- [ ] Real-time transaction feed is available
- [ ] Transactions can be searched by: ID, user, merchant, date, amount, type
- [ ] Transaction details include full processing trail
- [ ] Suspicious transactions can be flagged for review

**Dependencies:** FR-042

---

#### FR-046: KYC Review and Approval Workflow

**Priority:** Must Have

**Description:**
Admin portal provides a workflow for reviewing and approving KYC submissions.

**Acceptance Criteria:**
- [ ] Pending KYC submissions are listed in a review queue
- [ ] Reviewer can view ID document and selfie side by side
- [ ] Reviewer can approve, reject, or request resubmission
- [ ] KYC decisions are logged with reviewer ID and timestamp

**Dependencies:** FR-042, FR-002, FR-003

---

#### FR-047: System Configuration Management

**Priority:** Must Have

**Description:**
Admins can configure system parameters including fees, limits, and operational settings.

**Acceptance Criteria:**
- [ ] Transaction fees are configurable by type and tenant
- [ ] Transaction limits are configurable (daily, monthly, per-transaction)
- [ ] Commission rates are configurable for agents
- [ ] Configuration changes are logged and take effect immediately or on schedule

**Dependencies:** FR-042

---

#### FR-048: Dispute/Chargeback Management

**Priority:** Should Have

**Description:**
Admin portal supports managing transaction disputes and chargebacks.

**Acceptance Criteria:**
- [ ] Disputes can be raised by customers or admins
- [ ] Dispute workflow: open → investigate → resolve
- [ ] Refunds/reversals can be processed from dispute resolution
- [ ] Dispute history and resolution details are stored

**Dependencies:** FR-042, FR-045

---

### Feature Area 11: Reporting & Analytics Dashboard

#### FR-049: Real-Time Transaction Dashboard

**Priority:** Must Have

**Description:**
Dashboard displays real-time transaction volume and value metrics.

**Acceptance Criteria:**
- [ ] Live transaction count and value are displayed
- [ ] Dashboard shows transactions per second/minute/hour
- [ ] Visual charts for transaction trends
- [ ] Dashboard auto-refreshes at configurable interval

**Dependencies:** FR-042

---

#### FR-050: User Registration and Growth Reports

**Priority:** Must Have

**Description:**
Reports on user registration trends and growth metrics.

**Acceptance Criteria:**
- [ ] Daily, weekly, monthly registration counts
- [ ] KYC completion rates
- [ ] Active vs. inactive user ratios
- [ ] Growth trend visualizations

**Dependencies:** FR-042

---

#### FR-051: Merchant/Agent Performance Reports

**Priority:** Must Have

**Description:**
Reports on merchant and agent transaction performance.

**Acceptance Criteria:**
- [ ] Transaction volume and value per merchant/agent
- [ ] Agent cash-in/cash-out volumes
- [ ] Commission earned per agent
- [ ] Top performing and underperforming agents

**Dependencies:** FR-042

---

#### FR-052: Revenue and Fee Reports

**Priority:** Must Have

**Description:**
Reports on revenue from transaction fees, interchange, and terminal fees.

**Acceptance Criteria:**
- [ ] Revenue broken down by fee type
- [ ] Revenue per tenant (white-label)
- [ ] Period comparison (month over month)
- [ ] Revenue forecasting based on trends

**Dependencies:** FR-042

---

#### FR-053: Reconciliation Reports

**Priority:** Must Have

**Description:**
Reports on transaction reconciliation with national switch and settlements.

**Acceptance Criteria:**
- [ ] Daily reconciliation summary
- [ ] Matched vs. unmatched transaction counts
- [ ] Settlement amounts and discrepancies
- [ ] Drill-down into individual mismatches

**Dependencies:** FR-030, FR-042

---

#### FR-054: Exportable Reports

**Priority:** Should Have

**Description:**
All reports can be exported in standard formats.

**Acceptance Criteria:**
- [ ] Reports can be exported as CSV
- [ ] Reports can be exported as PDF
- [ ] Export includes applied filters and date range
- [ ] Scheduled report generation and email delivery

**Dependencies:** FR-049 through FR-053

---

### Feature Area 12: White-Label Configuration

#### FR-055: Configurable Branding per Tenant

**Priority:** Must Have

**Description:**
Each deploying institution can customize the app branding.

**Acceptance Criteria:**
- [ ] Logo, colors, and app name are configurable per tenant
- [ ] Branding is applied consistently across mobile app and admin portal
- [ ] Branding changes can be applied without code deployment
- [ ] Default branding is provided for unconfigured elements

**Dependencies:** None

---

#### FR-056: Tenant Data Isolation

**Priority:** Must Have

**Description:**
Each white-label deployment has complete data separation.

**Acceptance Criteria:**
- [ ] Each tenant's data is logically isolated
- [ ] Cross-tenant data access is prevented at application and database level
- [ ] Tenant-specific encryption keys are supported
- [ ] Tenant identification is enforced on all API calls

**Dependencies:** None

---

#### FR-057: Per-Tenant Fee and Limit Configuration

**Priority:** Must Have

**Description:**
Transaction fees and limits can be configured independently per tenant.

**Acceptance Criteria:**
- [ ] Each tenant can have unique fee structures
- [ ] Transaction limits are independently configurable per tenant
- [ ] Agent commission rates are configurable per tenant
- [ ] Changes apply only to the specified tenant

**Dependencies:** FR-047

---

#### FR-058: Per-Tenant Admin Portal Access

**Priority:** Must Have

**Description:**
Each tenant has its own admin portal access with isolated views.

**Acceptance Criteria:**
- [ ] Tenant admins only see their tenant's data
- [ ] Super admin (platform owner) can view all tenants
- [ ] Tenant admin roles are independent per tenant
- [ ] Tenant admin cannot access other tenants' configurations

**Dependencies:** FR-042, FR-056

---

### Feature Area 13: Security & Authentication

#### FR-059: User Authentication via PIN/Biometric

**Priority:** Must Have

**Description:**
Mobile app supports authentication via PIN and biometric (fingerprint/face).

**Acceptance Criteria:**
- [ ] User can authenticate with 4-6 digit PIN
- [ ] User can enable fingerprint or face authentication
- [ ] Biometric is optional; PIN is always available as fallback
- [ ] Failed authentication attempts are tracked and account locked after threshold

**Dependencies:** FR-004

---

#### FR-060: Session Management with Auto-Timeout

**Priority:** Must Have

**Description:**
App sessions expire automatically after period of inactivity.

**Acceptance Criteria:**
- [ ] Session timeout is configurable (default: 5 minutes of inactivity)
- [ ] User is prompted to re-authenticate after timeout
- [ ] Active transactions are not interrupted by timeout
- [ ] Session tokens are invalidated on timeout and logout

**Dependencies:** FR-059

---

#### FR-061: Transaction Authorization

**Priority:** Must Have

**Description:**
High-value transactions require explicit PIN authorization.

**Acceptance Criteria:**
- [ ] Authorization threshold is configurable per transaction type
- [ ] User must enter PIN for transactions above threshold
- [ ] Authorization is required regardless of biometric login
- [ ] Failed authorization attempts are logged

**Dependencies:** FR-059

---

#### FR-062: Device Binding

**Priority:** Must Have

**Description:**
User account is bound to a registered device for security.

**Acceptance Criteria:**
- [ ] Account is linked to device during registration
- [ ] Login from unregistered device triggers additional verification
- [ ] User can transfer account to new device via secure process
- [ ] Lost device can be deregistered via admin or self-service

**Dependencies:** FR-004

---

#### FR-063: Fraud Detection Alerts

**Priority:** Should Have

**Description:**
System generates alerts on suspicious transaction patterns.

**Acceptance Criteria:**
- [ ] Rules-based fraud detection on transaction patterns
- [ ] Alerts generated for: unusual amounts, velocity, location anomalies
- [ ] Alerts visible in admin portal for review
- [ ] Suspicious accounts can be automatically suspended pending review

**Dependencies:** FR-045

---

## Non-Functional Requirements

Non-Functional Requirements (NFRs) define **how** the system performs - quality attributes and constraints.

---

### NFR-001: Payment Transaction Performance

**Priority:** Must Have

**Description:**
Payment transactions must complete end-to-end within 2 seconds for 95% of requests.

**Acceptance Criteria:**
- [ ] NFC tap-to-approval completes in < 2 seconds (95th percentile)
- [ ] QR code payment completes in < 2 seconds (95th percentile)
- [ ] P2P transfer completes in < 2 seconds (95th percentile)

**Rationale:** Payment speed is critical for user adoption, especially at POS where delays cause friction.

---

### NFR-002: Concurrent User Capacity

**Priority:** Must Have

**Description:**
System must support 1,000 concurrent users without performance degradation.

**Acceptance Criteria:**
- [ ] System handles 1,000 concurrent API sessions
- [ ] Response times remain within NFR-001 targets at peak load
- [ ] No transaction failures due to capacity limits under 1,000 concurrent users

**Rationale:** Supports projected user base of 500,000 with typical concurrency ratios.

---

### NFR-003: Non-Payment API Performance

**Priority:** Must Have

**Description:**
Non-payment API operations respond within 500ms for 95% of requests.

**Acceptance Criteria:**
- [ ] Balance inquiry responds in < 500ms (95th percentile)
- [ ] Transaction history responds in < 500ms (95th percentile)
- [ ] Profile operations respond in < 500ms (95th percentile)

**Rationale:** Responsive app experience drives user engagement and satisfaction.

---

### NFR-004: Data in Transit Encryption

**Priority:** Must Have

**Description:**
All data transmitted between components is encrypted using TLS 1.2 or higher.

**Acceptance Criteria:**
- [ ] All API endpoints enforce TLS 1.2+
- [ ] Mobile app pins server certificates
- [ ] Inter-service communication uses TLS
- [ ] No plaintext transmission of sensitive data

**Rationale:** Protects financial data and personal information during transmission.

---

### NFR-005: Data at Rest Encryption

**Priority:** Must Have

**Description:**
All sensitive data stored in databases and file systems is encrypted using AES-256.

**Acceptance Criteria:**
- [ ] Database encryption at rest using AES-256
- [ ] KYC documents (ID images, selfies) are encrypted
- [ ] Encryption keys are managed via HSM
- [ ] Backup data is also encrypted

**Rationale:** Protects stored financial and personal data from unauthorized access.

---

### NFR-006: PCI-DSS Compliance

**Priority:** Must Have

**Description:**
System adheres to PCI-DSS standards for payment card data handling.

**Acceptance Criteria:**
- [ ] Cardholder data is not stored in plaintext
- [ ] Network segmentation isolates payment processing components
- [ ] Access to payment data is restricted and logged
- [ ] Regular vulnerability assessments are conducted

**Rationale:** Required for processing card-present and card-not-present transactions.

---

### NFR-007: HSM-Based Key Management

**Priority:** Must Have

**Description:**
All cryptographic key management for terminals and transactions uses HSM.

**Acceptance Criteria:**
- [ ] Master keys are generated within HSM (never exposed)
- [ ] Session keys are derived using HSM
- [ ] PIN encryption/decryption occurs within HSM boundary
- [ ] Key lifecycle (generation, rotation, destruction) managed via HSM

**Rationale:** HSM provides tamper-resistant key storage required for payment security.

---

### NFR-008: Audit Logging

**Priority:** Must Have

**Description:**
All financial transactions and administrative actions are logged in an immutable audit trail.

**Acceptance Criteria:**
- [ ] Every financial transaction is logged with full details
- [ ] Every admin action is logged with user ID, timestamp, and action
- [ ] Audit logs are immutable (append-only)
- [ ] Logs are retained for minimum 7 years

**Rationale:** Required for compliance, dispute resolution, and forensic investigation.

---

### NFR-009: User Capacity Scaling

**Priority:** Must Have

**Description:**
System architecture supports 500,000 registered users in year 1.

**Acceptance Criteria:**
- [ ] Database schema supports 500,000+ user accounts
- [ ] Storage capacity planned for associated KYC documents
- [ ] Transaction table partitioning supports projected volume

**Rationale:** Directly supports year 1 business objective.

---

### NFR-010: Horizontal Scalability

**Priority:** Should Have

**Description:**
Architecture supports horizontal scaling for transaction processing components.

**Acceptance Criteria:**
- [ ] Stateless API design allows adding processing nodes
- [ ] Database connection pooling supports multiple application instances
- [ ] Load balancing distributes traffic across instances

**Rationale:** Enables growth beyond initial capacity without re-architecture.

---

### NFR-011: System Availability

**Priority:** Must Have

**Description:**
System maintains 99.9% uptime (maximum ~8.7 hours downtime per year).

**Acceptance Criteria:**
- [ ] Core payment processing achieves 99.9% uptime
- [ ] Planned maintenance windows are scheduled during low-traffic periods
- [ ] Health monitoring with automated alerting on degradation
- [ ] Failover procedures documented and tested

**Rationale:** Payment systems must be highly available to maintain user trust.

---

### NFR-012: Database Backup and Recovery

**Priority:** Must Have

**Description:**
Automated daily database backups with point-in-time recovery capability.

**Acceptance Criteria:**
- [ ] Automated daily full backups
- [ ] Transaction log backups enable point-in-time recovery
- [ ] Backup restoration tested monthly
- [ ] Recovery point objective (RPO): 1 hour maximum data loss
- [ ] Recovery time objective (RTO): 4 hours maximum downtime

**Rationale:** Financial data must be recoverable in case of system failure.

---

### NFR-013: Transaction Atomicity

**Priority:** Must Have

**Description:**
All financial transactions are atomic — they either complete fully or roll back entirely.

**Acceptance Criteria:**
- [ ] No partial transactions exist in the system
- [ ] Failed transactions roll back all changes
- [ ] Timeout handling ensures no orphaned transactions
- [ ] Double-spend prevention is enforced

**Rationale:** Critical for financial integrity and accurate accounting.

---

### NFR-014: Multi-Language Support

**Priority:** Should Have

**Description:**
Mobile app supports English and at least one regional language.

**Acceptance Criteria:**
- [ ] App UI supports language switching
- [ ] English is the default language
- [ ] At least one Southern African regional language supported
- [ ] Language selection persists across sessions

**Rationale:** Improves accessibility for mass market users who may prefer local language.

---

### NFR-015: Onboarding Efficiency

**Priority:** Must Have

**Description:**
New user onboarding from app download to first transaction is completable in under 10 minutes.

**Acceptance Criteria:**
- [ ] Registration flow requires minimal steps
- [ ] KYC capture is streamlined (ID scan + selfie)
- [ ] Account activation is immediate upon KYC approval
- [ ] User can perform first cash-in or P2P transfer immediately

**Rationale:** Low friction onboarding is critical for mass market adoption.

---

### NFR-016: Mobile Platform Support

**Priority:** Must Have

**Description:**
Mobile app supports Android 8.0+ and iOS 14+ built with Kotlin Multiplatform.

**Acceptance Criteria:**
- [ ] Android app supports API level 26+ (Android 8.0+)
- [ ] iOS app supports iOS 14+
- [ ] Shared business logic via Kotlin Multiplatform
- [ ] Android is the primary deployment target (first release)

**Rationale:** Covers the vast majority of smartphones in the Southern African market.

---

### NFR-017: ISO 8583 Compliance

**Priority:** Must Have

**Description:**
National switch communication complies with ISO 8583 message format.

**Acceptance Criteria:**
- [ ] All transaction messages conform to ISO 8583 standard
- [ ] Message fields populated per national scheme specification
- [ ] Message authentication codes (MAC) correctly generated
- [ ] Compliance validated against scheme test suite

**Rationale:** Required for interoperability with national payment switch.

---

### NFR-018: EMV Contactless Compliance

**Priority:** Must Have

**Description:**
NFC payment implementation complies with EMV contactless specifications.

**Acceptance Criteria:**
- [ ] NFC payment follows EMV contactless kernel specifications
- [ ] Payment tokenization meets EMV token standards
- [ ] QR codes generated per EMV QR Code specification
- [ ] Compliance validated against EMV test tools

**Rationale:** Required for interoperability with standard EFT POS terminals.

---

## Epics

Epics are logical groupings of related functionality that will be broken down into user stories during sprint planning (Phase 4).

Each epic maps to multiple functional requirements and will generate 2-10 stories.

---

### EPIC-001: User Registration & KYC

**Description:**
End-to-end user onboarding including self-registration, national ID verification, selfie/photo matching, and account activation with zero fees.

**Functional Requirements:**
- FR-001: User Self-Registration
- FR-002: KYC - National ID Verification
- FR-003: KYC - Selfie/Photo Match
- FR-004: Free Account Creation

**Story Count Estimate:** 6-8

**Priority:** Must Have

**Business Value:**
Foundation for all other features. No users means no transactions, no revenue. KYC ensures regulatory compliance from day one.

---

### EPIC-002: Account Management

**Description:**
Core account features including profile management, balance inquiry, and transaction history.

**Functional Requirements:**
- FR-005: Account Profile Management
- FR-006: Account Balance Inquiry
- FR-007: Transaction History / Mini-Statement

**Story Count Estimate:** 4-5

**Priority:** Must Have

**Business Value:**
Essential for user engagement and trust. Users need visibility into their account and transaction activity.

---

### EPIC-003: NFC Contactless Payments

**Description:**
NFC-based contactless payment capability allowing users to tap their phone on POS terminals, including tokenization, PIN authorization, and receipts.

**Functional Requirements:**
- FR-008: NFC Contactless Payment at POS
- FR-009: NFC Payment Tokenization
- FR-010: PIN Entry for High-Value NFC Transactions
- FR-011: NFC Payment Receipt/Notification

**Story Count Estimate:** 6-8

**Priority:** Must Have

**Business Value:**
Core differentiator. This is what makes GoldBank unique — turning phones into contactless payment cards.

---

### EPIC-004: EMV QR Code Payments

**Description:**
QR code-based payment capability for receiving and making payments using EMV-compliant QR codes.

**Functional Requirements:**
- FR-012: Generate EMV QR Code for Receiving Payments
- FR-013: Scan EMV QR Code to Pay
- FR-014: QR Code Payment Confirmation

**Story Count Estimate:** 4-5

**Priority:** Must Have

**Business Value:**
Complements NFC by enabling payments on non-NFC devices and remote payment scenarios.

---

### EPIC-005: P2P Transfers

**Description:**
Person-to-person money transfers including domestic and cross-border transfers within Southern Africa.

**Functional Requirements:**
- FR-015: Send Money to GoldBank User
- FR-016: Cross-Border P2P Transfer
- FR-017: Transfer Confirmation Screen
- FR-018: P2P Transfer Notifications

**Story Count Estimate:** 5-6

**Priority:** Must Have

**Business Value:**
Addresses the core problem of cash dependency for remote transactions. Drives daily usage and transaction volume.

---

### EPIC-006: Agent Cash-In / Cash-Out

**Description:**
Cash deposit and withdrawal through merchant agents, including float management and commission processing.

**Functional Requirements:**
- FR-019: Cash-In at Agent
- FR-020: Cash-Out at Agent
- FR-021: Agent Commission Calculation
- FR-022: Agent Float/Balance Management
- FR-023: Agent Transaction Receipt

**Story Count Estimate:** 6-8

**Priority:** Must Have

**Business Value:**
Critical on-ramp and off-ramp for cash-dependent users. Agent commission drives merchant adoption.

---

### EPIC-007: Bill Payments

**Description:**
Bill payment functionality allowing users to pay utility, telecom, and other bills through the app.

**Functional Requirements:**
- FR-024: Browse Bill Payment Providers
- FR-025: Pay Bill
- FR-026: Bill Payment Confirmation and Receipt
- FR-027: Saved/Favorite Billers

**Story Count Estimate:** 4-6

**Priority:** Must Have

**Business Value:**
Adds everyday utility to the platform, driving regular usage and replacing cash-based bill payments.

---

### EPIC-008: National Network Switching

**Description:**
Integration with the national payment switch for routing transactions to external financial institutions, including ISO 8583 messaging and reconciliation.

**Functional Requirements:**
- FR-028: Route Outgoing Transactions to National Switch
- FR-029: Process Incoming Transactions from National Switch
- FR-030: Transaction Reconciliation with National Switch
- FR-031: ISO 8583 Message Formatting

**Story Count Estimate:** 6-8

**Priority:** Must Have

**Business Value:**
Enables interoperability with the broader financial system. Without this, GoldBank is a closed-loop system with limited value.

---

### EPIC-009: Terminal Management & HSM

**Description:**
Management of EFT POS terminals including provisioning, key management via HSM, PIN handling, and monitoring.

**Functional Requirements:**
- FR-032: Register and Provision EFT POS Terminals
- FR-033: Remote Terminal Key Management via HSM
- FR-034: Terminal PIN Encryption/Decryption via HSM
- FR-035: Terminal Status Monitoring
- FR-036: Remote Terminal Software/Configuration Updates

**Story Count Estimate:** 6-8

**Priority:** Must Have

**Business Value:**
Enables the physical payment acceptance infrastructure. Secure key management is non-negotiable for payment processing.

---

### EPIC-010: Merchant Management

**Description:**
Merchant lifecycle management including registration, KYC, settlement, and reporting.

**Functional Requirements:**
- FR-037: Merchant Registration and Onboarding
- FR-038: Merchant Profile Management
- FR-039: Merchant Settlement and Payout
- FR-040: Merchant Transaction History
- FR-041: Merchant Commission Reporting

**Story Count Estimate:** 5-7

**Priority:** Must Have

**Business Value:**
Merchants are the supply side of the ecosystem. Proper management ensures merchant satisfaction and retention.

---

### EPIC-011: Admin / Back-Office Portal

**Description:**
Web-based administration portal for managing customers, merchants, transactions, KYC reviews, system configuration, and disputes.

**Functional Requirements:**
- FR-042: Admin User Management with RBAC
- FR-043: Customer Account Management
- FR-044: Merchant/Agent Management
- FR-045: Transaction Monitoring and Search
- FR-046: KYC Review and Approval Workflow
- FR-047: System Configuration Management
- FR-048: Dispute/Chargeback Management

**Story Count Estimate:** 8-10

**Priority:** Must Have

**Business Value:**
Operational backbone. Without admin tools, the platform cannot be managed, monitored, or supported effectively.

---

### EPIC-012: Reporting & Analytics

**Description:**
Comprehensive reporting and analytics dashboard for transaction monitoring, growth tracking, revenue analysis, and reconciliation.

**Functional Requirements:**
- FR-049: Real-Time Transaction Dashboard
- FR-050: User Registration and Growth Reports
- FR-051: Merchant/Agent Performance Reports
- FR-052: Revenue and Fee Reports
- FR-053: Reconciliation Reports
- FR-054: Exportable Reports

**Story Count Estimate:** 6-8

**Priority:** Must Have

**Business Value:**
Data-driven decision making. Revenue tracking and reconciliation are essential for financial operations.

---

### EPIC-013: White-Label Configuration

**Description:**
Multi-tenant white-label capability including branding customization, data isolation, and per-tenant configuration.

**Functional Requirements:**
- FR-055: Configurable Branding per Tenant
- FR-056: Tenant Data Isolation
- FR-057: Per-Tenant Fee and Limit Configuration
- FR-058: Per-Tenant Admin Portal Access

**Story Count Estimate:** 4-6

**Priority:** Must Have

**Business Value:**
Core business model. White-labeling enables deployment to multiple institutions, directly driving the 3-tenant year 1 goal.

---

### EPIC-014: Security & Authentication

**Description:**
Mobile app security including PIN/biometric authentication, session management, transaction authorization, device binding, and fraud detection.

**Functional Requirements:**
- FR-059: User Authentication via PIN/Biometric
- FR-060: Session Management with Auto-Timeout
- FR-061: Transaction Authorization
- FR-062: Device Binding
- FR-063: Fraud Detection Alerts

**Story Count Estimate:** 5-7

**Priority:** Must Have

**Business Value:**
Trust and security are paramount for a financial platform. Security features protect users and the platform from fraud.

---

## User Stories (High-Level)

User stories follow the format: "As a [user type], I want [goal] so that [benefit]."

These are preliminary stories. Detailed stories will be created in Phase 4 (Implementation).

---

Detailed user stories will be created during sprint planning (Phase 4).

---

## User Personas

### Consumer (Primary)
- **Name:** Thabo
- **Age:** 25
- **Profile:** Unbanked, low-income, tech-savvy
- **Location:** Urban/peri-urban Southern Africa
- **Device:** NFC-capable Android smartphone
- **Goals:** Make digital payments, send money to family, pay bills without a bank account
- **Pain Points:** Cannot access digital payments, relies entirely on cash, excluded from online commerce

### Merchant/Agent (Secondary)
- **Name:** Maria
- **Age:** 35
- **Profile:** Small informal trader, shop owner
- **Location:** Urban/peri-urban market area
- **Device:** EFT POS terminal + smartphone
- **Goals:** Accept digital payments, earn extra income as cash-in/cash-out agent
- **Pain Points:** Loses customers who want to pay digitally, expensive traditional POS solutions

### Deploying Institution Admin
- **Name:** James
- **Age:** 40
- **Profile:** Operations manager at a bank or fintech
- **Location:** Corporate office
- **Device:** Desktop/laptop
- **Goals:** Deploy white-label mobile wallet for customers, monitor operations, manage merchants
- **Pain Points:** Building from scratch is too expensive and slow, needs turnkey solution

---

## User Flows

### Flow 1: Consumer Onboarding
1. Download app from Play Store / App Store
2. Enter phone number → Receive OTP → Verify
3. Create account PIN
4. Scan national ID document
5. Capture live selfie
6. KYC verification (auto or manual review)
7. Account activated → Home screen with zero balance
8. Visit merchant agent for first cash-in

### Flow 2: NFC Payment at POS
1. Open app → Authenticate (PIN or biometric)
2. Hold phone near POS terminal
3. NFC communication initiates payment
4. If above threshold → Enter PIN on terminal
5. Transaction processed (< 2 seconds)
6. Receive push notification with receipt
7. Transaction appears in history

### Flow 3: Cash-In at Merchant Agent
1. Consumer visits merchant agent location
2. Consumer provides phone number to agent
3. Agent enters cash-in amount on POS terminal
4. Consumer receives confirmation prompt on mobile app
5. Consumer confirms deposit
6. Cash handed to agent
7. Consumer account credited, agent float debited
8. Both receive transaction receipts

---

## Dependencies

### Internal Dependencies

- Mobile app depends on backend API being available
- NFC payments depend on terminal manager + HSM being operational
- Agent cash-in/cash-out depends on merchant management being in place
- Reporting depends on transaction data from all other subsystems
- White-label configuration must be in place before tenant deployment
- Security & authentication is foundational for all user-facing features

### External Dependencies

- National payment switch connectivity and sandbox access
- HSM hardware provisioning and configuration
- EFT POS terminal procurement and delivery
- Sponsoring merchant bank for regulatory compliance and licensing
- KYC verification service (national ID database access or third-party provider)

---

## Assumptions

- NFC-capable smartphones are widespread among target users in Southern Africa
- Sponsoring merchant bank will maintain regulatory approval throughout development and launch
- National payment switch connectivity and sandbox will be available for integration testing
- Merchants will adopt low-cost terminals due to agent commission incentive
- On-premise infrastructure will be provisioned and available before development milestones
- KYC verification service (national ID validation) will be accessible via API

---

## Out of Scope

- USSD channel for feature phone users
- Cryptocurrency / digital asset transactions
- Lending / credit products (future phase)
- Savings / interest-bearing accounts (future phase)
- iOS app launch (deferred after Android; same codebase via KMP)

---

## Open Questions

1. Which specific national payment switch(es) will be integrated first?
2. Which KYC verification provider/service will be used for national ID validation?
3. What are the specific transaction limits per tier (daily, monthly, per-transaction)?
4. Which bill payment providers will be supported at launch?
5. What is the commission structure for agents (percentage or flat fee per transaction)?

---

## Approval & Sign-off

### Stakeholders

- **Founder/CTO** — High influence. Primary decision maker on product direction and technology choices.
- **Deploying Institutions** — High influence. Banks and fintechs who white-label the platform; their requirements shape the product.
- **Sponsoring Merchant Bank** — Medium influence. Handles regulatory compliance and licensing; their requirements are constraints on the solution.

### Approval Status

- [ ] Product Owner
- [ ] Engineering Lead
- [ ] Design Lead
- [ ] QA Lead

---

## Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 2026-02-24 | wmapundu | Initial PRD |

---

## Next Steps

### Phase 3: Architecture

Run `/architecture` to create system architecture based on these requirements.

The architecture will address:
- All functional requirements (FRs)
- All non-functional requirements (NFRs)
- Technical stack decisions
- Data models and APIs
- System components

### Phase 4: Sprint Planning

After architecture is complete, run `/sprint-planning` to:
- Break epics into detailed user stories
- Estimate story complexity
- Plan sprint iterations
- Begin implementation

---

**This document was created using BMAD Method v6 - Phase 2 (Planning)**

*To continue: Run `/workflow-status` to see your progress and next recommended workflow.*

---

## Appendix A: Requirements Traceability Matrix

| Epic ID | Epic Name | Functional Requirements | Story Count (Est.) |
|---------|-----------|-------------------------|-------------------|
| EPIC-001 | User Registration & KYC | FR-001, FR-002, FR-003, FR-004 | 6-8 |
| EPIC-002 | Account Management | FR-005, FR-006, FR-007 | 4-5 |
| EPIC-003 | NFC Contactless Payments | FR-008, FR-009, FR-010, FR-011 | 6-8 |
| EPIC-004 | EMV QR Code Payments | FR-012, FR-013, FR-014 | 4-5 |
| EPIC-005 | P2P Transfers | FR-015, FR-016, FR-017, FR-018 | 5-6 |
| EPIC-006 | Agent Cash-In / Cash-Out | FR-019, FR-020, FR-021, FR-022, FR-023 | 6-8 |
| EPIC-007 | Bill Payments | FR-024, FR-025, FR-026, FR-027 | 4-6 |
| EPIC-008 | National Network Switching | FR-028, FR-029, FR-030, FR-031 | 6-8 |
| EPIC-009 | Terminal Management & HSM | FR-032, FR-033, FR-034, FR-035, FR-036 | 6-8 |
| EPIC-010 | Merchant Management | FR-037, FR-038, FR-039, FR-040, FR-041 | 5-7 |
| EPIC-011 | Admin / Back-Office Portal | FR-042, FR-043, FR-044, FR-045, FR-046, FR-047, FR-048 | 8-10 |
| EPIC-012 | Reporting & Analytics | FR-049, FR-050, FR-051, FR-052, FR-053, FR-054 | 6-8 |
| EPIC-013 | White-Label Configuration | FR-055, FR-056, FR-057, FR-058 | 4-6 |
| EPIC-014 | Security & Authentication | FR-059, FR-060, FR-061, FR-062, FR-063 | 5-7 |
| **TOTAL** | **14 Epics** | **63 FRs** | **73-100 stories** |

---

## Appendix B: Prioritization Details

### Functional Requirements by Priority

| Priority | Count | Percentage |
|----------|-------|------------|
| Must Have | 57 | 90% |
| Should Have | 6 | 10% |
| Could Have | 0 | 0% |
| **Total** | **63** | **100%** |

**Should Have FRs:**
- FR-027: Saved/Favorite Billers
- FR-036: Remote Terminal Software/Configuration Updates
- FR-048: Dispute/Chargeback Management
- FR-054: Exportable Reports
- FR-063: Fraud Detection Alerts

### Non-Functional Requirements by Priority

| Priority | Count | Percentage |
|----------|-------|------------|
| Must Have | 15 | 83% |
| Should Have | 3 | 17% |
| **Total** | **18** | **100%** |

**Should Have NFRs:**
- NFR-010: Horizontal Scalability
- NFR-014: Multi-Language Support
