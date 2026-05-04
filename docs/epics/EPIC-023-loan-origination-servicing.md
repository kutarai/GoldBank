# EPIC-023: Loan Origination & Servicing

**Priority:** Must Have
**Estimated Points:** 95
**Sprints:** 29–33
**Dependencies:** EPIC-001 (Core Accounts), EPIC-017 (AI Vision for ID/payslip OCR), EPIC-020a (Tariff Engine for fees & interest posting), bank-client LoanReview page (stub)

---

## Business Context

GoldBank currently has a stub Loan Review screen in bank-client but no actual loan product. Loans are the bank's highest-margin asset class. This epic delivers end-to-end loan origination (application → scoring → approval → disbursement) and servicing (repayment schedule, arrears, write-off) for unsecured personal loans and salary-backed loans, with a designed extension path for SME and asset-backed lending.

## User Personas

- **Borrower** — Existing customer applying via mobile or branch.
- **Credit Officer** — Reviews applications, runs scoring, approves/declines.
- **Branch Manager** — Approves loans within branch limit; escalates above.
- **Collections Officer** — Works arrears, agrees restructure plans.
- **Finance** — Reconciles loan book, posts interest/provisions to GL.

---

## Functional Requirements

### FR-001: Loan Product Catalogue
- `loan_products` table: name, currency, min/max amount, min/max tenor, base rate, fee schedule, eligibility rules (KYC tier, employment status, income band)
- Product variants: personal unsecured, salary-backed, business working capital, asset-backed (skeleton)

### FR-002: Application Submission
- Customer applies in mobile app or via teller (branch-assisted)
- Captures: requested amount, tenor, purpose, employer/income proof (payslip image), bank-statement consent
- AI-vision extracts income from payslip (EPIC-017) → pre-fills affordability fields

### FR-003: Affordability & Credit Scoring
- DTI calculation from declared income vs existing debits (last 90 days)
- Internal scorecard: behavioural score from on-us transaction history
- External bureau hook (pluggable): FCB / TransUnion stub adapter
- Output: pass / refer / decline with reason codes; PD bucket assigned

### FR-004: Approval Workflow
- Auto-approve if score ≥ X and amount ≤ branch limit
- Refer-to-officer queue for borderline cases
- Multi-level approval matrix: officer → branch manager → credit committee, by amount band
- Full audit trail of who approved what and when

### FR-005: Disbursement
- Booked loan creates a `loans` row + matching `loan_account` (asset side) and credits the customer's deposit account
- Origination fee posted via EPIC-020a tariff engine
- Disbursement is reversible only by Operations within T+0 cut-off

### FR-006: Repayment Schedule
- Generated at booking: amortising schedule (equal instalment / equal principal), bullet, or interest-only
- `loan_schedule` rows with due_date, principal, interest, fee, balance
- Visible to borrower in mobile app; downloadable PDF

### FR-007: Repayment Processing
- Standing order against the borrower's deposit account on due date
- Partial payments allocated principal-first or interest-first per product config
- Early settlement: rebate of unearned interest, fee per tariff

### FR-008: Arrears & Collections
- Daily job buckets accounts: 0, 1–30, 31–60, 61–90, 90+
- Auto-SMS reminders at D-3, D, D+3, D+7, D+14
- Collections workbench in bank-client: case list, contact log, promise-to-pay, restructure
- Auto-NPL flag at 90+ DPD

### FR-009: Provisioning & Write-Off
- IFRS 9 stage 1/2/3 classification with ECL parameters per product
- Monthly provision posting to GL via EPIC-020a
- Manual write-off workflow with dual approval

### FR-010: Reporting
- Loan book by product, branch, officer, vintage
- PAR (portfolio at risk) dashboard
- Disbursement vs collections trend
- Regulatory templates for central bank submission

---

## Non-Functional Requirements

- All loan-money movements must be ledger-balanced (no orphans)
- Application → decision (auto path) ≤ 60 s
- Schedule recalculation idempotent on every restructure event
- Full immutable audit log per loan

## Out of Scope

- Mortgage / home loans (separate epic)
- Group lending / microfinance methodology
- Insurance bundling
