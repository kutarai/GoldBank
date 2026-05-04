package com.goldbank.app.ui.auth

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.TopAppBar
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import com.goldbank.app.ui.components.ErrorDialog
import com.goldbank.app.ui.components.LoadingButton
import com.goldbank.app.ui.components.OtpInput
import com.goldbank.app.viewmodel.AuthUiState
import com.goldbank.app.viewmodel.AuthViewModel
import kotlinx.coroutines.delay

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun OtpScreen(
    viewModel: AuthViewModel,
    otpLength: Int,
    ttlSeconds: Int,
    onVerified: (accountId: String) -> Unit,
    onBack: () -> Unit,
) {
    val uiState by viewModel.uiState.collectAsState()
    var otpValue by rememberSaveable { mutableStateOf("") }
    var remainingSeconds by rememberSaveable { mutableIntStateOf(ttlSeconds) }
    var isError by rememberSaveable { mutableStateOf(false) }
    var errorMessage by remember { mutableStateOf<String?>(null) }

    // Countdown timer
    LaunchedEffect(remainingSeconds) {
        if (remainingSeconds > 0) {
            delay(1000)
            remainingSeconds--
        }
    }

    LaunchedEffect(uiState) {
        when (val state = uiState) {
            is AuthUiState.OtpVerified -> {
                onVerified(state.accountId)
                viewModel.resetState()
            }
            is AuthUiState.Error -> {
                isError = true
                otpValue = ""
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

    Scaffold(
        topBar = {
            TopAppBar(
                title = {},
                navigationIcon = {
                    IconButton(onClick = onBack) {
                        Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = "Back")
                    }
                },
            )
        },
    ) { padding ->
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(padding)
                .padding(horizontal = 24.dp),
            horizontalAlignment = Alignment.CenterHorizontally,
            verticalArrangement = Arrangement.Center,
        ) {
            Text(
                text = "Verify Phone",
                style = MaterialTheme.typography.headlineMedium,
            )
            Spacer(modifier = Modifier.height(8.dp))
            Text(
                text = "Enter the $otpLength-digit code sent to your phone",
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
                textAlign = TextAlign.Center,
            )
            Spacer(modifier = Modifier.height(32.dp))

            OtpInput(
                value = otpValue,
                onValueChange = {
                    otpValue = it
                    isError = false
                },
                otpLength = otpLength,
                enabled = uiState !is AuthUiState.Loading,
                isError = isError,
                onComplete = { viewModel.verifyOtp(it) },
            )

            Spacer(modifier = Modifier.height(16.dp))

            if (remainingSeconds > 0) {
                Text(
                    text = "Code expires in ${remainingSeconds}s",
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
            } else {
                TextButton(onClick = { /* Resend would re-call register */ }) {
                    Text("Resend Code")
                }
            }

            Spacer(modifier = Modifier.height(24.dp))

            LoadingButton(
                text = "Verify",
                onClick = { viewModel.verifyOtp(otpValue) },
                isLoading = uiState is AuthUiState.Loading,
                enabled = otpValue.length == otpLength,
            )
        }
    }
}
