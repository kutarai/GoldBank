package com.goldbank.app.ui.auth

import android.provider.Settings
import androidx.compose.foundation.Image
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import com.goldbank.app.R
import com.goldbank.app.ui.components.ErrorDialog
import com.goldbank.app.ui.components.LoadingButton
import com.goldbank.app.ui.components.PhoneInput
import com.goldbank.app.ui.components.PinInput
import com.goldbank.app.viewmodel.AuthUiState
import com.goldbank.app.viewmodel.AuthViewModel

@Composable
fun LoginScreen(
    viewModel: AuthViewModel,
    onAuthenticated: () -> Unit,
    onRegisterClick: () -> Unit,
    showPhoneField: Boolean = true,
    initialPhoneNumber: String = "",
) {
    val uiState by viewModel.uiState.collectAsState()
    val context = LocalContext.current
    var phoneNumber by rememberSaveable { mutableStateOf(initialPhoneNumber) }
    var pin by rememberSaveable { mutableStateOf("") }
    var isError by rememberSaveable { mutableStateOf(false) }
    var errorMessage by remember { mutableStateOf<String?>(null) }
    // When the phone is pre-filled (returning user) we render it as a tidy
    // read-only line by default but let the user tap "Change" to edit. Lets
    // demos and shared devices swap between accounts without re-installing.
    var editingPhone by rememberSaveable { mutableStateOf(false) }

    LaunchedEffect(uiState) {
        when (val state = uiState) {
            is AuthUiState.Authenticated -> {
                onAuthenticated()
                viewModel.resetState()
            }
            is AuthUiState.LoginFailed -> {
                isError = true
                pin = ""
                errorMessage = if (state.remainingAttempts > 0) {
                    "${state.message} (${state.remainingAttempts} attempts remaining)"
                } else {
                    state.message
                }
                viewModel.resetState()
            }
            is AuthUiState.LockedOut -> {
                isError = true
                pin = ""
                val minutes = (state.remainingSeconds + 59) / 60
                errorMessage = "Account locked. Try again in $minutes minute(s)."
                viewModel.resetState()
            }
            is AuthUiState.Error -> {
                isError = true
                pin = ""
                errorMessage = state.message
                viewModel.resetState()
            }
            else -> {}
        }
    }

    errorMessage?.let { message ->
        ErrorDialog(
            message = message,
            onDismiss = { errorMessage = null },
        )
    }

    Scaffold { padding ->
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(padding)
                .padding(horizontal = 24.dp),
            horizontalAlignment = Alignment.CenterHorizontally,
            verticalArrangement = Arrangement.Center,
        ) {
            Image(
                painter = painterResource(id = R.drawable.splash_logo),
                contentDescription = "GoldBank logo",
                modifier = Modifier.size(240.dp),
            )
            Spacer(modifier = Modifier.height(16.dp))
            Text(
                text = "Welcome Back",
                style = MaterialTheme.typography.headlineMedium,
                color = MaterialTheme.colorScheme.onSurface,
            )
            Spacer(modifier = Modifier.height(8.dp))
            Text(
                text = "Enter your PIN to sign in",
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
                textAlign = TextAlign.Center,
            )
            Spacer(modifier = Modifier.height(32.dp))

            if (showPhoneField || editingPhone) {
                PhoneInput(
                    value = phoneNumber,
                    onValueChange = {
                        phoneNumber = it
                        isError = false
                    },
                    enabled = uiState !is AuthUiState.Loading,
                )
                Spacer(modifier = Modifier.height(16.dp))
            } else if (phoneNumber.isNotBlank()) {
                androidx.compose.foundation.layout.Row(
                    modifier = Modifier.fillMaxWidth(),
                    verticalAlignment = Alignment.CenterVertically,
                    horizontalArrangement = androidx.compose.foundation.layout.Arrangement.Center,
                ) {
                    Text(
                        text = phoneNumber,
                        style = MaterialTheme.typography.titleMedium,
                        color = MaterialTheme.colorScheme.onSurface,
                    )
                    Spacer(modifier = Modifier.width(8.dp))
                    TextButton(onClick = { editingPhone = true }) { Text("Change") }
                }
                Spacer(modifier = Modifier.height(16.dp))
            }

            Text(
                text = "PIN",
                style = MaterialTheme.typography.labelMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
            Spacer(modifier = Modifier.height(12.dp))

            PinInput(
                value = pin,
                onValueChange = {
                    pin = it
                    isError = false
                },
                enabled = uiState !is AuthUiState.Loading,
                isError = isError,
                onComplete = { enteredPin ->
                    val cleaned = phoneNumber.filter { it.isDigit() || it == '+' }
                    val deviceId = Settings.Secure.getString(
                        context.contentResolver,
                        Settings.Secure.ANDROID_ID,
                    )
                    viewModel.login(cleaned, enteredPin, deviceId)
                },
            )

            Spacer(modifier = Modifier.height(24.dp))

            LoadingButton(
                text = "Sign In",
                onClick = {
                    val cleaned = phoneNumber.filter { it.isDigit() || it == '+' }
                    val deviceId = Settings.Secure.getString(
                        context.contentResolver,
                        Settings.Secure.ANDROID_ID,
                    )
                    viewModel.login(cleaned, pin, deviceId)
                },
                isLoading = uiState is AuthUiState.Loading,
                enabled = pin.length == 4 && phoneNumber.isNotBlank(),
            )

            Spacer(modifier = Modifier.height(16.dp))

            if (showPhoneField) {
                TextButton(onClick = onRegisterClick) {
                    Text("Don't have an account? Register")
                }
            }
        }
    }
}
