# GoldBank System Documentation

Living documentation of the GoldBank platform. Updated as features land; aimed at
a developer joining the project who needs to understand the shape of the system,
not at end-users.

## How to read

Pick your entry point:

| If you want to… | Start here |
| --- | --- |
| Understand the whole system in 10 minutes | [architecture.md](architecture.md) |
| Find an API endpoint or service | [server.md](server.md) |
| Understand the DB schema | [data-model.md](data-model.md) |
| Work on the customer-facing Android app | [mobile.md](mobile.md) |
| Work on the back-office admin UI | [bank-client.md](bank-client.md) |
| Work on the branch-teller UI | [bank-teller.md](bank-teller.md) |
| Understand card switching / EFT | [switch.md](switch.md) |
| Run the system locally / debug containers | [operations.md](operations.md) |

## The system in one paragraph

GoldBank is a **multi-tenant, multi-currency core banking platform** with five
front-ends: a customer **mobile app** (Android), a back-office **bank-client**
admin web app, a branch-counter **bank-teller** web app, an admin **Blazor**
console, and a **synergy-switch** that bridges POS terminals and national
payment networks (Zimswitch/ISO 8583). A single **.NET gateway** sits in the
middle exposing **gRPC** (mobile, switch) and **REST** (web admin, teller)
APIs over the same module backbone. State lives in **PostgreSQL** (multi-tenant
schemas) and **Redis** (sessions, OTPs, rate limits). Long-running work and
cross-module side-effects flow through a **Wolverine** in-process message bus
with an outbox table; SMS / push notifications are dispatched by a separate
**Notifications** service. AI features (OCR, fraud explanations, chat) call a
local **Ollama** server running Qwen3-VL.

## Two products you should know about

Beyond standard banking (accounts, transactions, cards, loans, KYC), two
product surfaces are worth calling out:

- **Asset Custody** — customers deposit physical gold/silver/platinum into
  trusted vault facilities ("deposit houses"). Each deposit is registered as
  an `Asset`, valued periodically, and can be used as **collateral** on a
  loan. Custody is **person-scoped** (a `Customer` aggregate, not an
  `Account`) so a deposit doesn't pick a currency. See
  [`modules/asset-custody`](#asset-custody) in [server.md](server.md) and the
  Asset Custody section of [bank-client.md](bank-client.md).
- **Ekub** — a community savings + lending product modelled on traditional
  rotating savings clubs. Members pool monthly contributions, members may
  borrow against the pot subject to a member vote, and the treasurer
  confirms disbursement. Interest can optionally be charged only on the
  portion of a loan that exceeds a borrower's own contributions. See
  [`modules/ekub`](server.md#ekub) in [server.md](server.md) and the Ekub
  feature in [mobile.md](mobile.md).

## Code map (where does what live)

| Path | What | Stack |
| --- | --- | --- |
| `server/GoldBank.Gateway/` | REST + gRPC entry point | .NET 10 / Kestrel |
| `server/GoldBank.Core/Modules/<Name>/` | Domain logic per module | .NET / EF Core |
| `server/GoldBank.Migrator/` | EF migrations + demo seeder + CLI jobs | .NET console |
| `server/GoldBank.Notifications/` | SMS + FCM push dispatcher | .NET / Wolverine |
| `server/GoldBank.Reporting/` | Operational reports | .NET / gRPC |
| `server/GoldBank.Protos/Protos/` | Proto definitions (gRPC contracts) | proto3 |
| `switch/` | SynergySwitch (POS/ATM EFT switch) | .NET / gRPC + ISO 8583 TCP |
| `admin/GoldBank.Admin/` | Compliance / system-config console | Blazor Server |
| `bank-client/` | Back-office operations web app | React + Vite + MUI |
| `bank-teller/` | Branch teller / supervisor / vault web app | React + Vite + MUI |
| `mobile/` | Customer mobile app | Kotlin Multiplatform + Compose |
| `hsm/` | Mock HSM for PIN/card crypto | .NET |
| `nfc-test-app/` | Standalone NFC HCE diagnostic app | Kotlin / Android |
| `docs/` | Documentation (you are here) | Markdown |
| `scripts/` | One-off SQL seeds + PowerShell jobs | SQL / PowerShell |
| `docker-compose.yml` | Container orchestration | Compose v3 |

## Conventions

- **Branches**: trunk-based-ish; `main` is the only long-lived branch on
  `origin`. Feature work goes in topic branches off `main`, squash-merged.
- **Commits**: conventional-commits style (`feat:`, `fix:`, `chore:`, etc.)
  with a one-line subject and a body when the diff is non-trivial.
- **Tests**: a `tests/` project tree exists for .NET; current coverage is
  patchy. Mobile/web have no unit tests yet (acknowledged debt).
- **Tenancy**: every persisted entity carries a `tenant_id`. The default
  tenant for local dev is the string `"goldbank"`. See
  [data-model.md](data-model.md#multi-tenancy).
- **Money on the wire**: amounts are sent as **decimal strings** in proto
  messages (`"50.00"`) and wrapped in a `Money { amount, currency }` value.
  Avoid floating-point in domain logic.
- **IDs**: every aggregate uses a `Guid` PK. Where short, human-typeable IDs
  are useful (admin lists, teller screens) the gateway projects them via
  `ShortId(prefix, guid)` — e.g. `AST-000001`, `LOAN-043521`.

## Historical context

Older timestamped docs at the top of `docs/` (e.g.
`architecture-goldbank-2026-02-24.md`, `product-brief-goldbank-2026-02-24.md`,
`prd-goldbank-2026-02-24.md`, `goldbank-system-overview.md`) describe the
platform's earlier shape and are kept as historical reference. Anything
in this `docs/system/` subdirectory is the current source of truth.
