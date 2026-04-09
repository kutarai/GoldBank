# EPIC-027: Treasury & Vault Cash Logistics

**Priority:** Should Have
**Estimated Points:** 70
**Sprints:** 32–35
**Dependencies:** EPIC-021 (Bank Teller & Vault — drawer/vault foundations), EPIC-001 (Core Accounts), EPIC-020a (Tariff Engine for CIT cost posting)

---

## Business Context

EPIC-021 delivered teller drawers and branch vaults. What's missing is the layer above: **branch-level cash forecasting, head-office central vault, cash-in-transit (CIT) scheduling between branches and to/from RBZ**, and the treasury view of total cash position by currency and denomination across the bank. Without this, branches over- or under-stock cash, pay excessive insurance, and hold idle reserves that could be earning.

## User Personas

- **Vault Manager (Branch)** — Knows how much cash the branch has, flags re-order requests.
- **Head Office Treasury** — Sees full bank cash position, schedules CIT runs, optimises reserves.
- **CIT Operator (External)** — Cash carrier (G4S, etc.); receives and confirms shipments.
- **Finance** — Posts CIT costs, vault insurance, and reserve interest to GL.

---

## Functional Requirements

### FR-001: Central Vault & Cash Position
- `central_vault` entity at HQ; aggregates cash by currency + denomination
- Real-time roll-up of branch vaults + central vault = bank-wide cash position
- Dashboard: position per currency, per branch, vs target reserve

### FR-002: Cash Forecasting
- 14-day rolling forecast per branch per currency, based on:
  - Historical demand (rolling 90-day moving average)
  - Day-of-week + month-end seasonality
  - Known events (salary days, public holidays)
- Surplus / deficit signals trigger re-order recommendations

### FR-003: Cash-In-Transit (CIT) Orders
- Branch creates CIT request: from → to (branch ↔ HQ ↔ RBZ), currency, denomination breakdown, requested date
- HQ Treasury approves / re-routes / consolidates with other requests
- Order lifecycle: requested → approved → scheduled → in transit → received → reconciled
- Each leg generates a vault movement at both ends; cash in transit sits in a `cash_in_transit` GL account

### FR-004: Carrier Integration (Pluggable)
- Adapter for G4S / SBV / in-house carrier — booking, status webhooks, electronic POD
- Manifest PDF generated with seal numbers, denomination listing, signatures

### FR-005: Reconciliation on Receipt
- Receiving vault counts the bag; system shows expected vs counted
- Variance triggers dispute case routed to Treasury + Carrier liaison
- Successful reconciliation closes the CIT order and posts to GL

### FR-006: RBZ Reserve Management
- Statutory reserve requirement per currency per period
- Daily check vs actual position; alert when within X% of breach
- Reserve top-up CIT to RBZ and draw-down workflows

### FR-007: Insurance & Limits
- Per-branch vault insured limit (cash on premises cap)
- Soft alert at 80%, hard alert at 95% — forces a CIT to HQ
- Per-CIT-shipment value cap (carrier policy)

### FR-008: Denomination Mix Optimisation
- Branch demand profile by denomination (e.g. salary days need more $20s)
- HQ Treasury rebalances mix in CIT plans
- Reduce coin/note shortages flagged at teller drawer level (EPIC-021)

### FR-009: GL Integration
- All vault movements double-entry posted via EPIC-020a
- CIT in-transit account auto-cleared on receipt
- Insurance, carrier fees, RBZ penalty interest accruals all GL-aware

### FR-010: Reporting
- Daily cash position report per branch and bank-wide
- Monthly CIT cost vs budget
- Variance / shrinkage incidents log
- Reserve compliance certificate per period

---

## Non-Functional Requirements

- Every cash movement is double-entry; no orphan vault transactions allowed
- Forecast model retrainable nightly; tunable per branch
- CIT manifests are tamper-evident PDFs (signed, sealed, immutable once dispatched)
- Audit trail ties every banknote bundle to a CIT order, a vault, and ultimately a teller drawer

## Out of Scope

- Bullion / precious metals custody (covered under EPIC-020b)
- ATM cash replenishment scheduling (related but separate; future epic)
- Foreign-currency import licensing workflow (treasury operations side)
