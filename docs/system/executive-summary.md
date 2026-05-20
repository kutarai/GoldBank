# GoldBank — Executive Summary

> Multi-tenant core banking platform with five front-ends, a payments switch,
> AI-powered KYC, and two flagship products: **physical-asset custody** (gold,
> silver, platinum) and **Ekub** community savings groups.

## What it is

A complete retail-banking platform built for a Southern African market. One
gateway exposes everything; five clients consume it; one payments switch
bridges the card-acceptance network.

```
       CUSTOMERS                       BANK STAFF
       ──────────                      ──────────
       📱 Mobile app                   🌐 Bank-client (back-office admin)
                                       🏦 Bank-teller (branch counter)
                                       🛡  Admin console (compliance / config)

                       ▼                ▼
                ┌────────────────────────────┐
                │      GATEWAY (.NET)        │
                │   REST + gRPC, multi-tenant│
                └─────────────┬──────────────┘
                              │
       Cards / ATMs ──► 💳 SynergySwitch  ──► PostgreSQL  +  Redis  +  AI (Ollama)
```

## What it does — at a glance

| Capability | Status |
| --- | --- |
| Customer registration with KYC (national ID + selfie + proof of address) | ✅ live |
| Dual-currency accounts (ZWG + USD) per customer | ✅ live |
| Card issuance with virtual PAN | ✅ live |
| Send money (P2P), pay bills, cash-in/out at agents, QR pay, NFC tap | ✅ live |
| Mobile loans + repayment | ✅ live |
| Merchant onboarding + POS acceptance | ✅ live |
| Fraud monitoring with 5 rule types | ✅ live |
| Card switching (ISO 8583 + ISO 20022) | ✅ live |
| **Physical-asset custody** (gold / silver / platinum, valuation, lending against) | ✅ **flagship** |
| **Ekub** community savings groups (member voting, treasurer-confirmed loans) | ✅ **flagship** |
| AI features (OCR, fraud explanation, in-app chat) via local Qwen3-VL | ✅ live |

## Flagship product 1 — Asset Custody

Customers deposit physical precious-metal assets into **trusted vault facilities
("deposit houses")** that the bank vets. Every deposit is registered as an
asset row with weight, purity, receipt number, and a chain of professional
valuations. Three things make the product distinctive:

1. **Person-scoped, not account-scoped.** Gold doesn't belong to your USD or
   ZWG account — it belongs to *you*. The data model reflects this through a
   `Customer` aggregate.
2. **Collateral lending.** Any asset can back a loan. The teller UI **blocks
   asset withdrawal** when there's an open loan secured against it.
3. **Live spot pricing.** Daily metal prices feed into portfolio valuation;
   members see their custody value updated in their mobile app.

## Flagship product 2 — Ekub

A digital implementation of the rotating savings + lending tradition (called
"Ekub" in some regions, "stokvel" / "chama" elsewhere). Members pool monthly
contributions; any member can borrow against the pot subject to a vote.

| Step | Who | What |
| --- | --- | --- |
| 1 | Chairman | Creates a group (currency, monthly amount, interest rate) |
| 2 | Chairman / Secretary | Invites 2+ more members |
| 3 | Invitees | Accept invitations; group auto-activates at 3 members |
| 4 | All members | Contribute the agreed monthly amount |
| 5 | Treasurer | Confirms each contribution; the pot grows |
| 6 | Any member | Apply for a loan against the pot |
| 7 | Other members | Vote approve/reject (borrower can't vote) |
| 8 | Treasurer | Confirms disbursement once a majority approves |
| 9 | Borrower | Repays in monthly installments; interest grows the pot |
| 10 | All members | See their pro-rata share of the pot in real time |

A configurable toggle lets groups choose to **charge no interest** on loans
that stay within a borrower's own contributions — effectively letting
members borrow their own money interest-free.

## Stack summary

| Layer | Technology |
| --- | --- |
| Server | .NET 10, Kestrel, EF Core 10, Wolverine, gRPC |
| Data | PostgreSQL 18, Redis 7 |
| Switch | .NET 10, ISO 8583 TCP, gRPC |
| AI | Ollama + Qwen3-VL (local, no cloud dependency) |
| Mobile | Kotlin Multiplatform, Jetpack Compose, Koin |
| Web (admin + teller) | React 19, Vite 6, Material UI 6 |
| Compliance console | Blazor Server |
| Orchestration | Docker Compose (Podman-compatible) |

## Operational shape

- **One container per service**, orchestrated by `docker-compose.yml` with
  profile-based subsets (`core`, `ai`, `monitoring`).
- **Single Postgres instance**, two databases (`goldbank` for the bank,
  `synergy_switch` for the switch).
- **Wolverine outbox** for at-least-once event delivery (notifications, audit).
- **Multi-tenant by design** — every aggregate carries a `tenant_id`. One
  gateway can serve many brands.

## Code layout

```
server/         .NET (Gateway, Core, Migrator, Notifications, Reporting)
switch/         SynergySwitch (ISO 8583 / 20022)
mobile/         Kotlin Multiplatform Android app
bank-client/    React back-office admin web
bank-teller/    React branch-counter web
admin/          Blazor compliance / system-config console
hsm/            Mock HSM for PIN crypto
docs/system/    Comprehensive technical documentation (you are here)
```

## Maturity

| Aspect | State |
| --- | --- |
| **Customer-facing flows** (mobile) | Production-ready feature surface; not yet load-tested |
| **Teller flows** (cash + asset deposit/withdrawal) | Real JWT auth, role-gated, audit-logged |
| **Back-office admin** | Functional but logs in via dev-stub seed accounts — **needs real auth before prod** |
| **Card switching** | Functional spec + service skeleton; needs real Zimswitch certification |
| **AI features** | Working locally with Ollama; needs GPU hosting for prod |
| **Tests** | Sparse — patchy unit coverage on .NET; no tests on web or mobile yet |
| **CI/CD** | `Jenkinsfile` + `.gitlab-ci.yml` exist but aren't wired to a runner |

The repository is **demo-complete** — every flow works end-to-end on a
developer laptop with seeded data. Hardening for production (real admin
auth, KYC document object storage, secret rotation, observability stack,
load testing, certification of card networks) is the remaining work.

## Read more

The full technical documentation lives under [docs/system/](README.md):

- [architecture.md](architecture.md) — system map + communication patterns
- [data-model.md](data-model.md) — schema, multi-tenancy, key invariants
- [server.md](server.md) — every module + every endpoint
- [switch.md](switch.md) — SynergySwitch deep-dive
- [mobile.md](mobile.md) — Android app structure + features
- [bank-client.md](bank-client.md) — back-office admin web
- [bank-teller.md](bank-teller.md) — branch teller web
- [operations.md](operations.md) — running, deploying, troubleshooting
