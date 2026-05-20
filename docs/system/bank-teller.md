# Bank-teller (Branch Counter Web)

A React + Vite + Material UI web app for **branch tellers**, **branch
managers**, and **vault managers**. Runs at **http://localhost:5174/**.
Unlike bank-client, this one **does** authenticate against the gateway —
it's the most "real" web surface today.

```
bank-teller/
  vite.config.js          ← port 5174
  src/
    App.jsx               ← route tree with HomeRouter (role-based)
    auth/
      TellerSessionContext.jsx  ← real JWT session
      ProtectedRoute.jsx
    components/
      DenominationGrid.jsx ← cash-count UI for shift open/close
      ErrorBoundary.jsx
      SecurityShell.jsx    ← inactivity lock screen
    layouts/MainLayout.jsx
    services/api.js
    pages/
      Login.jsx
      Dashboard.jsx
      SupervisorDashboard.jsx
      VaultDashboard.jsx
      CustomerSearch.jsx
      CustomerCard.jsx
      Deposit.jsx
      Withdrawal.jsx
      Drawer.jsx           ← shift open / close
```

## Authentication

Real JWT, server-side validated. **`POST /api/teller/auth/login`** with
`{ Username, Password }` returns `{ accessToken, user }`. The accessToken
is a JWT with claims: `sub` (admin_user.Id), `role`
(Teller/BranchManager/VaultManager/Admin), `tenant_id`, `branch_id`,
`username`.

The `Authorization: Bearer <jwt>` header rides on every API call. On
`401` the session is cleared and the user bounced to `/login`. There's
no refresh token currently — when the JWT expires, the user logs back
in. (See the standing offer to add refresh; mobile already has it via
`TokenRefresher`.)

Auth wrapping flow:

```
TellerSessionProvider          ← React Context
  ↓
ProtectedRoute                 ← redirects to /login if no token
  ↓
SecurityShell                  ← inactivity timer → lock screen
  ↓
MainLayout                     ← top bar, side nav, logout
  ↓
HomeRouter                     ← BranchManager/Admin see SupervisorDashboard,
                                 everyone else sees Dashboard
```

## Roles

Demo credentials (PINs not used here — username/password instead):

| Username | Password | Role | What they see |
| --- | --- | --- | --- |
| `teller` | `teller` | Teller | Dashboard + Customer card + Deposit + Withdrawal + Drawer |
| `branch` | `branch` | BranchManager | Supervisor Dashboard + everything tellers see + Vault |
| `admin` | `admin` | Admin | Everything |

Passwords come from the `DemoSeeder` (username == password for demo).
Real deployments would use BCrypt hashes seeded externally.

## Page reference

### Login (`/login`)
Username + password form. Calls `loginTeller(username, password)`. On
success stores `(accessToken, user)` in sessionStorage and navigates
to `/`. If the response role is `Admin` or `BranchManager`, they land
on the supervisor dashboard.

### Dashboard (`/`) — Teller view
- Open shift CTA if no active drawer.
- KPIs for today: deposits / withdrawals count + value, transactions
  reversed, fees collected.
- Quick links to Customer Search, Deposit, Withdrawal.

### Supervisor Dashboard (`/`) — BranchManager / Admin view
- Branch-wide KPIs (active tellers, open drawers, total cash).
- Per-teller cards showing shift status + cash position.
- High-value pending approvals (transactions above the daily limit
  that need supervisor sign-off).

### Vault Dashboard (`/vault`) — BranchManager / VaultManager
- Branch vault denomination breakdown (notes + coins per currency).
- Movement log (cash in / out with denominations).
- "New movement" dialog (in/out, amount, denominations, reference,
  witness).
- Spot-check workflow: take an inventory count, compare with ledger,
  adjust if variance.

### Customer Search (`/customers`)
- Free-text search across phone / national ID / card PAN / name.
- Server-side via `GET /api/teller/customers/search?q=...`.
- Row click → `/customers/:accountId`.

### Customer Card (`/customers/:accountId`)
Three tabs:
1. **Profile** — Personal info, balances per currency, KYC status,
   flags (Frozen / Suspended / SignatureVerified).
2. **Transactions** — Date-ranged transaction history. From/To pickers,
   results table.
3. **Assets** — Custom-built for the gold-custody product. Lists every
   asset the customer owns, each row showing description, type,
   quantity, receipt #, deposit house, last valuation, status, and a
   `Collateral` chip if the asset is securing an open loan.

The Assets tab has two action paths:

#### Deposit asset
"Deposit asset" button → dialog with:
- Deposit house picker (loaded from `GET /api/teller/deposit-houses`)
- Receipt number
- Asset type (GoldCoin / GoldBar / Silver / Platinum / PreciousStone /
  Other)
- Description
- Quantity + unit
- Weight (grams) + purity (0–1)
- Optional initial valuation + currency

On submit → `POST /api/teller/customers/{accountId}/assets`. Server
validates uniqueness of `(deposit_house, receipt_number)`, creates the
asset in `PendingVerification` status, writes the initial valuation row
if provided.

#### Withdraw asset
Per-row "Withdraw" button (disabled when `isCollateral` or
non-Active). Confirms via a small dialog with an optional reason. On
submit → `POST /api/teller/assets/{assetUuid}/withdraw`.

**Critical pre-flight check**: the server pulls every non-Closed /
non-Defaulted loan across **all of the customer's sibling accounts**
and filters in-memory (EF can't translate `Contains` on the JSON
`collateral_asset_ids` column). If any loan lists this asset as
collateral, the response is **409 Conflict** with:

```json
{
  "error": "Cannot withdraw — asset is collateral on an open loan.",
  "blockingLoan": {
    "loanId": "LOAN-043521",
    "reference": "LOAN-COLL-TEST",
    "outstanding": 1000.00,
    "currency": "USD",
    "status": "active"
  }
}
```

The dialog surfaces this inline — the teller sees the loan reference
+ outstanding balance and can advise the customer to settle the loan
first. On 200, the asset is soft-deleted (`is_deleted = true`,
`status = Released`, `deleted_by = tellerId`) and disappears from the
list on refresh.

### Deposit (`/deposit`)
Cash-deposit workflow:
1. Select customer (search-as-you-type).
2. Enter amount + currency.
3. Denomination breakdown (which notes/coins the customer handed
   over) — validated client-side and again server-side.
4. Reference / narration.
5. Submit → creates a `Transaction` + `BranchCashTransaction`,
   credits the account balance.
6. Print receipt (PDF stream from `/api/teller/transactions/{id}/receipt.pdf`).

Above-limit deposits route to a pending-approval queue visible to the
branch manager.

### Withdrawal (`/withdrawal`)
Mirror of Deposit:
1. Select customer.
2. Verify identity (ID type + number + signature shown alongside DB
   record).
3. Enter amount + currency.
4. Denomination breakdown (which notes you handed out).
5. Pull from drawer / from vault if drawer is short.
6. Submit → debits account, deducts from drawer.

### Drawer (`/drawer`)
Shift management:
- **Open**: Count starting cash with denominations grid → record. The
  shift session is created; all subsequent transactions are attributed
  to it.
- **Close**: Count ending cash. The system computes
  *expected = opening + deposits − withdrawals − fees + adjustments*
  and shows the variance. Submit → seals the shift, generates EOD
  report PDF (`/api/teller/drawer/{id}/eod-report.pdf`).

If variance exceeds tolerance the close is gated on supervisor
approval.

## REST endpoint map

| Function | Endpoint | Page |
| --- | --- | --- |
| `loginTeller` | `POST /api/teller/auth/login` | Login |
| `getDashboard` | `GET /api/teller/dashboard` | Dashboard |
| `searchCustomers` | `GET /api/teller/customers/search` | Customer Search |
| `getCustomerCard` | `GET /api/teller/customers/{accountId}/card` | Customer Card → Profile tab |
| `getCustomerTransactions` | `GET /api/teller/customers/{accountId}/transactions` | Customer Card → Transactions tab |
| `listCustomerAssets` | `GET /api/teller/customers/{accountId}/assets` | Customer Card → Assets tab |
| `registerCustomerAsset` | `POST /api/teller/customers/{accountId}/assets` | Asset Deposit dialog |
| `withdrawAsset` | `POST /api/teller/assets/{assetId}/withdraw` | Asset Withdraw dialog |
| `listDepositHouses` | `GET /api/teller/deposit-houses` | Deposit dialog picker |
| `getCurrentDrawer` | `GET /api/teller/drawer/current` | Dashboard / Drawer |
| `openDrawer` | `POST /api/teller/drawer/open` | Drawer |
| `closeDrawer` | `POST /api/teller/drawer/close` | Drawer |
| `postDeposit` | `POST /api/teller/deposits` | Deposit |
| `postWithdrawal` | `POST /api/teller/withdrawals` | Withdrawal |
| `approveWithdrawal` | `POST /api/teller/withdrawals/{pendingId}/approve` | Supervisor Dashboard |
| `reverseTransaction` | `POST /api/teller/transactions/{cashTxnId}/reverse` | Customer Card / Supervisor |
| `getBranchDashboard` | `GET /api/teller/branches/{branchId}/dashboard` | Supervisor Dashboard |
| `getVault` | `GET /api/teller/vaults/{vaultId}` | Vault Dashboard |
| `postVaultMovement` | `POST /api/teller/vaults/{vaultId}/movements` | Vault Dashboard |
| `postSpotCheck` | `POST /api/teller/vaults/{vaultId}/spot-checks` | Vault Dashboard |

All of the above (except `auth/login`) require a valid teller JWT and
return 401 otherwise. They additionally enforce **tenant gates**:
`account.TenantId == tellerJwt.tenant_id` (the string `"goldbank"` for
the demo).

## Running locally

```powershell
cd c:\Users\wmapu\Projects\GoldBank\bank-teller
$env:OPENSSL_CONF = ''
npm install
npm run dev   # http://localhost:5174/
```

The Vite config does not configure a /api proxy — REST calls go to
`http://localhost:5001` directly via the `API_BASE` constant in
`services/api.js`. This means **CORS must be enabled** on the
gateway's REST host, which it is via `[EnableCors("BankClient")]` on
the controllers.

## Workflows

### A customer comes in to deposit gold coins

1. Customer Search → enter phone (`+263770003287`).
2. Open customer card → Assets tab.
3. Click "Deposit asset". Fill the dialog (deposit house = GoldVault
   Harare, receipt # = GV-2026-0106, asset type = GoldCoin,
   description, qty = 1, unit = coins, weight = 31.10, purity = 0.999,
   initial valuation = 2800, currency = USD).
4. Submit. The asset appears in the table immediately with status
   `PendingVerification` and the bank's valuer will promote it to
   `Active` from the bank-client UI.

### A customer comes in to withdraw their gold coins

1. Find the customer's card → Assets tab.
2. Identify the row by receipt #. The Withdraw button:
   - **Disabled** with tooltip if `isCollateral: true` → "Cannot
     withdraw — collateral on LOAN-XYZ". Ask the customer to settle
     the loan first.
   - **Disabled** if status ≠ Active (e.g. already `PendingRelease`).
   - **Enabled** otherwise.
3. Click Withdraw → optional reason → Confirm.
4. The physical handover happens at the counter; the row vanishes from
   the table. The asset row is preserved soft-deleted in the DB for
   audit.

### Closing a shift with a small cash variance

1. Drawer → Close shift.
2. Count cash by denomination, enter actuals.
3. System computes `actual − expected` per denomination.
4. If total variance is within tolerance (configurable in
   `system_configs`), the close completes and an EOD PDF is generated.
5. If outside tolerance, the close is stalled pending supervisor
   approval. The supervisor sees the pending close on their dashboard.

## Conventions

- **Tenant** is always `"goldbank"` for demo. The JWT carries it; every
  endpoint that touches customer data checks it.
- **Identity verification at the counter**: signature image, ID number,
  and DOB are shown side-by-side on the customer card. The
  `signatureVerified` flag tracks whether a teller has confirmed the
  signature in person — it's a manual click + reason field.
- **PDF receipts** stream from the gateway via `ReceiptPdfService`
  (QuestPDF). Print directly from the browser tab that opens.
- **High-value transactions** require supervisor approval. Defined by
  `fraud.high_value_threshold_zwg` and `fraud.high_value_threshold_usd`
  in `system_configs`.
- **Inactivity lock** kicks in after 5 minutes (configurable). PIN-less
  unlock for now (just resume with username + password).
