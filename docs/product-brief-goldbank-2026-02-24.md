# Product Brief: GoldBank

**Date:** 2026-02-24
**Author:** wmapundu
**Version:** 1.0
**Project Type:** other (multi-component banking suite)
**Project Level:** 4

---

## Executive Summary

GoldBank is a white-label banking platform targeting the Southern African region, comprising a mobile wallet app, account management backend, national network switching server, and EFT POS terminal manager with HSM. Its key differentiator is NFC-based contactless payments that turn capable smartphones into virtual payment cards, enabling card-present transactions without physical cards. The platform serves the unbanked mass market with zero-cost accounts while creating a self-sustaining merchant agent ecosystem.

---

## Problem Statement

### The Problem

The mass market in Southern Africa remains predominantly cash-dependent. Traditional banking cards are too expensive for this segment due to card issuance costs, monthly fees, and account charges, leaving millions financially excluded from digital payments.

### Why Now?

Modern smartphones — now widespread in the region — come with NFC as a default capability, making it possible to turn every phone into a contactless payment card without the cost of physical card issuance.

### Impact if Unsolved

Cash dependency locks consumers out of remote person-to-person transactions and online commerce entirely, limiting economic participation and growth.

---

## Target Audience

### Primary Users

Unbanked individuals aged 18-35 in Southern Africa. Low-income, tech-savvy smartphone users currently excluded from digital payments.

### Secondary Users

Small informal merchants who accept payments via low-cost EFT POS terminals and also serve as cash-in/cash-out agents for the network.

### User Needs

- Free accounts with no monthly charges or card issuance costs
- Easy payments via NFC contactless and EMV QR Codes for remote transactions
- Low-cost merchant terminals enabling small traders to accept digital payments and act as cash-in/cash-out agents

---

## Solution Overview

### Proposed Solution

A white-label banking platform with four core components: a mobile wallet app (NFC contactless + EMV QR), an account management backend, a national network switching server, and a terminal manager with HSM for EFT POS devices.

### Key Features

- NFC contactless payments (phone as card)
- EMV QR Code payments for remote transactions
- Free account with no monthly charges
- P2P money transfers (including cross-border remittances)
- Bill payments
- KYC / identity verification
- Cash-in/Cash-out via merchant agents
- National payment network switching
- EFT POS terminal management with HSM
- Merchant agent commission system
- Admin/back-office portal
- Reporting/analytics dashboard

### Value Proposition

A two-sided ecosystem where consumers get zero-cost digital payment access and merchants get low-cost terminals while earning commission as cash-in/cash-out agents — creating a self-sustaining network that drives adoption from both sides.

---

## Business Objectives

### Goals

- Onboard 500,000 consumers in year 1
- Build a merchant agent network of 10,000 in year 1
- Reach 5 million transactions per month
- Deploy 3 white-label organisations in the first year

### Success Metrics

- Monthly active users (MAU)
- Monthly transaction volume
- Merchant network growth

### Business Value

- Revenue model: transaction fees, interchange fees, merchant terminal fees
- Projected net earning of 10c per transaction average
- At target volume (5M transactions/month): ~$6M annual net revenue

---

## Scope

### In Scope

- Mobile wallet app (Android first, iOS via KMP)
- NFC contactless payments
- EMV QR Code payments
- Account management backend (free accounts)
- KYC / identity verification
- P2P transfers (including cross-border remittances)
- Bill payments
- National network switching server
- Terminal manager with HSM
- EFT POS merchant terminals
- Cash-in/Cash-out via merchant agents
- Merchant agent commission system
- Admin/back-office portal
- Reporting/analytics dashboard

### Out of Scope

- USSD channel
- Cryptocurrency / digital assets

### Future Considerations

- Lending / credit products
- Savings / interest-bearing accounts

---

## Key Stakeholders

- **Founder/CTO** — High influence. Primary decision maker on product direction and technology choices.
- **Deploying Institutions** — High influence. Banks and fintechs who white-label the platform; their requirements shape the product.
- **Sponsoring Merchant Bank** — Medium influence. Handles regulatory compliance and licensing; their requirements are constraints on the solution.

---

## Constraints and Assumptions

### Constraints

- Time-to-market is the primary constraint — need to launch ASAP (6 month target)
- Team of 4 senior developers
- On-premise infrastructure
- Devices (terminals, HSM) are already compliant

### Assumptions

- NFC-capable smartphones are widespread among target users in Southern Africa
- Sponsoring merchant bank will maintain regulatory approval
- National payment switch connectivity will be available
- Merchants will adopt low-cost terminals due to agent commission incentive

---

## Success Criteria

- Net revenue of at least 5c per transaction average
- 3 white-label organisations deployed in the first year
- 500,000 consumers onboarded in year 1
- 10,000 merchant agents in year 1
- 5 million transactions per month at target volume

---

## Timeline and Milestones

### Target Launch

August 2026 (6 months from project start)

### Key Milestones

- Backend core + account management ready
- Mobile app MVP (Android)
- National switch integration
- Terminal manager + HSM integration
- Pilot with first white-label institution
- Public launch

---

## Risks and Mitigation

- **Risk:** National switch integration delays — connecting to the payment backbone may have dependencies outside your control
  - **Likelihood:** Medium
  - **Mitigation:** Start integration early; use sandbox/test environment; maintain direct relationship with switch operator

- **Risk:** Tight timeline with small team — 4 developers, 6 months, enterprise-scale scope
  - **Likelihood:** High
  - **Mitigation:** Prioritize MVP features; use agile sprints; leverage white-label architecture for reuse

- **Risk:** Merchant adoption — getting 10,000 merchants onboarded requires sales/distribution effort
  - **Likelihood:** Medium
  - **Mitigation:** Incentivize early adopters with commission structure; partner with deploying institutions for distribution

- **Risk:** Regulatory changes — sponsoring bank relationship or compliance requirements could shift
  - **Likelihood:** Low
  - **Mitigation:** Lean on sponsoring merchant bank for compliance; maintain regular communication with regulators

- **Risk:** NFC device fragmentation — not all Android NFC implementations behave the same
  - **Likelihood:** Medium
  - **Mitigation:** EMV QR Code as fallback for devices with NFC issues; test across popular device models in the region

---

## Next Steps

1. Create Product Requirements Document (PRD) - `/prd`
2. Conduct user research (optional) - `/research`
3. Create UX design (if UI-heavy) - `/create-ux-design`

---

**This document was created using BMAD Method v6 - Phase 1 (Analysis)**

*To continue: Run `/workflow-status` to see your progress and next recommended workflow.*
