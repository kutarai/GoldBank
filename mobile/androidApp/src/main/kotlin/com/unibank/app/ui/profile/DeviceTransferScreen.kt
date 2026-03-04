package com.unibank.app.ui.profile

import androidx.compose.foundation.layout.*
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import com.unibank.app.viewmodel.ProfileUiState
import com.unibank.app.viewmodel.ProfileViewModel

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun DeviceTransferScreen(
    viewModel: ProfileViewModel,
    onComplete: () -> Unit,
    onBack: () -> Unit,
) {
    val uiState by viewModel.uiState.collectAsState()

    var phoneNumber by remember { mutableStateOf("") }
    var otp by remember { mutableStateOf("") }
    var pin by remember { mutableStateOf("") }
    var transferReference by remember { mutableStateOf("") }
    var step by remember { mutableIntStateOf(1) } // 1: initiate, 2: verify

    LaunchedEffect(uiState) {
        when (val state = uiState) {
            is ProfileUiState.DeviceTransferInitiated -> {
                transferReference = state.result.transferReference
                step = 2
            }
            is ProfileUiState.DeviceTransferCompleted -> onComplete()
            else -> {}
        }
    }

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text("Transfer Device") },
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
                .padding(16.dp),
            verticalArrangement = Arrangement.spacedBy(16.dp),
        ) {
            if (step == 1) {
                Text(
                    "Transfer your account to a new device",
                    style = MaterialTheme.typography.titleMedium,
                )
                Text(
                    "Enter your phone number to receive a verification code on your current device.",
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )

                OutlinedTextField(
                    value = phoneNumber,
                    onValueChange = { phoneNumber = it },
                    label = { Text("Phone Number") },
                    modifier = Modifier.fillMaxWidth(),
                    singleLine = true,
                )

                if (uiState is ProfileUiState.Error) {
                    Text(
                        (uiState as ProfileUiState.Error).message,
                        color = MaterialTheme.colorScheme.error,
                        style = MaterialTheme.typography.bodySmall,
                    )
                }

                Button(
                    onClick = { viewModel.initiateDeviceTransfer(phoneNumber) },
                    modifier = Modifier.fillMaxWidth(),
                    enabled = phoneNumber.isNotBlank() && uiState !is ProfileUiState.Loading,
                ) {
                    if (uiState is ProfileUiState.Loading) {
                        CircularProgressIndicator(
                            modifier = Modifier.size(20.dp),
                            strokeWidth = 2.dp,
                            color = MaterialTheme.colorScheme.onPrimary,
                        )
                    } else {
                        Text("Initiate Transfer")
                    }
                }
            } else {
                Text(
                    "Verify Transfer",
                    style = MaterialTheme.typography.titleMedium,
                )
                Text(
                    "Enter the OTP sent to your phone and your PIN to complete the transfer.",
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )

                OutlinedTextField(
                    value = otp,
                    onValueChange = { otp = it },
                    label = { Text("OTP") },
                    modifier = Modifier.fillMaxWidth(),
                    singleLine = true,
                )
                OutlinedTextField(
                    value = pin,
                    onValueChange = { pin = it },
                    label = { Text("PIN") },
                    modifier = Modifier.fillMaxWidth(),
                    singleLine = true,
                )

                if (uiState is ProfileUiState.Error) {
                    Text(
                        (uiState as ProfileUiState.Error).message,
                        color = MaterialTheme.colorScheme.error,
                        style = MaterialTheme.typography.bodySmall,
                    )
                }

                Button(
                    onClick = {
                        viewModel.completeDeviceTransfer(transferReference, otp, pin)
                    },
                    modifier = Modifier.fillMaxWidth(),
                    enabled = otp.isNotBlank() && pin.isNotBlank() && uiState !is ProfileUiState.Loading,
                ) {
                    if (uiState is ProfileUiState.Loading) {
                        CircularProgressIndicator(
                            modifier = Modifier.size(20.dp),
                            strokeWidth = 2.dp,
                            color = MaterialTheme.colorScheme.onPrimary,
                        )
                    } else {
                        Text("Complete Transfer")
                    }
                }
            }
        }
    }
}
