# EPIC-021: Bank Teller & Vault Client Application

**Priority:** Must Have
**Estimated Points:** 80
**Sprints:** 25–28
**Dependencies:** EPIC-001 (Core Accounts), KYC selfie + ID image storage (added 2026-04), Account signature fields (added 2026-04), Branches (existing in admin module)

---

## Business Context

Branch tellers need a dedicated front-end to process **physical cash transactions** — deposits and withdrawals — at the counter. The existing bank-client (admin portal) is built for back-office operations and is not optimised for the high-volume, denomination-based, identity-verified workflow tellers perform every day. A purpose-built **bank-teller** application provides a focused UI for queue management, customer lookup with biometric/signature verification, denomination capture, receipt printing, and end-of-day cash drawer reconciliation.

The teller client also produces an authoritative cash audit trail per teller per branch — currently missing from the platform — which is required for branch operations, internal audit, and central bank reporting.

## User Personas

- **Teller** — Branch staff member who processes deposits/withdrawals at the counter. Needs to find customers fast, verify identity visually, count denominations, print receipts.
- **Vault Manager** — Custodian of the branch vault. Issues morning float to tellers, receives end-of-day surrenders, runs spot checks, balances vault stock against the GL, requests cash injections from head office.
- **Branch Supervisor** — Approves high-value transactions, opens/closes teller drawers, resolves discrepancies, performs spot checks, signs off on end-of-day cash-up.
- **Customer (depositor)** — May or may not be the account holder. For deposits, anyone can pay in. For withdrawals, **must** be the account holder (verified by signature + photo + ID).
- **Operations / Finance** — Reviews end-of-day teller reports, branch-level cash position, vault reconciliation, and posting to the GL.

---

## Functional Requirements

### FR-001: Teller Authentication & Session
- Tellers log in with existing `admin_users` credentials, role = `teller`
- New `admin_users.role` value: `teller` (alongside super_admin, operations, support, finance, compliance, branch_manager)
- A teller is bound to a `branch_id` (already exists on admin_users)
- Teller session ends on logout, inactivity timeout (15 min), or supervisor override
- All teller sessions logged in `audit_logs`

### FR-002: Open / Close Cash Drawer
- At start of shift: teller "opens drawer" by **drawing float from the branch vault** (FR-014). The float-out is a vault movement, not a free-standing entry — denominations are recorded against both the drawer session and the vault.
- At end of shift: teller "closes drawer" by **surrendering all cash to the vault** (FR-015). The system shows expected vs counted cash; teller enters actual denominations; supervisor or vault manager approves any variance.
- Drawer state stored per `(teller_id, branch_id, business_date)` and holds running balances per currency.
- A drawer cannot be opened until the previous day's drawer is closed and balanced.

### FR-003: Customer Lookup
- Teller searches by: phone number, account number, national ID, or name
- Search returns matching accounts with: name, account ID, currency, balance, status, KYC level
- For each result, the teller can open a **Customer Card** showing:
  - **Photo** (selfie from KYC) — large, prominent
  - **ID Document** image (national_id from KYC)
  - **Signature** image (from `accounts.signature_image`) + verifier and date
  - Full profile: phone, email, DOB, national ID, addresses, balance(s)
  - Account status flags: frozen, suspended, signature unverified, KYC not approved
- The Customer Card is the **mandatory verification step** before any withdrawal

### FR-004: Cash Deposit
- Teller selects an account, enters:
  - **Depositor name** (free text — may be a third party, not the account holder)
  - **Currency** (must match an account currency for that customer)
  - **Amount** (numeric)
  - **Denomination breakdown** — number of notes/coins per denomination, must sum to amount
- System validates: account is active, currency matches an account, amount > 0, denominations sum equals amount
- On confirm:
  - Credit the customer account (creates a `transactions` row, `type = cash_in_branch`)
  - Insert a `branch_cash_transactions` row with full denomination breakdown + teller + drawer + depositor name
  - Update teller drawer running balance (`+amount` for that currency)
  - Print or save receipt (PDF) with: branch name, teller, customer name, account, amount, denominations, reference, timestamp
- No customer authentication required for deposits (anyone may deposit)

### FR-005: Cash Withdrawal
- Teller MUST first open the **Customer Card** (FR-003) and visually verify:
  - The person at the counter matches the **photo** on file
  - Their **signature** matches the stored signature image
  - Their **ID document** matches the person and the stored ID
- Teller ticks an explicit "**Identity verified by photo, signature, and ID**" checkbox before the withdrawal form unlocks
- Teller enters:
  - Currency
  - Amount
  - Denomination breakdown (notes/coins to be paid out, must sum to amount)
- System validates:
  - Account is active (not frozen/suspended/closed)
  - KYC level meets the configured threshold for cash withdrawal
  - Available balance ≥ amount
  - Amount within daily / monthly limit and remaining drawer cash
  - Identity verification checkbox is ticked
- High-value withdrawals (> configurable threshold per currency) require **supervisor approval** — supervisor enters their PIN to authorise
- On confirm:
  - Debit the customer account (`transactions` row, `type = cash_out_branch`)
  - Insert `branch_cash_transactions` row with denomination breakdown + identity verification flag + supervisor approver if any
  - Update teller drawer running balance (`-amount`)
  - Print receipt for customer to sign — signed receipt is scanned/uploaded back to the transaction record (optional, configurable)

### FR-006: Currency & Denomination Registry
- Per currency, the bank defines **every legal-tender denomination**, including:
  - Face value (e.g., 100, 50, 20, 10, 5, 2, 1, 0.50, 0.25, 0.10, 0.05)
  - **Type: `note` or `coin`** (drives UI grouping and physical cash handling rules)
  - Display order (largest to smallest)
  - Active flag (denominations can be retired without deleting historical records)
- Stored in `currency_denominations` table, editable by admin
- Used by teller deposits/withdrawals, vault stock, vault float-out, vault surrender, and spot checks
- Teller and vault UIs present a grid grouped by Notes / Coins with quantity inputs; sub-total and grand total auto-calculated per group and overall
- Validation prevents submission if Σ (denom × count) ≠ amount

### FR-007: Transaction Reversal
- A teller can reverse their **own** in-flight cash transaction within the same shift (e.g., miscount), supervisor approval required
- Reversal creates a compensating `transactions` row + flips the drawer balance back
- Cannot reverse cross-shift; that goes through the dispute workflow (existing)

### FR-008: End-of-Day Cash-Up
- At end of shift, system computes expected drawer balance per currency = opening float + Σ deposits − Σ withdrawals
- Teller counts physical cash and enters actual denominations
- System computes variance (actual − expected); if non-zero, supervisor must approve and a `cash_variance` audit event is recorded
- Closing balance is sealed and becomes the next shift's opening float (or returned to vault — supervisor decision)
- End-of-day report generated: PDF with teller, branch, transaction count, total in / out, opening, closing, variance, signed by teller + supervisor

### FR-009: Branch Dashboard (Supervisor)
- Branch supervisor sees a live dashboard:
  - Active tellers and current drawer balances
  - Total cash in branch (sum across drawers + vault)
  - Today's transaction count and volume by teller
  - Pending high-value withdrawals awaiting approval
  - Variance alerts

### FR-010: Receipt Printing & Reprint
- Receipts generated as PDF (single A6 size, suitable for thermal printer)
- Includes barcode/QR with the transaction reference
- Teller can reprint any of their transactions from the day on demand
- Receipt template configurable per tenant (logo, footer text, language)

### FR-011: Audit Trail
- Every teller action recorded in `audit_logs`: login, logout, drawer open, drawer close, deposit, withdrawal, reversal, identity verification, supervisor approval
- Each `branch_cash_transactions` row links back to the originating `transactions` row and the teller's drawer session
- Audit trail accessible from bank-client admin portal (existing Audit Trail screen)

---

## Vault Functional Requirements

### FR-012: Vault Definition
- Each branch has **exactly one vault** (1:1 with `branches`)
- A vault holds **physical cash stock** broken down by currency and by denomination
- Vault attributes:
  - Branch ID (FK, unique)
  - Name / location label (e.g., "Main Strongroom")
  - Vault manager admin user ID (custodian)
  - Spot-check frequency (cron expression OR "daily" / "weekly" / "monthly" presets)
  - Last spot-check timestamp + result
  - Active flag
- New `admin_users.role` value: `vault_manager` (alongside teller, branch_manager, super_admin, …)

### FR-013: Vault Stock & Denomination Holdings
- For every `(vault, currency)` the system tracks the running stock as a **denomination breakdown**, not just a total
- Per denomination: count of pieces × face value = sub-total; sum across all denominations = total stock for that currency
- Stock changes are **append-only via `vault_movements`** — no row in `vault_denomination_stock` is ever directly edited; the table is a materialised aggregate that the gateway recomputes after each movement
- Recomputation is atomic in the same DB transaction as the movement insert
- A vault may hold multiple currencies; each currency tracked independently

### FR-014: Morning Float-Out (Vault → Teller)
- The vault manager initiates a **float-out** to a specific teller before the teller opens their drawer
- Inputs:
  - Teller (must be assigned to the same branch)
  - Currency
  - Denomination breakdown (notes + coins) being handed over
  - Total amount (auto-calculated, must match Σ denominations)
- Validation:
  - Vault must hold at least the requested count of EACH denomination
  - Teller must not already have an open drawer for the day
  - Vault manager re-enters PIN to authorise
- On confirm:
  - Inserts a `vault_movements` row with `Type = float_out`, the denomination breakdown, the destination teller, and the (about-to-be-created) drawer session ID
  - **Decreases vault stock** for each denomination
  - **Creates the teller drawer session** with this float as the opening balance
  - Both records share the same `Reference` for traceability
- Receipt printed for both vault manager and teller to sign

### FR-015: End-of-Day Surrender (Teller → Vault)
- At end of shift, after the teller has counted their physical cash, they initiate a **surrender** to the vault manager
- Inputs:
  - Drawer session being closed
  - Per-currency denomination breakdown of the cash being handed over
- The system computes the **expected** drawer balance per currency from: opening float ± all branch_cash_transactions that touched that drawer
- Variance per currency = surrendered − expected. If non-zero, vault manager must approve and a variance audit entry is recorded
- On confirm:
  - Inserts a `vault_movements` row with `Type = surrender`, denomination breakdown, source teller, source drawer session, variance per currency
  - **Increases vault stock** for each denomination
  - Closes the teller drawer session, sealing the actual closing balance
- Receipt printed and signed by teller + vault manager

### FR-016: Vault Spot Check
- Each vault has a configurable **spot-check frequency** (FR-012)
- The system raises a "spot check due" task on the vault manager dashboard when the next check is due (last_spot_check + frequency ≤ now)
- A spot check is performed by the vault manager AND a witness (branch supervisor or another senior staff member) — the witness's PIN is required to submit
- During the spot check the team physically counts the vault and enters the actual denomination breakdown per currency
- The system computes:
  - Expected per denomination (from `vault_denomination_stock`)
  - Actual per denomination (from the count)
  - Variance per denomination AND per currency total
- On confirm:
  - Inserts a `vault_spot_checks` row with both expected and actual breakdowns, the witness ID, and the variance
  - If variance is non-zero **for any denomination**, the system inserts a compensating `vault_movements` row with `Type = adjustment` and updates `vault_denomination_stock` so it now matches the counted reality
  - The adjustment is **flagged for review** by the branch manager / finance — it does not bypass audit, it just keeps the books in sync with physical reality
- A spot-check report PDF is generated with both signatures (vault manager + witness) and any variance commentary

### FR-017: Cash Injection / Withdrawal (Vault ↔ Head Office)
- Vault manager can record an **injection** when physical cash is delivered to the branch from head office or central bank
- Vault manager can record a **withdrawal** when surplus cash is sent back
- Both record full denomination breakdowns and a reference to the source/destination (free text or external transfer ID)
- Inserts a `vault_movements` row with `Type = injection` or `Type = withdrawal_to_hq`
- Updates vault stock accordingly
- Both require vault manager + branch supervisor co-signature (two PINs)

### FR-018: Vault Dashboard & Reporting
- Vault manager dashboard shows:
  - Current stock per currency, with denomination breakdown
  - Total stock value per currency
  - Today's movements (float-outs, surrenders, injections, adjustments)
  - Tellers currently holding float (and how much)
  - Next spot check due date / overdue indicator
  - Recent spot check results with variances
- End-of-day **Branch Vault Report** PDF:
  - Opening stock (start of business day) per currency × denomination
  - All movements during the day
  - Closing stock per currency × denomination
  - Variance summary
  - Vault manager + supervisor signature blocks

### FR-019: Mandatory Signed Document Capture (Cross-Cutting)
**Applies to every transaction where physical cash changes hands**, namely:
- Cash deposits (FR-004)
- Cash withdrawals (FR-005)
- Reversals (FR-007)
- Vault float-out to teller (FR-014)
- Teller surrender to vault (FR-015)
- Vault injection from head office (FR-017)
- Vault withdrawal to head office (FR-017)

**Workflow:**
1. Teller / vault manager confirms the transaction in the UI; system writes the row to `branch_cash_transactions` or `vault_movements` with status `pending_signature`
2. System prints (or generates as PDF for download) the **receipt** with signature blocks for both parties (e.g., teller + customer for deposits/withdrawals; teller + vault manager for float/surrender; vault manager + supervisor for injections/withdrawals to HQ)
3. Both parties sign the printed receipt physically
4. Teller / vault manager **scans the signed receipt** using a flatbed/document scanner attached to the workstation OR a phone camera capture, then **uploads it** through the UI ("Upload signed receipt" button on the transaction row)
5. System stores the scanned image bytes against the transaction (`signed_document_image` bytea), records `signed_document_uploaded_by` and `signed_document_uploaded_at`, and transitions the row to `completed`

**Enforcement rules:**
- A teller drawer **cannot be closed (surrender to vault)** if any of the day's `branch_cash_transactions` are still `pending_signature` — the surrender screen lists the missing scans and blocks until they're all uploaded
- A vault manager **cannot perform end-of-day** if any vault movements (float-outs, surrenders, injections, withdrawals) are `pending_signature`
- Spot check (FR-016) explicitly does **not** require a scanned signature — its own PDF report carries the dual signatures and is captured by the spot check workflow itself
- Re-uploads are allowed (e.g., poor quality scan); each upload appends a new version on the row, with the previous one retained for audit
- Supported formats: PNG, JPEG, PDF (single page). Server-side compression pipeline:
  - Image inputs (PNG / JPEG): downscale longest edge to ≤ 2000 px and re-encode as JPEG quality 80
  - PDF inputs: rasterize at 200 DPI and re-encode as JPEG quality 80
  - Iteratively reduce JPEG quality (75 → 70 → 65 …) until the resulting payload is **≤ 2 MB**
  - Reject the upload only if even at quality 50 the payload still exceeds 2 MB (extremely rare for a single signed receipt)
- **Hard cap on the stored bytea: 2 MB per document.** The original upload may be larger but the row only ever stores the compressed result.
- **Hot / cold tiering policy:**
  - **Hot (≤ 3 months old):** signed document stored as `bytea` directly on `branch_cash_transactions.signed_document_image` / `vault_movements.signed_document_image`. Served instantly from PG with no extra hop.
  - **Cold (> 3 months old):** a nightly archival job moves the bytea content to encrypted file storage (reuses `DocumentStorageService` from KYC), populates a new `signed_document_archive_path` column, sets `bytea` to NULL, and flips a `document_storage_tier` flag to `cold`.
  - Retrieval is transparent: the admin endpoint reads the `tier` flag and either returns the bytea inline or streams from the archive.
  - Configurable via `system_config` key `cash.signed_doc_hot_days` (default `90`).
  - **Disable switch:** if `cash.signed_doc_hot_days = 0` the tiering policy is **completely disabled** — the nightly archival job exits immediately on each run, all documents stay in the hot bytea tier forever, and `document_storage_tier` remains `Hot` for every row. The disable switch is the supported way to opt out of cold-tiering for tenants with abundant PG storage or compliance requirements that mandate everything stay in the primary database.
- A bank-client admin endpoint (`GET /api/admin/cash-transactions/{id}/signed-document`) returns the bytes for back-office review (transparent across hot/cold tiers)

**Why no cash counter integration:** the deployment uses **standalone cash counters** (Glory, Talaris, Magner, etc.) that are not networked to GoldBank. Tellers and vault managers count physically using these machines, then enter the totals + denomination breakdown into the UI manually. The signed receipt is the legal record bridging the physical count and the digital ledger.

---

## Stories

### Sprint 25: Server Foundation (17 pts)

| Story | Title | Points | Description |
|-------|-------|--------|-------------|
| STORY-148 | Branch cash domain model + DB schema | 5 | Create `BranchCashTransaction`, `TellerDrawerSession`, `DenominationConfig`, `CashCount` entities + migrations. New `admin_users.role` value `teller`. |
| STORY-149 | Teller cash gRPC + REST endpoints | 5 | `/api/teller/customers/search`, `/api/teller/customers/{id}/card`, `/api/teller/deposits`, `/api/teller/withdrawals`, `/api/teller/drawer/open`, `/api/teller/drawer/close`, `/api/teller/transactions/{id}/reverse`. JWT-protected with role=teller. |
| STORY-150 | Customer Card endpoint | 3 | Returns the customer's photo, ID image, signature, and profile in a single payload (reuses `kyc_documents.file_data` and `accounts.signature_image`). |
| STORY-151 | Denomination validation engine | 2 | Validate denomination breakdown sums to the amount; per-currency denomination registry; reusable in deposits, withdrawals, and cash-up. |
| STORY-152 | High-value withdrawal supervisor approval flow | 2 | Threshold per currency in `system_config`. Supervisor PIN re-authentication. Audit log entry with both teller and supervisor IDs. |

### Sprint 26: Bank-Teller Front-end (18 pts)

| Story | Title | Points | Description |
|-------|-------|--------|-------------|
| STORY-153 | bank-teller scaffolding | 3 | New Vite/React project at `bank-teller/`, MUI theme matching bank-client, login screen, JWT auth, role guard. |
| STORY-154 | Customer search + Customer Card screen | 5 | Search box (phone/ID/name/account). Result list. Card screen with prominent photo, ID image, signature, profile, balance, account flags. |
| STORY-155 | Deposit screen | 4 | Account selector → currency → depositor name → amount → denomination grid → confirm → receipt preview. |
| STORY-156 | Withdrawal screen | 4 | Customer Card → identity-verified checkbox → unlock form → currency → amount → denominations → confirm → supervisor approval modal if over threshold → receipt. |
| STORY-157 | Drawer open / close / running balance | 2 | Header shows current drawer balances per currency. Open/close drawer modals. Cash-up screen with denomination count vs expected. |

### Sprint 27: Reports, Reconciliation & Polish (12 pts)

| Story | Title | Points | Description |
|-------|-------|--------|-------------|
| STORY-158 | Receipt PDF generation + printing | 3 | Server-side PDF (QuestPDF or similar). Thermal-printer friendly A6 layout. Reprint endpoint. |
| STORY-159 | End-of-day teller report | 3 | PDF report: teller, branch, txn count, totals in/out, opening, closing, variance, signature blocks. Stored against the drawer session. |
| STORY-160 | Branch supervisor dashboard | 3 | Live drawer balances, pending approvals, daily volume per teller, variance alerts. |
| STORY-161 | Reversal flow + audit trail integration | 2 | UI for reversing same-shift transactions. Audit entries for every teller action surfaced in bank-client Audit Trail screen. |
| STORY-162 | Bank-teller production hardening | 1 | Idle-timeout, session lock screen, double-submit protection, offline guard (cannot operate if gateway is unreachable). |

### Sprint 28: Vault Management + Signed Document Capture (33 pts)

| Story | Title | Points | Description |
|-------|-------|--------|-------------|
| STORY-163 | Currency denomination registry | 3 | New `currency_denominations` table (face value, type note/coin, display order, active flag). Replaces the simple `denomination_config`. Migration + admin CRUD endpoints + bank-client UI tab. |
| STORY-164 | Vault domain model + DB schema | 5 | `Vault`, `VaultDenominationStock`, `VaultMovement`, `VaultSpotCheck` entities + EF config + migrations. New role `vault_manager`. 1:1 vault per branch. |
| STORY-165 | Vault stock recompute service | 3 | Atomic recompute of `vault_denomination_stock` after each `vault_movement` insert. Handled inside the same DB transaction. Idempotent and verifiable. |
| STORY-166 | Vault gRPC + REST endpoints | 5 | `/api/vault/{vaultId}/stock`, `/api/vault/{vaultId}/movements`, `POST /api/vault/{vaultId}/float-out`, `POST /api/vault/{vaultId}/surrender`, `POST /api/vault/{vaultId}/spot-check`, `POST /api/vault/{vaultId}/injection`, `POST /api/vault/{vaultId}/withdrawal`. JWT-protected with role `vault_manager` (or `branch_manager` / `super_admin`). |
| STORY-167 | Vault Manager screens (bank-teller app) | 5 | Dashboard with per-currency denomination grid, today's movements, pending spot check task. Float-out screen (select teller, enter denominations, sign). Surrender screen (close drawer, count, variance approval). |
| STORY-168 | Spot check workflow | 3 | Vault manager + witness PIN re-auth. Denomination count entry. Variance compute + auto-adjustment movement. Spot-check PDF report. Scheduler raises tasks based on `vault.spot_check_cron`. |
| STORY-169 | Branch Vault Report (PDF) | 2 | Daily PDF: opening stock × denomination, all movements, closing stock × denomination, variance summary, dual signature blocks. |
| STORY-170 | Signed-document capture workflow | 5 | New bytea + status fields on `branch_cash_transactions` and `vault_movements`. Migration. `POST /api/teller/cash-transactions/{id}/signed-document` and `POST /api/vault/movements/{id}/signed-document` upload endpoints (multipart). Server-side compression pipeline: PNG/JPEG → downscale to ≤ 2000 px → re-encode JPEG q80, PDF → rasterize 200 DPI → JPEG, iteratively drop quality until payload ≤ **2 MB**. UI: "Upload signed receipt" button on every cash transaction; pending counter on dashboard; drawer close + vault EOD blocked until all `pending_signature` rows are resolved. Re-upload appends version history in audit log. Bank-client admin endpoint to fetch the signed image for back-office review. |
| STORY-171 | Hot/cold tiering for signed documents | 2 | Add `signed_document_archive_path` + `document_storage_tier` columns to both ledger tables (migration). Nightly Wolverine background job scans rows with `tier = Hot` AND `signed_document_uploaded_at < now − cutoff`, writes the bytea to encrypted file storage via `DocumentStorageService`, sets the path, NULLs the bytea, flips tier to `Cold`. Admin retrieval endpoint reads the tier flag and returns hot bytea inline OR streams from archive. Configurable cutoff via `system_config` key `cash.signed_doc_hot_days` (default `90`). **If `cash.signed_doc_hot_days = 0` the job is a no-op** (early-return at the top of `Handle()`), tiering is disabled, and all documents stay in the hot bytea tier indefinitely. Unit test must cover the disabled case. |

---

## Data Model

```
TellerDrawerSession
├── Id (UUID)
├── TellerId (UUID, FK → admin_users)
├── BranchId (UUID, FK → branches)
├── BusinessDate (DateOnly)
├── Status (enum: Open, Closed, Suspended)
├── OpeningFloatJson (jsonb: { "USD": 5000.00, "ZWG": 10000.00 })
├── ClosingBalanceJson (jsonb, nullable)
├── ExpectedClosingJson (jsonb, nullable — computed by system at close time)
├── VarianceJson (jsonb, nullable — actual − expected per currency)
├── OpenedAt (timestamptz)
├── ClosedAt (timestamptz, nullable)
├── ClosedBySupervisorId (UUID, nullable, FK → admin_users)
├── TenantId, CreatedAt, UpdatedAt

BranchCashTransaction
├── Id (UUID)
├── TransactionId (UUID, FK → transactions — the resulting credit/debit)
├── DrawerSessionId (UUID, FK → teller_drawer_sessions)
├── TellerId (UUID, FK → admin_users)
├── BranchId (UUID, FK → branches)
├── AccountId (UUID, FK → accounts)
├── Direction (enum: Deposit, Withdrawal, Reversal)
├── Currency (string, 3)
├── Amount (decimal 18,2)
├── DepositorName (string, 200 — free text, may differ from account holder)
├── DenominationBreakdownJson (jsonb: [{ "denom": 100, "count": 5 }, { "denom": 20, "count": 3 }])
├── IdentityVerified (bool — true for withdrawals, false for deposits)
├── SupervisorApproverId (UUID, nullable, FK → admin_users)
├── SupervisorApprovedAt (timestamptz, nullable)
├── ReceiptPdfPath (string, nullable)             -- generated unsigned receipt
├── SignedDocumentImage (bytea, nullable)         -- hot tier: ≤ 3 months old, NULL once cold-tiered
├── SignedDocumentContentType (string, 50, nullable)
├── SignedDocumentUploadedBy (UUID, nullable, FK → admin_users)
├── SignedDocumentUploadedAt (timestamptz, nullable)
├── SignedDocumentArchivePath (string, 500, nullable)  -- cold tier: encrypted file storage
├── DocumentStorageTier (enum: Hot, Cold) DEFAULT Hot
├── DocumentStatus (enum: PendingSignature, Completed) DEFAULT PendingSignature
├── ReversedByTransactionId (UUID, nullable, self-FK to a reversal entry)
├── ReversedAt (timestamptz, nullable)
├── TenantId, CreatedAt, UpdatedAt

CurrencyDenomination          -- replaces the simpler DenominationConfig
├── Id (UUID)
├── Currency (string, 3)        -- "USD", "ZWG"
├── FaceValue (decimal 18,4)    -- 100, 50, 20, 10, 5, 2, 1, 0.50, 0.25, 0.10, 0.05
├── DenominationType (enum: Note, Coin)
├── DisplayOrder (int)          -- largest to smallest
├── IsActive (bool)             -- retire without deleting historical references
├── TenantId, CreatedAt, UpdatedAt
└── unique (TenantId, Currency, FaceValue)

Vault                            -- one per branch
├── Id (UUID)
├── BranchId (UUID, FK → branches, UNIQUE)
├── Name (string, 100)
├── VaultManagerId (UUID, nullable, FK → admin_users)
├── SpotCheckCron (string, e.g. "0 9 * * 1" — Mondays 09:00, or "daily")
├── LastSpotCheckAt (timestamptz, nullable)
├── LastSpotCheckResult (enum: NotYet, Balanced, Variance)
├── IsActive (bool)
├── TenantId, CreatedAt, UpdatedAt

VaultDenominationStock           -- materialised aggregate, recomputed on every movement
├── Id (UUID)
├── VaultId (UUID, FK → vaults)
├── Currency (string, 3)
├── DenominationId (UUID, FK → currency_denominations)
├── Count (int)                  -- number of pieces of this denomination held
├── UpdatedAt (timestamptz)
└── unique (VaultId, DenominationId)

VaultMovement                    -- append-only ledger of vault changes
├── Id (UUID)
├── VaultId (UUID, FK → vaults)
├── Type (enum: FloatOut, Surrender, SpotCheckAdjustment, Injection, WithdrawalToHq)
├── Direction (enum: In, Out)    -- In = stock increases; Out = stock decreases
├── Currency (string, 3)
├── TotalAmount (decimal 18,2)
├── DenominationBreakdownJson (jsonb: [{ "denominationId": "uuid", "faceValue": 100, "type": "Note", "count": 12 }, …])
├── TellerId (UUID, nullable, FK → admin_users — for FloatOut/Surrender)
├── DrawerSessionId (UUID, nullable, FK → teller_drawer_sessions)
├── PerformedBy (UUID, FK → admin_users — vault manager or supervisor)
├── WitnessId (UUID, nullable, FK → admin_users — for spot checks and dual-control movements)
├── Reference (string, 30)       -- shared ref between paired records (vault movement ↔ drawer session)
├── Notes (string, 1000, nullable)
├── ReceiptPdfPath (string, nullable)              -- generated unsigned receipt
├── SignedDocumentImage (bytea, nullable)          -- hot tier: ≤ 3 months old, NULL once cold-tiered
├── SignedDocumentContentType (string, 50, nullable)
├── SignedDocumentUploadedBy (UUID, nullable, FK → admin_users)
├── SignedDocumentUploadedAt (timestamptz, nullable)
├── SignedDocumentArchivePath (string, 500, nullable)  -- cold tier: encrypted file storage
├── DocumentStorageTier (enum: Hot, Cold) DEFAULT Hot
├── DocumentStatus (enum: PendingSignature, Completed, NotRequired) DEFAULT PendingSignature
                                                    -- NotRequired only for SpotCheckAdjustment
├── TenantId, CreatedAt, UpdatedAt

VaultSpotCheck
├── Id (UUID)
├── VaultId (UUID, FK → vaults)
├── PerformedBy (UUID, FK → admin_users — vault manager)
├── WitnessId (UUID, FK → admin_users)
├── ExpectedJson (jsonb: per currency, per denomination, expected count + face value)
├── ActualJson (jsonb:   per currency, per denomination, counted count + face value)
├── VarianceJson (jsonb: per currency, per denomination, actual − expected)
├── HasVariance (bool)
├── AdjustmentMovementId (UUID, nullable, FK → vault_movements — the compensating entry, if any)
├── ReportPdfPath (string, nullable)
├── TenantId, CreatedAt, UpdatedAt
```

### Schema notes
- `admin_users.role` adds two new values: `teller` and `vault_manager`. No new column needed.
- `branches` table already exists from EPIC-019 / pending model changes.
- `accounts.signature_image`, `signature_verified_by`, `signature_verified_at` already added (migration `20260408080000_AddAccountSignature`).
- `kyc_documents.file_data` already added (migration `20260408070000_AddKycDocumentFileData`) — provides ID and selfie bytes for the Customer Card.
- `currency_denominations` **replaces** the simple `DenominationConfig` proposed earlier — same epic, evolved scope.
- A `Vault` is **created automatically** for every existing and future branch via a one-time data migration plus a domain event handler on `BranchCreated`.
- `vault_denomination_stock` is a materialised view of all `vault_movements` for that vault. It can always be regenerated from scratch by replaying the movement ledger — useful for audit and disaster recovery.

---

## Authentication & Authorization

- Reuse JWT auth from gateway. New role claim `teller`.
- New gRPC interceptor or controller-level `[Authorize(Roles = "teller,branch_manager,super_admin")]` for `/api/teller/*`.
- Tellers can only see customers in their own tenant; branch_id is captured on every transaction for reporting.
- Supervisor approval re-authenticates the supervisor's PIN against `admin_users.password_hash` (the supervisor must be present at the teller's terminal).

## Front-end Architecture

- **Framework:** React + Vite + MUI (matches bank-client for fast cross-team work)
- **Folder:** `bank-teller/` at repo root
- **Routing:** React Router. Top-level routes: `/login`, `/`, `/customer/:id`, `/deposit`, `/withdrawal`, `/drawer`, `/eod`
- **State:** React Context for the active drawer session + JWT; per-screen local state for forms
- **API client:** Plain `fetch` against `/api/teller/*` (same pattern as bank-client)
- **Receipt rendering:** PDF generated server-side, opened in a new tab for printing

## Reports

### End-of-Day Teller Report (PDF, A4)
- Header: tenant logo, branch name, business date, teller name + ID
- Opening float per currency
- Transaction list (txn ref, time, account, type, amount, currency)
- Totals: deposits, withdrawals, reversals per currency
- Expected closing balance vs counted closing balance
- Variance per currency (highlighted if non-zero)
- Signature blocks: teller, supervisor

### Branch Daily Cash Report (PDF, A4)
- Header: branch name, business date
- Per-teller summary: opening, in, out, closing, variance
- Branch totals
- Outstanding approvals
- Variance summary

---

## Receipt Format

```
        GOLDBANK — KUWADZANA BRANCH
        ─────────────────────────────
        DEPOSIT RECEIPT

Date:        2026-04-08 14:32
Teller:      mtsunga (TLR-042)
Reference:   DEP-000123456

Account:     ACC-000005
Holder:      WILLIE MOYO
Depositor:   GRACE MOYO
Currency:    USD
Amount:      $520.00

Denominations:
  $100 ×  5  =  $500.00
   $20 ×  1  =   $20.00

        Thank you for banking with us.
        ─────────────────────────────
        [QR code: txn ref]
```

---

## Security Considerations

- **Cash drawer state must be tamper-resistant** — every drawer transaction is append-only; no delete or edit allowed
- **Identity verification audit** — the `IdentityVerified` flag plus the timestamp + teller ID is a legal liability record; must be immutable
- **Photos and signatures are PII** — only loaded on-demand for the active customer card, never bulk-exported, and the Customer Card endpoint logs each access in `audit_logs`
- **Supervisor PIN** — re-authentication requires the supervisor's actual PIN, not the teller's session JWT
- **Receipt PDFs** — store hash + path; PDF storage encrypted at rest (reuse DocumentStorageService)
- **No browser caching of sensitive screens** — `Cache-Control: no-store` on Customer Card and any cash transaction page
- **Session binding** — JWT tied to the teller's machine fingerprint; if a teller logs in elsewhere, the previous session is invalidated

---

## Open Questions

1. **Hardware integration** — **resolved**:
   - **Cash counters** (Glory / Talaris / Magner / similar): present at every teller and vault but **NOT integrated**. Tellers and vault managers count physically and enter the totals + denomination breakdown into the UI manually.
   - **Receipt printing**: server-generates a PDF; printing handled by the workstation OS (no custom driver).
   - **Document scanning**: workstations have flatbed/document scanners and/or phone cameras. The UI accepts uploads via standard file picker / drag-and-drop. No vendor SDK integration needed.
   - **Fingerprint scanners**: not in scope for this epic.
2. **Cash logistics with vault** — **resolved by FR-012 — FR-018**: full vault flow with float-out and surrender, denomination-tracked.
3. **Multi-currency drawer** — confirmed: a single drawer holds multiple currencies, balanced independently.
4. **Receipt language** — should receipts be configurable per tenant (English / Shona / Ndebele)?
5. **Offline mode** — should the teller app continue accepting deposits when the gateway is unreachable, queueing locally? Risky for withdrawals; probably no.
6. **Signed-document storage size** — **resolved by FR-019 + STORY-171**: 2 MB cap × ~50 transactions/teller/day × 10 tellers × 90 hot days ≈ **9 GB hot bytea per branch**, capped (older rows tier to encrypted file storage). Cold tier grows linearly but is on cheap storage.

---

## Success Metrics

- Average deposit transaction time: < 60 seconds
- Average withdrawal transaction time: < 90 seconds (including identity verification)
- End-of-day cash-up completion: < 5 minutes per teller
- Zero unauthorized withdrawals (all flagged by audit)
- 100 % of withdrawal transactions have an identity-verified record with supervisor approval where required
