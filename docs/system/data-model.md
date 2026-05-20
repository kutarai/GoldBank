# Data Model

All persistent state lives in PostgreSQL on **`schema = bank`** (server) and
**`schema = synergy_switch`** (switch). EF Core owns the migrations.

## Multi-tenancy

Every aggregate carries a `tenant_id` column. Demo data uses the text value
`"goldbank"`. Mobile registrations and teller-issued JWTs both carry this
string. The gateway enforces tenant gates in two places:

- **`AdminApiController`** and **`TellerApiController`** — explicit
  `if (account.TenantId != TenantId) return Forbid()` on customer/account
  reads.
- **gRPC services** — read `ITenantProvider.GetTenantId()` from the
  `x-tenant-id` header and either filter by it or store it on writes.

Two tenant column types coexist:

| Column type | Used by | Stored value |
| --- | --- | --- |
| `text` | `accounts`, `customers`, `loans`, `merchants`, `bill_*`, `ekub_*`, `admin_users`, … | `"goldbank"` |
| `uuid` | `assets`, `deposit_houses`, `asset_valuations`, `daily_prices`, `branches` | `00000000-0000-0000-0000-000000000000` (placeholder) |

The mixed shape is historical — newer modules picked `uuid` thinking
`tenant_id` would always be a Guid; older modules were already using text.
There's no functional gap (the asset module fall back to `Guid.Empty` when
`ParseTenantId` fails), but a future cleanup migration could re-type the
uuid columns to text for consistency.

## Customer aggregate (the "person not account" refactor)

A `Customer` is a person. Each customer has **one or more `Account` rows**
— typically two, one per currency (ZWG + USD). Assets and Ekub
contributions are scoped to the customer, **not** the account, so they're
independent of which currency the user is "in" at the moment.

```
   Customer  (1 person, identified by phone within a tenant)
      │
      ├─ Account ZWG    (balances, card PAN, daily limits)
      └─ Account USD    (balances, card PAN, daily limits)
      │
      ├─ Asset 0a..0001  (Krugerrand × 2)         ◄── owned at Customer level
      ├─ Asset 0a..0002  (Gold Eagle × 1)
      └─ Asset 0a..0003  (Maple Leaf × 3)
      │
      └─ EkubMembership in Borrowdale Savings   ◄── group savings
```

This was added in the `20260430231033_AddCustomersAggregate` migration.
Before it, assets pointed at `account_id` and the customer had to pick
"which account does the gold belong to?" — which made no sense for
non-currency-specific assets. The migration creates customers from the
existing `(tenant_id, phone)` pairs in accounts, then re-FKs everything.

## Schema overview

Run `\dt bank.*` in psql for the current 50+ table list. The ones a
developer needs to remember:

### Identity / accounts

| Table | Aggregate | Purpose |
| --- | --- | --- |
| `customers` | `Customer` | Person; FK target for accounts + assets + ekub |
| `accounts` | `Account` | Currency-bound balance container; PIN + daily limits |
| `refresh_tokens` | `RefreshToken` | JWT refresh tokens, one per device |
| `device_transfer_requests` | `DeviceTransferRequest` | OTP-mediated "I lost my phone" flow |
| `admin_users` | `AdminUser` | Internal staff (admin, kyc, fraud, support, branch, teller) |

### Transactions / money movement

| Table | Purpose |
| --- | --- |
| `transactions` | Unified ledger row for every money movement (P2P, BillPay, CashIn/Out, etc.) |
| `transfers` | P2P-specific extension data |
| `payments` | Merchant payment events |
| `payment_tokens` | Tokenised card data |
| `card_transactions` | ATM/POS card events from the switch |
| `bill_payments` | Utility bill payments |
| `bill_providers` | ZESA, TelOne, Econet, etc. |
| `saved_billers` | A user's saved utility accounts |
| `agent_floats` | Agent's cash balance for cash-in/out |
| `agent_commissions` | Agent fee accrual |

### KYC / compliance

| Table | Purpose |
| --- | --- |
| `kyc_documents` | National ID, passport, drivers' licence, selfie, proof-of-address |
| `kyc_verifications` | AI verification result per document |
| `disputes` | Customer-raised disputes against transactions |
| `transaction_disputes` | Per-transaction extension to disputes |
| `fraud_alerts` | Suspicious activity flagged by rules engine |
| `fraud_rules` | Rule definitions (velocity, geo, device, pattern, amount) |
| `audit_logs` | Internal-staff action log |

### Assets / gold custody

| Table | Aggregate | Purpose |
| --- | --- | --- |
| `customers` (FK) | — | Owner of the asset |
| `deposit_houses` | `DepositHouse` | Trusted vault facility (license, contact, trust status) |
| `assets` | `Asset` | A specific physical asset in custody (qty, weight, purity, receipt #) |
| `asset_valuations` | `AssetValuation` | History of professional valuations |
| `daily_prices` | `DailyPrice` | Spot price per gram for gold/silver/platinum |

### Loans

| Table | Purpose |
| --- | --- |
| `loans` | `Loan` aggregate root (principal, rate, tenure, status, **`collateral_asset_ids`** JSON list) |
| `loan_payments` | Repayment installments |

### Ekub (group savings + lending)

| Table | Purpose |
| --- | --- |
| `ekub_groups` | Group (name, currency, monthly amount, loan rate, **`apply_interest_on_contributions`**) |
| `ekub_memberships` | Customer + role (Chairman/Treasurer/Secretary/Member) |
| `ekub_invitations` | Pending or resolved invitations (status, expiry) |
| `ekub_contributions` | A member's contribution; treasurer confirms |
| `ekub_fees` | Monthly bank fee debit per (group, period) |
| `ekub_loans` | A member's loan against the pot |
| `ekub_loan_votes` | One row per voter per loan (borrower excluded) |
| `ekub_loan_repayments` | Split into principal + interest portions |

### Branch / vault operations

| Table | Purpose |
| --- | --- |
| `branches` | Physical branch (name, code, city) |
| `teller_drawer_sessions` | Open/close shifts; cash counts |
| `branch_cash_transactions` | Per-shift cash movements |
| `currency_denominations` | Configurable per-tenant denomination list |
| `vaults` | Branch vault (1 per branch) |
| `vault_denomination_stock` | Notes/coins on hand per vault |
| `vault_movements` | Cash in/out of vault (with denominations breakdown) |
| `vault_spot_checks` | Periodic count vs ledger reconciliation |

### Merchants / acquiring

| Table | Purpose |
| --- | --- |
| `merchants` | POS-accepting business |
| `merchant_documents` | Onboarding docs |
| `merchant_settlements` | Daily settlement batches |

### Configuration

| Table | Purpose |
| --- | --- |
| `system_configs` | Key/value JSON config (PIN policy, fraud thresholds, fee config, etc.) |
| `tenant_branding` | Per-tenant logo, colours, name |
| `tenant_fee_configs` | Per-tenant transaction fees |
| `tenant_transaction_limits` | Per-tenant daily/monthly caps |

### Async / events

| Table | Purpose |
| --- | --- |
| `outbox_messages` | Wolverine outbox pattern — events to dispatch |
| `cache_entries` | Application-side cache (in addition to Redis) |

## Key invariants

These are encoded as unique indexes or check constraints. Worth keeping in
your head:

- **One customer per phone within a tenant**: `ix_customers_tenant_phone_unique`.
- **One account per phone-currency within a tenant**: `ix_accounts_phone_currency_unique`.
- **One asset per (deposit_house, receipt_number)**: `ix_assets_deposit_house_receipt_unique`.
  Stops the same physical receipt being recorded twice.
- **One Ekub membership per (group, customer) when active**:
  `ix_ekub_memberships_group_customer_active_unique` (filtered on `left_at IS NULL`).
- **One Ekub fee per (group, period)**: `ix_ekub_fees_group_period_unique`.
  Makes the monthly-fee job idempotent.
- **One vote per (loan, voter)**: `ix_ekub_loan_votes_loan_voter_unique`.
  Members can change their vote (UPDATE), not stack votes.
- **One pending invitation per (group, phone)**: filtered partial unique
  index on `status = 'Pending'`.
- **Soft-delete via `is_deleted` + `deleted_at`** on `assets`; via
  `deleted_at` IS NOT NULL on accounts/customers/loans. Active queries
  always include the not-deleted filter.

## Outbox / event flow

Domain events declared in `SharedKernel/Events/` are published through
`IMessageBus` (Wolverine). The outbox table guarantees delivery across
restarts:

```
1. EF transaction commits
   ├─ business state change          (accounts.balance -= 100)
   └─ row inserted into outbox       (event payload + status='pending')
2. OutboxProcessor (background svc)
   ├─ polls every 5 s
   ├─ dispatches to in-process handlers
   └─ marks row processed
```

`OutboxProcessor` is started in `Program.cs` at gateway boot. The
notifications service reads from the same bus.

## Migrations

Located at `server/GoldBank.Migrator/Migrations/GoldBankDb/`. Namespace
`GoldBank.Migrator.Migrations.GoldBankDb`. Each migration is a
hand-edited or `dotnet ef migrations add`-generated pair (.cs +
.Designer.cs) plus a single shared `GoldBankDbContextModelSnapshot.cs`
snapshot file.

Recent migrations of note:

| Migration | What it does |
| --- | --- |
| `20260430231033_AddCustomersAggregate` | Creates `bank.customers`, backfills from accounts, re-FKs assets from `account_id` → `customer_id` |
| `20260501063756_AddEkubModule` | 5 tables for Ekub v1 (groups, memberships, invitations, contributions, fees) |
| `20260501070815_AddEkubLoans` | 3 tables for Ekub v2 (loans, votes, repayments) |
| `20260501153434_AddEkubInterestOptOut` | Adds `apply_interest_on_contributions` to ekub_groups |
| `20260430120000_AddLoanCollateralAssetIds` | Adds JSON column to loans for asset-backed lending |

Apply with `dotnet run --project server/GoldBank.Migrator -- --context GoldBankDb`.

## Demo data

`server/GoldBank.Migrator/DemoSeeder.cs` is idempotent and runs when
`--demo` is passed:

```
20 customers + 40 accounts        (Tendai Moyo, Chiedza Mutasa, …)
 8 admin users                    (admin / kyc / fraud / support / loans / compliance / branch / teller; password = username)
 5 loans                          (pending; mix of customers)
 8 merchants                      (OK Supermarket, TM Pick n Pay, etc.)
 5 bill providers                 (ZESA, TelOne, NetOne, Econet, ZINWA)
50 transactions, 15 disputes, 12 fraud alerts, 6 KYC docs, 60 audit logs
 6 branches                       (HQ, Borrowdale, Bulawayo Main, Mutare, Gweru, Vic Falls)
 1 Ekub group (Borrowdale Savings) + 5 memberships + 3 contributions + 1 voting loan
22 system_configs                 (limits, fraud thresholds, OTP TTL, Ekub monthly fees, etc.)
```

Additional one-off SQL seeds in `scripts/`:
- `seed-gold-coins.sql` — 1 deposit house + 5 gold-coin assets for John Moyo
- `seed-asset-valuations.sql` — 10 valuation rows (initial + revalued) so the
  history tab shows % change
- `age-asset-valuations.sql` — backdates 2 valuations so the Valuation Queue
  has overdue items

## Connection string

Local dev: `Host=localhost;Port=5432;Database=goldbank;Username=goldbank;Password=goldbank_dev_password`

Container-to-container: `Host=postgres;Port=5432;...` (same credentials).
Both the gateway and the switch share the same Postgres instance but
different databases (`goldbank` and `synergy_switch` respectively).
