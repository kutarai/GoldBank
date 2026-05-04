---
title: "GoldBank White-Label Banking Platform"
subtitle: "Executive Summary"
date: "February 2026"
author: "GoldBank Engineering"
geometry: "margin=2.5cm"
fontsize: 11pt
toc: true
toc-depth: 2
header-includes:
  - \usepackage{fancyhdr}
  - \usepackage{xcolor}
  - \usepackage{booktabs}
  - \usepackage{longtable}
  - \usepackage{graphicx}
  - \usepackage{titlesec}
  - \definecolor{ubblue}{RGB}{0,82,136}
  - \definecolor{ubgray}{RGB}{100,100,100}
  - \pagestyle{fancy}
  - \fancyhf{}
  - \fancyhead[L]{\textcolor{ubgray}{\small GoldBank White-Label Banking Platform}}
  - \fancyhead[R]{\textcolor{ubgray}{\small Executive Summary}}
  - \fancyfoot[C]{\textcolor{ubgray}{\small Confidential --- \thepage}}
  - \renewcommand{\headrulewidth}{0.4pt}
  - \renewcommand{\footrulewidth}{0.4pt}
  - \titleformat{\section}{\Large\bfseries\color{ubblue}}{}{0em}{}
  - \titleformat{\subsection}{\large\bfseries\color{ubblue!80}}{}{0em}{}
  - \titleformat{\subsubsection}{\normalsize\bfseries\color{ubblue!60}}{}{0em}{}
---

\newpage

# Platform Overview

GoldBank is an enterprise-grade **white-label digital banking platform** purpose-built for the Southern African market. Designed to serve the unbanked and underbanked population, the platform transforms smartphones into full-featured banking instruments, enabling contactless NFC payments, peer-to-peer transfers, bill payments, and agent-assisted cash services --- all without requiring physical bank cards or branch visits.

The platform supports **multi-tenant white-label deployments**, allowing financial institutions, mobile network operators, and fintech companies to launch their own branded banking services rapidly, with complete data isolation, configurable fee structures, and independent branding.

## Key Value Propositions

- **Zero-cost accounts** for end users --- eliminating barriers to financial inclusion
- **NFC-as-Card technology** --- smartphones become virtual payment cards at existing POS terminals
- **Agent banking network** --- extending financial services through merchant agents earning commissions
- **White-label multi-tenancy** --- one platform powering multiple independent banking brands
- **On-premise deployment** --- meeting regulatory and data sovereignty requirements

## Target Metrics (Year 1)

| Metric | Target |
|:-------|-------:|
| Consumer users | 500,000 |
| Merchant agents | 10,000 |
| Monthly transactions | 5,000,000 |
| White-label tenants | 3 |
| Payment latency | < 2 seconds |
| System uptime | 99.9% |

\newpage

# Architecture Overview

GoldBank uses a **Modular Monolith with Satellite Services** architecture, balancing operational simplicity with enterprise-grade security and compliance requirements.

## Design Philosophy

The core banking logic runs as a single deployable unit (modular monolith) for transactional consistency and operational simplicity, while security-sensitive and hardware-dependent components run as isolated satellite services. This approach delivers:

- **ACID transactions** across all core banking operations
- **PCI-DSS compliance** through network isolation of payment processing
- **Hardware integration** for HSM cryptography and POS terminal management
- **Manageable complexity** for a focused development team

## Technology Stack

| Component | Technology | Purpose |
|:----------|:-----------|:--------|
| Backend | .NET 10 / C# | Core services, high performance |
| Mobile | Kotlin Multiplatform | Shared logic, native Android/iOS UI |
| API Protocol | gRPC + Protocol Buffers | Binary serialization, strong contracts |
| Database | PostgreSQL 18 | ACID-compliant, zero licensing cost |
| Cache | Redis | Hot data, session management |
| Messaging | Wolverine + MQTT | Async processing, terminal comms |
| Admin UI | Blazor Server + MudBlazor | Real-time back-office portal |
| Monitoring | Prometheus + Grafana + ELK | Metrics, dashboards, log aggregation |
| Deployment | Docker Compose | On-premise, reproducible environments |
| CI/CD | GitLab CI / Jenkins | Automated build, test, deploy pipeline |

\newpage

# System Modules

## 1. API Gateway

The API Gateway is the single entry point for all client requests, providing a unified security perimeter and traffic management layer.

**Capabilities:**

- JWT authentication and token validation
- Multi-tenant context extraction and routing
- Per-user and per-tenant rate limiting via Redis
- Request/response logging with PII masking
- gRPC server reflection for client discovery
- Health check and readiness endpoints
- Prometheus metrics exposure

The gateway enforces that every request carries a valid tenant context, ensuring complete data isolation from the network edge.

## 2. Accounts Module

The Accounts module manages the complete user lifecycle from registration through daily account operations.

**Capabilities:**

- **Self-registration** via phone number with OTP verification
- **PIN creation** and secure management (bcrypt hashed)
- **Biometric authentication** (fingerprint/face) with PIN fallback
- **Device binding** --- sessions tied to specific devices for security
- **Device transfer** --- account portability when users change phones
- **Multi-device session management** with concurrent session controls
- **Account status management** (Active, Suspended, Frozen, Closed)
- **Lockout protection** --- automatic lockout after 3 failed authentication attempts
- **Balance inquiry** with Redis-cached real-time balances
- **Transaction history** with filtering and pagination

## 3. KYC (Know Your Customer) Module

The KYC module implements regulatory-compliant identity verification using document and biometric checks.

**Capabilities:**

- **Document upload** --- national ID, passport, or driver's license (streaming upload)
- **Selfie capture** with liveness detection token
- **Facial recognition matching** between selfie and ID document photo
- **Tiered KYC levels** --- progressive verification unlocking higher transaction limits
- **Automated verification** with manual review fallback
- **Admin review workflow** --- approve, reject, or request resubmission

\newpage

## 4. Payments Module

The Payments module is the platform's core differentiator, enabling smartphone-based contactless payments at existing POS infrastructure.

**Capabilities:**

- **NFC contactless payments** --- tap phone on POS terminal to pay (Host Card Emulation)
- **Payment tokenization** --- virtual card credentials stored securely, real card data never exposed
- **EMV QR code generation** --- merchants generate scannable payment codes
- **QR code payment processing** --- customers scan to pay, supporting both static and dynamic QR
- **High-value transaction authorization** --- PIN verification for amounts above configurable thresholds
- **Real-time payment notifications** --- server-streaming gRPC for instant transaction alerts
- **HSM-backed cryptography** --- all card operations secured through Hardware Security Module

## 5. Transfers Module

The Transfers module enables domestic and international money movement between accounts.

**Capabilities:**

- **P2P transfers** --- send money to any registered user by phone number
- **Cross-border remittances** --- international transfers with corridor-based routing
- **Real-time exchange rates** --- transparent currency conversion with live rates
- **Transfer status tracking** --- full lifecycle visibility from initiation to completion
- **Delivery time estimation** --- corridor-specific expected delivery windows
- **Multi-currency support** --- ZWG (Zimbabwe Gold) as home currency with regional currency support

## 6. Bill Payments Module

The Bill Payments module connects users to utility and service providers for bill settlement.

**Capabilities:**

- **Provider catalog** --- electricity, water, telecommunications, and other utility billers
- **Bill payment processing** --- pay any registered provider by reference number
- **Saved billers** --- store frequently used providers and references for quick repeat payments
- **Provider discovery** --- search and filter by category and country
- **Payment history** --- complete bill payment records with receipt data

\newpage

## 7. Merchants Module

The Merchants module supports onboarding and ongoing management of businesses accepting GoldBank payments.

**Capabilities:**

- **Merchant registration** --- business details, location, and type classification
- **Business document upload** --- KYC for merchants (business registration, tax certificates)
- **Profile management** --- update business information, contact details, operating hours
- **Settlement processing** --- automated periodic payouts of collected funds
- **Commission tracking** --- per-transaction commission calculation and reporting
- **Transaction history** --- full merchant transaction records with export capability

## 8. Agent Banking Module

The Agent module extends financial services through a network of merchant agents who perform cash services on behalf of the platform.

**Capabilities:**

- **Cash-in operations** --- agents accept physical cash and deposit to customer accounts
- **Cash-out operations** --- agents dispense cash against customer account withdrawals
- **Dual PIN verification** --- both agent and customer PIN required for cash transactions
- **Float management** --- real-time agent working capital tracking with threshold alerts
- **Float limit enforcement** --- prevents transactions exceeding available float
- **Commission reporting** --- per-transaction commission visibility and payout tracking
- **Transaction receipts** --- printable confirmation for both agent and customer

## 9. Switching Server

The Switching Server is a network-isolated satellite service that integrates with the national payment infrastructure.

**Capabilities:**

- **ISO 8583 integration** --- standard ATM/POS protocol for legacy switch connectivity
- **ISO 20022 integration** --- modern XML-based banking standard for interbank transfers
- **Outbound transaction routing** --- route payments to acquiring banks and institutions
- **Inbound response handling** --- process authorization responses from the national switch
- **Daily reconciliation** --- automated matching of sent vs. settled transactions
- **Error handling and retries** --- resilient message delivery with configurable retry policies
- **Redundant connectivity** --- dual connections to the national switch backbone for high availability

\newpage

## 10. Terminal Manager

The Terminal Manager is a satellite service that manages the lifecycle of EFT POS terminals deployed at merchant locations.

**Capabilities:**

- **Terminal registration** --- onboard new POS devices with serial number and model tracking
- **MQTT-based communication** --- lightweight, reliable messaging to terminals in the field
- **Firmware distribution** --- push firmware updates to terminals over the air
- **Encryption key provisioning** --- secure key injection and rotation via HSM
- **Configuration management** --- push configuration changes (merchant details, parameters)
- **Status monitoring** --- real-time terminal health (online, offline, decommissioned)
- **Heartbeat tracking** --- detect and alert on unresponsive terminals

## 11. HSM Service

The HSM (Hardware Security Module) Service is a network-isolated satellite service providing cryptographic operations for payment security.

**Capabilities:**

- **Key generation and storage** --- create and manage encryption keys within the HSM
- **Session key derivation** --- derive per-transaction keys from master keys
- **PIN block encryption/decryption** --- ISO 0, ISO 3, and ISO 4 PIN block formats
- **Message authentication codes** --- HMAC-SHA256, CMAC-AES, CBC-MAC generation and verification
- **Payment tokenization** --- generate tokens for card credentials (NFC payments)
- **PKCS#11 interop** --- standard hardware abstraction for physical and virtual HSM devices
- **Key rotation** --- scheduled rotation of encryption keys per PCI-DSS requirements

## 12. Notification Service

The Notification Service delivers real-time alerts across multiple channels.

**Capabilities:**

- **Push notifications** --- Firebase Cloud Messaging (Android) and APNs (iOS)
- **SMS delivery** --- integration with SMS gateway for critical alerts
- **In-app notifications** --- persistent notification history within the mobile app
- **Template management** --- configurable notification templates per event type
- **Delivery tracking** --- monitor delivery status with retry on failure
- **PII masking** --- sensitive data masked in notification content
- **Event-driven** --- triggered automatically by domain events (transactions, auth, KYC, fraud)

\newpage

## 13. Reporting Engine

The Reporting Engine aggregates platform data into actionable business intelligence.

**Capabilities:**

- **Executive dashboard** --- real-time metrics (users, transactions, revenue, active merchants)
- **User growth reports** --- registration trends, churn analysis, daily/monthly active users
- **Merchant performance** --- transaction volumes, settlement history, commission payouts
- **Revenue reporting** --- breakdown by transaction type, currency, and tenant
- **Reconciliation reports** --- daily matching against the national switch
- **Export functionality** --- CSV, PDF, and Excel report generation (streaming download)
- **Configurable granularity** --- daily, weekly, monthly aggregation windows

## 14. Admin Portal

The Admin Portal is the back-office web application for platform operations, support, and configuration.

**Capabilities:**

- **Customer management** --- search, view, suspend, freeze, or close user accounts
- **Merchant management** --- approve, suspend, or close merchant accounts
- **KYC review workflow** --- review identity documents, approve or reject verification
- **Transaction search** --- query transactions by account, date, status, type, or amount
- **Dispute management** --- create, track, and resolve chargebacks and disputes
- **Fraud alert review** --- review flagged transactions, take action, suspend accounts
- **System configuration** --- branding, fees, limits, and commission rates per tenant
- **Reporting dashboards** --- visual analytics with drill-down capabilities
- **Audit log** --- searchable history of all administrative actions
- **Real-time updates** --- SignalR-based live data refresh

## 15. Fraud Detection Module

The Fraud Detection module provides real-time transaction monitoring and anomaly detection.

**Capabilities:**

- **Velocity checks** --- detect abnormal transaction frequency per account or merchant
- **Geolocation analysis** --- identify impossible travel patterns between transactions
- **Device fingerprinting** --- alert on transactions from unrecognized devices
- **Amount threshold monitoring** --- flag unusually large or atypical transaction amounts
- **Risk scoring** --- real-time fraud score calculation for every transaction
- **Alert generation** --- automatic fraud alerts escalated to admin review queue
- **Account suspension** --- immediate account freeze capability for confirmed fraud

\newpage

## 16. White-Label & Multi-Tenancy Module

The White-Label module enables the platform to serve multiple independent banking brands from a single deployment.

**Capabilities:**

- **Branding configuration** --- custom logos, color schemes, CSS, and support contacts per tenant
- **Fee structure management** --- configurable fees per transaction type per tenant
- **Transaction limits** --- per-transaction, daily, and monthly limits per tenant
- **Commission rates** --- customizable agent and merchant commission structures
- **Schema-per-tenant isolation** --- each tenant's data in a separate database schema
- **Tenant-scoped configuration** --- independent settings that do not affect other tenants

## 17. Mobile Application

The mobile app is the primary user interface, built with Kotlin Multiplatform for code sharing and native performance.

**Capabilities:**

- **Cross-platform** --- single shared codebase for Android (Jetpack Compose) and iOS (SwiftUI)
- **NFC Host Card Emulation** --- tap-to-pay at any contactless POS terminal
- **QR code scanning** --- camera-based EMV QR payment scanning
- **Biometric authentication** --- fingerprint and face recognition
- **Offline transaction queuing** --- queue transactions when connectivity is intermittent
- **Push notification handling** --- FCM (Android) and APNs (iOS) integration
- **White-label branding** --- dynamic theming based on tenant configuration
- **Secure gRPC communication** --- TLS-encrypted binary protocol to the backend

\newpage

# Security & Compliance

## Multi-Tenancy & Data Isolation

- **Schema-per-tenant** database isolation --- each operator's data is physically separated
- JWT claims enforce tenant context on every API call
- Row-level security policies prevent cross-tenant data access at the database layer
- Tenant secrets (API keys, encryption keys) stored encrypted at rest

## Encryption & Key Management

- **TLS 1.3** on all communication channels (gRPC, MQTT, HTTPS)
- **AES-256** encryption for sensitive data at rest (PII, credentials, documents)
- PINs hashed with **bcrypt** --- never logged or transmitted in plaintext
- All payment cryptography routed through the **Hardware Security Module**
- Automated key rotation on configurable schedules

## Authentication & Authorization

- **JWT tokens** --- 15-minute access tokens, 7-day refresh tokens
- **Device binding** --- sessions tied to device identifiers, preventing token theft
- **Lockout protection** --- 30-minute lockout after 3 failed attempts
- **Role-Based Access Control** --- admin portal roles: viewer, support, admin, super-admin
- **Comprehensive audit logging** --- all actions logged with actor, timestamp, and result

## PCI-DSS Compliance

- Payment card data is **never stored** --- tokenization only
- Switching and HSM services deployed on **isolated network segments**
- Restricted access to payment processing systems
- All card-related cryptographic operations performed within the HSM
- Complete transaction audit trail maintained

\newpage

# Development Summary

## Project Scope

The platform was delivered across **8 development sprints**, comprising **76 user stories** totaling **362 story points**.

| Sprint | Focus Area | Stories | Points |
|:------:|:-----------|:-------:|:------:|
| 1 | Foundation & scaffolding | 11 | 62 |
| 2 | KYC, authentication, device binding | 12 | 49 |
| 3 | NFC payments, QR codes, tokenization | 8 | 45 |
| 4 | P2P transfers, agent cash services, bill pay | 11 | 47 |
| 5 | Payment switch integration, reconciliation | 9 | 47 |
| 6 | Terminal management, white-label configuration | 7 | 29 |
| 7 | Admin portal, reporting, dispute management | 13 | 58 |
| 8 | Fraud detection, security hardening | 5 | 25 |
| **Total** | | **76** | **362** |

## Testing Strategy

- **Unit tests** --- domain logic, command/query handlers, validation rules
- **Integration tests** --- end-to-end API flows with TestContainers (PostgreSQL, Redis, MQTT)
- **Automated CI/CD** --- build, test, and container image generation on every commit

\newpage

# Module Summary

| Module | Type | Purpose |
|:-------|:-----|:--------|
| API Gateway | Service | Authentication, routing, rate limiting |
| Accounts | Core | Registration, auth, profiles, sessions |
| KYC | Core | Identity verification, document review |
| Payments | Core | NFC, QR code, tokenization |
| Transfers | Core | P2P, cross-border, exchange rates |
| Bill Payments | Core | Utility and service bill settlement |
| Merchants | Core | Merchant onboarding, settlement |
| Agent Banking | Core | Cash-in/out, float management |
| Fraud Detection | Core | Anomaly detection, risk scoring |
| White-Label | Core | Branding, fees, limits, multi-tenancy |
| Admin | Core | Back-office operations, disputes |
| Switching Server | Satellite | ISO 8583/20022 switch integration |
| Terminal Manager | Satellite | MQTT-based POS lifecycle |
| HSM Service | Satellite | PKCS#11 cryptographic operations |
| Notifications | Satellite | Push, SMS, in-app alerts |
| Reporting Engine | Satellite | Analytics, dashboards, exports |
| Admin Portal | Application | Blazor web back-office |
| Mobile App | Client | KMP Android/iOS application |

\newpage

# Conclusion

GoldBank delivers a **complete digital banking ecosystem** addressing the specific needs of the Southern African market:

1. **Financial Inclusion** --- zero-cost accounts and agent networks reach the unbanked population where traditional banking infrastructure does not exist.

2. **NFC Innovation** --- transforming smartphones into virtual payment cards eliminates the need for costly card issuance while leveraging existing POS terminal infrastructure.

3. **White-Label Scalability** --- a single platform deployment supports multiple independent banking brands with complete data isolation, enabling rapid market expansion.

4. **Agent Ecosystem** --- a self-sustaining network of merchant agents earns commissions for providing cash services, organically expanding the platform's physical reach.

5. **Regulatory Compliance** --- schema-per-tenant data isolation, HSM-based cryptography, PCI-DSS network segmentation, and comprehensive audit logging meet the requirements of banking regulators.

6. **On-Premise Sovereignty** --- Docker-based on-premise deployment ensures data residency compliance and operational control for regulated financial environments.

The platform is architecturally sound, security-hardened, and ready for pilot deployment targeting **500,000 users** and **10,000 merchant agents** in its first year of operation.

---

*This document is confidential and intended for authorized recipients only.*
