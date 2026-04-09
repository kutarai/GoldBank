# STORY-166: Vault gRPC + REST Endpoints

**Epic:** EPIC-021 Bank Teller & Vault Client Application
**Priority:** Must Have
**Story Points:** 5
**Status:** Not Started
**Created:** 2026-04-08
**Sprint:** 28

---

## User Story

As a **vault manager using the bank-teller front-end**
I want **server endpoints for inspecting stock, performing float-outs, surrenders, spot checks, and HQ injections/withdrawals**
So that **every vault movement is recorded as an immutable ledger entry with denomination-level fidelity**

---

## Description

### Background
With the schema (164) and stock service (165) in place, this story exposes the actual command endpoints. Each endpoint validates inputs, calls `DenominationValidationService`, inserts the movement, and atomically updates stock — all in one DB transaction.

### Scope
**In scope:**
- REST endpoints under `/api/vault/*`
- Equivalent gRPC service `unibank.v1.vault.VaultService`
- All endpoints JWT-protected with role `vault_manager`, `branch_manager`, or `super_admin`
- Tenant scoping: vault manager only sees their own tenant's vaults

### Endpoints
| Method | Path | Purpose |
|---|---|---|
| GET  | `/api/vault/{vaultId}/stock` | Returns per-currency, per-denomination stock |
| GET  | `/api/vault/{vaultId}/movements?from=&to=` | Lists movements |
| POST | `/api/vault/{vaultId}/float-out` | Issue morning float to a teller |
| POST | `/api/vault/{vaultId}/surrender` | Receive teller end-of-day surrender |
| POST | `/api/vault/{vaultId}/spot-check` | Perform a spot check (witness PIN required) |
| POST | `/api/vault/{vaultId}/injection` | Record cash delivery from head office |
| POST | `/api/vault/{vaultId}/withdrawal-to-hq` | Record cash sent back to head office |

---

## Acceptance Criteria

- [ ] `GET /stock` returns per-currency totals plus per-denomination counts and sub-totals
- [ ] `POST /float-out` validates: requesting user is vault manager, target teller belongs to same branch, vault holds sufficient denominations, breakdown is valid; on success creates a `vault_movements` row + atomically creates the `teller_drawer_sessions` row using the same `Reference`
- [ ] `POST /surrender` validates: drawer session belongs to same branch, drawer is `Open`, breakdown is valid; computes expected vs surrendered; if variance ≠ 0 records it and proceeds (vault manager already approved by being the caller); closes the drawer; updates vault stock
- [ ] `POST /spot-check` requires `witnessUsername` + `witnessPin`; validates witness role; computes expected vs actual; if variance ≠ 0 inserts a `SpotCheckAdjustment` movement and updates stock; creates `vault_spot_checks` row with both expected and actual breakdowns
- [ ] `POST /injection` requires both vault manager AND branch supervisor PINs; records movement with `Type=Injection, Direction=In`
- [ ] `POST /withdrawal-to-hq` requires both PINs; records movement with `Type=WithdrawalToHq, Direction=Out`
- [ ] All movements use `VaultStockService.ApplyMovementAsync` inside the same DB transaction
- [ ] All endpoints write `audit_logs` rows
- [ ] Cross-tenant attempts return 403
- [ ] Integration tests cover happy paths AND failures (insufficient stock, wrong PIN, cross-tenant)

---

## Technical Notes

### Controller location
`server/UniBank.Gateway/Controllers/VaultApiController.cs`

### Atomic float-out + drawer creation
```csharp
using var tx = await _db.Database.BeginTransactionAsync();
var movement = new VaultMovement { ... };
_db.VaultMovements.Add(movement);
await _stockService.ApplyMovementAsync(movement, ct);
var drawer = new TellerDrawerSession { OpeningFloatJson = ..., ... };
_db.TellerDrawerSessions.Add(drawer);
movement.DrawerSessionId = drawer.Id;
await _db.SaveChangesAsync(ct);
await tx.CommitAsync();
```

---

## Dependencies

**Prerequisite Stories:** STORY-148 (drawer FK), 163, 164, 165, 152 (PIN re-auth pattern)

**Blocked Stories:** STORY-167, 168, 169

---

## Definition of Done

- [ ] All seven endpoints implemented
- [ ] Atomic transactions verified
- [ ] PIN re-auth on injections / withdrawals / spot checks
- [ ] Audit logging
- [ ] Integration tests pass
- [ ] Code reviewed
- [ ] Merged

---

**This story was created using BMAD Method v6 - Phase 4 (Implementation)**
