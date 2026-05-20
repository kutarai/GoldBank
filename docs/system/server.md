# Server

The server is a **modular monolith** in .NET 10 — one `GoldBank.Gateway`
executable hosts gRPC + REST endpoints; one `GoldBank.Core` library houses
every business module; one `GoldBankDbContext` owns the schema; one
PostgreSQL database backs the lot.

Solution structure:

```
server/
  GoldBank.SharedKernel/    base classes, value objects, message bus, results
  GoldBank.Core/            domain logic (Modules/<Name>/...)
  GoldBank.Protos/          *.proto definitions → C# stubs at build
  GoldBank.Gateway/         hosting + Kestrel + controllers + Program.cs
  GoldBank.Notifications/   SMS / FCM sidecar
  GoldBank.Reporting/       reports (separate assembly so it can be split off)
  GoldBank.Migrator/        EF migrations + DemoSeeder + CLI utilities
```

## Endpoints at a glance

The gateway exposes two wire surfaces. Both are served by the same Kestrel
host, listening on two different ports.

### gRPC (port 1111 inside container, host :5000)

For mobile and switch traffic. Registered in `Program.cs` via
`app.MapGrpcService<T>()`:

| Service | File | Used by |
| --- | --- | --- |
| `AccountService` | `Modules/Accounts/Grpc/AccountGrpcService.cs` | mobile (register, OTP, PIN, login, refresh, profile, balance, transactions, device-transfer) |
| `PaymentService` | `Modules/Payments/Grpc/PaymentGrpcService.cs` | mobile (QR generate/scan, NFC payment) |
| `TransferService` | `Modules/Transfers/Grpc/TransferGrpcService.cs` | mobile (P2P transfers, history) |
| `BillPayService` | `Modules/BillPay/Grpc/BillPayGrpcService.cs` | mobile (provider list, pay bill, saved billers) |
| `AgentService` | `Modules/Agents/Grpc/AgentGrpcService.cs` | mobile (cash-in / cash-out at agents) |
| `KycService` | `Modules/KYC/Grpc/KycGrpcService.cs` | mobile (upload ID, selfie, POA) |
| `MerchantService` | `Modules/Merchants/Grpc/MerchantGrpcService.cs` | mobile (merchant onboarding + dashboard) |
| `WhiteLabelService` | `Modules/WhiteLabel/Grpc/WhiteLabelGrpcService.cs` | mobile (tenant branding) |
| `LoanService` | `Modules/Loans/Grpc/LoanGrpcService.cs` | mobile (apply, list, detail, repay) |
| `CardTransactionService` | `Modules/CardTransactions/Grpc/CardTransactionGrpcService.cs` | **switch** (POS/ATM authorisations) |
| `AdminService` | `Modules/Admin/Grpc/AdminGrpcService.cs` | (legacy — admin web used to use this; bank-client now uses REST) |
| `AiService` | `Modules/AI/Grpc/AiGrpcService.cs` | mobile (chat, OCR, insights) |
| `ReportingService` | `Reporting/Grpc/ReportingGrpcService.cs` | bank-client reports |
| `AssetService` | `Modules/AssetCustody/Grpc/AssetGrpcService.cs` | mobile (register/list/release assets, valuation history, daily prices) |
| `EkubService` | `Modules/Ekub/Grpc/EkubGrpcService.cs` | mobile (groups, contributions, loans, voting) |

Each `.proto` lives at `server/GoldBank.Protos/Protos/<service>_service.proto`.
The `Grpc.Tools` package regenerates C# stubs on every build.

### REST (port 1112 inside container, host :5001)

For the React web apps. Controllers under `GoldBank.Gateway/Controllers/`:

| Controller | Prefix | Purpose |
| --- | --- | --- |
| `AdminApiController` | `/api/admin` | bank-client read endpoints + admin actions |
| `TellerApiController` | `/api/teller` | bank-teller endpoints (auth-gated) |
| `TellerAuthController` | `/api/teller/auth` | teller JWT login |

REST is read-heavy by design. State changes still mostly happen through
gRPC; REST endpoints either project from the DB (`AdminApi.GetCustomers`)
or wrap an existing module command (`TellerApi.PostAssetValuation` mirrors
`AssetGrpcService.SubmitValuation`).

## Modules

Each module owns one or more **aggregate roots** plus the gRPC service (if
exposed). Listed alphabetically below; the order is not architectural.

### Accounts

`Modules/Accounts/` — authentication, account lifecycle, transactions.

Aggregates: `Account`, `Transaction`, `RefreshToken`, `DeviceTransferRequest`.

Handlers worth knowing:
- `RegisterHandler` — phone-validation, OTP issue. Returns `registrationId`.
- `VerifyOtpHandler` — validates OTP, creates **Customer + dual-currency
  accounts** atomically, issues a temporary token.
- `CreatePINHandler` — sets BCrypt PIN hash on all of a customer's accounts
  (so PIN is one-per-person, not one-per-account), generates a card PAN,
  issues the real JWT pair.
- `AuthenticateHandler` — phone+PIN login. Lockout via `LockoutService` (Redis).
- `RefreshTokenHandler` — exchange refresh token for a new access token.
- `DeviceTransferHandler` — "I have a new phone" — OTP-protected device rebind.

Cross-module: publishes `UserRegistered`, `AccountCreated`,
`PINCreated`, `UserAuthenticated`. Notifications subscribes.

### Customers

`Modules/Customers/` — the person aggregate. One row per `(tenant_id, phone)`.

Tiny module by design — `Customer.cs` is 14 properties. Doesn't expose
gRPC of its own; created by `VerifyOtpHandler` and updated by
`UpdateProfileHandler`.

### Asset Custody

`Modules/AssetCustody/` — physical asset (gold, silver, platinum,
gemstones) custody, valuation, and release.

Aggregates: `Asset`, `DepositHouse`, `AssetValuation`, `DailyPrice`.

Key gRPC RPCs:
- `RegisterAsset` — customer registers an asset deposit (with a receipt #).
- `ListMyAssets`, `GetAssetDetail` — read flow.
- `RequestAssetRelease` — customer asks to withdraw. Marks `PendingRelease`.
- `SubmitValuation` (admin) — record a valuation; if asset was
  `PendingVerification`, promote to `Active`.
- `VerifyCertificate` (admin) — record a certificate-of-authenticity check.
- `ApproveAssetRelease` (admin) — physically release; soft-deletes the row.
- `GetPortfolioValue` — sum across all assets, in ZWG + USD.

Two REST counterparts in `TellerApiController` for the branch flow:
- `POST /api/teller/customers/{accountId}/assets` — teller-mediated deposit.
- `POST /api/teller/assets/{assetId}/withdraw` — release at the counter,
  blocked when the asset is **active collateral** on a loan (see Loans).
- `POST /api/admin/asset-valuations` — admin/valuer submits a valuation.

Pricing logic in `AssetValuationService`: `quantity × weight_grams × purity
× spot_price_per_gram` for metals; falls back to the most recent recorded
valuation amount for `PreciousStone` / `Other`.

### Loans

`Modules/Loans/` — straight-bank loan origination (not Ekub loans, which
live in their own module).

Aggregate: `Loan`, `LoanPayment`.

`Loan.CollateralAssetIds` is a `List<Guid>` serialised to a JSON `text`
column. When a loan is taken with collateral, the asset IDs land here.
`TellerApiController.WithdrawAsset` cross-references this when releasing
assets — see [bank-teller.md](bank-teller.md).

Loans use a `string` status field (not an enum) for historical reasons:
`pending` → `approved` → `disbursed` → `repaying` → `closed` /
`defaulted` / `rejected`. Cleanup to a proper enum is pending.

### Ekub

`Modules/Ekub/` — group savings + lending. See `EkubGroup`,
`EkubMembership`, `EkubInvitation`, `EkubContribution`, `EkubFee`,
`EkubLoan`, `EkubLoanVote`, `EkubLoanRepayment`.

The full RPC surface (15 RPCs):

```
Group lifecycle:     CreateGroup, AssignRole, CloseGroup
Membership:          InviteMember, RevokeInvitation, ListMyInvitations,
                     RespondToInvitation, KickMember
Contributions:       RecordContribution, ConfirmContribution,
                     ListGroupContributions
Reads:               ListMyGroups, GetGroupDetail, GetMyShare
Monthly fee:         ApplyMonthlyFee
Loans (v2):          ApplyForLoan, VoteOnLoan, ConfirmLoanByTreasurer,
                     RecordLoanRepayment, ListGroupLoans, ListMyLoans,
                     GetLoanDetail
```

Key rules baked into the service:

- **Quorum to activate**: a group needs **≥ 3 active members** before it
  flips from `Forming` to `Active`. Auto-promoted on the 3rd acceptance.
- **Role assignment**: only `Chairman` can call `AssignRole`. Chairman /
  Treasurer / Secretary roles are **unique** per group (assigning to a new
  member demotes the prior holder to plain Member).
- **Invitations**: only `Chairman` or `Secretary` can invite. 30-day TTL.
  No duplicate pending invites per (group, phone).
- **Contributions**: any active member submits. Only `Treasurer` can
  confirm. Pending contributions don't count in pot until confirmed.
- **Loan eligibility**:
  - Group must be `Active`.
  - Borrower must be an active member.
  - One open loan per borrower per group.
  - Pot ≥ principal **net of pending loans** (Voting + AwaitingTreasurer).
- **Voting**:
  - Borrower **cannot** vote on their own loan.
  - Strict majority of non-borrower active members approves
    (`floor(eligible/2) + 1`). Treasurer's vote counts as a normal member
    vote — the separate "treasurer confirmation" step is what disburses.
- **Treasurer disbursement**: at confirm time the pot is **re-checked**;
  if it can't cover the principal (e.g. fees were applied or another loan
  disbursed since application), the confirmation fails.
- **Interest math** (`ApplyForLoan`):
  ```
  if group.ApplyInterestOnContributions:
      interestable = principal
  else:
      interestable = max(0, principal − borrower_confirmed_contributions)
  totalInterest  = interestable × rate% / 100 × term_months / 12
  totalRepayable = principal + totalInterest
  installment    = totalRepayable / term_months
  ```
  So with the flag off, a member borrowing within their own contributions
  pays zero interest.
- **Repayment split** (`RecordLoanRepayment`): only `Treasurer` may
  record. Each payment is split into principal vs interest by the loan's
  original ratio. Interest goes into `loan.TotalInterestEarned` and
  redistributes pro-rata across all members' shares via `GetMyShare`.
- **Pot balance**:
  ```
  pot = sum(confirmed_contributions)
      − sum(fees)
      − sum(disbursed loan principals)     // Disbursed/Repaying/Closed/Defaulted
      + sum(repayments_amount_paid)
  ```
  Defined in `ComputePotBalanceRawAsync`.
- **My share** (per-member):
  ```
  my_contributions = sum(my confirmed contributions)
  my_interest      = total_repayment_interest × (my_contributions / total_confirmed)
  my_share_total   = my_contributions + my_interest
  ```

### KYC

`Modules/KYC/` — `KycDocument` + `KycVerification`.

Files are stored **inline in the DB** (`file_data BYTEA`) for the demo; in
prod this would be S3/MinIO. Verification is async — a Wolverine handler
calls `OllamaClient.ExtractFromImageAsync<IdentityFields>(...)` and
records confidence per field. `kyc.face_match_auto_approve` (0.80 by
default) and `kyc.face_match_reject` (0.40) thresholds gate auto-decisions.

### BillPay

`Modules/BillPay/` — `BillProvider`, `SavedBiller`, `BillPayment`.

The bill providers are seeded (ZESA, TelOne, NetOne, Econet, ZINWA). A
real provider integration would inject `IProviderClient` per provider;
the demo writes a transaction row and acks.

### Fraud Detection

`Modules/FraudDetection/` — `FraudAlert`, `FraudRule`.

Five rule types, all evaluated in-line by `FraudRuleEvaluator` during
transaction authorization:
- **Velocity**: > N transactions in M minutes.
- **GeoAnomaly**: device location differs from prior pattern.
- **DeviceAnomaly**: new device with a high-value transaction.
- **AmountAnomaly**: amount exceeds the per-user statistical threshold.
- **PatternMatch**: signature against known mule/scam patterns.

Thresholds live in `system_configs` under `fraud.*` keys. Alerts have a
status field (`New` → `Reviewed` → `Escalated` / `Dismissed`). Fraud
analysts triage from the bank-client UI.

### Branch Cash & Vault

`Modules/BranchCash/` — physical cash management at branches.

- `TellerDrawerSession` — a teller's shift, opened + closed with cash
  counts. EOD report PDF generated at close.
- `BranchCashTransaction` — every cash in/out at the counter, with a
  denomination breakdown.
- `Vault`, `VaultDenominationStock`, `VaultMovement`, `VaultSpotCheck` —
  the branch vault layer above tellers.
- `CurrencyDenomination` — configurable per-tenant denomination list (note
  and coin face values).

The teller / vault / supervisor dashboards in `bank-teller/` consume this
module's REST endpoints under `/api/teller/`.

### Card Transactions

`Modules/CardTransactions/` — receives card auth requests from the
**switch** and posts to the appropriate account. ATM withdrawals, POS
purchases, refunds. The on-us vs off-us routing decision happens in the
switch, not here.

### Admin

`Modules/Admin/` — internal staff users, audit logs, system configs,
disputes.

`AdminUser` includes the staff JWT identity (BCrypt password hash, role,
branch ID for tellers/branch-managers). `audit_logs` is append-only.
`system_configs` is the key/value JSON store for runtime tuneables.

### White Label

`Modules/WhiteLabel/` — `TenantBranding`, `TenantFeeConfig`,
`TenantTransactionLimit`. The platform is multi-tenant by design (one
gateway, N tenants); each tenant gets a logo + colours + per-currency
limits.

### Reporting

`server/GoldBank.Reporting/` — six report types, one streaming export.
Lives in its own assembly so it can be lifted into a separate process
later. Exposed as `ReportingService` gRPC; surfaced in the bank-client
"Reports" pages.

| RPC | Returns |
| --- | --- |
| `GetDashboard` | Live counters (users, txns, volume, merchants, agents, loans) |
| `GetUserGrowth` | Registrations + churn by day/week/month |
| `GetMerchantReport` | Volume + commission per merchant |
| `GetRevenueReport` | Revenue breakdown by transaction type |
| `GetReconciliationReport` | Settlement vs ledger discrepancies |
| `Export` | Streams CSV chunks for the active report |

### AI

`Modules/AI/` — single `OllamaClient` that speaks OpenAI-compatible API.
Use cases:

| Handler | Prompt purpose |
| --- | --- |
| `VerifyIdentityHandler` | Extract ID fields from a national ID / passport image |
| `VerifyProofOfAddressHandler` | Validate name + address on a utility bill |
| `ExtractReceiptHandler` (asset custody) | Parse a safe-deposit receipt photo |
| `ExtractBillFieldsHandler` | Parse a utility bill for amount + meter # |
| `ExtractChequeFieldsHandler` | Parse cheque amount + payee |
| `ExtractDocumentFieldsHandler` | Generic document OCR |
| `ChatHandler` | In-app assistant (RAG-style, conversation history) |
| `GetSpendingInsightsHandler` | Generate spending summary from transaction history |
| `ExplainFraudAlertHandler` | Human-readable explanation of why an alert triggered |
| `TriageDisputeHandler` | Auto-classify dispute type |
| `CheckLoanEligibilityHandler` | Pre-screen a loan application |
| `VerifyLoanDocumentsHandler` | Validate uploaded supporting docs |

The `ai.ollama_url` + `ai.model_name` system_configs control the endpoint
and model (default: `qwen3-vl`). AI calls are best-effort with a circuit
breaker — if Ollama is unreachable, the calling handler falls back to a
"pending manual review" state.

## Conventions when adding to the server

1. **New aggregate** → new folder under `Modules/<Name>/Domain/Entities/`
   + an `IEntityTypeConfiguration<T>` under `Infrastructure/Persistence/`.
   Add `modelBuilder.ApplyConfiguration(new TConfig())` in
   `GoldBankDbContext.OnModelCreating`.
2. **Run** `dotnet ef migrations add <Name> --context GoldBankDbContext
   --output-dir Migrations/GoldBankDb` from `server/GoldBank.Migrator/`.
3. **New gRPC service** → add a `.proto` under `GoldBank.Protos/Protos/`.
   Rebuild — stubs regenerate. Implement `XxxServiceBase`. Map in
   `GoldBank.Gateway/Program.cs`: `app.MapGrpcService<XxxGrpcService>();`.
4. **New REST endpoint** → add to `AdminApiController` (bank-client) or
   `TellerApiController` (bank-teller). Use `[Authorize(Roles = "...")]`.
5. **Mobile-facing changes** → also update `mobile/shared/.../grpc/...` Kotlin
   client + `mobile/shared/.../mapper/...` + any view model.
6. **Money** stays as `decimal` in the domain, `string` on the wire.
7. **Commit + rebuild gateway image** for the change to land in the
   running container: `podman build -f server/GoldBank.Gateway/Dockerfile
   -t localhost/goldbank_gateway:latest .`

## Common gotchas

- **EF can't translate JSON-column `Contains`** for `List<Guid>` columns
  like `loans.CollateralAssetIds`. Pull the rows into memory and filter
  in C# (`AsEnumerable().Where(...)`).
- **Tenant gates use string equality**. If you stage data with a UUID
  tenant_id (`"00000000-..."`) the teller's `tenant = "goldbank"` JWT
  won't see it — 403. See [data-model.md](data-model.md#multi-tenancy).
- **`MapLoanResponseAsync` needs the requesterId** to populate `my_vote`.
  Pass it from every call site that knows who's asking (5 of the 7).
- **Notifications fail silently** if Wolverine can't reach Redis. Check
  the gateway logs for `OutboxProcessor` errors when "the SMS didn't
  come through".
- **AI calls are slow** (Qwen3-VL is 8B params). Don't block UI on them;
  use the "pending review" pattern in the calling handler.
