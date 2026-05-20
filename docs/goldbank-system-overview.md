# GoldBank Digital Banking Platform — System Overview

**Prepared for:** Executive Leadership & Board of Directors
**Date:** 24 March 2026
**Classification:** Internal — Confidential

---

## Table of Contents

1. [Executive Summary](#1-executive-summary)
2. [System Architecture](#2-system-architecture)
3. [Customer Journey](#3-customer-journey)
4. [AI-Powered Features](#4-ai-powered-features)
5. [Credit Scoring Model](#5-credit-scoring-model)
6. [Card Transaction Processing](#6-card-transaction-processing)
7. [Security and Compliance](#7-security-and-compliance)
8. [Admin Portal](#8-admin-portal)
9. [Multi-Currency and Multi-Tenant Architecture](#9-multi-currency-and-multi-tenant-architecture)
10. [Infrastructure](#10-infrastructure)

---

## 1. Executive Summary

### What GoldBank Is

GoldBank is a full-stack digital banking platform built to deliver modern financial services across Southern Africa. It provides a mobile-first banking experience covering account management, payments, transfers, lending, bill payments, merchant services, and agent banking — all from a single integrated platform.

### Key Differentiators

- **On-Premise AI:** All artificial intelligence — identity verification, document scanning, fraud detection, and a conversational banking assistant — runs entirely within the bank's own infrastructure using an AI vision-language model. No customer data ever leaves the bank's network, meeting POTRAZ and Reserve Bank of Zimbabwe (RBZ) data localization requirements while eliminating recurring cloud API costs.

- **Integrated EFT Switch (SynergySwitch):** A built-in electronic funds transfer switch connects POS terminals, ATMs, and national payment networks (Zimswitch) directly to the core banking system. This eliminates dependency on third-party switching providers, reduces per-transaction costs, and gives the bank full control over transaction routing and settlement.

- **Dual-Currency Architecture:** Every customer receives both a ZWG (Zimbabwe Gold) and USD account upon registration, with virtual card PANs generated automatically for each — reflecting the economic reality of Zimbabwe's dual-currency environment.

- **Multi-Tenant White-Label Capability:** The platform can serve multiple banking institutions from a single deployment, each with isolated data, customizable branding, fee structures, and transaction limits. This opens a path to licensing the platform as a banking-as-a-service offering.

- **Comprehensive Tariff Engine:** A unified engine calculates customer fees, merchant/agent commissions, and statutory IMTT (Intermediated Money Transfer Tax at 2%) across all transaction types, ensuring regulatory compliance and transparent revenue attribution.

### Target Market

Zimbabwe and the broader Southern African region. The platform is designed for markets where:
- Credit bureau coverage is limited, requiring behavioral credit scoring
- Dual-currency operations are necessary
- Agent networks serve as the bridge between digital and cash economies
- Regulatory requirements mandate on-premise data processing
- Mobile-first adoption outpaces traditional branch banking

---

## 2. System Architecture

### High-Level Component View

```
    CUSTOMERS                          MERCHANTS / AGENTS
        |                                      |
   Mobile App                           POS Terminals / ATMs
  (Android/Kotlin)                             |
        |                                      |
        | gRPC                                 | ISO 20022 / ISO 8583
        |                                      |
  ------+--------------------------------------+------
        |                                      |
        v                                      v
  +-----------+                       +------------------+
  |  GoldBank  |<--- gRPC ----------->|  SynergySwitch   |
  |  Gateway  |                      |  (EFT Switch)    |
  |  :5000    |                      |  :3333 / :8080   |
  +-----+-----+                      +------------------+
        |
        |   Internal Module Communication
        |
  +-----+------------------------------------------+
  |                  Core Banking                   |
  |                                                 |
  |  Accounts | Payments | Transfers | Loans        |
  |  BillPay  | Merchants | Agents | KYC            |
  |  FraudDetection | CardTransactions | AI/Vision  |
  |  Admin    | Notifications | Reporting           |
  +-----+-----+------+------+-----+----------------+
        |           |        |       |
        v           v        v       v
  +---------+  +-------+  +------+  +-----------+
  |Postgres |  | Redis |  |Ollama|  |  ArcFace  |
  |   18    |  | 7     |  |AI    |  |           |
  | (Data)  |  |(Cache)|  |Model |  |(Face AI)  |
  +---------+  +-------+  +------+  +-----------+

  Monitoring: Prometheus + Grafana + Elasticsearch + Kibana
```

### How Components Communicate

- **Mobile App to Gateway:** All communication uses gRPC, a high-performance binary protocol that is significantly faster and more bandwidth-efficient than traditional REST APIs — critical for users in areas with limited connectivity.

- **SynergySwitch Integration:** The switch accepts transactions from POS terminals and the national payment network via two industry-standard protocols (ISO 8583 over TCP for legacy systems, ISO 20022 over gRPC for modern terminals), then translates them into calls against the core banking system's CardTransactionService.

- **Multi-Tenant Isolation:** Each tenant (banking institution) operates within its own isolated database schema. This means one institution's customer data is completely separated from another's at the database level, not just the application level.

- **Containerized Deployment:** All services run as isolated containers using Podman (an enterprise-grade container runtime), making deployments predictable, scalable, and independently upgradable.

### Service Inventory

| Service | Purpose | Resources |
|---------|---------|-----------|
| **Gateway** | Core banking API server — 14 gRPC service endpoints | 512 MB |
| **Admin Portal** | Back-office web application (Blazor Server) | 256 MB |
| **SynergySwitch** | EFT payment switch with BIN routing | 512 MB |
| **Terminal Manager** | POS terminal lifecycle management via MQTT | 256 MB |
| **HSM** | Hardware Security Module emulator — PIN encryption, MAC generation | 256 MB |
| **Notifications** | Push notification delivery service | 256 MB |
| **Ollama (AI)** | On-premise AI inference engine  | 12 GB |
| **PostgreSQL 18** | Primary relational database | 512 MB |
| **Redis 7** | In-memory cache for sessions, OTPs, rate limiting | 256 MB |
| **Prometheus** | Metrics collection and alerting | 256 MB |
| **Grafana** | Monitoring dashboards and visualization | 256 MB |
| **Elasticsearch** | Structured log storage and search | 1 GB |
| **Kibana** | Log analysis dashboard | 512 MB |

---

## 3. Customer Journey

### 3.1 Registration

The registration process is designed to get customers onboarded in under two minutes:

1. **Phone Number Entry:** The customer enters their mobile number (E.164 format, validated for Southern African country codes).

2. **OTP Verification:** A 6-digit one-time password is sent via SMS, valid for 5 minutes. The OTP is stored securely in Redis and never logged.

3. **PIN Creation:** Upon OTP verification, the system automatically creates two accounts — one in ZWG and one in USD — each with a virtual card PAN (BIN prefix as defined). The customer then sets a 4-digit PIN that is hashed using BCrypt before storage. A JWT authentication token is issued.

4. **Profile Information:** The customer provides their name, date of birth, national ID number, and email address.

5. **Outcome:** The customer now has two fully provisioned accounts with KYC Level 0 (pending verification), initial daily limits of $1,000 and monthly limits of $5,000.

### 3.2 KYC Verification (Identity Verification)

KYC (Know Your Customer) verification is automated using on-premise AI, with human oversight for edge cases:

1. **ID Document Upload:** The customer photographs their Zimbabwe national ID, biometric card, or passport. The AI extracts all fields: full name, ID number, date of birth, nationality, gender, expiry date, and document type.

2. **Selfie Capture:** The customer takes a live selfie using the phone's front camera.

3. **AI Face Matching:** The Face AI neural network generates mathematical representations (embeddings) of both the selfie face and the photo on the ID document, then compares them using cosine similarity. This runs entirely on-premise using the Face AI runtime.

4. **Automated Decision:**

   | Face Match Score | ID Field Match | Decision |
   |-----------------|---------------|----------|
   | Above 60% | All fields match | **Auto-Approved** — KYC Level 2 |
   | 40% - 60% | Partial match | **Manual Review** — queued for KYC Officer |
   | Below 40% | Name mismatch | **Rejected** with reason provided |

5. **Proof of Address (KYC Level 3):** Customers can submit a utility bill, bank statement, or lease agreement. The AI extracts the name, address, and document date, verifies the name matches, and confirms the document is recent.

### 3.3 Home Dashboard

After login, customers see:
- **Account Balances:** ZWG and USD accounts with available balances, displayed with the last 4 digits of their virtual card PAN
- **Daily Limit Usage:** Visual indicator of daily spending against configured limits
- **AI Spending Insights:** 2-3 natural language insights generated by the AI analyzing recent transaction patterns (e.g., "Your grocery spending is 15% higher than last month")
- **Quick Actions:** One-tap access to Send Money, Pay Bill, Scan QR, Deposit Cheque
- **Recent Transactions:** Scrollable list with counterparty names, amounts, and status
- **Notification Badge:** Unread count for fraud alerts, dispute updates, and KYC status changes

### 3.4 Payments

**NFC Tap-to-Pay:**
The customer taps their phone on a POS terminal. The system verifies the account, looks up the merchant, calculates the tariff (0.5% customer fee + 2% IMTT tax), checks the balance, and processes the payment in real time. For high-value transactions, a PIN prompt appears. The merchant receives the payment amount minus their discount rate commission (1.5%).

**QR Code Payments:**
Merchants generate a QR code containing their details and the payment amount. The customer scans it with their camera, verifies the details, enters their PIN, and confirms. QR codes expire after a configurable time-to-live period to prevent replay attacks. Customer fee is 0.3% + 2% IMTT.

**Bill Payments:**
Customers select from a list of registered bill providers (electricity, water, airtime, internet, insurance, council rates), enter their billing reference and amount, and confirm with their PIN. For prepaid utilities like electricity and airtime, a token is generated and displayed. The fee is the greater of a $2.00 flat fee or 0.5% of the amount. Customers can save billers as favorites for quick repeat payments.

**AI Bill Scanning:**
Instead of manually entering bill details, customers can photograph a physical bill. The AI extracts the provider name, account number, amount due, due date, and reference — automatically filling the payment form. The system even attempts to match the extracted provider to a registered bill provider in the system.

### 3.5 Transfers

**Person-to-Person (Domestic):**
Send money to any GoldBank customer by phone number. The system verifies both parties, validates the sender's PIN, calculates a 1% fee, debits the sender (amount + fee), and instantly credits the recipient. Both parties receive transaction records and notifications.

**Cross-Border:**
Send money to recipients in other countries across 11 SADC corridors. The system obtains the current exchange rate, calculates a 2.5% fee, debits the sender, and initiates the outbound transfer. Estimated delivery is 1 business day for neighboring SADC countries (Zambia, Mozambique, Botswana, South Africa, and others) or 3 business days for other corridors.

### 3.6 Loans

The loan process is fully automated with AI-assisted verification:

1. **Eligibility Pre-Check (Optional):** The customer enters a desired amount, tenure, and purpose. The AI analyzes their account history and provides a likelihood assessment (high / medium / low), an estimated interest rate range, and a natural language assessment — before they formally apply.

2. **Application:** The customer specifies the loan amount, tenure (3, 6, 12, 18, or 24 months), and purpose, then confirms with their PIN.

3. **AI Payslip Verification (Optional):** The customer can photograph their payslip. The AI extracts employer name, employee name, gross salary, net salary, and pay period. The system compares the extracted income against any declared income, flags variances above 10%, and verifies the name matches.

4. **Credit Scoring:** The system calculates a behavioral credit score from 0 to 1,000 (detailed in Section 5).

5. **Auto-Decision:** Scores of 500 or above are auto-approved. The system calculates the interest rate, generates a full amortization schedule, and disburses the funds directly into the customer's account — all within seconds.

6. **Rejection:** Scores below 500 result in rejection with a reason provided. Customers with defaulted loans are blocked from new applications.

### 3.7 Disputes

When a customer identifies a problem with a transaction, they file a dispute through a guided 4-step wizard:

1. **Transaction Context:** The disputed transaction details are auto-filled and shown as read-only.
2. **Describe the Issue:** Free-text description of the problem (up to 2,000 characters).
3. **Attach Evidence:** Optional photograph of a receipt, screenshot, or other evidence (max 5 MB).
4. **AI Triage Result:** The AI automatically classifies the dispute (unauthorized transaction, duplicate charge, wrong amount, service not received, ATM failed dispensing, or card fraud), assigns a priority level (high / medium / low), routes it to the appropriate team, and provides an estimated resolution timeframe.

Customers can track their disputes through a dedicated list with filter tabs (All / Open / Investigating / Resolved) and view full details including the AI classification, assigned team, and timeline.

### 3.8 Fraud Alerts

The fraud detection engine continuously evaluates transactions and generates alerts (see Section 7 for details). When flagged:

1. The customer receives a push notification.
2. The fraud alert screen shows an AI-generated plain-language explanation of why the transaction was flagged, the risk level, and the triggered rules.
3. Two clear action buttons: **"This Was Me"** (confirms the transaction is legitimate) or **"Report Fraud"** (freezes the activity and automatically creates a dispute).

### 3.9 Agent Banking (Cash-In / Cash-Out)

Agents serve as the bridge between digital accounts and physical cash:

- **Cash-In:** An agent accepts cash from a customer and credits their digital account. The customer pays a 1% fee + 2% IMTT. The agent earns a 1.5% commission (1.0% for transactions above $10,000).
- **Cash-Out:** A customer withdraws cash from an agent. The customer pays a 1.5% fee + 2% IMTT. The agent earns a 2.0% commission (1.5% above $10,000).
- **Float Management:** Agents monitor their float balance and available float in real time.
- **Commission Reporting:** Agents access detailed commission reports broken down by transaction type and date range.

---

## 4. AI-Powered Features

All AI features run on-premise using two models:

| Model | Purpose | Deployment |
|-------|---------|------------|
| ** Ollama** | Vision-language model for document understanding, chat, and analysis | Ollama container, 12 GB memory |
| **Face AI** | Dedicated face embedding model for biometric verification | Embedded runtime, in-process |

**Critical differentiator:** No customer data — images, documents, conversations, or financial information — ever leaves the bank's infrastructure. This satisfies Zimbabwe's data localization requirements and eliminates the $0.01-0.10 per-call costs of cloud AI services.

### 4.1 KYC Identity Verification

**What it does:** Compares a customer's selfie against the photo on their ID document, and extracts all text fields from the ID.

**How it works:** ArcFace generates 512-dimensional face embeddings from both images, then computes cosine similarity. AI simultaneously reads the ID document and extracts structured data (name, ID number, DOB, nationality). The system performs fuzzy name matching with Levenshtein distance tolerance to handle minor OCR variations.

**Business value:** Reduces KYC processing time from days (manual review) to seconds (auto-approve), while maintaining quality through the three-tier decision system (auto-approve / manual review / reject).

### 4.2 Cheque Deposit

**What it does:** A customer photographs a cheque and the AI extracts all fields — cheque number, amount in figures, amount in words, payee, drawer, bank name, branch code, date, and currency.

**Business value:** Eliminates manual data entry for cheque deposits. The system cross-checks the amount in figures against the amount in words and flags inconsistencies.

### 4.3 Bill Scanning

**What it does:** A customer photographs a physical utility bill and the AI extracts the provider, account number, amount due, due date, reference, and currency, auto-filling the payment form.

**Business value:** Removes friction from bill payments — no more typing long account numbers. The system attempts to match the extracted provider name to a registered provider for one-tap payment.

### 4.4 Receipt Scanning

**What it does:** After making a purchase, a customer can photograph the receipt. The AI extracts the merchant name, date, total amount, currency, category (groceries, fuel, dining, electronics, clothing), and individual line items.

**Business value:** Enriches transaction records with merchant and category data, enabling more granular spending insights and better financial management tools.

### 4.5 Banking Assistant

**What it does:** A conversational AI assistant accessible via a floating action button on every screen. Customers can ask questions about their accounts, transactions, or banking features in natural language.

**How it works:** The assistant is injected with the customer's account context (balances, recent transactions) and uses AI to generate responses. Responses stream token-by-token for real-time conversational feel. Conversation history is maintained for the last 5 turns. Rate limited to 20 messages per hour.

**Business value:** Reduces call center volume by enabling self-service for common inquiries. Available 24/7 with no per-interaction cost after initial deployment.

### 4.6 Spending Insights

**What it does:** Analyzes a customer's transaction history and generates 2-3 natural language insights about their spending patterns (e.g., trends, comparisons to prior periods, unusual activity).

**Business value:** Increases customer engagement with the app and helps customers make better financial decisions — a feature typically reserved for premium banking apps.

### 4.7 Loan Eligibility Assessment

**What it does:** Before formally applying, customers can get an AI-powered assessment of their likelihood of approval, estimated interest rate range, and a plain-language explanation.

**Business value:** Reduces failed applications (which waste processing resources) and improves customer experience by setting expectations. The disclaimer clearly states this is an estimate only.

### 4.8 Loan Document Verification

**What it does:** Extracts income information from a photographed payslip (employer, employee name, gross salary, net salary, pay period) and compares it against the declared income on the loan application. Flags variances and verifies the name matches.

**Business value:** Automates income verification — a traditionally manual and time-consuming step in loan processing. Flags potential misrepresentation early in the process.

### 4.9 Dispute Triage

**What it does:** When a customer files a dispute, the AI reads the description and any attached evidence, then classifies the dispute type, assigns a priority level, determines the appropriate investigation team, generates a summary, and provides a confidence score.

**Classification types:** Unauthorized transaction, duplicate charge, wrong amount, service not received, ATM failed dispensing, card fraud.

**Business value:** Eliminates the initial manual triage step, ensures consistent prioritization, and routes disputes to the correct team immediately — reducing time-to-resolution.

### 4.10 Fraud Alert Explanation

**What it does:** When the fraud detection engine flags a transaction, the AI generates a plain-language explanation of why it was flagged, making the alert understandable to both customers and fraud analysts.

**Business value:** Customers can quickly understand and respond to alerts (confirm or report), reducing false-positive investigation workload. Analysts get clear context before beginning their review.

---

## 5. Credit Scoring Model

### Why Behavioral Scoring Matters for Zimbabwe

Traditional credit scoring relies heavily on credit bureau data — payment histories across multiple lenders, credit utilization ratios, and established credit accounts. In Zimbabwe and much of Southern Africa, credit bureau coverage is limited. Many potential borrowers have no formal credit history despite being financially responsible.

GoldBank's credit scoring model addresses this gap by scoring customers based on their **observed behavior within the banking platform** — how they use their account, how actively they transact, and how they manage existing obligations. This approach:

- Includes the unbanked and underbanked population
- Rewards consistent banking behavior
- Provides real-time scoring (no stale bureau data)
- Generates revenue from a previously unserved market segment

### The Scoring Algorithm

The credit score ranges from **0 to 1,000** and is composed of five weighted factors:

```
TOTAL SCORE = Account Age + KYC Level + Transaction Activity + Loan History + Account Balance
              (max 200)     (max 200)    (max 300)             (max 200)      (max 100)
                                                                                = 1,000 max
```

#### Factor 1: Account Age (up to 200 points)

| Account Age | Points |
|-------------|--------|
| 1 year or more | 200 |
| 6 months - 1 year | 150 |
| 3 - 6 months | 100 |
| 1 - 3 months | 50 |
| Less than 1 month | 20 |

*Rationale:* Longer-standing accounts demonstrate stability and commitment to the platform.

#### Factor 2: KYC Level (up to 200 points)

| KYC Level | Points | What It Means |
|-----------|--------|---------------|
| Level 3 (full verification + proof of address) | 200 | Customer has provided comprehensive identity documentation |
| Level 2 (ID + selfie verified) | 150 | Customer identity is confirmed via AI |
| Level 1 (basic details) | 100 | Customer has provided basic personal information |
| Level 0 (pending) | 0 | Verification not yet completed |

*Rationale:* Higher KYC levels indicate a customer willing to be fully identified — a strong indicator of legitimate intent.

#### Factor 3: Transaction Activity — Last 90 Days (up to 300 points)

This factor considers both the number and value of completed transactions:

**Transaction Count (up to 150 points):**

| Completed Transactions | Points |
|----------------------|--------|
| 50 or more | 150 |
| 20 - 49 | 100 |
| 10 - 19 | 60 |
| 5 - 9 | 30 |
| Fewer than 5 | 10 |

**Transaction Volume (up to 150 points):**

| Total Volume | Points |
|-------------|--------|
| $10,000 or more | 150 |
| $5,000 - $9,999 | 100 |
| $1,000 - $4,999 | 60 |
| $500 - $999 | 30 |
| Below $500 | 10 |

*Rationale:* Active, high-volume accounts indicate a customer who uses the platform as their primary financial tool — a reliable repayment source.

#### Factor 4: Loan Repayment History (up to 200 points)

| Loan History | Points |
|-------------|--------|
| Multiple loans fully repaid (paid off) | 100 + 50 per loan paid (max 200) |
| No loan history (neutral) | 100 |
| Active loans, no defaults | 80 |
| Any defaulted loan | 0 |

*Rationale:* Past loan performance is the strongest predictor of future behavior. A single default eliminates all points in this category.

#### Factor 5: Current Account Balance (up to 100 points)

| Balance | Points |
|---------|--------|
| $5,000 or more | 100 |
| $2,000 - $4,999 | 70 |
| $500 - $1,999 | 40 |
| $100 - $499 | 20 |
| Below $100 | 5 |

*Rationale:* A maintained balance demonstrates financial health and provides a buffer for repayment.

### Score-to-Interest-Rate Mapping

| Credit Score | Rating | Annual Interest Rate |
|-------------|--------|---------------------|
| 800 - 1,000 | Excellent | **18%** |
| 650 - 799 | Good | **22%** |
| 500 - 649 | Fair | **26%** |
| 350 - 499 | Poor (rejected) | 30% (not offered) |
| 0 - 349 | Very Poor (rejected) | 36% (not offered) |

**Minimum score for approval: 500**

### Monthly Repayment Calculation

Monthly payments are calculated using the standard amortization formula:

```
Monthly Payment = P x [r(1+r)^n] / [(1+r)^n - 1]

Where:  P = Principal (loan amount)
        r = Monthly interest rate (annual rate / 12)
        n = Tenure in months (3, 6, 12, 18, or 24)
```

**Example:** A customer with a credit score of 700 (22% annual rate) borrowing $5,000 over 12 months would pay approximately $466.08 per month, with a full amortization schedule generated showing the principal and interest breakdown for each payment.

### Loan Processing Guardrails

- KYC Level 1 or higher is required to apply
- Customers with an existing defaulted loan cannot apply for a new one
- PIN verification is mandatory
- Approved loans are disbursed instantly to the customer's account
- A complete amortization schedule is generated and stored at disbursement

---

## 6. Card Transaction Processing

### Transaction Flow

Card transactions flow through the SynergySwitch payment switch before reaching the core banking system. There are two entry paths:

```
PATH A — National Network (Legacy):

  Zimswitch / National Network
        |
        | ISO 8583 over TCP
        |
        v
  +------------------+        +------------------+
  |  SynergySwitch   |------->|  GoldBank Gateway |
  |                  |  gRPC  |                  |
  |  - Parse ISO 8583|        | CardTransactions |
  |  - BIN Routing   |        |    Module        |
  |  - Conn Pool     |        |                  |
  +------------------+        +------------------+


PATH B — Modern POS Terminals:

  POS Terminal / ATM
        |
        | ISO 20022 over gRPC
        |
        v
  +------------------+        +------------------+
  |  SynergySwitch   |------->|  GoldBank Gateway |
  |                  |  gRPC  |                  |
  |  - Parse ISO20022|        | CardTransactions |
  |  - BIN Routing   |        |    Module        |
  +------------------+        +------------------+
```

Both paths converge at the same CardTransactionService endpoints. Responses flow back through the same path in reverse.

### How BIN Routing Works

Every payment card has a Bank Identification Number (BIN) — the first 6-8 digits of the card number. SynergySwitch maintains a routing table that maps BIN prefixes to gateways using longest-prefix matching:

1. A transaction arrives with a card number.
2. The switch checks its BIN routing table for the longest matching prefix.
3. The transaction is forwarded to the matched gateway.
4. If no specific BIN match is found, it routes to the default gateway (ordered by priority).

This routing table is cached in memory for sub-millisecond lookups and refreshed automatically when gateways or routes are modified. All changes are audit-logged.

### On-Us vs. Off-Us Transactions

- **On-Us:** Both the cardholder and the merchant are GoldBank customers. The system debits the cardholder's account and credits the merchant's account directly — no external settlement required. This is the most profitable transaction type.

- **Off-Us:** The cardholder is a GoldBank customer but the merchant banks elsewhere. The system debits the cardholder and credits the acquiring bank's suspense account for later interbank settlement.

### Supported Transaction Types

| Transaction Type | Description |
|-----------------|-------------|
| **Purchase** | Card payment at a POS terminal or online merchant |
| **Deposit** | Cash or cheque deposit at a terminal |
| **Balance Enquiry** | Card holder checks available and ledger balance |
| **Statement Enquiry** | Card holder requests a mini-statement of recent transactions |

### Idempotency and Reliability

Every transaction includes a STAN (System Trace Audit Number) that is checked for duplicates before processing. If a duplicate is detected (e.g., due to a network retry), the original response is returned without reprocessing the transaction — preventing double-charging.

### Connection Monitoring

The switch maintains persistent gRPC channels to the GoldBank gateway with:
- Continuous state monitoring (Ready, Connecting, Idle, TransientFailure)
- Automatic reconnection on connection drops
- Keep-alive pings every 15 seconds with 5-second timeout
- Prometheus metrics for all state transitions
- Configurable connection pools per gateway

---

## 7. Security and Compliance

### Authentication Layers

GoldBank employs multiple authentication layers:

1. **Phone + OTP:** Registration requires SMS verification of phone number ownership.
2. **PIN:** A 4-digit PIN, hashed with BCrypt, is required for all sensitive operations (login, payments, transfers, loan applications, bill payments).
3. **Biometric Authentication:** Android BiometricPrompt API supports fingerprint and face recognition as a convenience layer on top of PIN authentication. If biometrics fail 3 times, the system falls back to PIN entry.
4. **JWT Tokens:** Time-limited access tokens with refresh token rotation. Access tokens expire, and refresh tokens are device-bound.

### Session Security

- **Inactivity Timeout:** Configurable from 1 to 10 minutes (default: 3 minutes). After the timeout, the session locks and requires re-authentication.
- **Device Binding:** Each account is bound to a specific device ID. Transferring to a new device requires OTP verification and PIN confirmation.
- **Account Lockout:** After 5 consecutive failed PIN attempts, the account is temporarily locked. The remaining attempts counter is returned to the client.

### Data Protection

- **Encrypted Storage:** All sensitive data on the mobile device is stored using Android EncryptedSharedPreferences (AES-256-GCM).
- **PIN Never Stored in Clear:** PINs are hashed with BCrypt before storage. The cleartext PIN exists only in memory during verification.
- **OTP Security:** OTPs are stored in Redis with a 5-minute expiry and are never logged.
- **Phone Number Masking:** Phone numbers are masked in all log output.

### Multi-Tenant Data Isolation

Each tenant's data is isolated at the database level using PostgreSQL's schema isolation. Each tenant receives its own schema with identical table structures, ensuring:
- No cross-tenant data leakage is possible at the query level
- Each tenant can have independent configuration (fees, limits, branding)
- Database-level security policies prevent cross-schema access
- Monthly table partitioning on the transactions table ensures performance at scale

### KYC Levels and Progressive Limits

| KYC Level | Verification Required | Transaction Limits |
|-----------|----------------------|-------------------|
| Level 0 | Phone + OTP only | Minimal (receive only) |
| Level 1 | Basic profile details | $1,000/day, $5,000/month |
| Level 2 | AI-verified ID + selfie | Elevated limits |
| Level 3 | Level 2 + proof of address | Full limits |

Higher KYC levels unlock higher transaction limits and access to lending products — incentivizing customers to complete verification while maintaining regulatory compliance.

### Fraud Detection

The fraud detection engine evaluates every transaction against six rule types in real time:

| Rule | What It Detects | Threshold |
|------|----------------|-----------|
| **Unusual Amount** | Transaction exceeding 5x the customer's 30-day average | 5x average = High; 10x = Critical |
| **Velocity Breach** | Too many transactions in a short period | More than 10 per hour |
| **Geographic Anomaly** | Transaction from a different tenant/region than the account's registration | Cross-tenant mismatch |
| **Pattern Anomaly** | Same amount sent to the same recipient repeatedly | 3 or more identical transfers in 24 hours |
| **New Account Risk** | Large transactions on newly created accounts | Transaction exceeding 50% of daily limit within 24 hours of creation |
| **Failed Attempts** | Multiple failed payment attempts | More than 5 failures in 30 minutes |

Each alert is assigned a severity (Medium / High / Critical) and logged with full context. The fraud engine uses Redis for high-speed velocity tracking, ensuring zero impact on transaction processing latency.

### Audit Trail

- All admin actions are logged with timestamps and user identification
- Gateway configuration changes are audit-logged in the switch
- All KYC review decisions record the admin user, decision, and notes
- Dispute resolution records the admin, decision, and refund amount
- Fraud alert reviews record the analyst, decision, notes, and whether the account was suspended

### HSM (Hardware Security Module)

The HSM service provides cryptographic operations critical for card transaction security:
- PIN block encryption/decryption (ISO Format 0, 3, and 4)
- Session key derivation from master keys
- MAC (Message Authentication Code) generation and verification
- Payment token generation for NFC transactions

---

## 8. Admin Portal

### Role-Based Access Control

The admin portal supports seven specialized roles, each with access scoped to their operational domain:

| Role | Access Level | Responsibilities |
|------|-------------|------------------|
| **Admin** | Full access to all modules | System configuration, user management, complete oversight |
| **KYC Officer** | KYC module | Review identity documents, approve/reject/request resubmission |
| **Fraud Analyst** | Fraud module | Investigate fraud alerts, review flagged transactions, suspend accounts |
| **Customer Service** | Disputes + Accounts | Handle customer complaints, resolve disputes, manage account status |
| **Loan Officer** | Loans module | Review loan applications, verify AI document analysis results |
| **Compliance Officer** | Read-only access to all modules + reports | Monitor regulatory compliance, generate audit reports |
| **Branch Manager** | All modules within their branch scope | Branch-level oversight of all operations |

Roles are scoped by tenant (multi-tenant isolation). Super admins have a null tenant ID, giving them cross-tenant access. Branch managers are further scoped to their specific branch.

### KYC Review Workflow

1. Customer submits ID document and selfie through the mobile app.
2. AI processes the submission and makes an initial decision.
3. **Auto-approved** submissions are completed without admin involvement.
4. **Manual review** submissions appear in the KYC Officer's queue with:
   - The AI's face match score and field comparison results
   - The original selfie and ID document images
   - Extracted fields alongside the customer's profile data
5. The KYC Officer can **Approve**, **Reject**, or **Request Resubmission** with notes.

### Fraud Investigation Workflow

1. The fraud detection engine generates an alert.
2. The alert appears in the Fraud Analyst's dashboard with severity, type, description, and the flagged transaction details.
3. The analyst reviews the alert and can:
   - **Dismiss** (false positive) with notes
   - **Confirm fraud** with notes, optionally suspending the customer's account
4. All review decisions are logged for audit purposes.

### Dispute Resolution

1. Disputes arrive pre-triaged by AI with classification, priority, and assigned team.
2. Customer service agents see a queue filtered by status and priority.
3. For each dispute, agents see the AI's classification, confidence score, and recommended action.
4. Resolution options include refund (with configurable amount), rejection, or escalation.
5. SLA tracking ensures disputes are resolved within expected timeframes.

### Reporting and Analytics

The admin portal provides comprehensive reporting:

| Report | Contents |
|--------|----------|
| **Executive Dashboard** | Total users, active users, transaction count, volume, revenue, active merchants/agents/terminals, daily metrics |
| **User Growth** | Registration trends, active users, churn rate, by configurable time period |
| **Merchant Report** | Transaction volume and count per merchant, commission totals |
| **Revenue Report** | Revenue by transaction type, daily/weekly/monthly breakdowns, percentage contribution |
| **Reconciliation** | Batch-level matching between switch transactions and bank records, discrepancy identification |

Reports can be exported in configurable formats with streamed delivery for large datasets.

### Customer and Merchant Management

**Customer Management:**
- Search by phone number, name, or account ID
- View account details, KYC status, balances, and transaction history
- Administrative actions: Suspend, Activate, Close, Freeze, Unfreeze, Reset PIN

**Merchant Management:**
- Approve, Suspend, Activate, or Close merchant accounts
- Configure commission rates and settlement frequency
- View transaction history and settlement reports

### Branch and User Management

- Create and manage bank branches (name, code, address, city, phone)
- Create admin users with specific roles and branch/tenant assignments
- Deactivate users with reason tracking
- List and filter admin users by role, tenant, and active status

---

## 9. Multi-Currency and Multi-Tenant Architecture

### Dual-Currency Accounts

Zimbabwe's economy operates with two currencies — ZWG (Zimbabwe Gold) and USD. GoldBank addresses this directly:

- **Automatic dual-account creation:** When a customer verifies their phone number, the system creates two accounts — one ZWG and one USD — in a single atomic operation.
- **Virtual card PANs:** Each currency account receives its own virtual card PAN with BIN prefix as defined, enabling card-based transactions in either currency.
- **Currency-aware transactions:** All financial operations (transfers, payments, loans) are currency-specific, with balances maintained independently per currency account.
- **Cross-currency transfers:** The exchange rate service handles conversions for cross-border transfers with real-time rate lookups.

### Schema-Per-Tenant Isolation

The multi-tenant architecture uses PostgreSQL schema isolation:

```
PostgreSQL Database: goldbank
  |
  +-- public schema (shared functions, tenant registry)
  |
  +-- tenant_alpha schema
  |     +-- accounts
  |     +-- transactions (partitioned monthly)
  |     +-- loans, loan_payments
  |     +-- transfers, payments
  |     +-- bill_payments, bill_providers, saved_billers
  |     +-- merchants, card_transactions
  |     +-- fraud_alerts, fraud_rules
  |     +-- kyc_documents, disputes
  |     +-- admin_users
  |     +-- ... (full schema)
  |
  +-- tenant_beta schema
  |     +-- (identical structure, completely isolated data)
  |
  +-- tenant_gamma schema
        +-- (identical structure, completely isolated data)
```

A provisioning function (`provision_tenant_schema`) creates the complete schema for a new tenant with all tables, constraints, indexes, and monthly partitions — enabling rapid onboarding of new banking institutions.

### White-Label Capability

Each tenant can be customized through the WhiteLabel service:

| Customizable Element | Example |
|---------------------|---------|
| App name | "BankX Mobile" |
| Logo and favicon | Custom branding assets |
| Color scheme | Primary, secondary, and accent colors |
| Support contact | Tenant-specific email and phone |
| Custom CSS | Additional styling overrides |
| Fee structure | Unique fee rules per transaction type |
| Transaction limits | Per-transaction, daily, and monthly limits |
| Commission rates | Volume-based discount tiers |

This enables the bank to license the platform to other financial institutions, each presenting a fully branded experience to their customers while sharing the underlying infrastructure.

---

## 10. Infrastructure

### Containerized Architecture

All services run as Podman containers on the `synergy-net` network, with deployment profiles for selective service startup:

- **"infra" profile:** PostgreSQL and Redis only (for development and testing)
- **"core" profile:** All application services (Gateway, Admin, Switch, Terminal Manager, HSM, Notifications, Ollama)
- **"monitoring" profile:** Prometheus, Grafana, Elasticsearch, Kibana

### Monitoring Stack

```
  Application Services
        |
        | Prometheus metrics endpoints (/metrics)
        |
        v
  +-------------+       +-----------+
  | Prometheus   |------>| Grafana   |
  | (metrics)    |       | (dashboards)|
  | :9190        |       | :3100      |
  +-------------+       +-----------+

  Application Logs
        |
        | Structured JSON logs
        |
        v
  +---------------+      +-----------+
  | Elasticsearch |----->| Kibana    |
  | (log storage) |      | (search)  |
  | :9200         |      | :5601     |
  +---------------+      +-----------+
```

**Key metrics collected:**
- Transaction throughput and latency per type
- Gateway connectivity state transitions
- ISO 8583/ISO 20022 message counts and response code distribution
- Connection pool utilization
- AI inference duration and token counts
- Fraud alert generation rates
- Error rates across all services

**Alert rules** are configured in Prometheus for critical conditions such as gateway connection drops, elevated error rates, and resource exhaustion.

### Service Communication

All inter-service communication uses **gRPC** (Google Remote Procedure Call), which provides:
- Binary serialization (Protocol Buffers) — 5-10x smaller than JSON
- HTTP/2 multiplexing — multiple requests over a single TCP connection
- Bi-directional streaming — used for the chat assistant, payment notifications, and transaction history
- Strongly typed contracts — all APIs are defined in `.proto` files, preventing interface drift
- Code generation — client and server code is generated automatically from proto definitions

The platform exposes **16 gRPC service definitions** covering all functionality:

| Service | Endpoints | Purpose |
|---------|-----------|---------|
| AccountService | 17 RPCs | Registration, auth, profile, balance, transactions, disputes, fraud alerts, notifications |
| PaymentService | 6 RPCs | NFC and QR payments, tokenization, payment notifications |
| TransferService | 2 RPCs | Domestic P2P and cross-border transfers |
| LoanService | 4 RPCs | Loan application, details, listing, amortization schedule |
| BillPayService | 4 RPCs | Provider listing, bill payment, saved billers |
| AIService | 14 RPCs | KYC verification, document OCR, chat, insights, dispute triage, fraud explanation |
| KycService | 4 RPCs | Document upload, selfie upload, verification status |
| AdminService | 12 RPCs | Customer management, KYC review, disputes, fraud, user/branch management |
| MerchantService | 10 RPCs | Registration, profile, transactions, settlements, commissions |
| AgentService | 5 RPCs | Cash-in, cash-out, float management, commission reporting |
| CardTransactionService | 4 RPCs | Purchase, deposit, balance enquiry, statement enquiry |
| TerminalService | 3 RPCs | Terminal registration, status, firmware updates |
| HSMService | 7 RPCs | Key generation, PIN encryption, MAC operations, tokenization |
| ReportingService | 6 RPCs | Dashboard, growth, merchant, revenue, reconciliation, export |
| WhiteLabelService | 4 RPCs | Branding and fee configuration per tenant |

### Database Architecture

**PostgreSQL 18** serves as the primary data store with:
- Schema-per-tenant isolation
- Monthly partitioning on high-volume tables (transactions)
- Automatic partition creation (3 months ahead)
- Soft deletes across all entities (deleted_at column)
- Check constraints for status fields and transaction types
- UUID primary keys throughout

**Redis 7** provides:
- OTP storage with automatic expiry (5 minutes)
- Session token management
- Fraud detection velocity counters (1-hour and 24-hour windows)
- QR payment data with time-to-live expiry
- Rate limiting counters for the AI chat assistant
- General-purpose caching

### Scalability Considerations

The architecture is designed for horizontal scaling:
- The Gateway service is stateless and can be replicated behind a load balancer
- Redis provides shared session and cache state across gateway instances
- PostgreSQL supports read replicas for reporting workloads
- The Ollama AI service can be scaled by adding GPU-equipped nodes
- The switch maintains persistent connection pools with configurable sizing
- Monthly table partitioning ensures query performance doesn't degrade as data grows

---

*This document provides a comprehensive overview of the GoldBank platform as of March 2026. For technical implementation details, API specifications, or deployment procedures, please consult the engineering team.*
