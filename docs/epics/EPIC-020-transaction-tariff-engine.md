# EPIC-020: Transaction Tariff Engine — Fees, Commissions & Tax

**Created:** 2026-03-24
**Priority:** Must Have
**Status:** In Progress
**Estimated Points:** 21
**Estimated Stories:** 3

---

## Overview

Introduce a unified **TariffEngine** that calculates customer fees, merchant/agent commissions, and statutory taxes (IMTT) for all transaction types. Currently fees are hardcoded in individual handlers with no customer charges on cash-in/cash-out and no tax calculation anywhere.

## Goal

Every financial transaction must transparently apply:
1. **Customer Fee** — charged to the customer for using the service
2. **Agent/Merchant Commission** — earned by the agent (cash-in/out) or charged to the merchant (POS purchase)
3. **Tax (IMTT)** — 2% Intermediated Money Transfer Tax on the transaction amount, as required by ZIMRA

## Tariff Schedule

| Transaction Type | Customer Fee | Agent/Merchant Commission | IMTT Tax |
|-----------------|-------------|--------------------------|----------|
| Cash-In | 1.0% (min $0.50) | 1.5% base, 1.0% >$10K (earned by agent) | 2% of amount |
| Cash-Out | 1.5% (min $1.00) | 2.0% base, 1.5% >$10K (earned by agent) | 2% of amount |
| POS Purchase (NFC) | 0.5% | 1.5% (charged to merchant as discount rate) | 2% of amount |
| POS Purchase (QR) | 0.3% | 1.0% (charged to merchant as discount rate) | 2% of amount |

## Money Flow

### Cash-In (Agent Deposit)
```
Customer receives: amount
Customer pays: customer_fee + tax
Agent float debited: amount
Agent earns: commission (paid by bank)
Bank revenue: customer_fee + tax - agent_commission
```

### Cash-Out (Agent Withdrawal)
```
Customer debited: amount + customer_fee + tax
Agent float credited: amount
Agent earns: commission (paid by bank)
Bank revenue: customer_fee + tax - agent_commission
```

### POS Purchase
```
Customer debited: amount + customer_fee + tax
Merchant credited: amount - merchant_commission
Bank revenue: customer_fee + tax + merchant_commission
```

## Stories

### STORY-084: TariffEngine Service
**Points:** 8
Create `TariffEngine` service that calculates fees, commissions, and taxes for all transaction types. Configurable per tenant via `tenant_fee_configs` table (already exists in schema). Returns a `TariffBreakdown` record with customer_fee, commission, tax, total_customer_debit, and merchant_credit.

### STORY-085: Apply Tariff to Cash-In and Cash-Out
**Points:** 8
Update `CashInHandler` and `CashOutHandler` to use `TariffEngine`. Customer now pays fee + tax. Transaction records include fee and tax fields. Proto responses updated with fee/tax breakdown.

### STORY-086: Apply Tariff to POS Purchases
**Points:** 5
Update `NfcPaymentHandler` and `QrPaymentHandler` to use `TariffEngine`. Merchant receives amount minus discount rate commission. Customer pays fee + tax. Proto responses include tax field.

## Dependencies
- Existing `tenant_fee_configs` table (already in DB migration)
- Existing `CommissionEngine` (will be replaced by `TariffEngine`)
