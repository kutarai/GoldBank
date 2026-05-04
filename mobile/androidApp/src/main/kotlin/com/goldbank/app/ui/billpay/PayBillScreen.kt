package com.goldbank.app.ui.billpay

import androidx.compose.foundation.layout.*
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.CameraAlt
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import com.goldbank.app.ui.components.CurrencyAmountField
import com.goldbank.app.ui.components.LoadingButton
import com.goldbank.app.ui.components.PinInput
import com.goldbank.app.viewmodel.BillPayUiState
import com.goldbank.app.viewmodel.BillPayViewModel
import com.goldbank.shared.domain.util.MoneyFormatter

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun PayBillScreen(
    viewModel: BillPayViewModel,
    providerId: String,
    providerName: String,
    onSuccess: () -> Unit,
    onBack: () -> Unit,
    onScanBill: () -> Unit = {},
    prefilledProvider: String? = null,
    prefilledAccountNumber: String? = null,
    prefilledAmount: String? = null,
) {
    val uiState by viewModel.uiState.collectAsState()
    val snackbarHostState = remember { SnackbarHostState() }
    var billingRef by rememberSaveable { mutableStateOf(prefilledAccountNumber ?: "") }
    var amount by rememberSaveable { mutableStateOf(prefilledAmount ?: "") }
    var currency by rememberSaveable { mutableStateOf("ZWG") }
    var pin by rememberSaveable { mutableStateOf("") }
    var step by rememberSaveable { mutableIntStateOf(0) }

    // Apply new pre-fills whenever they arrive (e.g. after returning from BillScanScreen)
    LaunchedEffect(prefilledAccountNumber) {
        if (!prefilledAccountNumber.isNullOrBlank()) billingRef = prefilledAccountNumber
    }
    LaunchedEffect(prefilledAmount) {
        if (!prefilledAmount.isNullOrBlank()) amount = prefilledAmount
    }

    LaunchedEffect(uiState) {
        when (val state = uiState) {
            is BillPayUiState.Success -> {
                onSuccess()
                viewModel.resetState()
            }
            is BillPayUiState.Error -> {
                pin = ""
                step = 0
                snackbarHostState.showSnackbar(state.message)
                viewModel.resetState()
            }
            else -> {}
        }
    }

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text(providerName) },
                navigationIcon = {
                    IconButton(onClick = { if (step == 1) { step = 0; pin = "" } else onBack() }) {
                        Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = "Back")
                    }
                },
                actions = {
                    if (step == 0) {
                        IconButton(onClick = onScanBill) {
                            Icon(Icons.Default.CameraAlt, contentDescription = "Scan Bill")
                        }
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
        ) {
            if (step == 0) {
                if (!prefilledProvider.isNullOrBlank()) {
                    SuggestionChip(
                        onClick = {},
                        label = { Text("Matched: $prefilledProvider") },
                        modifier = Modifier
                            .align(Alignment.Start)
                            .padding(bottom = 4.dp),
                    )
                    Spacer(modifier = Modifier.height(8.dp))
                }
                OutlinedTextField(
                    value = billingRef,
                    onValueChange = { billingRef = it },
                    label = { Text("Account / Meter Number") },
                    modifier = Modifier.fillMaxWidth(),
                    singleLine = true,
                )
                Spacer(modifier = Modifier.height(12.dp))
                CurrencyAmountField(
                    amount = amount,
                    onAmountChange = { value: String -> amount = value },
                    currency = currency,
                    onCurrencyChange = { code: String -> currency = code },
                    modifier = Modifier.fillMaxWidth(),
                )
                Spacer(modifier = Modifier.height(24.dp))
                Button(
                    onClick = { step = 1 },
                    modifier = Modifier.fillMaxWidth().height(52.dp),
                    enabled = billingRef.isNotBlank() && amount.isNotBlank(),
                ) { Text("Continue") }
            } else {
                Text("Confirm Payment", style = MaterialTheme.typography.titleMedium)
                Spacer(modifier = Modifier.height(8.dp))
                Text(providerName, style = MaterialTheme.typography.bodyMedium)
                Text("Ref: $billingRef", style = MaterialTheme.typography.bodySmall)
                Text(
                    MoneyFormatter.format(amount, currency),
                    style = MaterialTheme.typography.headlineSmall,
                    fontWeight = FontWeight.Bold,
                )
                Spacer(modifier = Modifier.height(32.dp))
                Text("Enter PIN", style = MaterialTheme.typography.labelMedium)
                Spacer(modifier = Modifier.height(12.dp))
                PinInput(
                    value = pin,
                    onValueChange = { pin = it },
                    enabled = uiState !is BillPayUiState.Loading,
                    onComplete = { viewModel.payBill(providerId, billingRef, amount, currency, pin = it) },
                )
                Spacer(modifier = Modifier.height(24.dp))
                LoadingButton(
                    text = "Pay Bill",
                    onClick = { viewModel.payBill(providerId, billingRef, amount, currency, pin = pin) },
                    isLoading = uiState is BillPayUiState.Loading,
                    enabled = pin.length == 4,
                )
            }
        }
    }
}
