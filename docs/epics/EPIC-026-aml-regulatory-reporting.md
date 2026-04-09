# EPIC-026: AML, Sanctions Screening & Regulatory Reporting

**Priority:** Must Have
**Estimated Points:** 80
**Sprints:** 30–33
**Dependencies:** EPIC-001 (Core Accounts), KYC module, EPIC-017 (AI Vision for ID OCR), all transaction-producing modules (deposits, transfers, cards, loans, remittance)

---

## Business Context

Banks live and die by their compliance posture. UniBank already captures KYC documents but has no transactional monitoring, sanctions screening, PEP checks, or regulatory submission pipeline. This epic delivers the compliance backbone that every other transactional epic depends on, and produces the central-bank and FIU reports required to operate as a licensed bank in Zimbabwe.

## User Personas

- **Compliance Officer (MLRO)** — Reviews alerts, files STRs, owns the overall AML programme.
- **KYC Analyst** — Refreshes due diligence on existing customers; clears name-screen hits.
- **Branch Staff** — Frontline source of suspicion; must be able to flag a customer.
- **Regulator (RBZ, FIU)** — Receives reports; audits the bank's controls.

---

## Functional Requirements

### FR-001: Sanctions & PEP Screening
- Pluggable list providers: OFAC, UN, EU, RBZ local, in-house manual list
- Screening on customer onboarding, beneficiary add, transaction party (sender/recipient)
- Fuzzy matching with score; configurable thresholds for auto-pass / refer / block
- Hits create `screening_hits` rows for analyst review

### FR-002: Customer Risk Rating
- Composite risk score: KYC tier, geography, occupation, expected vs actual activity, PEP status
- Tiers: low / medium / high; periodic refresh schedule (e.g. low = 36 mo, high = 12 mo)
- High-risk customers gated through enhanced due diligence (EDD) workflow

### FR-003: Transaction Monitoring Engine
- Rule engine evaluating every transaction post-settlement against scenarios:
  - Structuring (multiple sub-threshold cash deposits)
  - Velocity spikes vs customer baseline
  - Round-amount cash-intensive activity
  - Geography mismatch
  - Pass-through (in-and-out within X hours)
- Rules configurable in bank-client; backtesting harness on historical data

### FR-004: Alert Workbench
- Per-alert case file: customer, triggering rule, transaction list, risk rating, history
- Statuses: open → in review → escalated → closed (false positive / suspicious / STR filed)
- SLA timers per status; auto-escalation when overdue
- Dual review for high-severity alerts

### FR-005: STR / SAR Filing
- Compliance officer composes Suspicious Transaction Report from alert
- Structured form matching FIU template + free-text narrative + supporting documents
- Submission via FIU portal (manual export) or API when available
- Filed STRs immutable; access logged

### FR-006: CTR (Cash Transaction Report)
- Auto-detect cash transactions ≥ threshold (RBZ: USD 10,000 equivalent)
- Aggregation across same customer same day
- Daily CTR file generated for FIU submission

### FR-007: Periodic KYC Refresh
- Scheduler enqueues customers due for refresh based on risk-tier interval
- Customer prompted in mobile app to re-confirm details + upload fresh ID
- Branch fallback for non-digital customers
- Account auto-restricted if refresh overdue > grace period

### FR-008: PEP & Adverse Media Watch
- Daily delta-load of PEP list updates
- Re-screen all customers against deltas
- Adverse media adapter (stub) — flag customers mentioned in negative news

### FR-009: Regulatory Reports
- RBZ prudential returns (capital adequacy, liquidity, large exposures) — templated XBRL/Excel exports
- Monthly statistical returns (deposits by type, loans by sector)
- Audit-ready snapshots, immutable, signed
- Report generation runs as scheduled job; results stored in `regulatory_reports` table

### FR-010: Audit Log Hardening
- Tamper-evident audit trail for all compliance actions (hash-chained or write-once storage)
- Immutable retention: 7 years minimum
- Auditor read-only role with full historical access

---

## Non-Functional Requirements

- Screening latency on the customer onboarding path < 2 s p95
- Alert engine throughput ≥ all platform transactions in real time
- Zero data loss on rule changes (replayable from immutable transaction log)
- All compliance code paths covered by integration tests against synthetic high-risk fixtures

## Out of Scope

- Tax reporting (FATCA / CRS) — separate epic if international correspondent banking added
- Trade-based money laundering rules (specialised, future)
- AI-driven anomaly detection (Phase 2 enhancement)
