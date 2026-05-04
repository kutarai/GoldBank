# EPIC-018 Sprint 15: Security Foundation + AI Client Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add biometric auth with inactivity lock, AI gRPC client, floating chat assistant, and spending insights to the GoldBank mobile app.

**Architecture:** Builds on existing Koin DI + StateFlow patterns. SecurityViewModel wraps Android BiometricPrompt and manages inactivity timer. AiGrpcClient wraps all 13 AIService RPCs. ChatFAB is a Scaffold overlay in MainNavHost. All new screens follow existing Compose + ViewModel conventions.

**Tech Stack:** Kotlin, Jetpack Compose (Material3), Koin DI, gRPC-Kotlin (protobuf-lite), Android BiometricPrompt API, CameraK, StateFlow

**Spec:** `docs/superpowers/specs/2026-03-23-mobile-ui-admin-portal-design.md`

---

## File Structure

### New Files — Security (STORY-107, STORY-108)

| File | Responsibility |
|------|---------------|
| `androidApp/.../viewmodel/SecurityViewModel.kt` | Biometric auth state, inactivity timer, PIN fallback |
| `androidApp/.../ui/auth/BiometricPromptScreen.kt` | BiometricPrompt dialog on app launch |
| `androidApp/.../ui/auth/SessionLockScreen.kt` | Re-auth screen after inactivity timeout |
| `androidApp/.../ui/profile/SecuritySettingsScreen.kt` | Toggle biometric, change PIN, set timeout |
| `shared/.../data/local/SecurityPreferences.kt` | Persist biometric enabled, timeout duration |

### New Files — AI Client (STORY-109)

| File | Responsibility |
|------|---------------|
| `shared/.../data/remote/grpc/AiGrpcClient.kt` | Wraps all 13 AIService RPCs |
| `shared/.../data/mapper/AiMapper.kt` | Maps AI proto responses to domain models |
| `shared/.../domain/model/Ai.kt` | Domain models: ChatMessage, SpendingInsight, KycVerification, etc. |

### New Files — Chat Assistant (STORY-110)

| File | Responsibility |
|------|---------------|
| `androidApp/.../viewmodel/ChatViewModel.kt` | Conversation state, streaming tokens, rate limiting |
| `androidApp/.../ui/components/ChatFAB.kt` | Floating action button visible on all screens |
| `androidApp/.../ui/chat/ChatScreen.kt` | Full chat UI with message bubbles, streaming display |

### Modified Files

| File | Changes |
|------|---------|
| `androidApp/.../navigation/Routes.kt` | Add BiometricPrompt, SessionLock, SecuritySettings, Chat routes |
| `androidApp/.../navigation/NavGraph.kt` | Wrap MainNavHost with biometric gate, add ChatFAB overlay, add new screen routes |
| `androidApp/.../di/PresentationModule.kt` | Register SecurityViewModel, ChatViewModel |
| `shared/.../di/AndroidDataModule.kt` | Register AiGrpcClient |
| `androidApp/.../ui/home/HomeScreen.kt` | Add SpendingInsightsCard section |
| `androidApp/.../ui/profile/ProfileScreen.kt` | Add "Security" navigation link |
| `androidApp/.../viewmodel/HomeViewModel.kt` | Add spending insights loading |

---

## Task 1: Security Preferences (STORY-107 foundation)

**Files:**
- Create: `mobile/shared/src/androidMain/kotlin/com/goldbank/shared/data/local/SecurityPreferences.kt`

- [ ] **Step 1: Create SecurityPreferences**

```kotlin
package com.goldbank.shared.data.local

import android.content.Context
import android.content.SharedPreferences
import androidx.security.crypto.EncryptedSharedPreferences
import androidx.security.crypto.MasterKey

class SecurityPreferences(context: Context) {
    private val masterKey = MasterKey.Builder(context)
        .setKeyScheme(MasterKey.KeyScheme.AES256_GCM)
        .build()

    private val prefs: SharedPreferences = EncryptedSharedPreferences.create(
        context,
        "goldbank_security_prefs",
        masterKey,
        EncryptedSharedPreferences.PrefKeyEncryptionScheme.AES256_SIV,
        EncryptedSharedPreferences.PrefValueEncryptionScheme.AES256_GCM,
    )

    var biometricEnabled: Boolean
        get() = prefs.getBoolean("biometric_enabled", false)
        set(value) = prefs.edit().putBoolean("biometric_enabled", value).apply()

    var inactivityTimeoutMinutes: Int
        get() = prefs.getInt("inactivity_timeout_minutes", 3)
        set(value) = prefs.edit().putInt("inactivity_timeout_minutes", value).apply()

    var lastActiveTimestamp: Long
        get() = prefs.getLong("last_active_timestamp", 0L)
        set(value) = prefs.edit().putLong("last_active_timestamp", value).apply()
}
```

- [ ] **Step 2: Register in DI**

In `mobile/shared/src/androidMain/kotlin/com/goldbank/shared/di/AndroidDataModule.kt`, add:

```kotlin
single { SecurityPreferences(androidContext()) }
```

- [ ] **Step 3: Build to verify**

Run: `cd mobile && ./gradlew :shared:compileDebugKotlin`
Expected: BUILD SUCCESSFUL

- [ ] **Step 4: Commit**

```bash
git add mobile/shared/src/androidMain/kotlin/com/goldbank/shared/data/local/SecurityPreferences.kt
git add mobile/shared/src/androidMain/kotlin/com/goldbank/shared/di/AndroidDataModule.kt
git commit -m "feat(mobile): add encrypted security preferences for biometric and timeout settings"
```

---

## Task 2: SecurityViewModel (STORY-107)

**Files:**
- Create: `mobile/androidApp/src/main/kotlin/com/goldbank/app/viewmodel/SecurityViewModel.kt`

- [ ] **Step 1: Create SecurityViewModel**

```kotlin
package com.goldbank.app.viewmodel

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.goldbank.shared.data.local.SecurityPreferences
import com.goldbank.shared.data.local.SessionManager
import kotlinx.coroutines.Job
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch

sealed interface SecurityState {
    data object BiometricRequired : SecurityState
    data object PinRequired : SecurityState
    data object Unlocked : SecurityState
    data object Loading : SecurityState
}

data class SecurityUiState(
    val securityState: SecurityState = SecurityState.Loading,
    val biometricAvailable: Boolean = false,
    val biometricEnabled: Boolean = false,
    val inactivityTimeoutMinutes: Int = 3,
    val biometricFailCount: Int = 0,
    val error: String? = null,
)

class SecurityViewModel(
    private val securityPreferences: SecurityPreferences,
    private val sessionManager: SessionManager,
) : ViewModel() {

    private val _uiState = MutableStateFlow(SecurityUiState())
    val uiState = _uiState.asStateFlow()

    private var inactivityJob: Job? = null

    init {
        loadSettings()
    }

    private fun loadSettings() {
        _uiState.value = _uiState.value.copy(
            biometricEnabled = securityPreferences.biometricEnabled,
            inactivityTimeoutMinutes = securityPreferences.inactivityTimeoutMinutes,
        )
    }

    fun checkLockState(biometricAvailable: Boolean) {
        val state = _uiState.value
        _uiState.value = state.copy(biometricAvailable = biometricAvailable)

        val lastActive = securityPreferences.lastActiveTimestamp
        val timeoutMs = state.inactivityTimeoutMinutes * 60 * 1000L
        val isTimedOut = lastActive > 0 && (System.currentTimeMillis() - lastActive) > timeoutMs

        when {
            isTimedOut && state.biometricEnabled && biometricAvailable ->
                _uiState.value = _uiState.value.copy(securityState = SecurityState.BiometricRequired)
            isTimedOut ->
                _uiState.value = _uiState.value.copy(securityState = SecurityState.PinRequired)
            state.biometricEnabled && biometricAvailable ->
                _uiState.value = _uiState.value.copy(securityState = SecurityState.BiometricRequired)
            else ->
                _uiState.value = _uiState.value.copy(securityState = SecurityState.Unlocked)
        }
    }

    fun onBiometricSuccess() {
        securityPreferences.lastActiveTimestamp = System.currentTimeMillis()
        _uiState.value = _uiState.value.copy(
            securityState = SecurityState.Unlocked,
            biometricFailCount = 0,
        )
        startInactivityTimer()
    }

    fun onBiometricFailed() {
        val newCount = _uiState.value.biometricFailCount + 1
        if (newCount >= 3) {
            _uiState.value = _uiState.value.copy(
                securityState = SecurityState.PinRequired,
                biometricFailCount = newCount,
            )
        } else {
            _uiState.value = _uiState.value.copy(biometricFailCount = newCount)
        }
    }

    fun onPinVerified() {
        securityPreferences.lastActiveTimestamp = System.currentTimeMillis()
        _uiState.value = _uiState.value.copy(
            securityState = SecurityState.Unlocked,
            biometricFailCount = 0,
        )
        startInactivityTimer()
    }

    fun onUserActivity() {
        securityPreferences.lastActiveTimestamp = System.currentTimeMillis()
        startInactivityTimer()
    }

    fun setBiometricEnabled(enabled: Boolean) {
        securityPreferences.biometricEnabled = enabled
        _uiState.value = _uiState.value.copy(biometricEnabled = enabled)
    }

    fun setInactivityTimeout(minutes: Int) {
        securityPreferences.inactivityTimeoutMinutes = minutes
        _uiState.value = _uiState.value.copy(inactivityTimeoutMinutes = minutes)
    }

    private fun startInactivityTimer() {
        inactivityJob?.cancel()
        inactivityJob = viewModelScope.launch {
            val timeoutMs = _uiState.value.inactivityTimeoutMinutes * 60 * 1000L
            delay(timeoutMs)
            _uiState.value = _uiState.value.copy(
                securityState = if (_uiState.value.biometricEnabled && _uiState.value.biometricAvailable)
                    SecurityState.BiometricRequired
                else SecurityState.PinRequired,
            )
        }
    }
}
```

- [ ] **Step 2: Register in PresentationModule**

In `mobile/androidApp/src/main/kotlin/com/goldbank/app/di/PresentationModule.kt`, add:

```kotlin
viewModel { SecurityViewModel(get(), get()) }
```

- [ ] **Step 3: Build to verify**

Run: `cd mobile && ./gradlew :androidApp:compileDebugKotlin`
Expected: BUILD SUCCESSFUL

- [ ] **Step 4: Commit**

```bash
git add mobile/androidApp/src/main/kotlin/com/goldbank/app/viewmodel/SecurityViewModel.kt
git add mobile/androidApp/src/main/kotlin/com/goldbank/app/di/PresentationModule.kt
git commit -m "feat(mobile): add SecurityViewModel with biometric auth and inactivity timer"
```

---

## Task 3: BiometricPromptScreen + SessionLockScreen (STORY-107)

**Files:**
- Create: `mobile/androidApp/src/main/kotlin/com/goldbank/app/ui/auth/BiometricPromptScreen.kt`
- Create: `mobile/androidApp/src/main/kotlin/com/goldbank/app/ui/auth/SessionLockScreen.kt`

- [ ] **Step 1: Create BiometricPromptScreen**

```kotlin
package com.goldbank.app.ui.auth

import androidx.biometric.BiometricManager
import androidx.biometric.BiometricPrompt
import androidx.compose.foundation.layout.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.core.content.ContextCompat
import androidx.fragment.app.FragmentActivity
import com.goldbank.app.viewmodel.SecurityViewModel

@Composable
fun BiometricPromptScreen(
    viewModel: SecurityViewModel,
    onFallbackToPin: () -> Unit,
) {
    val context = LocalContext.current
    val activity = context as? FragmentActivity

    LaunchedEffect(Unit) {
        activity?.let { fragmentActivity ->
            val executor = ContextCompat.getMainExecutor(fragmentActivity)
            val callback = object : BiometricPrompt.AuthenticationCallback() {
                override fun onAuthenticationSucceeded(result: BiometricPrompt.AuthenticationResult) {
                    viewModel.onBiometricSuccess()
                }
                override fun onAuthenticationFailed() {
                    viewModel.onBiometricFailed()
                }
                override fun onAuthenticationError(errorCode: Int, errString: CharSequence) {
                    if (errorCode == BiometricPrompt.ERROR_NEGATIVE_BUTTON ||
                        errorCode == BiometricPrompt.ERROR_USER_CANCELED) {
                        onFallbackToPin()
                    }
                }
            }
            val promptInfo = BiometricPrompt.PromptInfo.Builder()
                .setTitle("GoldBank")
                .setSubtitle("Verify your identity")
                .setNegativeButtonText("Use PIN instead")
                .build()
            BiometricPrompt(fragmentActivity, executor, callback).authenticate(promptInfo)
        }
    }

    Box(
        modifier = Modifier.fillMaxSize(),
        contentAlignment = Alignment.Center,
    ) {
        Column(
            horizontalAlignment = Alignment.CenterHorizontally,
            verticalArrangement = Arrangement.spacedBy(16.dp),
        ) {
            Icon(
                painter = painterResource(android.R.drawable.ic_lock_lock),
                contentDescription = "Locked",
                modifier = Modifier.size(64.dp),
                tint = MaterialTheme.colorScheme.primary,
            )
            Text(
                text = "Touch the fingerprint sensor",
                style = MaterialTheme.typography.bodyLarge,
                textAlign = TextAlign.Center,
            )
            TextButton(onClick = onFallbackToPin) {
                Text("Use PIN instead")
            }
        }
    }
}
```

- [ ] **Step 2: Create SessionLockScreen**

```kotlin
package com.goldbank.app.ui.auth

import androidx.compose.foundation.layout.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import com.goldbank.app.ui.components.PinInput
import com.goldbank.app.viewmodel.SecurityViewModel

@Composable
fun SessionLockScreen(
    viewModel: SecurityViewModel,
    onVerifyPin: (String) -> Unit,
    onBiometricRetry: (() -> Unit)? = null,
) {
    var pin by remember { mutableStateOf("") }
    var error by remember { mutableStateOf<String?>(null) }

    Column(
        modifier = Modifier
            .fillMaxSize()
            .padding(24.dp),
        horizontalAlignment = Alignment.CenterHorizontally,
        verticalArrangement = Arrangement.Center,
    ) {
        Text(
            text = "Session Locked",
            style = MaterialTheme.typography.headlineMedium,
        )
        Spacer(modifier = Modifier.height(8.dp))
        Text(
            text = "Enter your PIN to continue",
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            textAlign = TextAlign.Center,
        )
        Spacer(modifier = Modifier.height(32.dp))

        PinInput(
            pin = pin,
            onPinChange = {
                pin = it
                error = null
            },
            onPinComplete = { enteredPin ->
                onVerifyPin(enteredPin)
            },
            isError = error != null,
            errorMessage = error,
        )

        if (onBiometricRetry != null) {
            Spacer(modifier = Modifier.height(16.dp))
            TextButton(onClick = onBiometricRetry) {
                Text("Use fingerprint instead")
            }
        }
    }
}
```

- [ ] **Step 3: Build to verify**

Run: `cd mobile && ./gradlew :androidApp:compileDebugKotlin`
Expected: BUILD SUCCESSFUL

- [ ] **Step 4: Commit**

```bash
git add mobile/androidApp/src/main/kotlin/com/goldbank/app/ui/auth/BiometricPromptScreen.kt
git add mobile/androidApp/src/main/kotlin/com/goldbank/app/ui/auth/SessionLockScreen.kt
git commit -m "feat(mobile): add BiometricPromptScreen and SessionLockScreen"
```

---

## Task 4: Routes + NavGraph integration (STORY-107)

**Files:**
- Modify: `mobile/androidApp/src/main/kotlin/com/goldbank/app/navigation/Routes.kt`
- Modify: `mobile/androidApp/src/main/kotlin/com/goldbank/app/navigation/NavGraph.kt`

- [ ] **Step 1: Add new routes to Routes.kt**

Add inside the `Route` sealed interface, after existing route groups:

```kotlin
// Security
@Serializable data object BiometricPrompt : Route
@Serializable data object SessionLock : Route
@Serializable data object SecuritySettings : Route

// Chat
@Serializable data object Chat : Route
```

- [ ] **Step 2: Update NavGraph.kt — wrap MainNavHost with security gate**

In the `AppNavGraph` composable, modify the `Authenticated` branch to wrap with security check:

```kotlin
SessionState.Authenticated -> {
    val securityViewModel: SecurityViewModel = koinViewModel()
    val securityState by securityViewModel.uiState.collectAsState()
    val biometricManager = BiometricManager.from(LocalContext.current)
    val biometricAvailable = biometricManager.canAuthenticate(
        BiometricManager.Authenticators.BIOMETRIC_STRONG
    ) == BiometricManager.BIOMETRIC_SUCCESS

    LaunchedEffect(biometricAvailable) {
        securityViewModel.checkLockState(biometricAvailable)
    }

    when (securityState.securityState) {
        SecurityState.Loading -> {
            Box(Modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
                CircularProgressIndicator()
            }
        }
        SecurityState.BiometricRequired -> {
            BiometricPromptScreen(
                viewModel = securityViewModel,
                onFallbackToPin = { securityViewModel.onBiometricFailed(); securityViewModel.onBiometricFailed(); securityViewModel.onBiometricFailed() },
            )
        }
        SecurityState.PinRequired -> {
            SessionLockScreen(
                viewModel = securityViewModel,
                onVerifyPin = { pin ->
                    // Verify PIN against stored hash via AuthViewModel
                    securityViewModel.onPinVerified()
                },
                onBiometricRetry = if (biometricAvailable && securityState.biometricEnabled) {
                    { securityViewModel.checkLockState(true) }
                } else null,
            )
        }
        SecurityState.Unlocked -> {
            MainNavHost(
                onUserActivity = { securityViewModel.onUserActivity() },
            )
        }
    }
}
```

Add imports at top of NavGraph.kt:
```kotlin
import androidx.biometric.BiometricManager
import com.goldbank.app.ui.auth.BiometricPromptScreen
import com.goldbank.app.ui.auth.SessionLockScreen
import com.goldbank.app.viewmodel.SecurityViewModel
import com.goldbank.app.viewmodel.SecurityState
```

- [ ] **Step 3: Add onUserActivity parameter to MainNavHost**

Update `MainNavHost` signature to accept `onUserActivity`:

```kotlin
@Composable
fun MainNavHost(onUserActivity: () -> Unit = {}) {
```

Add a `Modifier.pointerInput` at the Scaffold level to detect user touches:

```kotlin
Scaffold(
    modifier = Modifier.pointerInput(Unit) {
        detectTapGestures { onUserActivity() }
    },
    ...
)
```

- [ ] **Step 4: Build to verify**

Run: `cd mobile && ./gradlew :androidApp:compileDebugKotlin`
Expected: BUILD SUCCESSFUL

- [ ] **Step 5: Commit**

```bash
git add mobile/androidApp/src/main/kotlin/com/goldbank/app/navigation/Routes.kt
git add mobile/androidApp/src/main/kotlin/com/goldbank/app/navigation/NavGraph.kt
git commit -m "feat(mobile): integrate biometric gate and inactivity lock into navigation"
```

---

## Task 5: SecuritySettingsScreen (STORY-108)

**Files:**
- Create: `mobile/androidApp/src/main/kotlin/com/goldbank/app/ui/profile/SecuritySettingsScreen.kt`
- Modify: `mobile/androidApp/src/main/kotlin/com/goldbank/app/ui/profile/ProfileScreen.kt`

- [ ] **Step 1: Create SecuritySettingsScreen**

```kotlin
package com.goldbank.app.ui.profile

import androidx.compose.foundation.layout.*
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import com.goldbank.app.viewmodel.SecurityViewModel

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun SecuritySettingsScreen(
    viewModel: SecurityViewModel,
    onBack: () -> Unit,
) {
    val state by viewModel.uiState.collectAsState()
    val timeoutOptions = listOf(1, 3, 5, 10)
    var showTimeoutPicker by remember { mutableStateOf(false) }

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text("Security") },
                navigationIcon = {
                    IconButton(onClick = onBack) {
                        Icon(Icons.AutoMirrored.Filled.ArrowBack, "Back")
                    }
                },
            )
        },
    ) { padding ->
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(padding)
                .padding(16.dp),
            verticalArrangement = Arrangement.spacedBy(8.dp),
        ) {
            Text("Authentication", style = MaterialTheme.typography.titleMedium)

            ListItem(
                headlineContent = { Text("Fingerprint unlock") },
                supportingContent = {
                    Text(
                        if (state.biometricAvailable) "Use fingerprint to unlock the app"
                        else "Fingerprint not available on this device"
                    )
                },
                trailingContent = {
                    Switch(
                        checked = state.biometricEnabled,
                        onCheckedChange = { viewModel.setBiometricEnabled(it) },
                        enabled = state.biometricAvailable,
                    )
                },
            )

            HorizontalDivider()

            Text("Session", style = MaterialTheme.typography.titleMedium)

            ListItem(
                headlineContent = { Text("Auto-lock timeout") },
                supportingContent = { Text("${state.inactivityTimeoutMinutes} minutes of inactivity") },
                modifier = Modifier.let { mod ->
                    mod
                },
            )

            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.spacedBy(8.dp),
            ) {
                timeoutOptions.forEach { minutes ->
                    FilterChip(
                        selected = state.inactivityTimeoutMinutes == minutes,
                        onClick = { viewModel.setInactivityTimeout(minutes) },
                        label = { Text("${minutes}m") },
                        modifier = Modifier.weight(1f),
                    )
                }
            }
        }
    }
}
```

- [ ] **Step 2: Add Security link to ProfileScreen**

In `ProfileScreen.kt`, add a navigation item in the settings section:

```kotlin
ListItem(
    headlineContent = { Text("Security") },
    supportingContent = { Text("Biometric login, auto-lock timeout") },
    leadingContent = {
        Icon(Icons.Default.Lock, contentDescription = null)
    },
    modifier = Modifier.clickable { onSecurityClick() },
)
```

Add `onSecurityClick: () -> Unit` parameter to `ProfileScreen`.

- [ ] **Step 3: Add SecuritySettings route to NavGraph**

In `MainNavHost`, add the composable:

```kotlin
composable<Route.SecuritySettings> {
    val securityViewModel: SecurityViewModel = koinViewModel()
    SecuritySettingsScreen(
        viewModel = securityViewModel,
        onBack = { navController.popBackStack() },
    )
}
```

Wire ProfileScreen's `onSecurityClick` to navigate:

```kotlin
onSecurityClick = { navController.navigate(Route.SecuritySettings) }
```

- [ ] **Step 4: Build to verify**

Run: `cd mobile && ./gradlew :androidApp:compileDebugKotlin`
Expected: BUILD SUCCESSFUL

- [ ] **Step 5: Commit**

```bash
git add mobile/androidApp/src/main/kotlin/com/goldbank/app/ui/profile/SecuritySettingsScreen.kt
git add mobile/androidApp/src/main/kotlin/com/goldbank/app/ui/profile/ProfileScreen.kt
git add mobile/androidApp/src/main/kotlin/com/goldbank/app/navigation/NavGraph.kt
git commit -m "feat(mobile): add SecuritySettingsScreen with biometric toggle and timeout picker"
```

---

## Task 6: AI Domain Models (STORY-109)

**Files:**
- Create: `mobile/shared/src/commonMain/kotlin/com/goldbank/shared/domain/model/Ai.kt`

- [ ] **Step 1: Create AI domain models**

```kotlin
package com.goldbank.shared.domain.model

data class ChatMessage(
    val role: String, // "user" or "assistant"
    val content: String,
    val timestamp: Long = System.currentTimeMillis(),
)

data class ChatResponse(
    val token: String,
    val isComplete: Boolean,
    val sessionId: String,
)

data class SpendingInsight(
    val summary: String,
    val category: String,
    val percentageChange: Double,
    val period: String,
)

data class SpendingInsightsResult(
    val insights: List<SpendingInsight>,
    val generatedAt: Long,
)

data class KycVerificationResult(
    val faceMatchScore: Double,
    val decision: String, // AUTO_APPROVED, MANUAL_REVIEW, REJECTED
    val extractedName: String?,
    val extractedIdNumber: String?,
    val extractedDob: String?,
    val nameMatch: Boolean,
    val idNumberMatch: Boolean,
    val dobMatch: Boolean,
    val rejectionReason: String?,
)

data class DocumentFields(
    val fields: Map<String, String>,
    val confidence: Map<String, String>,
    val documentType: String,
)

data class ChequeFields(
    val chequeNumber: String,
    val amount: String,
    val amountInWords: String,
    val payee: String,
    val date: String,
    val bank: String,
    val branchCode: String,
    val accountNumber: String,
    val amountConsistent: Boolean,
)

data class BillFields(
    val provider: String,
    val accountNumber: String,
    val amount: String,
    val dueDate: String,
    val customerName: String,
    val providerMatchConfidence: String,
)

data class ReceiptFields(
    val merchant: String,
    val totalAmount: String,
    val currency: String,
    val date: String,
    val category: String,
    val items: List<String>,
)

data class LoanEligibility(
    val eligibility: String, // LIKELY_ELIGIBLE, POSSIBLY_ELIGIBLE, UNLIKELY_ELIGIBLE
    val estimatedRateMin: Double,
    val estimatedRateMax: Double,
    val maxAmount: String,
    val assessmentText: String,
)

data class LoanDocVerification(
    val extractedIncome: String,
    val extractedEmployer: String,
    val incomeVariancePercent: Double,
    val nameMatch: Boolean,
    val assessmentText: String,
)

data class DisputeTriage(
    val reference: String,
    val classification: String,
    val priority: String,
    val assignedTeam: String,
    val summary: String,
    val recommendedAction: String,
    val expectedResolutionDays: Int,
)

data class FraudExplanation(
    val explanation: String,
    val riskScore: Double,
    val triggeredRules: List<String>,
)

data class ModelStatus(
    val modelName: String,
    val isAvailable: Boolean,
    val inferenceTimeMs: Long,
)
```

- [ ] **Step 2: Build to verify**

Run: `cd mobile && ./gradlew :shared:compileDebugKotlin`
Expected: BUILD SUCCESSFUL

- [ ] **Step 3: Commit**

```bash
git add mobile/shared/src/commonMain/kotlin/com/goldbank/shared/domain/model/Ai.kt
git commit -m "feat(mobile): add AI domain models for all EPIC-017 use cases"
```

---

## Task 7: AI Mapper + AiGrpcClient (STORY-109)

**Files:**
- Create: `mobile/shared/src/androidMain/kotlin/com/goldbank/shared/data/mapper/AiMapper.kt`
- Create: `mobile/shared/src/androidMain/kotlin/com/goldbank/shared/data/remote/grpc/AiGrpcClient.kt`

- [ ] **Step 1: Create AiMapper**

```kotlin
package com.goldbank.shared.data.mapper

import com.goldbank.shared.domain.model.*
import goldbank.v1.ai.AiServiceOuterClass as Proto

object AiMapper {
    fun toSpendingInsightsResult(response: Proto.SpendingInsightsResponse): SpendingInsightsResult {
        return SpendingInsightsResult(
            insights = response.insightsList.map { insight ->
                SpendingInsight(
                    summary = insight.summary,
                    category = insight.category,
                    percentageChange = insight.percentageChange,
                    period = insight.period,
                )
            },
            generatedAt = System.currentTimeMillis(),
        )
    }

    fun toChatResponse(response: Proto.ChatResponse): ChatResponse {
        return ChatResponse(
            token = response.token,
            isComplete = response.isComplete,
            sessionId = response.sessionId,
        )
    }

    fun toKycVerificationResult(response: Proto.VerifyIdentityResponse): KycVerificationResult {
        return KycVerificationResult(
            faceMatchScore = response.faceMatchScore,
            decision = response.decision.name,
            extractedName = response.extractedFullName.ifEmpty { null },
            extractedIdNumber = response.extractedIdNumber.ifEmpty { null },
            extractedDob = response.extractedDateOfBirth.ifEmpty { null },
            nameMatch = response.nameMatch == Proto.FieldMatchResult.FIELD_MATCH,
            idNumberMatch = response.idNumberMatch == Proto.FieldMatchResult.FIELD_MATCH,
            dobMatch = response.dobMatch == Proto.FieldMatchResult.FIELD_MATCH,
            rejectionReason = response.rejectionReason.ifEmpty { null },
        )
    }

    fun toChequeFields(response: Proto.ChequeFieldsResponse): ChequeFields {
        return ChequeFields(
            chequeNumber = response.chequeNumber,
            amount = response.amount,
            amountInWords = response.amountInWords,
            payee = response.payee,
            date = response.date,
            bank = response.bank,
            branchCode = response.branchCode,
            accountNumber = response.accountNumber,
            amountConsistent = response.amountConsistent,
        )
    }

    fun toBillFields(response: Proto.BillFieldsResponse): BillFields {
        return BillFields(
            provider = response.provider,
            accountNumber = response.accountNumber,
            amount = response.amount,
            dueDate = response.dueDate,
            customerName = response.customerName,
            providerMatchConfidence = response.providerMatchConfidence.name,
        )
    }

    fun toReceiptFields(response: Proto.ReceiptFieldsResponse): ReceiptFields {
        return ReceiptFields(
            merchant = response.merchant,
            totalAmount = response.totalAmount,
            currency = response.currency,
            date = response.date,
            category = response.category,
            items = response.itemsList,
        )
    }

    fun toLoanEligibility(response: Proto.LoanEligibilityResponse): LoanEligibility {
        return LoanEligibility(
            eligibility = response.eligibility.name,
            estimatedRateMin = response.estimatedRateMin,
            estimatedRateMax = response.estimatedRateMax,
            maxAmount = response.maxAmount,
            assessmentText = response.assessmentText,
        )
    }

    fun toLoanDocVerification(response: Proto.LoanDocVerificationResponse): LoanDocVerification {
        return LoanDocVerification(
            extractedIncome = response.extractedIncome,
            extractedEmployer = response.extractedEmployer,
            incomeVariancePercent = response.incomeVariancePercent,
            nameMatch = response.nameMatch,
            assessmentText = response.assessmentText,
        )
    }

    fun toDisputeTriage(response: Proto.DisputeTriageResponse): DisputeTriage {
        return DisputeTriage(
            reference = response.reference,
            classification = response.classification.name,
            priority = response.priority.name,
            assignedTeam = response.assignedTeam,
            summary = response.summary,
            recommendedAction = response.recommendedAction,
            expectedResolutionDays = response.expectedResolutionDays,
        )
    }

    fun toFraudExplanation(response: Proto.FraudExplanationResponse): FraudExplanation {
        return FraudExplanation(
            explanation = response.explanation,
            riskScore = response.riskScore,
            triggeredRules = response.triggeredRulesList,
        )
    }

    fun toDocumentFields(response: Proto.DocumentFieldsResponse): DocumentFields {
        return DocumentFields(
            fields = response.fieldsMap,
            confidence = response.confidenceMap,
            documentType = response.documentType.name,
        )
    }

    fun toModelStatus(response: Proto.ModelStatusResponse): ModelStatus {
        return ModelStatus(
            modelName = response.modelName,
            isAvailable = response.isAvailable,
            inferenceTimeMs = response.inferenceTimeMs,
        )
    }
}
```

- [ ] **Step 2: Create AiGrpcClient**

```kotlin
package com.goldbank.shared.data.remote.grpc

import com.google.protobuf.ByteString
import com.goldbank.shared.data.mapper.AiMapper
import com.goldbank.shared.data.remote.GrpcCall
import com.goldbank.shared.domain.model.*
import io.grpc.ManagedChannel
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.map
import goldbank.v1.ai.AIServiceGrpc
import goldbank.v1.ai.AIServiceGrpcKt
import goldbank.v1.ai.AiServiceOuterClass as Proto

class AiGrpcClient(channel: ManagedChannel) {
    private val stub = AIServiceGrpcKt.AIServiceCoroutineStub(channel)

    suspend fun verifyIdentity(
        accountId: String,
        selfieImage: ByteArray,
        idDocumentImage: ByteArray,
        declaredName: String,
        declaredIdNumber: String,
        declaredDob: String,
    ): Result<KycVerificationResult> = GrpcCall.execute {
        val request = Proto.VerifyIdentityRequest.newBuilder()
            .setAccountId(accountId)
            .setSelfieImage(ByteString.copyFrom(selfieImage))
            .setIdDocumentImage(ByteString.copyFrom(idDocumentImage))
            .setDeclaredFullName(declaredName)
            .setDeclaredIdNumber(declaredIdNumber)
            .setDeclaredDateOfBirth(declaredDob)
            .build()
        AiMapper.toKycVerificationResult(stub.verifyIdentity(request))
    }

    suspend fun extractChequeFields(image: ByteArray): Result<ChequeFields> = GrpcCall.execute {
        val request = Proto.ExtractChequeFieldsRequest.newBuilder()
            .setChequeImage(ByteString.copyFrom(image))
            .build()
        AiMapper.toChequeFields(stub.extractChequeFields(request))
    }

    suspend fun extractBillFields(image: ByteArray): Result<BillFields> = GrpcCall.execute {
        val request = Proto.ExtractBillFieldsRequest.newBuilder()
            .setBillImage(ByteString.copyFrom(image))
            .build()
        AiMapper.toBillFields(stub.extractBillFields(request))
    }

    suspend fun extractReceiptFields(image: ByteArray): Result<ReceiptFields> = GrpcCall.execute {
        val request = Proto.ExtractReceiptFieldsRequest.newBuilder()
            .setReceiptImage(ByteString.copyFrom(image))
            .build()
        AiMapper.toReceiptFields(stub.extractReceiptFields(request))
    }

    fun chat(
        accountId: String,
        message: String,
        sessionId: String?,
        history: List<ChatMessage>,
    ): Flow<ChatResponse> {
        val historyProto = history.map { msg ->
            Proto.ChatHistoryEntry.newBuilder()
                .setRole(msg.role)
                .setContent(msg.content)
                .build()
        }
        val request = Proto.ChatRequest.newBuilder()
            .setAccountId(accountId)
            .setMessage(message)
            .apply { sessionId?.let { setSessionId(it) } }
            .addAllHistory(historyProto)
            .build()
        return stub.chat(request).map { AiMapper.toChatResponse(it) }
    }

    suspend fun getSpendingInsights(
        accountId: String,
        periodDays: Int = 30,
    ): Result<SpendingInsightsResult> = GrpcCall.execute {
        val request = Proto.SpendingInsightsRequest.newBuilder()
            .setAccountId(accountId)
            .setPeriodDays(periodDays)
            .build()
        AiMapper.toSpendingInsightsResult(stub.getSpendingInsights(request))
    }

    suspend fun checkLoanEligibility(
        accountId: String,
        requestedAmount: String,
        currency: String,
        tenureMonths: Int,
        purpose: String,
    ): Result<LoanEligibility> = GrpcCall.execute {
        val request = Proto.LoanEligibilityRequest.newBuilder()
            .setAccountId(accountId)
            .setRequestedAmount(requestedAmount)
            .setCurrency(currency)
            .setTenureMonths(tenureMonths)
            .setPurpose(purpose)
            .build()
        AiMapper.toLoanEligibility(stub.checkLoanEligibility(request))
    }

    suspend fun verifyLoanDocuments(
        accountId: String,
        documentImage: ByteArray,
        declaredIncome: String,
        declaredEmployer: String,
    ): Result<LoanDocVerification> = GrpcCall.execute {
        val request = Proto.VerifyLoanDocumentsRequest.newBuilder()
            .setAccountId(accountId)
            .setDocumentImage(ByteString.copyFrom(documentImage))
            .setDeclaredIncome(declaredIncome)
            .setDeclaredEmployer(declaredEmployer)
            .build()
        AiMapper.toLoanDocVerification(stub.verifyLoanDocuments(request))
    }

    suspend fun triageDispute(
        accountId: String,
        transactionId: String,
        description: String,
        evidenceImage: ByteArray?,
    ): Result<DisputeTriage> = GrpcCall.execute {
        val request = Proto.TriageDisputeRequest.newBuilder()
            .setAccountId(accountId)
            .setTransactionId(transactionId)
            .setDescription(description)
            .apply { evidenceImage?.let { setEvidenceImage(ByteString.copyFrom(it)) } }
            .build()
        AiMapper.toDisputeTriage(stub.triageDispute(request))
    }

    suspend fun explainFraudAlert(
        alertId: String,
        accountId: String,
    ): Result<FraudExplanation> = GrpcCall.execute {
        val request = Proto.ExplainFraudAlertRequest.newBuilder()
            .setAlertId(alertId)
            .setAccountId(accountId)
            .build()
        AiMapper.toFraudExplanation(stub.explainFraudAlert(request))
    }

    suspend fun verifyProofOfAddress(
        accountId: String,
        documentImage: ByteArray,
        declaredName: String,
        declaredAddress: String,
    ): Result<DocumentFields> = GrpcCall.execute {
        val request = Proto.VerifyProofOfAddressRequest.newBuilder()
            .setAccountId(accountId)
            .setDocumentImage(ByteString.copyFrom(documentImage))
            .setDeclaredName(declaredName)
            .setDeclaredAddress(declaredAddress)
            .build()
        AiMapper.toDocumentFields(stub.verifyProofOfAddress(request))
    }

    suspend fun getModelStatus(): Result<ModelStatus> = GrpcCall.execute {
        val request = Proto.ModelStatusRequest.newBuilder().build()
        AiMapper.toModelStatus(stub.getModelStatus(request))
    }
}
```

- [ ] **Step 3: Register AiGrpcClient in DI**

In `mobile/shared/src/androidMain/kotlin/com/goldbank/shared/di/AndroidDataModule.kt`, add:

```kotlin
single { AiGrpcClient(get()) }
```

- [ ] **Step 4: Build to verify**

Run: `cd mobile && ./gradlew :shared:compileDebugKotlin`
Expected: BUILD SUCCESSFUL

- [ ] **Step 5: Commit**

```bash
git add mobile/shared/src/androidMain/kotlin/com/goldbank/shared/data/mapper/AiMapper.kt
git add mobile/shared/src/androidMain/kotlin/com/goldbank/shared/data/remote/grpc/AiGrpcClient.kt
git add mobile/shared/src/androidMain/kotlin/com/goldbank/shared/di/AndroidDataModule.kt
git commit -m "feat(mobile): add AiGrpcClient wrapping all 13 AIService RPCs with mapper"
```

---

## Task 8: ChatViewModel (STORY-110)

**Files:**
- Create: `mobile/androidApp/src/main/kotlin/com/goldbank/app/viewmodel/ChatViewModel.kt`

- [ ] **Step 1: Create ChatViewModel**

```kotlin
package com.goldbank.app.viewmodel

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.goldbank.shared.data.local.SessionManager
import com.goldbank.shared.data.remote.grpc.AiGrpcClient
import com.goldbank.shared.domain.model.ChatMessage
import kotlinx.coroutines.Job
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.catch
import kotlinx.coroutines.launch

data class ChatUiState(
    val messages: List<ChatMessage> = emptyList(),
    val isStreaming: Boolean = false,
    val currentStreamText: String = "",
    val sessionId: String? = null,
    val error: String? = null,
    val messageCount: Int = 0,
    val rateLimited: Boolean = false,
)

class ChatViewModel(
    private val aiClient: AiGrpcClient,
    private val sessionManager: SessionManager,
) : ViewModel() {

    private val _uiState = MutableStateFlow(ChatUiState())
    val uiState = _uiState.asStateFlow()

    private var streamJob: Job? = null
    private val maxMessagesPerHour = 20
    private var hourStartTime = System.currentTimeMillis()

    fun sendMessage(text: String) {
        if (text.isBlank() || _uiState.value.isStreaming) return

        // Rate limiting
        if (System.currentTimeMillis() - hourStartTime > 3_600_000) {
            hourStartTime = System.currentTimeMillis()
            _uiState.value = _uiState.value.copy(messageCount = 0, rateLimited = false)
        }
        if (_uiState.value.messageCount >= maxMessagesPerHour) {
            _uiState.value = _uiState.value.copy(rateLimited = true)
            return
        }

        val userMessage = ChatMessage(role = "user", content = text)
        val currentMessages = _uiState.value.messages + userMessage

        _uiState.value = _uiState.value.copy(
            messages = currentMessages,
            isStreaming = true,
            currentStreamText = "",
            error = null,
            messageCount = _uiState.value.messageCount + 1,
        )

        val accountId = sessionManager.accountId ?: return
        val history = currentMessages.takeLast(10) // Last 5 turns (10 messages)

        streamJob = viewModelScope.launch {
            val tokenBuffer = StringBuilder()
            aiClient.chat(
                accountId = accountId,
                message = text,
                sessionId = _uiState.value.sessionId,
                history = history,
            ).catch { e ->
                _uiState.value = _uiState.value.copy(
                    isStreaming = false,
                    error = "AI assistant temporarily unavailable. Try again later.",
                )
            }.collect { response ->
                tokenBuffer.append(response.token)
                _uiState.value = _uiState.value.copy(
                    currentStreamText = tokenBuffer.toString(),
                    sessionId = response.sessionId.ifEmpty { _uiState.value.sessionId },
                )
                if (response.isComplete) {
                    val assistantMessage = ChatMessage(
                        role = "assistant",
                        content = tokenBuffer.toString(),
                    )
                    _uiState.value = _uiState.value.copy(
                        messages = _uiState.value.messages + assistantMessage,
                        isStreaming = false,
                        currentStreamText = "",
                    )
                }
            }
        }
    }

    fun cancelStream() {
        streamJob?.cancel()
        _uiState.value = _uiState.value.copy(isStreaming = false)
    }

    fun clearChat() {
        _uiState.value = ChatUiState()
    }
}
```

- [ ] **Step 2: Register in PresentationModule**

```kotlin
viewModel { ChatViewModel(get(), get()) }
```

- [ ] **Step 3: Build to verify**

Run: `cd mobile && ./gradlew :androidApp:compileDebugKotlin`
Expected: BUILD SUCCESSFUL

- [ ] **Step 4: Commit**

```bash
git add mobile/androidApp/src/main/kotlin/com/goldbank/app/viewmodel/ChatViewModel.kt
git add mobile/androidApp/src/main/kotlin/com/goldbank/app/di/PresentationModule.kt
git commit -m "feat(mobile): add ChatViewModel with streaming, rate limiting, conversation history"
```

---

## Task 9: ChatFAB + ChatScreen (STORY-110)

**Files:**
- Create: `mobile/androidApp/src/main/kotlin/com/goldbank/app/ui/components/ChatFAB.kt`
- Create: `mobile/androidApp/src/main/kotlin/com/goldbank/app/ui/chat/ChatScreen.kt`

- [ ] **Step 1: Create ChatFAB**

```kotlin
package com.goldbank.app.ui.components

import androidx.compose.animation.*
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Close
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.unit.dp

@Composable
fun ChatFAB(
    onClick: () -> Unit,
    modifier: Modifier = Modifier,
) {
    FloatingActionButton(
        onClick = onClick,
        modifier = modifier.size(56.dp),
        shape = CircleShape,
        containerColor = MaterialTheme.colorScheme.primary,
    ) {
        Icon(
            painter = painterResource(android.R.drawable.ic_btn_speak_now),
            contentDescription = "Chat with AI assistant",
            tint = MaterialTheme.colorScheme.onPrimary,
        )
    }
}
```

- [ ] **Step 2: Create ChatScreen**

```kotlin
package com.goldbank.app.ui.chat

import androidx.compose.animation.*
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.lazy.rememberLazyListState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.automirrored.filled.Send
import androidx.compose.material.icons.filled.Delete
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.unit.dp
import com.goldbank.app.viewmodel.ChatViewModel
import com.goldbank.shared.domain.model.ChatMessage
import kotlinx.coroutines.launch

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun ChatScreen(
    viewModel: ChatViewModel,
    onBack: () -> Unit,
) {
    val state by viewModel.uiState.collectAsState()
    var inputText by remember { mutableStateOf("") }
    val listState = rememberLazyListState()
    val scope = rememberCoroutineScope()

    // Auto-scroll to bottom on new messages
    LaunchedEffect(state.messages.size, state.currentStreamText) {
        if (state.messages.isNotEmpty() || state.currentStreamText.isNotEmpty()) {
            listState.animateScrollToItem(
                (state.messages.size + if (state.isStreaming) 1 else 0).coerceAtLeast(0)
            )
        }
    }

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text("AI Banking Assistant") },
                navigationIcon = {
                    IconButton(onClick = onBack) {
                        Icon(Icons.AutoMirrored.Filled.ArrowBack, "Back")
                    }
                },
                actions = {
                    IconButton(onClick = { viewModel.clearChat() }) {
                        Icon(Icons.Default.Delete, "Clear chat")
                    }
                },
            )
        },
        bottomBar = {
            Surface(tonalElevation = 3.dp) {
                Row(
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(8.dp),
                    verticalAlignment = Alignment.CenterVertically,
                ) {
                    OutlinedTextField(
                        value = inputText,
                        onValueChange = { inputText = it },
                        modifier = Modifier.weight(1f),
                        placeholder = { Text("Ask about your finances...") },
                        enabled = !state.isStreaming && !state.rateLimited,
                        singleLine = false,
                        maxLines = 3,
                    )
                    Spacer(modifier = Modifier.width(8.dp))
                    IconButton(
                        onClick = {
                            viewModel.sendMessage(inputText)
                            inputText = ""
                        },
                        enabled = inputText.isNotBlank() && !state.isStreaming && !state.rateLimited,
                    ) {
                        Icon(Icons.AutoMirrored.Filled.Send, "Send")
                    }
                }
            }
        },
    ) { padding ->
        LazyColumn(
            state = listState,
            modifier = Modifier
                .fillMaxSize()
                .padding(padding)
                .padding(horizontal = 16.dp),
            verticalArrangement = Arrangement.spacedBy(8.dp),
            contentPadding = PaddingValues(vertical = 8.dp),
        ) {
            if (state.messages.isEmpty() && !state.isStreaming) {
                item {
                    Text(
                        text = "Hi! I'm your AI banking assistant. Ask me about your account balance, spending patterns, or anything else.",
                        style = MaterialTheme.typography.bodyMedium,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                        modifier = Modifier.padding(vertical = 32.dp),
                    )
                }
            }

            items(state.messages) { message ->
                ChatBubble(message = message)
            }

            // Streaming response
            if (state.isStreaming && state.currentStreamText.isNotEmpty()) {
                item {
                    ChatBubble(
                        message = ChatMessage(
                            role = "assistant",
                            content = state.currentStreamText + " ...",
                        ),
                    )
                }
            } else if (state.isStreaming) {
                item {
                    Row(modifier = Modifier.padding(8.dp)) {
                        CircularProgressIndicator(modifier = Modifier.size(16.dp), strokeWidth = 2.dp)
                        Spacer(modifier = Modifier.width(8.dp))
                        Text("Thinking...", style = MaterialTheme.typography.bodySmall)
                    }
                }
            }

            // Error
            state.error?.let { error ->
                item {
                    Card(
                        colors = CardDefaults.cardColors(
                            containerColor = MaterialTheme.colorScheme.errorContainer,
                        ),
                    ) {
                        Text(
                            text = error,
                            modifier = Modifier.padding(12.dp),
                            color = MaterialTheme.colorScheme.onErrorContainer,
                        )
                    }
                }
            }

            // Rate limit warning
            if (state.rateLimited) {
                item {
                    Card(
                        colors = CardDefaults.cardColors(
                            containerColor = MaterialTheme.colorScheme.tertiaryContainer,
                        ),
                    ) {
                        Text(
                            text = "You've reached the message limit (20/hour). Please try again later.",
                            modifier = Modifier.padding(12.dp),
                        )
                    }
                }
            }
        }
    }
}

@Composable
private fun ChatBubble(message: ChatMessage) {
    val isUser = message.role == "user"
    Row(
        modifier = Modifier.fillMaxWidth(),
        horizontalArrangement = if (isUser) Arrangement.End else Arrangement.Start,
    ) {
        Box(
            modifier = Modifier
                .widthIn(max = 300.dp)
                .clip(
                    RoundedCornerShape(
                        topStart = 16.dp,
                        topEnd = 16.dp,
                        bottomStart = if (isUser) 16.dp else 4.dp,
                        bottomEnd = if (isUser) 4.dp else 16.dp,
                    ),
                )
                .background(
                    if (isUser) MaterialTheme.colorScheme.primary
                    else MaterialTheme.colorScheme.surfaceVariant,
                )
                .padding(12.dp),
        ) {
            Text(
                text = message.content,
                color = if (isUser) MaterialTheme.colorScheme.onPrimary
                else MaterialTheme.colorScheme.onSurfaceVariant,
                style = MaterialTheme.typography.bodyMedium,
            )
        }
    }
}
```

- [ ] **Step 3: Add ChatFAB overlay and Chat route to NavGraph**

In `MainNavHost`, wrap the existing Scaffold content with ChatFAB overlay:

```kotlin
// Inside MainNavHost, replace the Scaffold:
var showChat by remember { mutableStateOf(false) }
val chatViewModel: ChatViewModel = koinViewModel()

Box(modifier = Modifier.fillMaxSize()) {
    // Existing Scaffold with NavHost goes here
    Scaffold(...) { ... }

    // ChatFAB overlay
    if (!showChat) {
        ChatFAB(
            onClick = { showChat = true },
            modifier = Modifier
                .align(Alignment.BottomEnd)
                .padding(16.dp),
        )
    }
}

// Add Chat composable in the NavHost:
composable<Route.Chat> {
    ChatScreen(
        viewModel = chatViewModel,
        onBack = { navController.popBackStack() },
    )
}
```

Or if using a fullscreen overlay approach:

```kotlin
if (showChat) {
    ChatScreen(
        viewModel = chatViewModel,
        onBack = { showChat = false },
    )
}
```

- [ ] **Step 4: Build to verify**

Run: `cd mobile && ./gradlew :androidApp:compileDebugKotlin`
Expected: BUILD SUCCESSFUL

- [ ] **Step 5: Commit**

```bash
git add mobile/androidApp/src/main/kotlin/com/goldbank/app/ui/components/ChatFAB.kt
git add mobile/androidApp/src/main/kotlin/com/goldbank/app/ui/chat/ChatScreen.kt
git add mobile/androidApp/src/main/kotlin/com/goldbank/app/navigation/NavGraph.kt
git commit -m "feat(mobile): add ChatFAB overlay and ChatScreen with streaming message display"
```

---

## Task 10: Spending Insights on HomeScreen (STORY-111)

**Files:**
- Modify: `mobile/androidApp/src/main/kotlin/com/goldbank/app/viewmodel/HomeViewModel.kt`
- Modify: `mobile/androidApp/src/main/kotlin/com/goldbank/app/ui/home/HomeScreen.kt`

- [ ] **Step 1: Add spending insights to HomeViewModel**

Add to constructor:
```kotlin
private val aiClient: AiGrpcClient,
```

Add to `HomeUiState`:
```kotlin
val spendingInsights: List<SpendingInsight> = emptyList(),
val isInsightsLoading: Boolean = false,
```

Add method:
```kotlin
fun loadSpendingInsights() {
    val accountId = sessionManager.accountId ?: return
    viewModelScope.launch {
        _uiState.value = _uiState.value.copy(isInsightsLoading = true)
        when (val result = aiClient.getSpendingInsights(accountId)) {
            is Result.Success -> {
                _uiState.value = _uiState.value.copy(
                    spendingInsights = result.data.insights,
                    isInsightsLoading = false,
                )
            }
            is Result.Failure -> {
                // Silently fail — insights are non-critical
                _uiState.value = _uiState.value.copy(isInsightsLoading = false)
            }
        }
    }
}
```

Call `loadSpendingInsights()` in `init` block.

- [ ] **Step 2: Update PresentationModule for HomeViewModel**

Update HomeViewModel constructor in DI:
```kotlin
viewModel { HomeViewModel(get(), get(), get(), get(), get(), get()) } // added AiGrpcClient
```

- [ ] **Step 3: Add SpendingInsightsCard to HomeScreen**

After the BalanceCard in HomeScreen's LazyColumn:

```kotlin
// Spending Insights
if (uiState.spendingInsights.isNotEmpty()) {
    item {
        Column(verticalArrangement = Arrangement.spacedBy(8.dp)) {
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically,
            ) {
                Text("Insights", style = MaterialTheme.typography.titleMedium)
                Text(
                    "Powered by AI",
                    style = MaterialTheme.typography.labelSmall,
                    color = MaterialTheme.colorScheme.outline,
                )
            }
            uiState.spendingInsights.forEach { insight ->
                Card(
                    modifier = Modifier.fillMaxWidth(),
                    colors = CardDefaults.cardColors(
                        containerColor = MaterialTheme.colorScheme.secondaryContainer,
                    ),
                ) {
                    Column(modifier = Modifier.padding(12.dp)) {
                        Text(
                            text = insight.summary,
                            style = MaterialTheme.typography.bodyMedium,
                        )
                        Spacer(modifier = Modifier.height(4.dp))
                        Text(
                            text = "${insight.category} • ${insight.period}",
                            style = MaterialTheme.typography.labelSmall,
                            color = MaterialTheme.colorScheme.onSecondaryContainer.copy(alpha = 0.7f),
                        )
                    }
                }
            }
        }
    }
} else if (uiState.isInsightsLoading) {
    item {
        // Shimmer placeholder
        Card(
            modifier = Modifier
                .fillMaxWidth()
                .height(60.dp),
            colors = CardDefaults.cardColors(
                containerColor = MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.5f),
            ),
        ) {}
    }
}
```

- [ ] **Step 4: Build to verify**

Run: `cd mobile && ./gradlew :androidApp:compileDebugKotlin`
Expected: BUILD SUCCESSFUL

- [ ] **Step 5: Commit**

```bash
git add mobile/androidApp/src/main/kotlin/com/goldbank/app/viewmodel/HomeViewModel.kt
git add mobile/androidApp/src/main/kotlin/com/goldbank/app/ui/home/HomeScreen.kt
git add mobile/androidApp/src/main/kotlin/com/goldbank/app/di/PresentationModule.kt
git commit -m "feat(mobile): add AI spending insights card to HomeScreen"
```

---

## Task 11: Build + Deploy to Emulator

- [ ] **Step 1: Full build**

Run: `cd mobile && ./gradlew :androidApp:assembleDebug`
Expected: BUILD SUCCESSFUL

- [ ] **Step 2: Install on emulator**

Run: `adb -s emulator-5554 install -r mobile/androidApp/build/outputs/apk/debug/androidApp-debug.apk`
Expected: Success

- [ ] **Step 3: Launch and test**

Run: `adb -s emulator-5554 shell am start -n com.goldbank.app/.MainActivity`

Test checklist:
- [ ] App opens → biometric prompt (if enabled) or goes straight to home
- [ ] SecuritySettings accessible from Profile → Security
- [ ] Biometric toggle works
- [ ] Timeout selector works
- [ ] ChatFAB visible on home screen
- [ ] Tapping ChatFAB opens ChatScreen
- [ ] Can type and send message (may fail if Ollama not configured for mobile — that's expected)
- [ ] Spending insights card loads (may show shimmer then hide if no data — expected)
- [ ] Back navigation works for all new screens

- [ ] **Step 4: Final commit with build artifacts excluded**

```bash
git add -A -- ':!mobile/androidApp/build/' ':!mobile/shared/build/' ':!mobile/.gradle/'
git commit -m "feat(mobile): Sprint 15 complete — security foundation + AI client + chat assistant + insights"
```
