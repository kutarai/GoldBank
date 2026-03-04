package com.unibank.app.ui.agent

import androidx.compose.foundation.layout.*
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import com.unibank.app.ui.components.CurrencyAmountField
import com.unibank.app.ui.components.ErrorDialog
import com.unibank.app.ui.components.LoadingButton
import com.unibank.app.ui.components.PhoneInput
import com.unibank.app.ui.components.PinInput
import com.unibank.app.viewmodel.AgentUiState
import com.unibank.app.viewmodel.AgentViewModel

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun CashOutScreen(
    viewModel: AgentViewModel,
    onSuccess: () -> Unit,
    onBack: () -> Unit,
) {
    val uiState by viewModel.uiState.collectAsState()
    var customerPhone by rememberSaveable { mutableStateOf("") }
    var amount by rememberSaveable { mutableStateOf("") }
    var currency by rememberSaveable { mutableStateOf("ZWG") }
    var customerPin by rememberSaveable { mutableStateOf("") }
    var agentPin by rememberSaveable { mutableStateOf("") }
    var step by rememberSaveable { mutableIntStateOf(0) } // 0=form, 1=customer pin, 2=agent pin
    var errorMessage by remember { mutableStateOf<String?>(null) }

    LaunchedEffect(uiState) {
        when (val state = uiState) {
            is AgentUiState.CashSuccess -> {
                onSuccess()
                viewModel.resetState()
            }
            is AgentUiState.Error -> {
                customerPin = ""
                agentPin = ""
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
                title = { Text("Cash Out") },
                navigationIcon = {
                    IconButton(onClick = {
                        when (step) {
                            2 -> { step = 1; agentPin = "" }
                            1 -> { step = 0; customerPin = "" }
                            else -> onBack()
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
                .padding(16.dp),
            horizontalAlignment = Alignment.CenterHorizontally,
        ) {
            when (step) {
                0 -> {
                    PhoneInput(
                        value = customerPhone,
                        onValueChange = { customerPhone = it },
                        label = "Customer Phone",
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
                        enabled = customerPhone.isNotBlank() && amount.isNotBlank(),
                    ) { Text("Continue") }
                }
                1 -> {
                    Text("Customer PIN", style = MaterialTheme.typography.titleMedium)
                    Spacer(modifier = Modifier.height(32.dp))
                    PinInput(
                        value = customerPin,
                        onValueChange = { customerPin = it },
                        enabled = true,
                        onComplete = { step = 2 },
                    )
                    Spacer(modifier = Modifier.height(24.dp))
                    Button(
                        onClick = { step = 2 },
                        modifier = Modifier.fillMaxWidth().height(52.dp),
                        enabled = customerPin.length == 4,
                    ) { Text("Continue") }
                }
                2 -> {
                    Text("Agent PIN", style = MaterialTheme.typography.titleMedium)
                    Spacer(modifier = Modifier.height(32.dp))
                    PinInput(
                        value = agentPin,
                        onValueChange = { agentPin = it },
                        enabled = uiState !is AgentUiState.Loading,
                        onComplete = {
                            val cleaned = customerPhone.filter { c -> c.isDigit() || c == '+' }
                            viewModel.cashOut(cleaned, amount, currency, customerPin, agentPin = it)
                        },
                    )
                    Spacer(modifier = Modifier.height(24.dp))
                    LoadingButton(
                        text = "Cash Out",
                        onClick = {
                            val cleaned = customerPhone.filter { c -> c.isDigit() || c == '+' }
                            viewModel.cashOut(cleaned, amount, currency, customerPin, agentPin = agentPin)
                        },
                        isLoading = uiState is AgentUiState.Loading,
                        enabled = agentPin.length == 4,
                    )
                }
            }
        }
    }
}
