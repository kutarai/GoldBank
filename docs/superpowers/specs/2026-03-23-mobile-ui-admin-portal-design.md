# Design Spec: Mobile UI Modernization (EPIC-018) & Admin Portal Enhancement (EPIC-019)

**Date:** 2026-03-23
**Status:** Draft
**Author:** Brainstorming session

---

## Context

The UniBank mobile app has ~30 screens covering 8 of 13 server modules. Five modules lack UI: AI Vision/Intelligence, Fraud Detection, Admin, CardTransactions, and advanced WhiteLabel. The admin portal (`unibank-admin`) exists as a Razor Pages container with basic admin functionality delivered in Sprint 7 (STORY-055 through STORY-067), including admin auth, KYC review, dispute management, and reporting RPCs. EPIC-019 enhances this with a Blazor Server frontend for real-time dashboards, granular roles, and multi-tenant user management.

This spec covers two EPICs:
- **EPIC-018** — Customer-facing mobile app updates (Jetpack Compose / Kotlin)
- **EPIC-019** — Admin portal enhancement — Blazor Server upgrade over existing Sprint 7 Razor Pages (reuses existing AdminService gRPC endpoints, adds new customer-facing RPCs)

## Design Decisions

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Admin screens location | Separate web portal (EPIC-019) | Different user base, tech stack, deployment |
| Session security | Biometric + PIN + inactivity timeout (3 min) | Banking standard, Android BiometricPrompt API |
| AI assistant placement | Floating action button (FAB) on all screens | Always accessible without consuming nav slot |
| Dispute filing flow | Multi-step wizard (4 steps) | Ensures sufficient info for AI triage, builds trust |
| Admin portal frontend | Blazor Server | Real-time dashboards, stays in .NET ecosystem |
| Admin role model | 7 granular roles | Maps to modules: Admin, KYC Officer, Fraud Analyst, Customer Service, Loan Officer, Compliance Officer, Branch Manager |
| Admin tenant model | Multi-tenant + multi-branch | Matches existing server architecture (schema-per-tenant) |

---

## EPIC-018: Mobile UI Modernization

### Overview

17 stories, 87 points, 4 sprints (15-18). All customer-facing. Adds security hardening, AI banking assistant, document scanning, dispute filing, fraud alerts, and loan intelligence.

### New gRPC Client

**AiGrpcClient** — single Kotlin client wrapping all 14 RPCs from `AIService.proto`:
- Unary calls: VerifyIdentity, ExtractDocumentFields, ExtractChequeFields, ExtractBillFields, ExtractReceiptFields, GetSpendingInsights, CheckLoanEligibility, VerifyLoanDocuments, TriageDispute, ExplainFraudAlert, VerifyProofOfAddress, GetModelStatus
- Server streaming: Chat (token-by-token for assistant)

Registered in `androidDataModule` alongside existing 8 gRPC clients.

### New ViewModels (5)

| ViewModel | Screens | Responsibilities |
|-----------|---------|-----------------|
| SecurityViewModel | BiometricPromptScreen, SessionLockScreen, SecuritySettingsScreen | Biometric auth, inactivity timer, PIN re-entry |
| ChatViewModel | ChatScreen (via FAB) | Conversation state, streaming tokens, account context |
| DisputeViewModel | DisputeWizardScreen, DisputeListScreen, DisputeDetailScreen | Dispute filing wizard state, list/filter, detail |
| FraudAlertViewModel | FraudAlertListScreen, FraudAlertDetailScreen | Alert list, AI explanation display, confirm/report actions |
| DocumentScanViewModel | ChequeScanScreen, BillScanScreen | Camera capture, OCR extraction, field confirmation |

### Sprint 15: Security Foundation + AI Client (23 pts)

**STORY-107: Biometric Unlock + Inactivity Lock (5 pts)**
- Note: Sprint 2 delivered PIN auth and SessionManager with PinRequired state. This story adds BiometricPrompt on top.
- BiometricPromptScreen: Android BiometricPrompt API, fingerprint/face, shown on app launch
- SessionLockScreen: re-auth after inactivity timeout (reuses existing SessionManager.SessionState)
- SecurityViewModel: wraps BiometricPrompt, manages inactivity timer
- PIN fallback when biometric unavailable or fails 3 times

**STORY-108: Security Settings (3 pts)**
- SecuritySettingsScreen: toggle biometric on/off, change PIN, set timeout (1/3/5/10 min)
- Update ProfileScreen with "Security" navigation link
- Persist settings via PreferencesManager

**STORY-109: AiGrpcClient + Proto Integration (5 pts)**
- Generate Kotlin protobuf classes from ai_service.proto
- AiGrpcClient wrapping all 14 RPCs with error handling
- Streaming support for Chat RPC (Flow<ChatToken>)
- Register in androidDataModule DI
- Add to shared/build.gradle.kts proto generation

**STORY-110: AI Banking Assistant — ChatFAB + ChatScreen (8 pts)**
- ChatFAB: floating action button, visible on all main screens via Scaffold overlay
- ChatScreen: full-screen chat UI with:
  - Message bubbles (user + assistant)
  - Streaming token display (character by character)
  - Conversation history (last 5 turns)
  - Typing indicator while streaming
  - Account context auto-injected (balance, recent txns)
- ChatViewModel: manages conversation state, calls AiGrpcClient.chat()
- Rate limit: disable send button after 20 messages/hour

**STORY-111: HomeScreen Spending Insights (2 pts)**
- Add insights card section below account balance
- Call GetSpendingInsights on home load (cached, refresh weekly)
- Display 2-3 insight strings as dismissable cards
- "Powered by AI" subtle label

### Sprint 16: KYC + Document Scanning (24 pts)

**STORY-112: Fix KYC Camera + Wire to AI VerifyIdentity (8 pts)**
- Fix camera permissions crash on SelfieScreen and DocumentUploadScreen
- Capture selfie + ID photo as byte arrays
- Send to AiGrpcClient.verifyIdentity()
- New KycVerificationResultScreen showing:
  - Face match score (visual gauge)
  - Extracted ID fields vs entered profile data
  - Field match indicators (green check / red X)
  - Overall decision: Auto-approved / Manual review / Rejected
- Update KycDashboardScreen with verification status
- On auto-approve: navigate to home with success message

**STORY-113: Proof of Address — KYC Level 3 (3 pts)**
- ProofOfAddressScreen: camera/gallery picker for utility bill or statement
- Send to AiGrpcClient.verifyProofOfAddress()
- Show extracted name + address + document date
- Display match result and new KYC level
- Link from KycDashboardScreen when KYC level is 2

**STORY-114: Cheque Scan — Mobile Deposit (8 pts)**
- ChequeScanScreen: CameraK capture of cheque front
- Send to AiGrpcClient.extractChequeFields()
- Confirmation screen: editable fields (cheque #, amount, payee, date, bank)
- Amount consistency warning if words != figures
- Submit deposit via existing payment flow
- DocumentScanViewModel manages capture → extract → confirm → submit
- Add "Deposit Cheque" quick action on HomeScreen

**STORY-115: Bill Scan + PayBillScreen Update (5 pts)**
- BillScanScreen: CameraK capture of utility bill
- Send to AiGrpcClient.extractBillFields()
- Auto-fill PayBillScreen with provider, account number, amount, due date
- Provider fuzzy matching indicator (matched vs manual entry)
- Add "Scan Bill" camera icon button on PayBillScreen

### Sprint 17: Disputes + Fraud Alerts (21 pts)

**STORY-116: Dispute Wizard — Multi-Step Filing (8 pts)**
- Entry point: "Dispute" button on TransactionDetailScreen
- 4-step wizard:
  1. Transaction context (auto-filled, read-only)
  2. Describe issue (free text, 2000 char limit)
  3. Attach evidence (optional camera/gallery, max 5MB)
  4. AI triage result: classification, priority, assigned team, reference number, expected resolution
- Send to AiGrpcClient.triageDispute()
- DisputeViewModel manages wizard state across steps
- Back navigation between steps without data loss

**STORY-117: Dispute List + Detail Screens (5 pts)**
- DisputeListScreen: list all user's disputes
  - Filter tabs: All / Open / Investigating / Resolved
  - Each item: reference, date, amount, type badge, status chip
- DisputeDetailScreen: full dispute details
  - AI classification and confidence
  - Assigned team
  - Timeline: filed → investigating → resolved
  - Resolution notes (when resolved)
- Navigation from ProfileScreen or HomeScreen quick action

**STORY-118: Fraud Alert List + Detail Screens (5 pts)**
- FraudAlertListScreen: list of fraud alerts for user
  - Each item: date, transaction description, risk level badge
  - Unread indicator
- FraudAlertDetailScreen:
  - AI-generated plain-language explanation
  - Transaction details
  - Two action buttons: "This Was Me" (confirm) / "Report Fraud" (freeze + dispute)
  - "Report Fraud" creates dispute automatically
- FraudAlertViewModel manages list state and actions
- Badge count on HomeScreen

**STORY-119: TransactionDetailScreen Updates (3 pts)**
- Add "Dispute This Transaction" button (opens DisputeWizard)
- Add "Attach Receipt" button (opens camera, OCR, enriches transaction)
- Show receipt data if previously attached (merchant, category, items)
- Show dispute status if dispute exists for transaction

### Sprint 18: Loan AI + Polish (18 pts)

**STORY-120: Loan Eligibility Pre-Check (5 pts)**
- LoanEligibilityScreen: lightweight form (amount, tenure, purpose)
- Send to AiGrpcClient.checkLoanEligibility()
- Display: likelihood gauge (high/medium/low), estimated rate range, AI assessment text
- Disclaimer: "This is an estimate only"
- "Apply Now" button pre-fills LoanApplyScreen
- Link from LoanListScreen: "Check Eligibility First"

**STORY-121: LoanApplyScreen Update — Interest + Doc Upload (5 pts)**
- Show current interest rate (fetched from server or calculated from credit score)
- Monthly repayment calculator: updates live as amount/tenure changes
- New step: "Upload Payslip" for income verification
- Send to AiGrpcClient.verifyLoanDocuments()
- Show verification result: extracted income vs declared, variance, name match
- Flag if variance > 10%

**STORY-122: Receipt Scanning on Transaction Detail (3 pts)**
- Camera capture from TransactionDetailScreen "Attach Receipt" button
- Send to AiGrpcClient.extractReceiptFields()
- Match to transaction by amount + date
- Enrich transaction display: merchant name, category tag, item list
- Stored for future reference

**STORY-123: Push Notifications for Fraud + Disputes (5 pts)**
- FCM integration for fraud alert notifications
- Notification payload: alert ID, transaction summary, risk level
- Tap notification → FraudAlertDetailScreen
- In-app notification centre accessible from HomeScreen bell icon
- Badge count for unread alerts + unresolved disputes
- Dispute status change notifications (investigating → resolved)

### Navigation Changes

```
App Launch
  → BiometricPromptScreen (or SessionLockScreen on timeout)
  → Main NavGraph
      ├── HomeScreen (+ insights card, + fraud badge, + cheque deposit action)
      │     └── ChatFAB (floating, visible on all screens)
      ├── Transactions
      │     ├── TransactionListScreen
      │     └── TransactionDetailScreen (+ Dispute btn, + Receipt btn)
      │           ├── DisputeWizardScreen (4 steps)
      │           └── ReceiptScanScreen
      ├── Payments (existing)
      ├── Transfers (existing)
      ├── BillPay
      │     ├── ProviderListScreen
      │     └── PayBillScreen (+ Scan Bill btn)
      │           └── BillScanScreen
      ├── Loans
      │     ├── LoanListScreen (+ Check Eligibility link)
      │     ├── LoanEligibilityScreen → LoanApplyScreen
      │     └── LoanApplyScreen (+ interest + doc upload)
      ├── KYC
      │     ├── KycDashboardScreen (+ verification status + L3 link)
      │     ├── SelfieScreen → KycVerificationResultScreen
      │     └── ProofOfAddressScreen
      ├── Disputes
      │     ├── DisputeListScreen
      │     └── DisputeDetailScreen
      ├── Fraud Alerts
      │     ├── FraudAlertListScreen
      │     └── FraudAlertDetailScreen
      ├── Profile
      │     ├── ProfileScreen (+ Security link)
      │     └── SecuritySettingsScreen
      └── ChequeScanScreen (from Home quick action)
```

---

## EPIC-019: Admin Portal Enhancement

### Overview

Blazor Server application within the existing `unibank-admin` container. Role-based access control with 7 roles, multi-tenant + multi-branch user management. Real-time dashboards for KYC review, fraud monitoring, and dispute resolution.

### Roles (7)

| Role | Access | Description |
|------|--------|-------------|
| **Admin** | Full access | System configuration, user management, all modules |
| **KYC Officer** | KYC module | Review KYC verifications, approve/reject, escalate |
| **Fraud Analyst** | Fraud module | Monitor alerts, investigate, block accounts |
| **Customer Service** | Disputes + accounts | Handle disputes, view customer profiles, basic account actions |
| **Loan Officer** | Loans module | Review loan applications, verify documents, approve/reject |
| **Compliance Officer** | Read-only all + reports | Audit trails, compliance reports, no write actions |
| **Branch Manager** | Branch-scoped all | Full access within their branch, staff management |

### Admin Portal Screens

**Authentication & User Management**
- Admin login (email + password + optional 2FA)
- User list (filterable by role, branch, tenant)
- Create/edit user (assign role, branch, tenant)
- Role management (view permissions per role)
- Branch management (create/edit branches per tenant)

**KYC Review Dashboard**
- Queue: verifications needing manual review (real-time via SignalR)
- Today's stats: auto-approved / manual review / rejected counts
- Review screen: side-by-side selfie vs ID photo, face match score, extracted fields, field comparisons
- Actions: Approve (activates account) / Reject (with reason, notifies customer) / Escalate to compliance

**Fraud Alert Dashboard**
- Active alerts list (real-time, sorted by risk score)
- Alert detail: transaction info, rules triggered, AI explanation, account history
- Actions: Dismiss (false positive) / Block account / Create investigation
- Investigation workflow: assign to analyst, add notes, resolution

**Dispute Management**
- Dispute queue (filterable by type, priority, team, status)
- Detail view: customer description, evidence images, AI classification, transaction details
- Actions: Assign to agent, update status, add resolution notes, notify customer
- SLA tracking: time since filed, expected resolution deadline

**Loan Review**
- Pending applications list
- Detail: applicant profile, credit score, AI doc verification results, income variance
- Actions: Approve / Reject / Request more documents

**Customer Management**
- Customer search (by name, phone, ID number, account number)
- Profile view: KYC status, account status, transaction history, disputes, loans
- Actions: freeze/unfreeze account, reset PIN, update KYC level

**Reporting**
- Daily/weekly/monthly KYC processing stats
- Fraud alert trends and false positive rate
- Dispute resolution metrics (avg time, by type)
- Loan approval/rejection rates
- Per-branch and per-tenant breakdowns

**Audit Trail**
- All admin actions logged with timestamp, user, action, target
- Filterable by user, action type, date range
- Compliance Officer read-only access

### Stories (EPIC-019)

| Story | Title | Points | Sprint |
|-------|-------|--------|--------|
| STORY-124 | Blazor Server migration + layout shell | 5 | 19 |
| STORY-125 | Admin authentication + role-based authorization | 8 | 19 |
| STORY-126 | User management CRUD (multi-tenant, multi-branch) | 8 | 19 |
| STORY-127 | Branch management | 3 | 19 |
| STORY-128 | KYC review dashboard + review workflow | 8 | 20 |
| STORY-129 | Fraud alert dashboard + investigation workflow | 8 | 20 |
| STORY-130 | Dispute management queue + resolution workflow | 5 | 20 |
| STORY-131 | Loan review dashboard | 5 | 21 |
| STORY-132 | Customer management + search | 5 | 21 |
| STORY-133 | Reporting dashboards | 5 | 21 |
| STORY-134 | Audit trail viewer | 3 | 21 |
| STORY-135 | Real-time updates via SignalR | 3 | 21 |
| **Total** | | **66** | |

### Sprint Allocation (EPIC-019)

- **Sprint 19 (24 pts):** Foundation — Blazor migration, auth, user/branch management
- **Sprint 20 (21 pts):** Core workflows — KYC review, fraud alerts, disputes
- **Sprint 21 (21 pts):** Extended — loans, customers, reporting, audit, real-time

### Architecture

```
Browser (Bank Employee)
  │
  │ HTTPS / WebSocket (SignalR)
  │
  ├── Blazor Server (unibank-admin container)
  │     ├── Pages/ (Razor components)
  │     ├── Services/ (gRPC clients to Gateway)
  │     ├── Auth/ (JWT + role claims)
  │     └── Hubs/ (SignalR for real-time)
  │
  └── UniBank Gateway (gRPC)
        ├── AdminService (existing)
        ├── AIService (KYC, fraud, disputes)
        ├── AccountService
        ├── LoanService
        └── ReportingService
```

---

## Summary

| EPIC | Stories | Points | Sprints | Tech |
|------|---------|--------|---------|------|
| **018 — Mobile UI** | 17 (STORY-107–123) | 86 | 15–18 | Kotlin / Jetpack Compose |
| **019 — Admin Portal** | 12 (STORY-124–135) | 66 | 19–21 | Blazor Server / .NET |
| **Total** | 29 | 152 | 7 sprints | |

---

## Proto Changes Required

EPIC-018 requires new customer-facing RPCs beyond `ai_service.proto`. These should be added to `account_service.proto` or a new `customer_service.proto`:

| RPC | Service | Purpose | Consuming Story |
|-----|---------|---------|-----------------|
| `ListMyDisputes` | AccountService or CustomerService | Return disputes for authenticated user, filterable by status | STORY-117 |
| `GetDisputeDetail` | AccountService or CustomerService | Return single dispute by ID (scoped to user) | STORY-117 |
| `ListMyFraudAlerts` | AccountService or CustomerService | Return fraud alerts for authenticated user | STORY-118 |
| `GetFraudAlertDetail` | AccountService or CustomerService | Return single alert with AI explanation | STORY-118 |
| `ConfirmTransaction` | AccountService | Customer confirms a flagged transaction is legitimate (unblocks) | STORY-118 |
| `ReportFraud` | AccountService | Customer reports fraud — initiates card freeze + auto-creates dispute | STORY-118 |
| `ListMyNotifications` | AccountService | Return in-app notifications (fraud alerts, dispute updates) | STORY-123 |
| `MarkNotificationRead` | AccountService | Mark notification as read | STORY-123 |

**gRPC message size**: STORY-116 allows evidence images up to 5MB. The gateway's `MaxReceiveMessageSize` is currently 16MB (sufficient). The mobile gRPC channel should also be configured to 16MB to match.

**RPC count correction**: `AIService` has 13 RPCs (not 14 as previously stated). The AiGrpcClient wraps all 13.

---

## Route Manifest (Routes.kt additions)

New routes grouped by sprint:

**Sprint 15:**
```
Routes.BiometricPrompt
Routes.SessionLock
Routes.SecuritySettings
Routes.Chat
```

**Sprint 16:**
```
Routes.KycVerificationResult(accountId: String)
Routes.ProofOfAddress
Routes.ChequeScan
Routes.BillScan
```

**Sprint 17:**
```
Routes.DisputeWizard(transactionId: String)
Routes.DisputeList
Routes.DisputeDetail(disputeId: String)
Routes.FraudAlertList
Routes.FraudAlertDetail(alertId: String)
```

**Sprint 18:**
```
Routes.LoanEligibility
Routes.Notifications
```

---

## Error & Degraded States

AI-dependent screens must handle Ollama unavailability and inference latency (~15-20s on CPU).

| Screen | Loading State | Error State | Timeout |
|--------|--------------|-------------|---------|
| ChatScreen | Typing indicator, streaming tokens | "AI assistant temporarily unavailable. Try again later." | 120s |
| KycVerificationResultScreen | "Verifying identity..." spinner | "Verification service unavailable. Your documents have been saved — we'll verify shortly." | 120s |
| ChequeScanScreen | "Reading cheque..." spinner | "Could not read cheque. Please enter details manually." with editable form | 60s |
| BillScanScreen | "Scanning bill..." spinner | "Could not read bill. Please enter details manually." with manual fallback | 60s |
| DisputeWizardScreen (step 4) | "Classifying dispute..." spinner | "Dispute filed successfully. Classification pending." (queue for async triage) | 60s |
| FraudAlertDetailScreen | Loading skeleton | "Explanation unavailable. Transaction details shown below." | 30s |
| LoanEligibilityScreen | "Assessing eligibility..." spinner | "Pre-check unavailable. You can still apply directly." with link to LoanApplyScreen | 60s |
| SpendingInsightsCard | Shimmer loading | Card hidden silently (non-critical) | 30s |
| ReceiptScanScreen | "Reading receipt..." spinner | "Could not read receipt. Please enter details manually." | 60s |

**Camera permission denied**: All camera screens (Selfie, ID, Cheque, Bill, Receipt) show a permission rationale dialog, then redirect to app settings if denied twice.

---

## Relationship Between EPIC-019 and Sprint 7 Work

Sprint 7 delivered the following admin functionality (STORY-055 through STORY-061):
- Admin authentication with basic RBAC (`AdminUser` entity, `AuthenticateAdminHandler`)
- KYC review and approval workflow (`ReviewKycHandler`)
- Dispute creation and resolution (`CreateDisputeHandler`, `ResolveDisputeHandler`)
- Customer search and account management (`SearchCustomersHandler`, `ManageAccountHandler`)
- Transaction search (`SearchTransactionsHandler`)
- System configuration (`UpdateSystemConfigHandler`)
- Audit logging (`CreateAuditLogHandler`)

**EPIC-019 reuses** all existing AdminService gRPC endpoints and handlers. It does NOT rewrite backend logic.

**EPIC-019 adds:**
- Blazor Server frontend replacing Razor Pages (richer interactivity)
- 7 granular roles (Sprint 7 had basic admin/non-admin)
- Multi-branch user assignment
- Real-time dashboard updates via SignalR
- Fraud alert investigation workflow (new — Sprint 7 only had basic alert listing)
- Loan review dashboard (new — Sprint 7 didn't cover loan admin)
- Enhanced reporting with per-branch/tenant breakdowns

**Existing gRPC endpoints consumed by EPIC-019:**
- `AdminService.AuthenticateAdmin` → STORY-125
- `AdminService.SearchCustomers`, `AdminService.ManageAccount` → STORY-132
- `AdminService.ReviewKyc` → STORY-128
- `AdminService.CreateDispute`, `AdminService.ResolveDispute` → STORY-130
- `AdminService.SearchTransactions` → STORY-129, STORY-132
- `AIService.ExplainFraudAlert` → STORY-129
- `ReportingService.*` → STORY-133
- `LoanService.ListLoans`, `LoanService.GetLoan` → STORY-131
