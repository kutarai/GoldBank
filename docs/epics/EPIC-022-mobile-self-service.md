# EPIC-022: Customer Self-Service Mobile Banking

**Priority:** Must Have
**Estimated Points:** 75
**Sprints:** 28–31
**Dependencies:** EPIC-001 (Core Accounts), existing mobile app shell, EPIC-016 (Synergy Switch) for inter-bank rails, EPIC-020a (Tariff Engine)

---

## Business Context

The mobile app today provides registration, KYC, and basic balance lookup. Customers cannot yet move money, pay bills, manage beneficiaries, or download statements without visiting a branch or calling support. Adding a complete self-service tier deflects branch traffic, improves NPS, and unlocks fee-bearing transactions (transfers, bill pay) at near-zero marginal cost.

## User Personas

- **Retail Customer** — Salaried individual using the phone for day-to-day banking.
- **Informal Trader / SME Owner** — Needs frequent transfers, EcoCash/OneMoney push, supplier payments.
- **Diaspora Sender** — Top-up flows from offshore (links to EPIC-025).

---

## Functional Requirements

### FR-001: Account Statements
- PDF + CSV statement download for any 1/3/6/12-month range, server-rendered via QuestPDF
- Inline transaction list with running balance, search, filter by direction/currency/amount
- Email statement to file-on-record address; rate-limited to 5/day per account

### FR-002: Internal Transfers (Own Accounts)
- Move funds between the customer's own accounts in different currencies; FX quote pulled from treasury rates
- Settlement is atomic via the existing ledger; receipt issued in-app
- Daily limit honoured per account-level KYC tier

### FR-003: Third-Party Transfers (Same Bank)
- Send to another UniBank customer by phone, account number, or QR
- Recipient lookup returns masked name for confirmation before debit
- Optional message/memo (max 140 chars) stored in transaction metadata

### FR-004: Inter-Bank Transfers (RTGS / Synergy Switch)
- Beneficiary by bank, branch, account number; routed through EPIC-016 switch
- Tariff applied via EPIC-020a engine, breakdown displayed pre-confirmation
- SLA banner: instant for participating banks, T+1 for others

### FR-005: Bill Payments
- Billers catalogued in `billers` table (electricity, water, council, DStv, schools)
- Customer selects biller → enters account/meter number → amount → confirm
- Receipt includes biller reference; biller payload posted via EPIC-016 switch or REST integration

### FR-006: Beneficiaries Management
- CRUD list of saved beneficiaries (internal, inter-bank, mobile money, billers)
- Cooling-off: a new beneficiary cannot be paid > X for the first 24 hours (configurable)
- Optional nickname, default amount, last-used sort

### FR-007: Mobile Money Push (EcoCash / OneMoney / InnBucks)
- Wallet integrations via EPIC-016 partner adapters
- Customer enters wallet number, amount, confirms with biometric/PIN
- Reconciliation against partner ledger nightly

### FR-008: In-App Card Controls (preview only)
- Freeze/unfreeze card, view card last-4, change PIN (delegated to EPIC-024)

### FR-009: Notifications & Receipts
- Push notifications for every credit/debit, with deep-link to transaction detail
- Re-send receipt by email/SMS from history
- In-app receipt = QR + reference + tariff breakdown

### FR-010: Limits & Security
- Per-channel daily limit (configurable per KYC tier)
- Step-up auth (biometric + PIN) for transfers > threshold
- Device binding; new device requires SMS OTP + selfie liveness re-check

---

## Non-Functional Requirements

- p95 transfer initiation → confirmation < 3 s (excluding switch SLA)
- Offline-tolerant: queued transfers retried with idempotency keys
- Localised in EN, SH, ND
- Accessibility: WCAG AA, dynamic font scaling

## Out of Scope

- Loan applications (EPIC-023)
- Card issuance flows (EPIC-024)
- Cross-border remittance (EPIC-025)
