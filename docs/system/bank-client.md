# Bank-client (Back-office Admin Web)

A React + Vite + Material UI single-page app for back-office staff
(admins, KYC officers, fraud analysts, compliance, loan officers,
support, branch managers). Runs on the Vite dev server at
**http://localhost:5173/** and proxies `/api/*` to the gateway on
`localhost:5001`.

```
bank-client/
  index.html
  vite.config.js          ← :5173, /api proxy → :5001
  package.json
  src/
    App.jsx               ← route table + role-gated ProtectedRoute
    auth/
      AuthContext.jsx     ← dev-stub login against SEED_ACCOUNTS
      roles.js            ← Roles enum + Access matrix + SEED_ACCOUNTS
      ProtectedRoute.jsx  ← role guard wrapper
    services/
      api.js              ← every fetch call against /api/admin/*
      snackbar.jsx        ← global toast provider
    pages/
      Dashboard.jsx
      Customers.jsx
      KycReview.jsx
      Disputes.jsx
      FraudAlerts.jsx
      Transactions.jsx
      UserManagement.jsx
      BranchManagement.jsx
      DepositHouses.jsx
      LoanReview.jsx
      AssetValuation.jsx
      SystemConfig.jsx
      Merchants.jsx
      Tariffs.jsx
      AuditTrail.jsx
      UserGrowthReport.jsx
      MerchantReport.jsx
      RevenueReport.jsx
      ReconReport.jsx
      Login.jsx
      NotFound.jsx
    layouts/
      MainLayout.jsx       ← top bar, side nav, user menu
    theme.js               ← MUI theme (dark/light toggle)
```

## Authentication

Currently a **dev stub** — there's no server-side admin login flow yet.
[`auth/roles.js`](../../bank-client/src/auth/roles.js) hard-codes seven
seed accounts:

| Username | Password | Role |
| --- | --- | --- |
| `admin` | `Admin@1234` | Admin |
| `kyc` | `Kyc@1234` | KycOfficer |
| `fraud` | `Fraud@1234` | FraudAnalyst |
| `support` | `Support@1234` | CustomerService |
| `loans` | `Loans@1234` | LoanOfficer |
| `compliance` | `Compliance@1234` | ComplianceOfficer |
| `branch` | `Branch@1234` | BranchManager |

`AuthContext.login()` checks these locally and stores the user in
sessionStorage. **No tokens, no server roundtrip.** The role string is
the basis for in-app guards but does NOT secure the underlying REST
endpoints — `AdminApiController` is currently unauthenticated. Wiring
this to real JWTs (e.g. against `admin_users` + `TellerAuthController`'s
pattern) is the obvious next-step debt; see the standing offer in
`server.md`.

## Role-based access

`Access` matrix in `roles.js`:

```js
KycAccess:        [Admin, KycOfficer, BranchManager]
FraudAccess:      [Admin, FraudAnalyst, BranchManager]
CustomerAccess:   [Admin, CustomerService, BranchManager]
DisputeAccess:    [Admin, CustomerService, BranchManager]
LoanAccess:       [Admin, LoanOfficer, BranchManager]
ReportAccess:     [Admin, ComplianceOfficer, BranchManager]
UserManagement:   [Admin, BranchManager]
ConfigAccess:     [Admin]
```

Routes in `App.jsx` are wrapped with
`<ProtectedRoute roles={Access.XAccess}>…</ProtectedRoute>`. If the
logged-in role isn't in the array, the page renders an Alert: *"You do
not have permission to view this page."* Side-nav items use the same
matrix to hide links — UI/UX consistency at the cost of duplicated
configuration.

## Page reference

### Dashboard (`/`)
Read from `/api/admin/dashboard` — live counters, 30-day transactions
chart. Visible to all logged-in roles.

### Customers (`/customers`) — CustomerAccess
- Search by name / phone / email, filter by status (`active`,
  `suspended`, `frozen`, `closed`).
- Row click → detail dialog with all-currency balances, KYC level,
  document timeline (`/api/admin/customers/{shortId}/activity`).
- Actions: Activate / Suspend / Freeze / Unfreeze / Reset PIN / Close
  → `POST /api/admin/customers/{shortId}/actions { action, reason }`.

### KYC Review (`/kyc`) — KycAccess
Pending KYC documents with AI extraction sidebar. Approve / reject /
escalate. Reads `kyc_documents` + `kyc_verifications`.

### Disputes (`/disputes`) — DisputeAccess
Filter by status (Open, Investigating, Resolved). Detail panel shows
the underlying transaction, customer history, activity log. Actions
update `disputes.status` + `resolution` + `refund_amount`.

### Fraud Alerts (`/fraud`) — FraudAccess
Sorted by severity. Detail panel shows the rule that triggered + a
human-readable AI explanation (calls
`AiService.ExplainFraudAlert`). Status workflow:
`New` → `Reviewed` → (`Escalated` | `Dismissed`).

### Transactions (`/transactions`)
All transactions, filterable by type, status, accountId. No role
gate — visible to anyone logged in (the page is purely read-only).

### User Management (`/users`) — UserManagement
List of `admin_users`. Create / update / deactivate.
- `GET /api/admin/admin-users`
- `POST /api/admin/admin-users` (create)
- `PUT /api/admin/admin-users/{id}` (update)

Role + branch dropdowns drive the JWT claims those staff get when they
log in to bank-teller.

### Branch Management (`/branches`) — UserManagement
CRUD for `branches`. Each teller / branch-manager is bound to one
branch via `admin_users.branch_id`.

### Deposit Houses (`/deposit-houses`) — ConfigAccess
The trusted vault facilities that hold customer assets. Name, address,
license number, **trust status** (`Verified`, `Probationary`,
`Suspended`), API endpoint (for automated valuation sync — not wired
yet).

### Loan Review (`/loans`) — LoanAccess
Pending and active loans. Loan officers approve/reject; can view
collateral assets linked via `loans.collateral_asset_ids`.

### Asset Custody & Valuation (`/assets`) — LoanAccess
Three tabs:
1. **Asset Registry** — every asset in custody, filterable by
   type / status, click row for detail.
2. **Valuation Queue** — assets with `lastValued > 30 days ago`,
   sorted by days overdue. Per-row "Assign valuer" and "Value" buttons.
   The "Value" dialog submits to `POST /api/admin/asset-valuations`
   which writes a row to `asset_valuations` and bumps
   `assets.LastValuationAmount`.
3. **Valuation History** — every recorded valuation with prev → new +
   % change.

### System Config (`/config`) — ConfigAccess
Key-value editor for `system_configs`. Categorised UI: PIN policy,
OTP, fraud thresholds, daily / monthly limits, Ekub monthly fees,
AI model selection.

### Merchants (`/merchants`) — CustomerAccess
Merchant onboarding queue (KYB documents), active merchants list,
suspend / activate actions.

### Tariffs (`/tariffs`) — ConfigAccess
Per-tenant fee configuration (`tenant_fee_configs`). Transfer fees,
bill-pay fees, agent commissions, FX margin.

### Audit Trail (`/audit`) — ReportAccess
Pageable view of `audit_logs`. Filter by admin user, action type,
date range. Read-only.

### Reports — ReportAccess

| Route | Source |
| --- | --- |
| `/reports/users` | `ReportingService.GetUserGrowth` |
| `/reports/merchants` | `ReportingService.GetMerchantReport` |
| `/reports/revenue` | `ReportingService.GetRevenueReport` |
| `/reports/recon` | `ReportingService.GetReconciliationReport` |

Each report has a CSV export button calling
`ReportingService.Export` streaming.

## REST endpoint map (bank-client → gateway)

Every page hits `/api/admin/*` via [`services/api.js`](../../bank-client/src/services/api.js):

| Function | Endpoint | Page |
| --- | --- | --- |
| `getDashboard` | `GET /api/admin/dashboard` | Dashboard |
| `generateCustomers` | `GET /api/admin/customers` | Customers |
| `generateCustomerActivity` | `GET /api/admin/customers/{shortId}/activity` | Customer detail dialog |
| `postCustomerAction` | `POST /api/admin/customers/{shortId}/actions` | Customer actions |
| `generateTransactions` | `GET /api/admin/transactions` | Transactions |
| `getDisputeActivities` | `GET /api/admin/disputes/{shortId}/activities` | Dispute detail |
| `generateAdminUsers` | `GET /api/admin/admin-users` | User Management |
| `createAdminUser` | `POST /api/admin/admin-users` | User Management |
| `updateAdminUser` | `PUT /api/admin/admin-users/{id}` | User Management |
| `generateBranches` | `GET /api/admin/branches` | Branch Management |
| `generateAssets` | `GET /api/admin/assets` | Asset Custody, Asset Registry tab |
| `generateAssetValuations` | `GET /api/admin/asset-valuations` | Asset Custody, History tab |
| `submitAssetValuation` | `POST /api/admin/asset-valuations` | Asset Custody, Value dialog |

Plus `KycReview`, `FraudAlerts`, `Disputes`, `LoanReview`, `Merchants`,
`DepositHouses`, `SystemConfig`, `Tariffs`, `AuditTrail`, and the four
report pages each have their own pair of endpoints — see
`bank-client/src/services/api.js` for the full list.

## Running locally

```powershell
cd c:\Users\wmapu\Projects\GoldBank\bank-client
$env:OPENSSL_CONF = ''   # clear the broken system OpenSSL config if present
npm install              # first time
npm run dev              # serves on http://localhost:5173/
```

The Vite proxy in [`vite.config.js`](../../bank-client/vite.config.js)
forwards `/api/*` to `http://localhost:5001`, which is the gateway's
REST port. Vite's hot reload picks up `.jsx` changes within seconds.

If the gateway is down: every page shows an empty / error state and
the browser console has `[api] /<path> failed: TypeError: fetch
failed`. Fix the gateway, refresh.

## Conventions

- **No global store.** State is local to each page (`useState` +
  `useEffect`). Don't introduce Redux/Zustand without a strong reason —
  the current pages have shallow state.
- **MUI components only.** No bespoke CSS-in-JS unless absolutely
  necessary. The `theme.js` overrides cover the gold-on-navy palette.
- **Always handle `null`** from `fetchApi(...)` — it's the convention for
  any API failure (logs to console, returns `null`). Pages render an
  "Unable to load" state in that case.
- **Currency strings**: `Number(value).toLocaleString(undefined, {
  minimumFractionDigits: 2 })` — never use raw JS arithmetic on money
  beyond that.
- **Routes are URL-typed** (`/customers/:shortId`, not query params).
  Keep deep-linkable.
- **Role gating is UI-only.** The REST API does not currently enforce
  it. **Don't ship to prod without** wiring `[Authorize(Roles="...")]`
  on `AdminApiController` and replacing the SEED_ACCOUNTS stub.

## Known limitations

- **No real authentication.** SEED_ACCOUNTS is a dev stub. The
  AdminApiController is unauthenticated.
- **No CSRF protection.** Required once real auth is added.
- **Customer search is server-side filtering on `Contains`** — fine for
  the demo's 20 customers, will scan-table on a real dataset. Add a
  trigram index on `(first_name, last_name, phone, email)` for prod.
- **CSV export streams chunks** but the React side buffers the whole
  response before triggering a download. For huge reports, switch to
  `ReadableStream` + `showSaveFilePicker`.
- **Branch supervisor view** isn't yet a separate page — branch
  managers use the standard customer list + filter manually by branch.
- **Reports don't paginate** — they emit one big dataset that gets
  rendered with `DataGrid`'s built-in pagination. Above ~10k rows this
  gets sluggish.
