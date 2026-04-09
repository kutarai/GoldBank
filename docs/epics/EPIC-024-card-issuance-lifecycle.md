# EPIC-024: Card Issuance & Lifecycle Management

**Priority:** Should Have
**Estimated Points:** 70
**Sprints:** 30–33
**Dependencies:** EPIC-001 (Core Accounts), EPIC-016 (Synergy Switch for ISO 8583 auth), HSM module, existing `cardPan` field on accounts

---

## Business Context

Customers expect debit cards (physical and virtual) tied to their accounts. The platform already has a `cardPan` column and an HSM module, but no end-to-end card issuance, PIN management, lifecycle, or 3D-Secure flow. This epic delivers virtual card issuance instantly in-app, physical card request/dispatch via branches, PIN management, card controls (block/freeze), and 3DS/CVV2 authorisation for e-commerce.

## User Personas

- **Customer** — Wants a card immediately on account opening, tap-to-pay, manageable from the app.
- **Card Operations** — Manages BIN, plastic stock, embossing batches, dispatch.
- **Risk / Fraud** — Watches authorisations, applies velocity rules, blocks compromised PANs.
- **Compliance** — PCI-DSS, scheme audit.

---

## Functional Requirements

### FR-001: Card Product Catalogue
- `card_products`: scheme (Visa/Mastercard/ZimSwitch), tier (classic/gold/platinum), currency, BIN range, fee schedule, daily/POS/ATM limits
- Virtual vs physical variants

### FR-002: Virtual Card Issuance (Instant)
- Customer requests in mobile app → PAN generated within BIN, expiry, CVV2
- Card details stored encrypted (HSM-wrapped); only last-4 visible after first reveal
- Immediately tokenisable for in-app payments

### FR-003: Physical Card Issuance
- Request from branch (teller) or mobile (delivery to branch)
- Embossing batch file generated (ISO PSE format) for personalisation bureau
- Card dispatch tracking: requested → personalised → in transit → ready for collection → collected
- Activation requires customer presence + ID verification at branch (or PIN-based mobile activation for re-issues)

### FR-004: PIN Management
- Initial PIN set by customer in-app (HSM-side translation, never plain on app server)
- PIN change via app with current-PIN verification
- PIN reset via OTP + biometric (for forgotten PINs)
- PIN block after 3 failed entries; unblock via branch supervisor

### FR-005: Card Controls
- Freeze / unfreeze (temporary, instant effect at switch)
- Permanent block (lost/stolen) — irreversible, triggers re-issue workflow
- Per-channel toggles: ATM, POS, e-commerce, contactless, international
- Per-channel limits override product defaults

### FR-006: Authorisation (via Synergy Switch)
- ISO 8583 0100/0110 message handlers piggyback on EPIC-016 switch
- Stand-in authorisation when core unreachable (configurable per product)
- Velocity rules: max txn count + amount per hour/day per channel

### FR-007: 3D-Secure 2.x
- ACS endpoint for e-commerce challenge
- Frictionless flow when device + behaviour score is high
- Step-up: in-app push notification with biometric approval; SMS OTP fallback

### FR-008: Card Statements & Disputes
- Card transaction history with merchant name, MCC, location
- Dispute initiation in-app: chargeback reason codes, evidence upload
- Dispute case workbench in bank-client (links to existing Disputes page)

### FR-009: Re-issue & Renewal
- Auto-renewal 60 days before expiry (notification + new plastic dispatched)
- Damaged-card re-issue with PAN preservation
- Lost-card re-issue with new PAN, blocking the old one

### FR-010: Compliance & Reporting
- PCI-DSS scope minimisation: PANs only inside HSM-protected vault
- Daily settlement file vs scheme
- Monthly fraud loss + chargeback ratio reporting

---

## Non-Functional Requirements

- All PAN/CVV/PIN operations through HSM; nothing in cleartext on app servers
- Switch authorisation p99 < 500 ms
- Tokenisation API for future Apple Pay / Google Pay
- Audit every card status change

## Out of Scope

- Credit cards (separate product, different risk profile)
- Pre-paid / gift cards
- Apple Pay / Google Pay tokenisation (future epic)
