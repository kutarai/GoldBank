# STORY-164: Vault Domain Model + DB Schema

**Epic:** EPIC-021 Bank Teller & Vault Client Application
**Priority:** Must Have
**Story Points:** 5
**Status:** Not Started
**Created:** 2026-04-08
**Sprint:** 28

---

## User Story

As a **platform engineer**
I want **the vault, vault stock, vault movement, and vault spot-check tables and EF entities**
So that **the vault subsystem has a typed, queryable foundation with the right indexes and constraints**

---

## Description

### Background
The vault subsystem (FR-012 to FR-018) requires four new tables. This story creates them all together with the EF mappings, indexes, FKs, and a one-time data migration that auto-creates a Vault row for every existing branch.

### Scope
**In scope:**
- `vaults` table (1:1 with branches)
- `vault_denomination_stock` table (materialised aggregate)
- `vault_movements` table (append-only ledger)
- `vault_spot_checks` table
- New `admin_users.role` value `vault_manager` (allowed in code; no schema change)
- EF entities, configurations, and DbContext additions
- Migration that creates all four tables
- Seed step that creates a `Vault` row for every existing branch with default `spot_check_cron = "daily"` at `09:00`
- Domain event handler on `BranchCreated` that auto-creates a Vault for any new branch
- Snapshot file regenerated

**Out of scope:**
- Stock recompute logic (STORY-165)
- Endpoints (STORY-166)
- UI (STORY-167)

### Schema highlights
```sql
CREATE TABLE bank.vaults (
  id uuid PRIMARY KEY,
  branch_id uuid UNIQUE NOT NULL REFERENCES bank.branches(id),
  name varchar(100) NOT NULL,
  vault_manager_id uuid REFERENCES bank.admin_users(id),
  spot_check_cron varchar(100) NOT NULL DEFAULT 'daily',
  last_spot_check_at timestamptz,
  last_spot_check_result varchar(20) NOT NULL DEFAULT 'NotYet',
  is_active bool NOT NULL DEFAULT true,
  tenant_id text NOT NULL,
  created_at timestamptz NOT NULL,
  updated_at timestamptz
);

CREATE TABLE bank.vault_denomination_stock (
  id uuid PRIMARY KEY,
  vault_id uuid NOT NULL REFERENCES bank.vaults(id),
  currency varchar(3) NOT NULL,
  denomination_id uuid NOT NULL REFERENCES bank.currency_denominations(id),
  count int NOT NULL DEFAULT 0,
  updated_at timestamptz NOT NULL,
  UNIQUE (vault_id, denomination_id)
);

CREATE TABLE bank.vault_movements (
  id uuid PRIMARY KEY,
  vault_id uuid NOT NULL REFERENCES bank.vaults(id),
  type varchar(30) NOT NULL,
  direction varchar(5) NOT NULL,
  currency varchar(3) NOT NULL,
  total_amount numeric(18,2) NOT NULL,
  denomination_breakdown_json jsonb NOT NULL,
  teller_id uuid REFERENCES bank.admin_users(id),
  drawer_session_id uuid REFERENCES bank.teller_drawer_sessions(id),
  performed_by uuid NOT NULL REFERENCES bank.admin_users(id),
  witness_id uuid REFERENCES bank.admin_users(id),
  reference varchar(30),
  notes varchar(1000),
  receipt_pdf_path varchar(500),
  tenant_id text NOT NULL,
  created_at timestamptz NOT NULL,
  updated_at timestamptz
);

CREATE TABLE bank.vault_spot_checks (
  id uuid PRIMARY KEY,
  vault_id uuid NOT NULL REFERENCES bank.vaults(id),
  performed_by uuid NOT NULL REFERENCES bank.admin_users(id),
  witness_id uuid NOT NULL REFERENCES bank.admin_users(id),
  expected_json jsonb NOT NULL,
  actual_json jsonb NOT NULL,
  variance_json jsonb NOT NULL,
  has_variance bool NOT NULL,
  adjustment_movement_id uuid REFERENCES bank.vault_movements(id),
  report_pdf_path varchar(500),
  tenant_id text NOT NULL,
  created_at timestamptz NOT NULL,
  updated_at timestamptz
);
```

---

## Acceptance Criteria

- [ ] All four tables created with the schema above
- [ ] Indexes on `vault_movements.created_at`, `(vault_id, currency, created_at)`, `(teller_id, created_at)`, `(drawer_session_id)`
- [ ] EF entities `Vault`, `VaultDenominationStock`, `VaultMovement`, `VaultSpotCheck` exist
- [ ] EF configurations correctly map all columns and FKs
- [ ] `UniBankDbContext` exposes `DbSet`s for all four
- [ ] Migration includes a data step that inserts a `vaults` row for every existing branch
- [ ] `BranchCreatedHandler` auto-creates a `vaults` row for new branches
- [ ] Migration applies cleanly forward AND backward
- [ ] Snapshot file regenerated

---

## Dependencies

**Prerequisite Stories:** STORY-148 (drawer session referenced as FK), STORY-163 (currency_denominations referenced as FK)

**Blocked Stories:** 165, 166, 167, 168, 169

---

## Definition of Done

- [ ] All entities, configs, migration, snapshot in place
- [ ] Migration runs cleanly on dev DB
- [ ] Auto-creation handler tested
- [ ] Code reviewed
- [ ] Merged

---

**This story was created using BMAD Method v6 - Phase 4 (Implementation)**
