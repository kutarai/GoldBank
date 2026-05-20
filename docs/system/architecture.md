# Architecture

## Component map

```
                       ┌────────────────────────────────────────────────┐
   Customers           │                MOBILE (Kotlin/KMM)             │
   on phones      ────►│  android app + shared module                   │
                       │  gRPC stub → :5000  (auto-refreshing JWT)      │
                       └─────────────────────┬──────────────────────────┘
                                             │
   POS / ATMs                                │ gRPC over h2c
   on ISO 8583  ────►  ┌──────────────────┐  │       ┌──────────────────────┐
                       │  SYNERGY SWITCH  │──┼──────►│      GATEWAY         │
   POS over    ──gRPC─►│  :3333 gRPC      │  │       │  REST :5001          │
   ISO 20022           │  :8080 web admin │  │       │  gRPC :5000          │
                       └──────────────────┘  │       │                      │
                                             │       │  controllers/       │
   Back-office        ┌─────────────────────┐│       │   AdminApi (REST)    │
   admins on web ────►│ BANK-CLIENT (React) ├┘       │   TellerApi (REST)   │
   :5173             │ fetch → /api/admin/* │        │   TellerAuth (JWT)   │
                     └──────────────────────┘        │   <module>Grpc       │
                                                     │                      │
   Branch tellers     ┌──────────────────────┐       │  Modules backbone    │
   on web :5174 ─────►│ BANK-TELLER (React)  ├──────►│  Accounts            │
                      │ fetch → /api/teller/*│       │  Customers           │
                      └──────────────────────┘       │  AssetCustody        │
                                                     │  Loans               │
   Compliance        ┌──────────────────────┐        │  Ekub                │
   on Blazor    ────►│  ADMIN (Blazor)      │        │  KYC, Fraud, Merch.  │
                     │  :5010 server-rendered│       │  BillPay, Transfers  │
                     └──────────────────────┘        │  BranchCash, Card    │
                                                     │  Admin, WhiteLabel   │
                                                     │  AI, Reporting       │
                                                     └────┬─────┬────┬──────┘
                                                          │     │    │
                                                ┌─────────┘     │    └────────┐
                                                ▼               ▼             ▼
                                       ┌──────────────┐  ┌────────────┐  ┌──────────────┐
                                       │  PostgreSQL  │  │   Redis    │  │ NOTIFICATIONS│
                                       │  :5432       │  │   :6379    │  │  SMS + FCM   │
                                       │  bank schema │  │  sessions, │  │  (Wolverine) │
                                       │  switch DB   │  │  OTPs,     │  └──────────────┘
                                       └──────────────┘  │  rate-lim. │
                                                         └────────────┘
                                                         ┌──────────────┐
                                                         │   OLLAMA     │
                                                         │   Qwen3-VL   │
                                                         │   (KYC OCR,  │
                                                         │   chat,      │
                                                         │   fraud xpl.)│
                                                         └──────────────┘
```

## Communication patterns

### 1. Mobile → Gateway (gRPC over plaintext HTTP/2)

Mobile uses gRPC because:
- Bidirectional streaming for chat
- Server streaming for transaction lists
- Smaller payloads / less battery
- Strong typing across the wire

The gateway listens on **port 1111** inside the container, mapped to host
**:5000**. Plaintext (no TLS) for dev; TLS terminated at the load balancer
in prod. Each mobile call carries two headers:
- `authorization: Bearer <JWT>` — set by `AuthClientInterceptor`
- `x-tenant-id: goldbank` — set by the same interceptor

The JWT is refreshed transparently by [`TokenRefresher.kt`](../../mobile/shared/src/androidMain/kotlin/com/goldbank/shared/data/remote/TokenRefresher.kt)
which sits in front of every `grpcCall { … }` and re-issues the access token
when it's within 60 seconds of expiry. Refresh is mutex-guarded with a
re-entrancy marker so the refresh call itself doesn't deadlock.

### 2. Bank-client / bank-teller → Gateway (REST/JSON over HTTP/1.1)

The two React apps run on their own Vite dev servers
(`:5173` for bank-client, `:5174` for bank-teller). They hit the gateway's
**REST surface on port 1112** (host `:5001`). Bank-client uses session-based
mock auth (currently hardcoded `SEED_ACCOUNTS` in the client); bank-teller
uses **JWT** via `POST /api/teller/auth/login`. Both call
`fetch('/api/<area>/...')` and rely on the gateway's `[EnableCors("BankClient")]`
policy.

### 3. Switch ↔ Gateway (gRPC)

The synergy-switch service handles POS/ATM traffic. It speaks two wire
protocols **inbound**:
- ISO 8583 over persistent TCP (legacy national networks)
- ISO 20022 over gRPC (modern smart POS terminals)

…and a single protocol **outbound** to the gateway: gRPC against
`gateway:1111` using the same `CardTransactionService` contract. This lets
the gateway treat all card activity uniformly regardless of which terminal
type initiated it.

The switch routes **on-us** transactions (cardholder + merchant both belong
to this bank — detected via BIN prefix matching) directly to the gateway,
bypassing the national network. **Off-us** transactions are routed out via
the configured gateway-pool to Zimswitch.

### 4. Notifications (in-process message bus + sidecar)

When a domain event fires inside a module (e.g. `UserAuthenticated`,
`AccountCreated`, `LoanApproved`), the handler publishes it to a
**Wolverine** in-process bus. A handler in `GoldBank.Notifications` picks it
up, resolves a template, applies rate limits and user preferences, and
dispatches via SMS gateway and/or Firebase FCM. The outbox-pattern table
in PostgreSQL ensures at-least-once delivery across service restarts.

### 5. AI features (OpenAI-compatible client → Ollama)

KYC document OCR, asset receipt extraction, fraud-alert explanations, and
chat all go through `OllamaClient` which speaks the OpenAI-compatible API
to a local Ollama server (container `goldbank-ollama`) running the
**qwen3-vl** vision-language model. Image inputs are base64-encoded JPEG;
prompts are deterministic templates per use case.

## Deployment topology

Containers are orchestrated by `docker-compose.yml` with three profiles:

- **`core`** — postgres, redis, gateway, switch, admin, hsm, notifications,
  switch-migrator, server-migrator. This is what you bring up to develop
  against.
- **`ai`** — ollama (large image, separate profile so you can dev without it).
- **`monitoring`** — prometheus, grafana, elasticsearch, kibana (off by default).

The two React dev servers (`bank-client`, `bank-teller`) run **on the host**
under Vite, not in containers. They proxy `/api` requests to the gateway on
`localhost:5001`. See [operations.md](operations.md) for the full port map.

## Modular monolith, not microservices

Every business module lives inside one .NET assembly (`GoldBank.Core`) and
shares one DbContext (`GoldBankDbContext`). The benefits:
- One transaction across modules (no distributed sagas)
- One schema, one migration set
- One container to rebuild

The costs:
- Strong coupling — changing one module's entity rebuilds the gateway image
- Single point of failure — gateway down ⇒ everything down

We've explicitly traded operational simplicity for a tighter dev loop. If
any module grows enough to warrant extraction (Reporting is the likely
candidate), the wire contract is already a clean gRPC service so the cut
is straightforward.

## Where the boundaries are

Inside the .NET solution the module folders follow a consistent layout:

```
Modules/<Name>/
  Domain/
    Entities/      ← aggregate roots + value objects
    ValueObjects/
  Application/
    Commands/      ← request DTOs
    Handlers/      ← orchestration (Wolverine handlers OR plain services)
    Validators/    ← FluentValidation
    Interfaces/    ← external dependencies (SMS gateway, OTP service)
  Infrastructure/
    Persistence/   ← EF Core IEntityTypeConfiguration<T>
    Services/      ← concrete impls (BCrypt PIN hasher, JWT generator)
  Grpc/            ← gRPC service implementation (XxxServiceBase)
```

The convention isn't religiously enforced — older modules pre-date it — but
new modules follow it. The Asset Custody, Customer, and Ekub modules are
the cleanest examples.

## Cross-cutting concerns

| Concern | Implementation |
| --- | --- |
| AuthN (mobile) | JWT issued by `JwtTokenService` after PIN verify; bearer header on every gRPC call; auto-refresh client-side |
| AuthN (web admin) | Hardcoded `SEED_ACCOUNTS` in `bank-client/src/auth/roles.js` — dev stub, not real |
| AuthN (teller) | JWT issued by `/api/teller/auth/login`; bearer on every REST call |
| AuthZ | `[Authorize(Roles = "...")]` on controllers; `ProtectedRoute` + `hasRole()` on web; role from JWT claim |
| Tenant isolation | `tenant_id` column on every aggregate; gRPC interceptor sets `x-tenant-id`; controllers filter |
| Idempotency | Outbox table for events; receipt numbers unique per (deposit_house, receipt_number); Ekub fees unique per (group, period) |
| Audit | `bank.audit_logs` table; admin actions on customers / loans / disputes / config writes a row |
| Money | Decimal in DB and domain; string-over-wire in protos; `Money { amount, currency }` value object |
| Phones | `PhoneNumber` value object validates E.164 (Southern African codes) |
| PINs | BCrypt hash, work factor 11; never logged; `PinHashingService` |
| Lockout | Redis-backed counter; configurable max attempts + lockout minutes via `system_configs` |

## Data flow examples

### A customer transfers $100 to another customer

```
phone PIN ──► AuthenticateHandler ──► JWT
       │                              │
mobile ◄───────────────────────────────┘
       │
       │  TransferGrpcService.SendP2P (bearer = JWT)
       ▼
GatewayTransferService
       ├─► TransactionAuthorizationService  (daily/monthly limits, fraud rules)
       ├─► FraudRuleEvaluator               (velocity, geo, device)
       ├─► AccountRepository.DebitCredit    (in one EF transaction)
       └─► IMessageBus.Publish(TransferCompleted)
                                        │
                                        ▼
                            NotificationOrchestrator  (SMS to both parties)
                            FraudAlertEvaluator       (post-tx check)
```

### A loan is approved by Ekub vote

```
mobile (borrower) ──► ApplyForLoan
                          │
                          ├─► validate pot ≥ principal + pending loans
                          └─► insert ekub_loans row status=Voting

mobile (members) ──► VoteOnLoan
                          │
                          ├─► insert ekub_loan_votes
                          └─► if approves ≥ majority of non-borrower members:
                              loan.status = AwaitingTreasurer

mobile (treasurer) ──► ConfirmLoanByTreasurer
                          │
                          ├─► re-check pot ≥ principal (defence in depth)
                          ├─► loan.status = Disbursed
                          └─► ComputePotBalanceRawAsync now subtracts principal
```

### A teller withdraws an asset

```
teller ──► withdrawAsset(assetId)
              │
              ├─► find every non-Closed/non-Defaulted loan
              │   across the customer's sibling accounts
              │
              ├─► IF any loan's CollateralAssetIds contains assetId:
              │      reject 409 { blockingLoan: { reference, outstanding } }
              │
              └─► ELSE:
                    asset.Status = Released
                    asset.IsDeleted = true (soft-delete)
                    asset.DeletedBy = tellerId
```
