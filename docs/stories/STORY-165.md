# STORY-165: Vault Stock Recompute Service

**Epic:** EPIC-021 Bank Teller & Vault Client Application
**Priority:** Must Have
**Story Points:** 3
**Status:** Not Started
**Created:** 2026-04-08
**Sprint:** 28

---

## User Story

As a **vault movement handler**
I want **the `vault_denomination_stock` aggregate to be atomically recomputed inside the same DB transaction as the movement insert**
So that **stock can never drift from the movement ledger and the vault is always self-consistent**

---

## Description

### Background
`vault_denomination_stock` is a materialised aggregate of all `vault_movements` for a vault. It must be updated atomically when a movement is inserted, so the UI never sees a partial/incorrect stock state. A separate "rebuild from history" command is also exposed for disaster recovery and audit verification.

### Scope
**In scope:**
- New `VaultStockService` with two methods:
  - `ApplyMovementAsync(VaultMovement movement, DbTransaction tx)` — increments/decrements stock per denomination based on movement direction
  - `RebuildFromHistoryAsync(Guid vaultId, CancellationToken ct)` — wipes the stock for a vault and replays every movement in order
- Called from every command handler that inserts a `vault_movements` row (float-out, surrender, spot-check adjustment, injection, withdrawal-to-HQ)
- All inside the same DB transaction; rollback on any failure
- Concurrency: row-level pessimistic lock on `vault_denomination_stock` rows for the vault during the update to prevent races
- Unit tests verify monotonic correctness over a sequence of movements
- Verification command `VerifyVaultStockCommand` admin-only — reports any drift between stock and replayed history

**Out of scope:**
- Endpoints (STORY-166 wires them to the service)

---

## Acceptance Criteria

- [ ] `ApplyMovementAsync` updates stock atomically inside the caller's transaction
- [ ] An `In` movement increments the count for each denomination by the breakdown's count
- [ ] An `Out` movement decrements; if any denomination would go negative, the operation fails (rollback)
- [ ] If a `vault_denomination_stock` row doesn't exist for a `(vault, denomination)` pair, it's created with the initial count
- [ ] Concurrent calls to `ApplyMovementAsync` for the same vault are serialised by row-level locks (no race causing duplicate counts)
- [ ] `RebuildFromHistoryAsync` produces stock that exactly matches the cumulative movement history
- [ ] `VerifyVaultStockCommand` reports drift if any (used in CI smoke tests)
- [ ] Unit tests cover: float-out → surrender round-trip, multiple movements, rejection of negative stock
- [ ] Integration test runs 100 random movements and verifies the rebuild matches the running aggregate

---

## Technical Notes

### Locking strategy
```csharp
// Pessimistic row-level lock for the duration of the txn
await _db.Database.ExecuteSqlInterpolatedAsync(
    $"SELECT 1 FROM bank.vault_denomination_stock WHERE vault_id = {vaultId} FOR UPDATE");
```

### Apply method skeleton
```csharp
public async Task ApplyMovementAsync(VaultMovement m, CancellationToken ct) {
    foreach (var line in m.DenominationBreakdown) {
        var stockRow = await _db.VaultDenominationStock
            .FirstOrDefaultAsync(s => s.VaultId == m.VaultId && s.DenominationId == line.DenominationId, ct);
        if (stockRow == null) {
            if (m.Direction == Direction.Out) throw new InvalidOperationException("...");
            stockRow = new VaultDenominationStock { VaultId = m.VaultId, DenominationId = line.DenominationId, Count = 0 };
            _db.VaultDenominationStock.Add(stockRow);
        }
        stockRow.Count += (m.Direction == Direction.In ? line.Count : -line.Count);
        if (stockRow.Count < 0) throw new InvalidOperationException("Insufficient stock");
        stockRow.UpdatedAt = DateTime.UtcNow;
    }
}
```

---

## Dependencies

**Prerequisite Stories:** STORY-164

**Blocked Stories:** STORY-166

---

## Definition of Done

- [ ] Service implemented with both methods
- [ ] Locking and rollback verified
- [ ] Unit + integration tests pass
- [ ] Code reviewed
- [ ] Merged

---

**This story was created using BMAD Method v6 - Phase 4 (Implementation)**
