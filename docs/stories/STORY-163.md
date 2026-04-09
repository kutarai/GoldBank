# STORY-163: Currency Denomination Registry

**Epic:** EPIC-021 Bank Teller & Vault Client Application
**Priority:** Must Have
**Story Points:** 3
**Status:** Not Started
**Created:** 2026-04-08
**Sprint:** 28

---

## User Story

As a **bank administrator**
I want **to define every legal-tender denomination per currency, including whether each is a note or a coin**
So that **the entire teller and vault subsystem can render denomination grids correctly and validate counts against an authoritative registry**

---

## Description

### Background
Sprint 25 used a hardcoded denomination list as a placeholder. This story replaces it with a real database-backed registry that admins can edit without redeploying. Notes and coins are tracked distinctly so the UI can group them and reports can summarise pieces by type.

### Scope
**In scope:**
- New `currency_denominations` table in `bank` schema
- New EF entity `CurrencyDenomination` and configuration
- Migration that creates the table and seeds the standard USD and ZWG denominations
- Admin REST CRUD: `GET /api/admin/currency-denominations`, `POST`, `PUT`, `DELETE` (soft via `is_active`)
- Update `DenominationValidationService` (STORY-151) to read from the table instead of the hardcoded map
- Update `GET /api/teller/denominations?currency=...` to read from the table
- New tab in bank-client admin portal "Currency Denominations" for CRUD

**Out of scope:**
- Per-tenant denomination overrides
- Cross-currency exchange

### Schema
```sql
CREATE TABLE bank.currency_denominations (
  id uuid PRIMARY KEY,
  tenant_id text NOT NULL,
  currency varchar(3) NOT NULL,
  face_value numeric(18,4) NOT NULL,
  denomination_type varchar(10) NOT NULL CHECK (denomination_type IN ('Note','Coin')),
  display_order int NOT NULL,
  is_active bool NOT NULL DEFAULT true,
  created_at timestamptz NOT NULL,
  updated_at timestamptz,
  UNIQUE (tenant_id, currency, face_value)
);
```

---

## Acceptance Criteria

- [ ] Migration creates `currency_denominations` with the schema above
- [ ] Migration seeds the standard set:
  - USD: 100, 50, 20, 10, 5, 1 (Notes); 0.50, 0.25, 0.10, 0.05, 0.01 (Coins)
  - ZWG: 200, 100, 50, 20, 10, 5, 2, 1 (Notes); 0.50, 0.25, 0.10, 0.05 (Coins)
- [ ] EF entity and configuration map all columns
- [ ] Admin endpoints work for CRUD with role `super_admin` or `branch_manager`
- [ ] `DenominationValidationService` reads from the table at runtime (with 5-minute cache)
- [ ] `GET /api/teller/denominations?currency=USD` returns the active denominations sorted by `display_order`
- [ ] Bank-client admin "Currency Denominations" tab shows a grid grouped by currency with active/inactive toggles
- [ ] Inactive denominations cannot be used in new transactions (validation rejects)
- [ ] Existing transactions referencing now-inactive denominations remain valid (historical record preserved)

---

## Technical Notes

### Cache invalidation
After admin edits, publish a `CurrencyDenominationsUpdated` event; the validation service handler invalidates its cache.

### EF entity location
`server/UniBank.Core/Modules/BranchCash/Domain/Entities/CurrencyDenomination.cs`

---

## Dependencies

**Prerequisite Stories:** STORY-148, 151

**Blocked Stories:** STORY-164 onwards depend on the registry; UI grids in 155, 156, 157, 167 read from it

---

## Definition of Done

- [ ] Schema, entity, migration, seed, endpoints, UI tab all in place
- [ ] Validation service reads from the table
- [ ] Existing transactions unaffected
- [ ] Code reviewed
- [ ] Merged

---

**This story was created using BMAD Method v6 - Phase 4 (Implementation)**
