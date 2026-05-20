# Mobile

`mobile/` is a **Kotlin Multiplatform** app. Only the Android variant ships;
the `commonMain` source set is structured for an iOS target that hasn't
been built out.

```
mobile/
  shared/
    src/
      commonMain/kotlin/     pure-Kotlin domain models (no Android types)
      androidMain/kotlin/    Android-specific: gRPC clients, EncryptedSharedPreferences,
                             AuthRepositoryImpl, mappers, DI module
  androidApp/
    src/main/kotlin/com/goldbank/app/
      MainActivity.kt        single Activity host
      GoldBankApplication.kt Application class, Koin bootstrap
      navigation/NavGraph.kt route table (Type-safe routes via kotlinx.serialization)
      ui/<feature>/          Compose screens, one folder per feature area
      viewmodel/<Feature>VM  ViewModels (one per screen group)
      di/PresentationModule  Koin viewModel bindings
```

## Stack

| Layer | Library |
| --- | --- |
| UI | Jetpack Compose + Material 3 |
| Navigation | androidx.navigation.compose (type-safe routes) |
| DI | Koin |
| Async | Kotlin coroutines |
| Networking | gRPC-Kotlin (`io.grpc:grpc-kotlin-stub`) + OkHttp channel |
| Persistence | DataStore (preferences) + EncryptedSharedPreferences (secrets) |
| Image / NFC | Compose Coil + AndroidX HCE |
| Biometrics | AndroidX Biometric |

## Auth flow

```
┌── REGISTER ────────────────────────────────────────────────────────┐
│  RegisterScreen      → AccountGrpcClient.register(phone, deviceId) │
│                     ◄── RegistrationResult { registrationId, ttl } │
│  OtpScreen           → AccountGrpcClient.verifyOtp(otp)            │
│                     ◄── OtpVerificationResult { accountId, tmpTok }│
│  CreatePinScreen     → AccountGrpcClient.createPin(pin, confirm)   │
│                     ◄── AuthTokens { access, refresh, customerId } │
│  ProfileInfoScreen   → accountClient.updateProfile(...)            │
│  RegistrationIdUpl   → KycGrpcClient.uploadDocument(NationalId)    │
│  RegistrationSelfie  → KycGrpcClient.uploadSelfie(...)             │
│  → sessionManager.logout()  (saves phone for next launch)          │
│  → SessionState.PinRequired → AuthNavHost(startAtLogin = true)     │
└────────────────────────────────────────────────────────────────────┘

┌── LOGIN (returning) ───────────────────────────────────────────────┐
│  LoginScreen (PIN-only; phone pre-filled, Change link to swap)     │
│                      → AccountGrpcClient.authenticate(phone, pin)  │
│                     ◄── AuthResult.Success(AuthTokens)             │
│                       │  also: LockedOut(seconds), Failed(remaining)│
│  → sessionManager.saveTokens(tokens)                               │
│  → SessionState.Authenticated → MainNavHost                        │
└────────────────────────────────────────────────────────────────────┘
```

### Token auto-refresh

`shared/.../data/remote/TokenRefresher.kt` is the centrepiece. Every
`grpcCall { … }` invocation routes through Koin's `GlobalContext` to
fetch `TokenRefresher` and call `ensureFresh()` before the actual gRPC
stub call.

```kotlin
suspend fun ensureFresh() {
    if (currentCoroutineContext()[RefreshGuard] != null) return  // re-entry guard
    val refresh = sessionManager.getRefreshToken() ?: return
    if (!sessionManager.isTokenExpiringSoon()) return            // 60-s threshold
    mutex.withLock {
        if (!sessionManager.isTokenExpiringSoon()) return@withLock   // re-check
        withContext(RefreshGuard()) {
            val r = authRepository.refreshToken(refresh, deviceIdProvider())
            if (r is Result.Failure) sessionManager.logout()      // refresh denied → re-login
        }
    }
}
```

The `RefreshGuard` `CoroutineContext.Element` is the key — when the
refresh call goes back through `grpcCall` (it does — `AccountGrpcClient.refreshToken`
uses the same helper), the inner call sees the guard and skips
the re-entry, avoiding mutex deadlock.

Visible to the user: nothing. Visible to `adb logcat -s TokenRefresher`:

```
D/TokenRefresher: Connection about to expire - refreshing
D/TokenRefresher: Connection refreshed
```

## Navigation graph

Defined in `androidApp/.../navigation/NavGraph.kt`. The top-level routes
are listed in `Routes.kt` as `@Serializable data object` (no args) or
`@Serializable data class` (with args). The route tree:

```
AuthGraph (when SessionState in { Unauthenticated, PinRequired })
  ├── Register
  ├── Otp(registrationId, otpLength, ttlSeconds)
  ├── CreatePin(accountId)
  ├── ProfileInfo
  ├── RegistrationIdUpload(accountId)
  ├── RegistrationSelfie
  └── Login(showPhoneField, initialPhoneNumber)

MainGraph (when SessionState.Authenticated)
  ├── Home
  │   ├── TransactionList
  │   │   └── TransactionDetail(txnId)
  │   ├── Notifications
  │   ├── Profile
  │   │   ├── EditProfile
  │   │   ├── SecuritySettings
  │   │   ├── NotificationSettings
  │   │   ├── DeviceTransfer
  │   │   └── Settings
  │   └── (quick-action grid → next sub-routes)
  │
  ├── Payments
  │   ├── P2PTransfer
  │   ├── QrGenerate
  │   ├── QrScan
  │   └── NfcPayment
  ├── BillPay
  │   ├── ProviderList
  │   └── PayBill(providerId)
  ├── CashFlow
  │   ├── CashIn
  │   └── CashOut
  ├── Loans
  │   ├── LoanList
  │   ├── LoanApply
  │   └── LoanDetail(loanId)
  ├── KYC
  │   ├── KycDashboard
  │   ├── DocumentUpload(type)
  │   ├── ProofOfAddress
  │   ├── Selfie
  │   └── KycVerificationResult
  ├── Disputes
  │   ├── DisputeList
  │   ├── DisputeDetail(disputeId)
  │   └── DisputeWizard(transactionId)
  ├── FraudAlerts
  │   ├── FraudAlertList
  │   └── FraudAlertDetail(alertId)
  ├── Merchant (for merchant accounts)
  │   ├── MerchantRegister
  │   ├── MerchantDashboard
  │   ├── MerchantTransactions
  │   ├── MerchantSettlements
  │   └── MerchantCommission
  ├── Scans
  │   ├── BillScan
  │   ├── ChequeScan
  │   └── ReceiptScan
  ├── Assets  (Asset Custody)
  │   ├── AssetList
  │   ├── AssetDetail(assetId)
  │   └── AssetRegister
  └── Ekub
      ├── EkubGroupList
      ├── EkubCreateGroup
      ├── EkubInvitations
      └── EkubGroupDetail(groupId)

ChatFAB overlay (any authenticated screen)
SessionLockScreen overlay (when inactivity timeout fires)
```

Two route keys land on `Home`'s quick-action grid that aren't directly
covered above: `"assets"` → `Route.AssetList`, `"ekub"` →
`Route.EkubGroupList`. The grid is in `ui/components/QuickActionGrid.kt`.

## Feature deep-dives

### Asset Custody (mobile side)

- `AssetListScreen` → `AssetGrpcClient.listMyAssets(customerId)` →
  rendered as cards with status chips.
- `AssetDetailScreen` → `getAssetDetail` + `getDailyPrices` for the
  current spot reference; shows valuation history + a "Request release"
  button.
- `AssetRegisterScreen` → multi-step (photograph receipt → AI OCR
  extracts fields → user confirms → submit). The OCR call uses
  `AiGrpcClient.extractDepositReceipt` with the receipt JPEG bytes.
- `PortfolioValue` — totals in ZWG + USD; surfaced on the Home screen as
  a "Custody" tile (when assets > 0).

All asset calls are scoped to **`customerId`** (from
`SessionManager.getCustomerId()`), not `accountId`. This makes the
assets visible regardless of which currency account is "active".

### Ekub (mobile side)

The full UI surface:

| Screen | What it does |
| --- | --- |
| `EkubGroupListScreen` | Lists groups the user is a member of; pending invitations badge; "Create group" CTA |
| `EkubInvitationsScreen` | Accept / decline pending invites |
| `CreateEkubGroupScreen` | Form for name, currency, monthly amount, interest rate, "charge interest on contributions" toggle |
| `EkubGroupDetailScreen` | Three tabs: Members / Contributions / Loans. Role-aware action buttons |

On the **Group Detail** screen the role determines what's possible:

| Role | Can | Cannot |
| --- | --- | --- |
| Chairman | Invite, AssignRole, KickMember, CloseGroup | Confirm contributions / loans, vote on own |
| Treasurer | Invite (NO), Confirm contributions, Confirm-and-disburse loans, Record repayments | Vote on own loan |
| Secretary | Invite | Confirm anything |
| Member | Apply for a loan, contribute, vote on others' loans | (no admin actions) |

The "Apply for a loan" dialog includes a **live projection** mirroring
the server's interest math, plus a **pot-balance gate** so the borrower
can't submit a principal that exceeds available pot.

After voting, the Approve/Reject buttons disappear and a
"You voted: Approve" line shows in primary colour — this is driven by
the server's `my_vote` field on the loan response.

### Token / session lifecycle

- **Access token TTL** is 15 minutes (`JwtSettings.AccessTokenExpiryMinutes`,
  configurable per-tenant). Refresh token TTL is 7 days.
- `TokenRefresher` keeps the access token fresh on every gRPC call.
- A "session lock" screen (`SessionLockScreen.kt`) kicks in after a
  configurable inactivity period (default 5 min) — requires PIN to
  resume, doesn't drop the session. Biometric unlock if enrolled.
- On `sessionManager.logout()` the access/refresh tokens + customer_id +
  account_id are cleared but **phone_number is retained** so the next
  launch lands on the PIN-only login. `fullLogout()` clears everything.

## Build configuration

`mobile/androidApp/build.gradle.kts` has the relevant flags:

| Flag | Debug | Release |
| --- | --- | --- |
| `GRPC_HOST` | `10.0.2.2` (emulator → host loopback) | `api.goldbank.co.zw` |
| `GRPC_PORT` | `5000` | `443` |
| `GRPC_USE_TLS` | `false` (h2c) | `true` |
| `DEFAULT_TENANT_ID` | `goldbank` | `goldbank` |

`10.0.2.2` is the magic IP that, from inside an Android emulator, hits
the host machine's `localhost`. On a physical device on the same Wi-Fi,
override with the host's LAN IP.

## DI

`shared/.../di/AndroidDataModule.kt` registers everything in a single
Koin `single` module:

```kotlin
single { SecureStorage(get()) }            // EncryptedSharedPreferences
single { SessionManager(get(), tenantId) }
single {                                    // gRPC channel — once per app
    GrpcChannelFactory(
        host = grpcHost, port = grpcPort, useTls = useTls,
        interceptors = listOf(get<AuthClientInterceptor>(), get<RetryInterceptor>()),
    )
}
single<ManagedChannel> { get<GrpcChannelFactory>().create() }
single { AccountGrpcClient(get()) }
…
single { TokenRefresher(
    sessionManager = get(), authRepository = get(),
    deviceIdProvider = { Settings.Secure.getString(...) }
) }
```

ViewModels go in `PresentationModule`:

```kotlin
viewModel { EkubViewModel(get(), get()) }
viewModel { AssetViewModel(get(), get(), get()) }
…
```

## Common gotchas

- **The Kotlin Language Server in VS Code** sometimes shows
  "Unresolved reference" red squigglies on freshly-created files in the
  same package. Gradle resolves them fine. If a build mysteriously fails
  with "classes.jar in use by another process", kill the LSP process
  (`fwcd.kotlin`) and re-run.
- **`grpcCall { … }` is a top-level suspend function**. Don't call
  blocking code inside — switch threads with `withContext(Dispatchers.IO)`
  if needed. The auto-refresh check itself runs on the calling dispatcher.
- **Adaptive icon on API 26+** uses `@drawable/splash_logo` as the
  foreground; legacy bitmaps for pre-API-26 are regenerated to match.
  Launcher caches icons — `adb shell pm clear com.google.android.apps.nexuslauncher`
  to force a refresh after icon changes.
- **Customer ID vs Account ID**: most APIs are customer-scoped now.
  Always pass `sessionManager.getCustomerId()` for asset / Ekub calls;
  use `getAccountId()` for currency-bound things like balance / transfer
  source.
- **Device transfer**: re-binding an account to a new phone is a
  separate flow (`InitiateDeviceTransfer` + OTP). `authenticate` will
  refuse a new device with `Auth.DeviceMismatch` until the transfer is
  approved.

## Testing

There are no unit tests on mobile yet. Smoke testing is `adb`:

```powershell
# Build, install, launch
cd c:\Users\wmapu\Projects\GoldBank\mobile
.\gradlew :androidApp:assembleDebug
$adb = "$env:LOCALAPPDATA\Android\Sdk\platform-tools\adb.exe"
& $adb install -r androidApp\build\outputs\apk\debug\androidApp-debug.apk
& $adb shell am force-stop com.goldbank.app
& $adb shell monkey -p com.goldbank.app -c android.intent.category.LAUNCHER 1

# Watch refresh logs
& $adb logcat -s TokenRefresher
```

Demo PIN for every seeded user is `1234`. Demo phones:

| Role | Phone |
| --- | --- |
| Chairman (Borrowdale Savings) | `+263770003287` (Tendai Moyo) |
| Treasurer | `+263775304489` (Chiedza Mutasa) |
| Secretary | `+263771882741` (Farai Chikwanha) |
| Member | `+263774538185` (Nyasha Dube) |
| Registered (gold coins) | `+263771000001` (John Moyo) |
