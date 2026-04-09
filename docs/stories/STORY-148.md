# STORY-148: Branch Cash Domain Model + DB Schema

**Epic:** EPIC-021 Bank Teller & Vault Client Application
**Priority:** Must Have
**Story Points:** 5
**Status:** Not Started
**Created:** 2026-04-08
**Sprint:** 25

---

## User Story

As a **platform engineer**
I want **the database tables and EF Core entities for branch cash transactions and teller drawer sessions**
So that **the rest of the teller backend has a typed, queryable foundation to record physical cash movements**

---

## Description

### Background
The platform currently has no concept of physical-cash transactions distinct from digital transfers. Tellers handle deposits and withdrawals at the counter and the bank needs an authoritative ledger of every banknote and coin that passes through their hands. This story creates the schema, entities, and migrations that all subsequent teller and vault stories build on.

### Scope
**In scope:**
- New `TellerDrawerSession` entity (one row per teller per business day per branch)
- New `BranchCashTransaction` entity (one row per cash deposit / withdrawal / reversal)
- New role value `teller` added to `admin_users.role`
- EF Core configurations mapping the entities to PostgreSQL
- EF migration creating both tables in the `bank` schema with indexes
- Update `UniBankDbContext` to expose `DbSet<TellerDrawerSession>` and `DbSet<BranchCashTransaction>`
- Snapshot file updated

**Out of scope:**
- Endpoints (STORY-149)
- UI (Sprint 26)
- Vault entities (STORY-164)
- Signed-document fields (STORY-170 adds them)

---

## Acceptance Criteria

- [ ] `bank.teller_drawer_sessions` table exists with: `id` (uuid PK), `teller_id`, `branch_id`, `business_date`, `status`, `opening_float_json` (jsonb), `closing_balance_json`, `expected_closing_json`, `variance_json`, `opened_at`, `closed_at`, `closed_by_supervisor_id`, `tenant_id`, `created_at`, `updated_at`
- [ ] `bank.branch_cash_transactions` table exists with: `id`, `transaction_id` (FK to transactions), `drawer_session_id`, `teller_id`, `branch_id`, `account_id`, `direction` (enum: Deposit/Withdrawal/Reversal), `currency`, `amount`, `depositor_name`, `denomination_breakdown_json` (jsonb), `identity_verified`, `supervisor_approver_id`, `supervisor_approved_at`, `receipt_pdf_path`, `reversed_by_transaction_id`, `reversed_at`, `tenant_id`, `created_at`, `updated_at`
- [ ] Indexes: `(teller_id, business_date)` on drawer sessions, `(account_id, created_at)` and `(drawer_session_id)` on cash transactions
- [ ] EF Core entity classes `TellerDrawerSession` and `BranchCashTransaction` exist under `UniBank.Core/Modules/BranchCash/Domain/Entities/`
- [ ] EF Core configurations in `UniBank.Core/Modules/BranchCash/Infrastructure/Persistence/` correctly map every column with the right types
- [ ] `UniBankDbContext` exposes both `DbSet`s
- [ ] Migration runs cleanly forward and backward against an empty DB
- [ ] Migration runs cleanly forward against the current dev DB without affecting existing rows
- [ ] Snapshot file is regenerated and matches the new entity model

---

## Technical Notes

### Module location
Create a new module at `server/UniBank.Core/Modules/BranchCash/` (Domain, Application, Infrastructure folders) — do not put cash entities under Accounts to keep concerns separate.

### Migration filename
`server/UniBank.Migrator/Migrations/UniBankDb/20260415090000_AddBranchCashTables.cs`

### admin_users role
`admin_users.role` is a string column already; no schema change needed — just begin allowing the new value `"teller"` (validated in code where roles are enumerated).

### Foreign keys
- `transaction_id` → `bank.transactions.Id` (existing)
- `account_id` → `bank.accounts.Id`
- `branch_id` → `bank.branches.id`
- `teller_id`, `supervisor_approver_id`, `closed_by_supervisor_id` → `bank.admin_users.Id`
- `drawer_session_id` → `bank.teller_drawer_sessions.id`

Use `OnDelete: Restrict` on all FKs (cash records must never cascade-delete).

### JSON shape — opening_float_json
```json
{ "USD": { "total": 5000.00, "denominations": [...] }, "ZWG": { "total": 10000.00, "denominations": [...] } }
```

### JSON shape — denomination_breakdown_json
```json
[{ "faceValue": 100, "type": "Note", "count": 5 }, { "faceValue": 20, "type": "Note", "count": 3 }]
```

---

## Dependencies

**Prerequisite Stories:** STORY-019 (Branches table — already exists in pending model changes)

**Blocked Stories:** STORY-149, 150, 151, 152 (all of Sprint 25), 164 (vault uses these as cross-FK targets)

---

## Definition of Done

- [ ] Entities, EF config, DbContext, migration committed to a feature branch
- [ ] Migration applies cleanly via `dotnet ef database update` against the dev DB
- [ ] Unit tests verify EF mappings (round-trip a sample row)
- [ ] Snapshot file updated and committed
- [ ] Code reviewed and approved
- [ ] Merged to main

---

**This story was created using BMAD Method v6 - Phase 4 (Implementation)**
