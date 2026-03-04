package com.unibank.app.ui.payment

import androidx.compose.foundation.layout.*
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import com.unibank.app.ui.components.LoadingButton
import com.unibank.app.ui.components.PinInput
import com.unibank.app.viewmodel.PaymentUiState
import com.unibank.app.viewmodel.PaymentViewModel
import com.unibank.shared.domain.util.MoneyFormatter

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun QrScanScreen(
    viewModel: PaymentViewModel,
    onPaymentComplete: (String) -> Unit,
    onBack: () -> Unit,
) {
    val uiState by viewModel.uiState.collectAsState()
    val snackbarHostState = remember { SnackbarHostState() }
    var qrData by rememberSaveable { mutableStateOf("") }
    var pin by rememberSaveable { mutableStateOf("") }
    var showPinEntry by rememberSaveable { mutableStateOf(false) }

    LaunchedEffect(uiState) {
        when (val state = uiState) {
            is PaymentUiState.PaymentComplete -> {
                onPaymentComplete(state.result.transactionId)
                viewModel.resetState()
            }
            is PaymentUiState.Error -> {
                snackbarHostState.showSnackbar(state.message)
                viewModel.resetState()
            }
            else -> {}
        }
    }

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text("Scan QR Code") },
                navigationIcon = {
                    IconButton(onClick = onBack) {
                        Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = "Back")
                    }
                },
            )
        },
        snackbarHost = { SnackbarHost(snackbarHostState) },
    ) { padding ->
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(padding)
                .padding(16.dp),
            horizontalAlignment = Alignment.CenterHorizontally,
            verticalArrangement = Arrangement.Center,
        ) {
            if (!showPinEntry) {
                // Manual QR data entry (camera integration in later iteration)
                Text("Scan a merchant QR code or enter data manually", style = MaterialTheme.typography.bodyMedium)
                Spacer(modifier = Modifier.height(16.dp))
                OutlinedTextField(
                    value = qrData,
                    onValueChange = { qrData = it },
                    label = { Text("QR Code Data") },
                    modifier = Modifier.fillMaxWidth(),
                )
                Spacer(modifier = Modifier.height(16.dp))
                Button(
                    onClick = { showPinEntry = true },
                    enabled = qrData.isNotBlank(),
                    modifier = Modifier.fillMaxWidth(),
                ) { Text("Continue") }
            } else {
                Text("Enter PIN to confirm payment", style = MaterialTheme.typography.titleMedium)
                Spacer(modifier = Modifier.height(32.dp))
                PinInput(
                    value = pin,
                    onValueChange = { pin = it },
                    enabled = uiState !is PaymentUiState.Loading,
                    onComplete = { viewModel.processQrPayment(qrData, it) },
                )
                Spacer(modifier = Modifier.height(24.dp))
                LoadingButton(
                    text = "Pay",
                    onClick = { viewModel.processQrPayment(qrData, pin) },
                    isLoading = uiState is PaymentUiState.Loading,
                    enabled = pin.length == 4,
                )
            }
        }
    }
}
