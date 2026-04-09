# STORY-149: Teller Cash gRPC + REST Endpoints

**Epic:** EPIC-021 Bank Teller & Vault Client Application
**Priority:** Must Have
**Story Points:** 5
**Status:** Not Started
**Created:** 2026-04-08
**Sprint:** 25

---

## User Story

As a **bank teller using the bank-teller front-end**
I want **server endpoints for opening/closing my drawer and recording cash deposits and withdrawals**
So that **every counter transaction is persisted as an immutable ledger entry tied to my teller session**

---

## Description

### Background
With the schema in place (STORY-148), the front-end needs server endpoints to drive the teller workflow. This story creates the REST controller (and matching gRPC service for the typed client) covering drawer lifecycle and the core cash transaction commands.

### Scope
**In scope:**
- REST endpoints under `/api/teller/*`
- Equivalent gRPC service `unibank.v1.teller.TellerService` (for future typed clients)
- Command handlers behind each endpoint
- DTOs for request/response
- JWT auth requiring role `teller`, `branch_manager`, or `super_admin`
- Tenant scoping (teller can only act on accounts in their tenant)

**Out of scope:**
- Customer Card endpoint (STORY-150)
- Denomination validation engine (STORY-151)
- Supervisor approval (STORY-152)
- Vault endpoints (STORY-166)
- Signed document upload (STORY-170)

### Endpoints
| Method | Path | Purpose |
|---|---|---|
| POST | `/api/teller/drawer/open` | Begin a new shift with an opening float |
| POST | `/api/teller/drawer/close` | Close the active drawer (handed off in STORY-167 to surrender) |
| GET  | `/api/teller/drawer/current` | Returns the active drawer for the authenticated teller |
| POST | `/api/teller/deposits` | Create a deposit transaction |
| POST | `/api/teller/withdrawals` | Create a withdrawal transaction |
| POST | `/api/teller/transactions/{id}/reverse` | Reverse a same-shift transaction (supervisor approval) |
| GET  | `/api/teller/transactions?date=` | List the teller's transactions for the day |

---

## Acceptance Criteria

- [ ] `POST /api/teller/drawer/open` accepts `{ branchId, openingFloatJson }` and creates a new `TellerDrawerSession` with status `Open`. Returns the new session ID. Fails 409 if a drawer is already open for the same teller on the same date.
- [ ] `POST /api/teller/drawer/close` marks the current drawer as `Closed` and seals `closing_balance_json`. Validates that no `pending_signature` rows remain (placeholder check; STORY-170 wires real check).
- [ ] `GET /api/teller/drawer/current` returns the open drawer (or 404 if none).
- [ ] `POST /api/teller/deposits` accepts `{ accountId, currency, amount, depositorName, denominationBreakdown }`, creates a `transactions` row (`type=cash_in_branch`), creates a `branch_cash_transactions` row, credits the account balance atomically (single DB transaction), and returns the new transaction ID.
- [ ] `POST /api/teller/withdrawals` accepts `{ accountId, currency, amount, denominationBreakdown, identityVerified }`, creates the `transactions` row (`type=cash_out_branch`), creates a `branch_cash_transactions` row with `identity_verified`, debits the account, and returns the transaction ID.
- [ ] Withdrawal returns 422 if `identity_verified` is false.
- [ ] All endpoints return 401 without JWT, 403 if the JWT role is not in (`teller`, `branch_manager`, `super_admin`).
- [ ] Tenant scoping: a teller can only operate on accounts whose `tenant_id` matches the JWT tenant claim. Cross-tenant attempts return 403.
- [ ] All endpoints write an `audit_logs` row with the action (`drawer.open`, `drawer.close`, `cash.deposit`, `cash.withdrawal`, `cash.reverse`).
- [ ] Reversal endpoint requires the original transaction to be from the same `drawer_session_id` and within the same business date. Inserts a compensating `transactions` row + flips the `BranchCashTransaction.reversed_*` columns. Supervisor approval is wired in STORY-152.
- [ ] Integration tests cover happy paths and error cases.

---

## Technical Notes

### Controller location
`server/UniBank.Gateway/Controllers/TellerApiController.cs` — separate file from `AdminApiController` to keep concerns clean.

### Authorization
```csharp
[Authorize(Roles = "teller,branch_manager,super_admin")]
[ApiController]
[Route("api/teller")]
public class TellerApiController : ControllerBase { ... }
```

### Atomicity
Each deposit/withdrawal must be a single DB transaction wrapping:
1. Insert `transactions` row
2. Insert `branch_cash_transactions` row
3. Update `accounts.balance` (and `available_balance`)
4. Insert `audit_logs` row

Use `_db.Database.BeginTransactionAsync()` and roll back on any failure. Idempotency keys not in scope here (mobile cash deposits/withdrawals are inherently single-shot).

### Wolverine event
Publish a `CashTransactionRecorded` domain event so notification service / reporting engine can react.

### gRPC service definition
`server/UniBank.Protos/Protos/teller_service.proto` — RPCs mirror the REST endpoints. Generate Kotlin/C# stubs as part of the build.

---

## Dependencies

**Prerequisite Stories:** STORY-148

**Blocked Stories:** STORY-150, 151, 152, 153–157 (all of Sprint 26)

---

## Definition of Done

- [ ] All endpoints implemented and returning correct status codes
- [ ] Integration tests cover deposit, withdrawal, drawer open/close, reversal happy paths
- [ ] Integration tests cover unauthorized, cross-tenant, and validation failures
- [ ] Audit logs are written for every action
- [ ] Code reviewed and approved
- [ ] Merged to main and gateway image rebuilt

---

**This story was created using BMAD Method v6 - Phase 4 (Implementation)**
