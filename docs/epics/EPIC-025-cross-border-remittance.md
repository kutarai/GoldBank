# EPIC-025: Cross-Border Remittance & FX

**Priority:** Should Have
**Estimated Points:** 85
**Sprints:** 31–34
**Dependencies:** EPIC-001, EPIC-016 (Synergy Switch), EPIC-020a (Tariff Engine), EPIC-026 (AML & Sanctions screening — hard prerequisite for compliant settlement)

---

## Business Context

Zimbabwe is a high-volume diaspora remittance market. UniBank can capture both inbound (cash-out / direct credit) and outbound corridors via partner Money Transfer Operators (MTOs) and SWIFT correspondent banks. This epic adds the corridor catalogue, FX quoting, settlement file generation, compliance gates, and customer-facing send/receive flows.

## User Personas

- **Diaspora Sender** — Outside Zimbabwe, sends to a Zimbabwean beneficiary.
- **Domestic Recipient** — Receives funds via mobile, branch, or wallet.
- **Outbound Sender** — Local SME paying foreign suppliers.
- **Treasury** — Manages FX positions, hedges, nostro funding.
- **Compliance** — Screens against sanctions lists, files large-value reports.

---

## Functional Requirements

### FR-001: Corridor Catalogue
- `corridors` table: send country, receive country, send currency, receive currency, partner MTO/correspondent, settlement model (prefunded, deferred), cut-off times, SLA
- Per-corridor min/max amount, fee tier, FX margin

### FR-002: FX Rate Management
- Treasury maintains base rates (mid + buy/sell spread) per currency pair
- Customer-facing rate = mid ± margin per corridor
- Rates time-stamped, history retained for dispute resolution
- Optional intra-day refresh from Reuters/Bloomberg adapter (stub)

### FR-003: Inbound Remittance — Direct Credit
- Partner MTO posts payment instruction via REST API or file drop
- System matches beneficiary by phone/account/national ID
- AML screening (EPIC-026) — pass / refer / block
- On pass: credit beneficiary deposit account, post FX gain/loss to GL, notify recipient

### FR-004: Inbound Remittance — Cash Pickup
- Recipient appears at any branch with PIN/reference + ID
- Teller looks up by reference, verifies ID against partner record, pays out cash
- Drawer movement booked via EPIC-021 vault module
- Unclaimed funds returned to MTO after configurable expiry

### FR-005: Outbound Remittance
- Customer initiates in-app or at branch: beneficiary name, address, country, account/IBAN, SWIFT, amount
- Real-time FX quote with rate-lock window
- Compliance gate: source-of-funds questionnaire above threshold
- Funds debited + held in suspense → released on partner ack → posted

### FR-006: Settlement & Nostro Reconciliation
- Per-corridor settlement file generated nightly (ISO 20022 pacs.008 / partner format)
- Nostro statement parsed and matched to outbound queue
- Breaks raised as exceptions for Treasury investigation

### FR-007: Compliance Hooks
- Hard gate on EPIC-026: every transaction screened for sanctions, PEP, adverse media
- CTR (Currency Transaction Report) auto-flagged above threshold
- STR (Suspicious Transaction Report) initiated by Compliance from workbench

### FR-008: Customer Communications
- Send confirmation with reference + tracking link
- Status updates at every leg (initiated → in transit → delivered / failed)
- Receipt PDF with reference, exchange rate, fees, beneficiary name

### FR-009: Refunds & Recalls
- Customer-initiated cancel within partner-defined window
- Recall request via partner channel; reversal posted on confirmation
- Failed-delivery auto-refund after expiry

### FR-010: Reporting & Limits
- Per-customer monthly send limits (RBZ regulation)
- Per-corridor volume + revenue dashboards
- Regulatory submission templates

---

## Non-Functional Requirements

- All FX margins auditable, no manual override without dual approval
- Idempotent partner integration: every remittance has a corridor-unique reference
- Timezone correctness across send/receive countries
- 7-year retention on remittance records

## Out of Scope

- Crypto rails
- Bulk payroll remittance for corporates (separate epic)
- Domestic FX trading desk (treasury epic)
