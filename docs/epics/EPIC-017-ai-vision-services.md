# EPIC-017: AI Vision & Intelligence Services (Qwen3-VL + ArcFace)

**Created:** 2026-03-23
**Priority:** Should Have
**Status:** Not Started
**Estimated Points:** 69
**Estimated Stories:** 14

---

## Overview

Deploy on-premise AI inference services powered by **Qwen3-VL-8B** (vision-language model via Ollama) and **InsightFace/ArcFace** (dedicated face embedding model) to enable intelligent document processing, biometric verification, and conversational banking across the UniBank platform.

All inference runs locally within the UniBank infrastructure — no data leaves the bank's network. This satisfies POTRAZ and RBZ data localization requirements and eliminates recurring cloud API costs.

## Goal

Maximize the value of the on-premise Qwen3-VL deployment across multiple banking use cases:

1. **KYC Verification** — Automated selfie-to-ID face matching and ID document OCR
2. **Cheque Deposit** — Mobile cheque capture with automatic field extraction
3. **Bill Scanning** — Scan utility bills to auto-fill payment details
4. **Banking Assistant** — On-premise conversational AI for account queries and insights
5. **Receipt Categorization** — Enrich transactions with merchant/category data from receipt photos
6. **Spending Insights** — Natural language summaries of transaction patterns

## Dependencies

- EPIC-001: User Registration & KYC — KYC flow that will consume face matching + ID OCR
- EPIC-007: Bill Payments — BillPay module that receives auto-filled data from bill scanning
- Ollama container running Qwen3-VL-8B (added to docker-compose.yml)
- ArcFace model deployed as a sidecar or embedded in the gateway

## Architecture

```
  ┌─────────────────────────────────────────────────────────┐
  │                    Mobile Client                         │
  │  Selfie / ID Photo / Cheque / Bill / Receipt capture    │
  └──────────────────────┬──────────────────────────────────┘
                         │ gRPC (image bytes + metadata)
                         │
  ┌──────────────────────▼──────────────────────────────────┐
  │                 UniBank Gateway                          │
  │                                                          │
  │  ┌─────────────────────────────────────────────────┐    │
  │  │         AI / Vision Module                       │    │
  │  │                                                  │    │
  │  │  InferenceService (orchestrator)                 │    │
  │  │    ├── FaceMatchingService → ArcFace (embedded)  │    │
  │  │    ├── DocumentOcrService  → Ollama/Qwen3-VL    │    │
  │  │    ├── ChatService         → Ollama/Qwen3-VL    │    │
  │  │    └── InsightsService     → Ollama/Qwen3-VL    │    │
  │  └─────────────────────────────────────────────────┘    │
  │                                                          │
  │  Modules that consume AI services:                       │
  │    Accounts (KYC) │ Payments (Cheque) │ BillPay │ Agent  │
  └──────────────────────┬──────────────────────────────────┘
                         │
          ┌──────────────┼──────────────┐
          │              │              │
  ┌───────▼──────┐  ┌───▼────┐  ┌──────▼──────┐
  │   Ollama     │  │ArcFace │  │  PostgreSQL  │
  │  Qwen3-VL   │  │(embed) │  │  (results)   │
  │  :11434      │  │in-proc │  │              │
  └──────────────┘  └────────┘  └──────────────┘
```

### Inference Flow

**Vision requests** (KYC, cheque, bill, receipt):
1. Mobile sends image bytes via gRPC
2. Gateway validates request, stores image temporarily
3. For face matching: ArcFace generates embeddings, computes cosine similarity
4. For OCR/extraction: Qwen3-VL processes image with structured prompt, returns JSON
5. Result stored and returned to caller

**Chat/Insights requests** (banking assistant, spending insights):
1. Mobile sends text query via gRPC
2. Gateway enriches prompt with account context (balance, recent transactions)
3. Qwen3-VL generates response
4. Response returned to mobile

### Decision Engine (KYC)

```
Face score > 0.6 AND all ID fields match    → AUTO-APPROVE (KYC Level 2)
Face score 0.4-0.6 OR partial field match   → MANUAL REVIEW queue
Face score < 0.4 OR name mismatch           → REJECT with reason
```

---

## Stories

### STORY-093: AI Module Scaffolding & Ollama Integration

**Priority:** Must Have
**Points:** 8

**User Story:**
As a developer, I want an AI/Vision module with Ollama client integration, so that all AI features share a common inference infrastructure.

**Acceptance Criteria:**
- [ ] `UniBank.Core.Modules.AI` module created with standard module structure
- [ ] `OllamaClient` service that calls Ollama HTTP API (`/api/generate`, `/api/chat`)
- [ ] Vision endpoint support: send image bytes + prompt, receive structured JSON
- [ ] Health check endpoint that verifies Ollama connectivity and model availability
- [ ] Configuration: Ollama URL, model name, timeout, max tokens via `appsettings.json`
- [ ] Retry policy with exponential backoff for transient Ollama failures
- [ ] Request/response logging (without logging image bytes)
- [ ] Ollama container in docker-compose.yml with Qwen3-VL model auto-pull on startup

**Technical Notes:**
- Ollama REST API: `POST http://ollama:11434/api/chat` with `"model": "qwen3-vl"`, `"images": [base64]`
- Use `HttpClient` with `IHttpClientFactory` for connection pooling
- Model auto-pull: init container or startup script that runs `ollama pull qwen3-vl`

---

### STORY-094: ArcFace Face Embedding Service

**Priority:** Must Have
**Points:** 8

**User Story:**
As a KYC officer, I want automated face matching between selfies and ID photos, so that identity verification is fast and reliable.

**Acceptance Criteria:**
- [ ] `FaceMatchingService` using InsightFace/ArcFace ONNX model
- [ ] Face detection: locate face region in both selfie and ID photo
- [ ] Face embedding: generate 512-dimensional embedding vector per face
- [ ] Similarity scoring: cosine similarity between selfie and ID embeddings
- [ ] Configurable thresholds: auto-approve (>0.6), manual review (0.4-0.6), reject (<0.4)
- [ ] Handle edge cases: no face detected, multiple faces, low quality image
- [ ] Return structured result: `{ score: 0.82, decision: "approved", confidence: "high" }`
- [ ] ONNX Runtime inference (CPU) — no GPU dependency
- [ ] Inference time < 500ms per comparison on CPU

**Technical Notes:**
- Use `Microsoft.ML.OnnxRuntime` NuGet package
- ArcFace model: `buffalo_l` or `antelopev2` from InsightFace model zoo (~100MB)
- Pre-process: align face, resize to 112x112, normalize
- Store model in `/app/models/` volume mount

---

### STORY-095: KYC ID Document OCR

**Priority:** Must Have
**Points:** 5

**User Story:**
As a KYC officer, I want the system to read identity document details from photos, so that entered data can be automatically verified.

**Acceptance Criteria:**
- [ ] `DocumentOcrService` sends ID photo to Qwen3-VL with structured extraction prompt
- [ ] Extracts: full name, ID number, date of birth, nationality, gender, expiry date
- [ ] Returns structured JSON: `{ "full_name": "...", "id_number": "63-123456A78", ... }`
- [ ] Handles Zimbabwe national ID (old green, new biometric card) and passport
- [ ] Field validation: ID number format check (Luhn or regex), date format, name sanity
- [ ] Confidence indication per field (extracted vs uncertain)
- [ ] Comparison service: match extracted fields against user-entered profile data
- [ ] Return mismatch report: `{ "name": "match", "id_number": "match", "dob": "mismatch" }`

**Technical Notes:**
- Prompt template: "Extract the following fields from this Zimbabwe national ID card. Return valid JSON only: {schema}"
- Temperature 0 for deterministic extraction
- Validate JSON response, retry once if malformed

---

### STORY-096: KYC Orchestration & Decision Engine

**Priority:** Must Have
**Points:** 5

**User Story:**
As a compliance officer, I want the KYC process to combine face matching and ID OCR into a single automated decision, so that accounts are verified efficiently.

**Acceptance Criteria:**
- [ ] `KycVerificationService` orchestrates: face match + ID OCR + decision
- [ ] Input: selfie image, ID image, user profile data
- [ ] Runs ArcFace face match and Qwen3-VL OCR in parallel
- [ ] Decision matrix: face score + field match count → approve/review/reject
- [ ] Stores verification result in `kyc_verifications` table with audit trail
- [ ] On auto-approve: updates account status to `active`, KYC level to 2
- [ ] On manual review: creates review queue entry for back-office
- [ ] On reject: returns reason to user, allows retry with new photos
- [ ] gRPC endpoint: `VerifyIdentity(selfie, id_photo, account_id) → KycResult`
- [ ] Mobile integration: update existing KYC selfie/ID flow to call new endpoint

**Dependencies:** STORY-093, STORY-094, STORY-095

---

### STORY-097: Mobile Cheque Deposit

**Priority:** Should Have
**Points:** 8

**User Story:**
As a customer, I want to deposit a cheque by photographing it with my phone, so that I don't need to visit a branch.

**Acceptance Criteria:**
- [ ] Mobile: new "Deposit Cheque" option on home screen
- [ ] CameraK capture: front of cheque (mandatory), back of cheque (optional)
- [ ] Image quality check: blur detection, minimum resolution, cheque boundary detection
- [ ] Qwen3-VL extraction: cheque number, amount (words + figures), payee name, drawer name, bank name, branch code, date
- [ ] Confirmation screen: show extracted fields, allow user to edit/correct
- [ ] Amount cross-validation: words amount vs figures amount must match
- [ ] Backend: `DepositCheque` gRPC endpoint in Payments module
- [ ] Creates pending deposit transaction (held for clearing — typically 3 business days)
- [ ] Stores cheque images with transaction for audit
- [ ] Duplicate detection: reject if same cheque number + bank already deposited

**Technical Notes:**
- Prompt: "Extract all fields from this cheque image. Return JSON: {cheque_number, amount_figures, amount_words, payee, drawer, bank, branch_code, date}"
- Zimbabwe cheques: ZWG and USD denominations, various bank formats
- Clearing period configurable per currency

---

### STORY-098: Bill Scanning & Auto-Fill

**Priority:** Should Have
**Points:** 5

**User Story:**
As a customer, I want to scan a utility bill and have payment details auto-filled, so that bill payment is faster and error-free.

**Acceptance Criteria:**
- [ ] Mobile: "Scan Bill" button on BillPay screen opens CameraK
- [ ] Qwen3-VL extraction: provider name, account number, amount due, due date, reference number
- [ ] Provider matching: map extracted provider to known billers (ZESA, ZETDC, TelOne, NetOne, Nyaradzo, ZBC, City of Harare, etc.)
- [ ] Auto-fill BillPay form fields with extracted data
- [ ] User confirms/edits before submitting payment
- [ ] Handles both printed bills and screen captures of e-bills
- [ ] Error handling: unrecognized provider → manual entry fallback

**Technical Notes:**
- Prompt: "Extract billing details from this utility bill. Return JSON: {provider, account_number, amount_due, due_date, reference}"
- Provider fuzzy matching against biller registry

---

### STORY-099: On-Premise Banking Assistant

**Priority:** Should Have
**Points:** 8

**User Story:**
As a customer, I want to ask my banking app questions in natural language, so that I can get quick answers about my accounts and transactions.

**Acceptance Criteria:**
- [ ] Mobile: wire existing AgentViewModel to new `ChatService` gRPC endpoint
- [ ] Backend `ChatService`: enriches user prompt with account context before sending to Qwen3-VL
- [ ] Context injection: current balances, last 10 transactions, active loans, pending bills
- [ ] Supported queries: balance enquiry, transaction search, spending summary, loan status, payment help
- [ ] Guardrails: refuse to discuss non-banking topics, never reveal internal system details
- [ ] Conversation history: maintain last 5 turns per session for context continuity
- [ ] Response streaming: stream tokens back to mobile for responsive UX
- [ ] Fallback: if Ollama unavailable, return "AI assistant temporarily unavailable"
- [ ] Rate limit: max 20 queries per user per hour

**Technical Notes:**
- System prompt: "You are UniBank's banking assistant. You help customers with account enquiries, transaction history, and banking guidance. Be concise, professional, and helpful. Only discuss banking topics. Never reveal system internals, API details, or other customers' data."
- Use gRPC server streaming for token-by-token delivery
- Context window: system prompt + account summary + conversation history + user query

---

### STORY-100: Receipt Scanning & Transaction Enrichment

**Priority:** Could Have
**Points:** 3

**User Story:**
As a customer, I want to photograph a receipt and link it to a transaction, so that my transaction history has detailed merchant and category information.

**Acceptance Criteria:**
- [ ] Mobile: "Attach Receipt" option on transaction detail screen
- [ ] CameraK capture of receipt
- [ ] Qwen3-VL extraction: merchant name, date, total amount, item categories
- [ ] Match to existing transaction by amount + date proximity
- [ ] Enrich transaction record: merchant name, category (groceries, fuel, dining, etc.)
- [ ] Store receipt image linked to transaction
- [ ] Viewable in transaction history

---

### STORY-101: Spending Insights Generation

**Priority:** Could Have
**Points:** 3

**User Story:**
As a customer, I want AI-generated spending insights on my home screen, so that I understand my financial patterns.

**Acceptance Criteria:**
- [ ] Backend: `InsightsService` runs weekly per account
- [ ] Analyzes last 30 days of transactions
- [ ] Qwen3-VL generates 2-3 natural language insights per account
- [ ] Examples: "Your fuel spending increased 35% this month", "You have 3 recurring subscriptions totaling $45/month"
- [ ] Insights stored in database, refreshed weekly
- [ ] Mobile: insights card on home screen below account balance
- [ ] Dismissable per insight

---

### STORY-102: Proof of Address Verification (KYC Level 3)

**Priority:** Could Have
**Points:** 2

**User Story:**
As a compliance officer, I want customers to upload proof of address documents for enhanced KYC, so that high-value accounts meet regulatory requirements.

**Acceptance Criteria:**
- [ ] Mobile: "Upload Proof of Address" in KYC/profile section
- [ ] Accepts: utility bill, bank statement, lease agreement
- [ ] Qwen3-VL extraction: name, physical address, document date
- [ ] Name matching against profile (fuzzy match for slight variations)
- [ ] Address stored in profile, document date must be within 3 months
- [ ] On match: upgrade KYC to level 3, increase daily/monthly limits
- [ ] Audit trail: store document image + extraction result

---

### STORY-103: Loan Document Verification

**Priority:** Should Have
**Points:** 5

**User Story:**
As a loan officer, I want the system to automatically verify supporting documents submitted with loan applications, so that loan processing is faster and fraud is reduced.

**Acceptance Criteria:**
- [ ] Mobile: "Upload Supporting Documents" step in loan application flow
- [ ] Accepts: payslip, bank statement, employment letter, proof of business (for SME loans)
- [ ] Qwen3-VL extraction from payslip: employer name, employee name, gross salary, net salary, pay period, deductions
- [ ] Qwen3-VL extraction from bank statement: account holder name, bank name, statement period, average balance, total credits
- [ ] Cross-validation: extracted salary vs declared income on loan application (flag if >10% variance)
- [ ] Cross-validation: extracted name vs account holder name
- [ ] Debt-to-income ratio calculation using extracted salary and existing loan obligations
- [ ] Store extracted data + document images with loan application for audit
- [ ] Flag discrepancies for manual review by loan officer

**Technical Notes:**
- Prompt: "Extract financial details from this payslip. Return JSON: {employer, employee_name, gross_salary, net_salary, currency, pay_period, deductions: [{name, amount}]}"
- For bank statements: may need multi-page processing — extract summary page first

**Dependencies:** STORY-093

---

### STORY-104: Loan Eligibility Pre-Check

**Priority:** Should Have
**Points:** 3

**User Story:**
As a customer, I want to get an instant AI-powered pre-assessment of my loan eligibility before applying, so that I know my chances and don't waste time on applications that will be rejected.

**Acceptance Criteria:**
- [ ] Mobile: "Check Eligibility" button on loan screen (before full application)
- [ ] Collects: desired amount, tenure, purpose (lightweight form)
- [ ] Backend gathers context: account age, average balance (30/60/90 days), transaction frequency, existing loans, KYC level, repayment history
- [ ] Qwen3-VL generates natural language assessment: likelihood (high/medium/low), estimated rate range, suggested adjustments
- [ ] Example response: "Based on your 6-month account history with an average balance of ZWG 12,000 and no existing loans, you have a HIGH chance of approval for ZWG 5,000 over 12 months at approximately 18-22% per annum. Consider applying for ZWG 8,000 — your income pattern supports it."
- [ ] Disclaimer: "This is an estimate only. Actual approval subject to full assessment."
- [ ] "Apply Now" button that pre-fills the full loan application with the checked parameters
- [ ] Rate limit: 3 pre-checks per user per day

**Technical Notes:**
- System prompt includes bank's lending criteria (min account age, min balance, max DTI ratio, rate tiers)
- No hard credit decision — advisory only, clearly labeled as estimate

**Dependencies:** STORY-093

---

### STORY-105: Transaction Dispute Triage

**Priority:** Should Have
**Points:** 3

**User Story:**
As a customer, I want to raise a transaction dispute through the app and have it automatically classified, so that my dispute reaches the right team faster.

**Acceptance Criteria:**
- [ ] Mobile: "Dispute" button on transaction detail screen
- [ ] User describes the issue in free text (+ optional photo of receipt/evidence)
- [ ] Qwen3-VL classifies dispute type: unauthorized_transaction, duplicate_charge, wrong_amount, service_not_received, atm_failed_dispensing, card_fraud
- [ ] Assigns priority: high (fraud/unauthorized), medium (wrong amount/duplicate), low (service dispute)
- [ ] Generates structured dispute record: type, priority, summary, recommended action
- [ ] If photo attached: extract relevant details (merchant name, amount, date) to corroborate claim
- [ ] Auto-routes to correct team: fraud team (high), operations (medium), customer service (low)
- [ ] Returns to user: dispute reference number, expected resolution timeline, next steps
- [ ] Stores dispute with classification + original user text for audit

**Technical Notes:**
- System prompt includes dispute classification taxonomy and bank's SLA timelines per type
- Classification confidence: if < 70%, flag for manual triage instead of auto-routing

**Dependencies:** STORY-093

---

### STORY-106: Fraud Alert Explanation

**Priority:** Should Have
**Points:** 3

**User Story:**
As a customer, I want clear, human-readable explanations when a transaction is flagged as suspicious, so that I understand why my transaction was blocked and what to do next.

**Acceptance Criteria:**
- [ ] When fraud detection flags a transaction, `FraudExplanationService` generates a plain-language explanation
- [ ] Input: transaction details, fraud rule(s) triggered, risk score, account history context
- [ ] Qwen3-VL generates explanation: what happened, why it was flagged, what the customer should do
- [ ] Example: "A ZWG 15,000 purchase at Electronics Hub was blocked because it's 5x your typical transaction amount and from a merchant you haven't used before. If this was you, tap 'Confirm Transaction' to approve it. If not, tap 'Report Fraud' and we'll freeze your card immediately."
- [ ] Push notification with summary, full explanation in app notification centre
- [ ] Action buttons: "Confirm Transaction" (unblock), "Report Fraud" (freeze card + open dispute)
- [ ] Explanation cached — same alert doesn't re-query Qwen3-VL
- [ ] Tone: reassuring, clear, actionable — not alarming

**Technical Notes:**
- System prompt: "You are a fraud alert communicator for UniBank. Explain why a transaction was flagged in simple, non-technical language. Always provide clear next steps. Be reassuring but take the situation seriously."
- Triggered asynchronously when fraud rule fires — not in the transaction path

**Dependencies:** STORY-093

---

## Story Point Summary

| Story | Title | Points | Priority |
|-------|-------|--------|----------|
| STORY-093 | AI Module Scaffolding & Ollama Integration | 8 | Must Have |
| STORY-094 | ArcFace Face Embedding Service | 8 | Must Have |
| STORY-095 | KYC ID Document OCR | 5 | Must Have |
| STORY-096 | KYC Orchestration & Decision Engine | 5 | Must Have |
| STORY-097 | Mobile Cheque Deposit | 8 | Should Have |
| STORY-098 | Bill Scanning & Auto-Fill | 5 | Should Have |
| STORY-099 | On-Premise Banking Assistant | 8 | Should Have |
| STORY-100 | Receipt Scanning & Transaction Enrichment | 3 | Could Have |
| STORY-101 | Spending Insights Generation | 3 | Could Have |
| STORY-102 | Proof of Address Verification (KYC L3) | 2 | Could Have |
| STORY-103 | Loan Document Verification | 5 | Should Have |
| STORY-104 | Loan Eligibility Pre-Check | 3 | Should Have |
| STORY-105 | Transaction Dispute Triage | 3 | Should Have |
| STORY-106 | Fraud Alert Explanation | 3 | Should Have |
| **Total** | | **69** | |

## Sprint Allocation

**Sprint 11 (Must Have — KYC + Infrastructure):** STORY-093, 094, 095, 096 = 26 points
**Sprint 12 (Should Have — Banking Features):** STORY-097, 098, 099, 104 = 24 points
**Sprint 13 (Should Have — Loans & Safety):** STORY-103, 105, 106 = 11 points
**Sprint 14 (Could Have — Enrichment):** STORY-100, 101, 102 = 8 points

## Risks

**High:**
- Qwen3-VL OCR accuracy on low-quality ID photos — mitigation: allow manual review fallback, prompt engineering
- ArcFace accuracy on Zimbabwe national IDs (limited training data for local ID formats) — mitigation: configurable threshold, manual review queue

**Medium:**
- CPU inference latency (~15-20s per Qwen3-VL call) — mitigation: async processing with notification, user sees "Processing..." spinner
- Ollama memory pressure (12GB) alongside other services — mitigation: memory limit in compose, model offloading when idle

**Low:**
- Model hallucination on OCR fields — mitigation: field validation (regex, Luhn check), temperature 0, structured JSON output
