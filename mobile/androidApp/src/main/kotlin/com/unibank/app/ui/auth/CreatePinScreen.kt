package com.unibank.app.ui.auth

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
import com.unibank.app.ui.components.ErrorDialog
import com.unibank.app.ui.components.LoadingButton
import com.unibank.app.ui.components.PinInput
import com.unibank.app.viewmodel.AuthUiState
import com.unibank.app.viewmodel.AuthViewModel

private const val PIN_LENGTH = 4

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun CreatePinScreen(
    viewModel: AuthViewModel,
    accountId: String,
    onAuthenticated: () -> Unit,
    onBack: () -> Unit,
) {
    val uiState by viewModel.uiState.collectAsState()
    var step by rememberSaveable { mutableIntStateOf(0) } // 0 = enter, 1 = confirm
    var pin by rememberSaveable { mutableStateOf("") }
    var confirmPin by rememberSaveable { mutableStateOf("") }
    var isError by rememberSaveable { mutableStateOf(false) }
    var errorMessage by remember { mutableStateOf<String?>(null) }

    LaunchedEffect(uiState) {
        when (val state = uiState) {
            is AuthUiState.PinCreated -> {
                onAuthenticated()
                viewModel.resetState()
            }
            is AuthUiState.Authenticated -> {
                onAuthenticated()
                viewModel.resetState()
            }
            is AuthUiState.Error -> {
                isError = true
                pin = ""
                confirmPin = ""
                step = 0
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
                    IconButton(onClick = {
                        if (step == 1) {
                            step = 0
                            confirmPin = ""
                        } else {
                            onBack()
                        }
                    }) {
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
                text = if (step == 0) "Create PIN" else "Confirm PIN",
                style = MaterialTheme.typography.headlineMedium,
            )
            Spacer(modifier = Modifier.height(8.dp))
            Text(
                text = if (step == 0) "Choose a $PIN_LENGTH-digit PIN for your account"
                else "Re-enter your PIN to confirm",
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
                textAlign = TextAlign.Center,
            )
            Spacer(modifier = Modifier.height(48.dp))

            if (step == 0) {
                PinInput(
                    value = pin,
                    onValueChange = {
                        pin = it
                        isError = false
                    },
                    pinLength = PIN_LENGTH,
                    enabled = uiState !is AuthUiState.Loading,
                    isError = isError,
                    onComplete = { step = 1 },
                )
            } else {
                PinInput(
                    value = confirmPin,
                    onValueChange = {
                        confirmPin = it
                        isError = false
                    },
                    pinLength = PIN_LENGTH,
                    enabled = uiState !is AuthUiState.Loading,
                    isError = isError,
                    onComplete = { confirmed ->
                        if (confirmed == pin) {
                            viewModel.createPin(pin, confirmed)
                        } else {
                            isError = true
                            confirmPin = ""
                        }
                    },
                )
                if (isError) {
                    Spacer(modifier = Modifier.height(8.dp))
                    Text(
                        text = "PINs do not match. Try again.",
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.error,
                    )
                }
            }

            Spacer(modifier = Modifier.height(32.dp))

            if (step == 1) {
                LoadingButton(
                    text = "Create PIN",
                    onClick = {
                        if (confirmPin == pin) {
                            viewModel.createPin(pin, confirmPin)
                        } else {
                            isError = true
                            confirmPin = ""
                        }
                    },
                    isLoading = uiState is AuthUiState.Loading,
                    enabled = confirmPin.length == PIN_LENGTH,
                )
            }
        }
    }
}
