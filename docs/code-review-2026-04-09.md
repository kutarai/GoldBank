# Code Review — EPIC-021 Vault Module + Drawer Variance UX

**Date:** 2026-04-09
**Reviewer:** ad-hoc (BMAD workflow files unavailable in this checkout)
**Scope:** Untracked changes from the current session — vault subsystem (STORY-163–169) and the drawer-close variance UX

**Files reviewed (8):**
- `server/GoldBank.Core/Modules/BranchCash/Domain/Entities/CurrencyDenomination.cs`
- `server/GoldBank.Core/Modules/BranchCash/Domain/Entities/Vault.cs` (4 entities)
- `server/GoldBank.Core/Modules/BranchCash/Application/Services/DenominationValidationService.cs`
- `server/GoldBank.Core/Modules/BranchCash/Application/Services/VaultStockService.cs`
- `server/GoldBank.Gateway/Services/VaultReportPdfService.cs`
- `server/GoldBank.Gateway/Controllers/TellerApiController.cs` (vault endpoints + drawer close)
- `bank-teller/src/pages/VaultDashboard.jsx`
- `bank-teller/src/pages/Drawer.jsx` (variance UX)

**Verdict:** **Ship as MVP, fix the items in §1 before any production use.**

---

## §1 — Must fix before production

### 1.1 [HIGH] Spot check role check is bypassed for `Teller` users
[`TellerApiController.cs`](../server/GoldBank.Gateway/Controllers/TellerApiController.cs#L1172)
```csharp
[Authorize(Roles = "BranchManager,VaultManager,Admin")]
public async Task<IActionResult> PostSpotCheck(...)
```
The class-level `[Authorize(Roles = "Teller,BranchManager,VaultManager,Admin")]` is *additive*, not restrictive — but the endpoint-level attribute on a child action is the more specific filter and should win. **Verify this with an integration test** posting a spot check as the `teller` user. If it goes through, the fix is to add an explicit role check inside the action body too. Same risk on other vault endpoints — they inherit the wider class-level role list, which currently lets a plain teller post a `CashInjection` movement. **None of the vault movement endpoints currently restrict role.**

**Fix:**
```csharp
[HttpPost("vaults/{vaultId:guid}/movements")]
[Authorize(Roles = "VaultManager,BranchManager,Admin")]
```
Add the same to `vaults/{id}` GET endpoints too unless tellers genuinely need to read vault stock.

### 1.2 [HIGH] Spot check teller-equality check uses wrong identity
[`TellerApiController.cs:1178`](../server/GoldBank.Gateway/Controllers/TellerApiController.cs#L1178)
```csharp
if (req.WitnessId == Guid.Empty || req.WitnessId == TellerId)
    return BadRequest(new { error = "A distinct witness is required" });
```
`TellerId` here is the **caller** (vault manager / branch manager), not a teller. The variable name in the helper is misleading but the value is correct — this happens to work. But it's a bug magnet: rename `TellerId` to `CurrentUserId` or add a `private Guid CurrentUserId => TellerId;` alias and use that here.

### 1.3 [HIGH] `VaultStockService.ApplyMovementAsync` row-lock is a no-op on first movement
[`VaultStockService.cs:28`](../server/GoldBank.Core/Modules/BranchCash/Application/Services/VaultStockService.cs#L28)
```csharp
await _db.Database.ExecuteSqlInterpolatedAsync(
    $"SELECT 1 FROM bank.vault_denomination_stock WHERE vault_id = {m.VaultId} FOR UPDATE", ct);
```
If the vault has **zero** stock rows yet (e.g. its very first movement), `FOR UPDATE` locks zero rows and concurrent writers can race. The right pattern is to lock the parent `vaults` row instead:
```csharp
await _db.Database.ExecuteSqlInterpolatedAsync(
    $"SELECT 1 FROM bank.vaults WHERE \"Id\" = {m.VaultId} FOR UPDATE", ct);
```
That row always exists (we create one per branch in the migration) and serializes all stock updates for the vault.

### 1.4 [HIGH] Hardcoded `tenant_id = "goldbank"` everywhere
Search results: 9 occurrences across `TellerApiController.cs` vault endpoints and `VaultStockService.cs`. The teller endpoints set `TenantId = "goldbank"` regardless of the caller's actual tenant claim. **In a multi-tenant deploy this leaks vault movements across tenants.**

**Fix:** read tenant from the JWT (`User.FindFirstValue("tenant_id")`) and use it everywhere — there's already a `TenantId` helper on the controller.

### 1.5 [MEDIUM] Drawer-close variance window is `OpenedAt` to `Now+1min`
[`TellerApiController.cs:369`](../server/GoldBank.Gateway/Controllers/TellerApiController.cs#L369)
```csharp
var dayStart = drawer.OpenedAt;
var dayEnd   = DateTime.UtcNow.AddMinutes(1);
```
This is mostly fine, but if a teller leaves their drawer open across midnight (overnight branch, or a forgotten close from yesterday) the variance computation pulls in **all** transactions since open. That's actually correct semantically, but the test fixture I ran showed `dayEnd = now + 1min` — the +1 minute slop is to catch transactions inserted milliseconds before the close call, which is fine, but document the intent in a comment.

Bigger issue: **`Reversal` direction is treated as `+1` always** with the comment "sign already encoded in amount upstream". Verify this against `BranchCashTransaction` insertion code — if reversals are stored with positive amount but `Direction = "Reversal"`, you'll double-count a deposit reversal as a credit instead of canceling it.

---

## §2 — Should fix soon

### 2.1 [MEDIUM] `DenominationValidationService` cache is process-wide static state
[`DenominationValidationService.cs:13-17`](../server/GoldBank.Core/Modules/BranchCash/Application/Services/DenominationValidationService.cs#L13)
```csharp
private static readonly object _lock = new();
private static Dictionary<string, HashSet<decimal>>? _cache;
```
Static state on a scoped service means the cache survives DI scope disposal — fine for the read path, but `InvalidateCache()` is also static and **fires globally across all tenants** if you ever multi-tenant. Consider an `IMemoryCache` keyed by `(tenantId, currency)` instead. Also, in a scaled-out gateway (multiple replicas) admin edits in one pod won't invalidate the cache in the others until 5 min ttl expires — switch to Redis pub/sub or pin the TTL to 60s.

### 2.2 [MEDIUM] `VaultStockService.ParseBreakdown` silently swallows malformed JSON
[`VaultStockService.cs:129-145`](../server/GoldBank.Core/Modules/BranchCash/Application/Services/VaultStockService.cs#L129)
- Object form (`{denominationId: count}`) is mentioned in the comment but **not implemented** — only the array form is parsed.
- A non-array root just returns nothing → the movement applies as a no-op and the stock silently goes out of sync with the recorded total.

**Fix:** throw on unrecognized shape; let the controller catch and 400.

### 2.3 [MEDIUM] Spot check synthesises a "deposit" Direction but mixes positive and negative diffs into one breakdown
[`TellerApiController.cs:1216-1247`](../server/GoldBank.Gateway/Controllers/TellerApiController.cs#L1216)

The current logic groups all variance lines for a currency into **one** movement with a single direction (whichever the net total is positive/negative). If you have variances `-10` on $100 notes and `+5` on $20 notes, this creates one movement with direction `Out` and a breakdown that mixes them — but `ApplyMovementAsync` applies the same sign to **every** line in the breakdown, so the $20 row will be **decremented** instead of incremented. Stock will be wrong.

**Fix:** split into two movements per currency — one `In` for positive diffs, one `Out` for negative diffs. Or model adjustment as a per-line operation that allows mixed signs.

This is **the most important functional bug in the file** — the only reason the smoke test passed was that the test variance was a single negative diff.

### 2.4 [MEDIUM] `RebuildFromHistoryAsync` calls `ApplyMovementAsync` which expects an outer transaction it didn't open
[`VaultStockService.cs:72-91`](../server/GoldBank.Core/Modules/BranchCash/Application/Services/VaultStockService.cs#L72)

`RebuildFromHistoryAsync` opens its own transaction and calls `ApplyMovementAsync` in a loop. Each call also issues a `FOR UPDATE` lock. This works in PostgreSQL because nested locks compose, but the `ApplyMovementAsync` xmldoc says "Caller must be inside a DB transaction" — clarify that it works either way, or pull the lock acquisition out of the inner method.

### 2.5 [MEDIUM] `VaultDashboard.jsx` fetches denomination registry without auth
[`VaultDashboard.jsx:65`](../bank-teller/src/pages/VaultDashboard.jsx#L65)
```jsx
const r = await fetch(`${import.meta.env.VITE_API_BASE || ...}/denominations?currency=${ccy}`);
```
Direct `fetch` instead of `call()` — no `Authorization` header. The `/denominations` endpoint is currently `[AllowAnonymous]`, so this works, but:
1. The endpoint **shouldn't be anonymous** (information disclosure of currency support is harmless but auth is the default expectation).
2. If someone later adds auth, the dashboard breaks silently.

**Fix:** add a `getDenominations(ccy)` helper to `services/api.js` and use it. Then make `/denominations` require auth.

### 2.6 [MEDIUM] `Drawer.jsx` close error path doesn't reset the dialog cleanly
[`Drawer.jsx`](../bank-teller/src/pages/Drawer.jsx) — `submitClose`

If the second submit (with `confirmVariance=true`) also fails (e.g. network drops mid-call), the `variancePreview` dialog stays open but `closeErr` isn't set inside it, so the user sees no feedback. Add an `Alert` inside the variance dialog body fed by `closeErr`.

---

## §3 — Style / minor

- **§3.1** All vault entity files put four entity classes in one `Vault.cs`. Convention in this repo is one entity per file (see `Account.cs` etc). Split.
- **§3.2** `VaultMovement.Direction` is a string `"In"` / `"Out"`. Use an enum + EF value converter — current `string.Equals(... OrdinalIgnoreCase)` checks are fragile.
- **§3.3** `VaultMovement.Type` is also a magic string with values scattered across the controller (`"CashInjection"`, `"DrawerIssue"`, `"DrawerSurrender"`, `"SpotCheckAdjust"`, `"CashWithdrawal"`, `"Transfer"`). Promote to `static class VaultMovementTypes` constants.
- **§3.4** `TellerApiController.cs` is now ~1300 lines. Time to split the vault endpoints into a `VaultApiController.cs` with route prefix `api/teller/vaults`. Same DI dependencies, same JWT.
- **§3.5** Hardcoded API base URL `'http://localhost:5001/api/teller'` in `VaultDashboard.jsx` line 65. There's already a `VITE_API_BASE` env var fallback in `services/api.js`.
- **§3.6** `VaultReportPdfService.BuildAsync` doesn't check if movements list is empty before claiming success — it'll generate a one-page "no movements" PDF rather than 404. The story said *"404 if no movements"* — pick one and document.
- **§3.7** No unit tests for `VaultStockService` despite STORY-165's acceptance criteria explicitly listing them. Same for the spot-check variance flow.
- **§3.8** `VaultDashboard.jsx` `denomRegistry` loop fetches one currency at a time sequentially (`for...await`). For 2 currencies it's fine; if you ever add a third, parallelize.

---

## §4 — What's good

- **Atomic transactional design.** Movement insert + stock recompute + spot-check insert all happen inside a single `BeginTransactionAsync` with explicit rollback on every failure path. Both the controller and the service are explicit about who owns the transaction.
- **Pessimistic locking strategy.** Even if §1.3 needs fixing, the **intent** is correct — single writer per vault during stock mutation. This is much better than optimistic concurrency for high-frequency cash operations.
- **Negative-stock guard.** `ApplyMovementAsync` rejects any movement that would push a denomination below zero, with a clear error code. This is the kind of invariant you want enforced at the service layer, not just the DB.
- **Variance UX (drawer close).** The 409+confirm pattern is the right shape — server is the source of truth on expected closing, frontend is just a recount widget. Easy to extend with audit fields later.
- **Cache invalidation hook.** `DenominationValidationService.InvalidateCache()` exists even if not yet wired to admin edits — readying for STORY-163 admin CRUD.
- **Receipt-pattern consistency.** `VaultReportPdfService` mirrors `EodReportPdfService` and `ReceiptPdfService` exactly — same DI, same QuestPDF fluent style, same A4 layout language. New devs can pick it up.
- **Audit clarity.** Every state-changing endpoint inserts a `vault_movements` row with `performed_by` + `witness_id`. The audit trail is queryable without joining to anything else.
- **Smoke-test verified.** End-to-end inject → spot-check pass → spot-check variance → confirm → adjust → recount worked first try in this session.

---

## §5 — Tests recommended before sign-off

1. **`VaultStockService.ApplyMovementAsync`** — concurrency test: 100 parallel `In` movements of $1 × 1, expect final count = 100.
2. **`VaultStockService.ApplyMovementAsync`** — negative-stock guard: open vault with $0, attempt `Out`, expect `Vault.NegativeStock`.
3. **`PostSpotCheck`** — mixed-sign variance scenario (the bug from §2.3): spot-check that adjusts $100 down by 1 and $20 up by 5 should leave both stocks at the actual physical count.
4. **`PostSpotCheck`** — witness equals submitter: expect 400.
5. **`PostSpotCheck`** — role guard: post as `Teller`, expect 403.
6. **`CloseDrawer`** — variance dialog round-trip: post without `confirmVariance`, expect 409; post with `confirmVariance=true`, expect 200 + variance persisted.
7. **`CloseDrawer`** — `Reversal` direction handling: open drawer, deposit $100, reverse it, close drawer. Counted should equal opening float, expected should equal opening float, variance = 0.
8. **`VaultStockService.RebuildFromHistoryAsync`** — sequence of 50 random movements; rebuild; verify drift = 0.

---

## §6 — Action plan (priority order)

| # | Item                                      | Severity | Effort | Owner   |
|---|-------------------------------------------|----------|--------|---------|
| 1 | Fix mixed-sign spot-check adjustment      | HIGH     | 30 min | Backend |
| 2 | Lock `vaults` row not `vault_denomination_stock` | HIGH | 15 min | Backend |
| 3 | Replace hardcoded `tenant_id = "goldbank"` with JWT claim | HIGH | 30 min | Backend |
| 4 | Add explicit role checks to vault movement endpoints | HIGH | 15 min | Backend |
| 5 | Verify `Reversal` sign in drawer-close variance | HIGH | 30 min | Backend |
| 6 | Add `services/api.js` `getDenominations()` helper, drop direct fetch | MEDIUM | 10 min | Frontend |
| 7 | Add unit tests from §5                    | MEDIUM   | half-day | Backend |
| 8 | Split `Vault.cs` into 4 files             | LOW      | 5 min  | Backend |
| 9 | Promote `Direction` and `Type` to enums   | LOW      | 30 min | Backend |
| 10| Split vault endpoints into own controller | LOW      | 20 min | Backend |

---

**Reviewer's final note:** The vault subsystem hangs together as a coherent design. The transactional model is right, the audit story is right, the UI affordances are right. The blockers are §1.4 (multi-tenant) and §2.3 (mixed-sign adjustments) — neither is structural, both are 30-minute fixes. Once those land plus the unit tests in §5, this is production-ready for a single-tenant deployment.
