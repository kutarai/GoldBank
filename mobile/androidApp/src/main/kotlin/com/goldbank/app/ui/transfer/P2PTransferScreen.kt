package com.goldbank.app.ui.transfer

import androidx.compose.foundation.layout.*
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import com.goldbank.app.ui.components.CurrencyAmountField
import com.goldbank.app.ui.components.LoadingButton
import com.goldbank.app.ui.components.PhoneInput
import com.goldbank.app.ui.components.PinInput
import com.goldbank.app.viewmodel.TransferUiState
import com.goldbank.app.viewmodel.TransferViewModel
import com.goldbank.shared.domain.util.MoneyFormatter

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun P2PTransferScreen(
    viewModel: TransferViewModel,
    onSuccess: () -> Unit,
    onBack: () -> Unit,
) {
    val uiState by viewModel.uiState.collectAsState()
    val snackbarHostState = remember { SnackbarHostState() }
    var recipientPhone by rememberSaveable { mutableStateOf("") }
    var amount by rememberSaveable { mutableStateOf("") }
    var currency by rememberSaveable { mutableStateOf("ZWG") }
    var description by rememberSaveable { mutableStateOf("") }
    var pin by rememberSaveable { mutableStateOf("") }
    var step by rememberSaveable { mutableIntStateOf(0) } // 0=form, 1=pin

    LaunchedEffect(uiState) {
        when (val state = uiState) {
            is TransferUiState.Success -> {
                onSuccess()
                viewModel.resetState()
            }
            is TransferUiState.Error -> {
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
                title = { Text("Send Money") },
                navigationIcon = {
                    IconButton(onClick = { if (step == 1) { step = 0; pin = "" } else onBack() }) {
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
        ) {
            if (step == 0) {
                PhoneInput(
                    value = recipientPhone,
                    onValueChange = { recipientPhone = it },
                    label = "Recipient Phone",
                )
                Spacer(modifier = Modifier.height(12.dp))
                CurrencyAmountField(
                    amount = amount,
                    onAmountChange = { value: String -> amount = value },
                    currency = currency,
                    onCurrencyChange = { code: String -> currency = code },
                    modifier = Modifier.fillMaxWidth(),
                )
                Spacer(modifier = Modifier.height(12.dp))
                OutlinedTextField(
                    value = description,
                    onValueChange = { description = it },
                    label = { Text("Description (optional)") },
                    modifier = Modifier.fillMaxWidth(),
                    singleLine = true,
                )
                Spacer(modifier = Modifier.height(24.dp))
                Button(
                    onClick = { step = 1 },
                    modifier = Modifier.fillMaxWidth().height(52.dp),
                    enabled = recipientPhone.isNotBlank() && amount.isNotBlank(),
                ) { Text("Continue") }
            } else {
                Text("Confirm Transfer", style = MaterialTheme.typography.titleMedium)
                Spacer(modifier = Modifier.height(16.dp))
                Text("To: $recipientPhone", style = MaterialTheme.typography.bodyMedium)
                Text(
                    text = MoneyFormatter.format(amount, currency),
                    style = MaterialTheme.typography.headlineSmall,
                    fontWeight = FontWeight.Bold,
                )
                Spacer(modifier = Modifier.height(32.dp))
                Text("Enter PIN", style = MaterialTheme.typography.labelMedium)
                Spacer(modifier = Modifier.height(12.dp))
                PinInput(
                    value = pin,
                    onValueChange = { pin = it },
                    enabled = uiState !is TransferUiState.Loading,
                    onComplete = {
                        val cleaned = recipientPhone.filter { c -> c.isDigit() || c == '+' }
                        viewModel.sendP2P(cleaned, amount, currency, description, it)
                    },
                )
                Spacer(modifier = Modifier.height(24.dp))
                LoadingButton(
                    text = "Send",
                    onClick = {
                        val cleaned = recipientPhone.filter { c -> c.isDigit() || c == '+' }
                        viewModel.sendP2P(cleaned, amount, currency, description, pin)
                    },
                    isLoading = uiState is TransferUiState.Loading,
                    enabled = pin.length == 4,
                )
            }
        }
    }
}
