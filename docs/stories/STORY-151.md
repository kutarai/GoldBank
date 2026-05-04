# STORY-151: Denomination Validation Engine

**Epic:** EPIC-021 Bank Teller & Vault Client Application
**Priority:** Must Have
**Story Points:** 2
**Status:** Not Started
**Created:** 2026-04-08
**Sprint:** 25

---

## User Story

As a **server-side handler processing a cash transaction**
I want **a single shared validator that checks a denomination breakdown sums to the requested amount and only uses currently active denominations for the currency**
So that **deposits, withdrawals, vault float-outs, surrenders, and spot checks all enforce the same physical-cash invariants without duplicated logic**

---

## Description

### Background
Every cash-handling endpoint takes a `denomination_breakdown_json` plus a `total_amount`. Without a shared validator, each endpoint will reinvent (and bug) the same checks. This story extracts a single `DenominationValidationService` that all command handlers call.

### Scope
**In scope:**
- New service `DenominationValidationService` in `GoldBank.Core/Modules/BranchCash/Application/Services/`
- Validation rules:
  - Σ (denom × count) must equal the stated total amount (to 2 decimal places)
  - Each denom must exist in the `currency_denominations` table for the given currency
  - Each denom must be `is_active = true` at the time of the transaction
  - All counts must be non-negative integers
  - At least one row with count > 0 (no empty breakdowns)
- Returns `Result<DenominationValidationResult>` with error codes the handlers can map to gRPC/HTTP responses
- Used by deposits, withdrawals, vault float-out, surrender, spot-check, injection, withdrawal-to-HQ

**Out of scope:**
- The `currency_denominations` table itself (STORY-163 creates it; for Sprint 25 use a hardcoded set as a placeholder, replaced when STORY-163 lands)
- UI denomination grid (STORY-155)

---

## Acceptance Criteria

- [ ] `DenominationValidationService.Validate(currency, totalAmount, breakdown)` returns `Result.Success` if every rule passes
- [ ] Returns `Result.Failure(DenominationErrors.SumMismatch)` when Σ (denom × count) ≠ totalAmount
- [ ] Returns `Result.Failure(DenominationErrors.UnknownDenomination)` when a face value isn't registered for the currency
- [ ] Returns `Result.Failure(DenominationErrors.InactiveDenomination)` when a face value is registered but inactive
- [ ] Returns `Result.Failure(DenominationErrors.NegativeCount)` when any count < 0
- [ ] Returns `Result.Failure(DenominationErrors.EmptyBreakdown)` when no row has count > 0
- [ ] Tolerance for floating-point: comparison uses decimal arithmetic, not float; rounding to 2 dp
- [ ] Service is `sealed class` with no state, registered as a singleton in DI
- [ ] Unit tests cover every error code path AND a happy path with a multi-denomination breakdown summing exactly
- [ ] Used by `RecordDepositHandler`, `RecordWithdrawalHandler` (and the vault handlers in Sprint 28)
- [ ] When Sprint 28 lands STORY-163, the validator switches from hardcoded denominations to the `currency_denominations` table — no API changes required

---

## Technical Notes

### Service signature
```csharp
public sealed class DenominationValidationService
{
    public Result<DenominationValidationResult> Validate(
        string currency,
        decimal totalAmount,
        IReadOnlyList<DenominationLine> breakdown);
}

public sealed record DenominationLine(decimal FaceValue, int Count, string? Type);

public sealed record DenominationValidationResult(
    decimal ComputedTotal,
    int TotalPieceCount,
    Dictionary<DenominationType, int> CountsByType);
```

### Sprint 25 hardcoded denominations (replaced by STORY-163)
```csharp
private static readonly Dictionary<string, decimal[]> _denoms = new() {
    ["USD"] = new[] { 100m, 50m, 20m, 10m, 5m, 1m },
    ["ZWG"] = new[] { 200m, 100m, 50m, 20m, 10m, 5m, 2m, 1m },
};
```

### Error definitions
```csharp
public static class DenominationErrors {
    public static readonly Error SumMismatch         = new("Denom.SumMismatch", "...");
    public static readonly Error UnknownDenomination = new("Denom.Unknown", "...");
    public static readonly Error InactiveDenomination = new("Denom.Inactive", "...");
    public static readonly Error NegativeCount       = new("Denom.NegativeCount", "...");
    public static readonly Error EmptyBreakdown      = new("Denom.Empty", "...");
}
```

---

## Dependencies

**Prerequisite Stories:** STORY-148 (BranchCash module exists)

**Blocked Stories:** STORY-149 (depends on this for validation), 167 (vault screens use it via vault handlers)

---

## Definition of Done

- [ ] Service implemented with the signature above
- [ ] Registered in DI as singleton
- [ ] Unit tests for every error code (5 negative + 1 happy path minimum)
- [ ] Wired into deposit and withdrawal handlers from STORY-149
- [ ] Code reviewed
- [ ] Merged to main

---

**This story was created using BMAD Method v6 - Phase 4 (Implementation)**
