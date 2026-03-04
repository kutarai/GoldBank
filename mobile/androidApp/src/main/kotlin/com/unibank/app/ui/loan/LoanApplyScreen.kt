package com.unibank.app.ui.loan

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
import com.unibank.app.ui.components.PinInput
import com.unibank.app.viewmodel.LoanUiState
import com.unibank.app.viewmodel.LoanViewModel
import com.unibank.shared.domain.util.MoneyFormatter

private val tenureOptions = listOf(3, 6, 12, 18, 24)

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun LoanApplyScreen(
    viewModel: LoanViewModel,
    onSuccess: () -> Unit,
    onBack: () -> Unit,
) {
    val uiState by viewModel.uiState.collectAsState()
    var amount by rememberSaveable { mutableStateOf("") }
    var currency by rememberSaveable { mutableStateOf("ZWG") }
    var tenureMonths by rememberSaveable { mutableIntStateOf(12) }
    var purpose by rememberSaveable { mutableStateOf("") }
    var pin by rememberSaveable { mutableStateOf("") }
    var step by rememberSaveable { mutableIntStateOf(0) } // 0=form, 1=pin, 2=result
    var errorMessage by remember { mutableStateOf<String?>(null) }
    var tenureExpanded by remember { mutableStateOf(false) }

    LaunchedEffect(uiState) {
        when (val state = uiState) {
            is LoanUiState.ApplicationSuccess -> step = 2
            is LoanUiState.Error -> {
                pin = ""
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
                title = { Text(if (step == 2) "Loan Result" else "Apply for Loan") },
                navigationIcon = {
                    IconButton(onClick = {
                        when (step) {
                            1 -> { step = 0; pin = "" }
                            2 -> { onSuccess(); viewModel.resetState() }
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
                    CurrencyAmountField(
                        amount = amount,
                        onAmountChange = { value: String -> amount = value },
                        currency = currency,
                        onCurrencyChange = { code: String -> currency = code },
                        modifier = Modifier.fillMaxWidth(),
                    )
                    Spacer(modifier = Modifier.height(12.dp))

                    ExposedDropdownMenuBox(
                        expanded = tenureExpanded,
                        onExpandedChange = { tenureExpanded = it },
                        modifier = Modifier.fillMaxWidth(),
                    ) {
                        OutlinedTextField(
                            value = "$tenureMonths months",
                            onValueChange = {},
                            readOnly = true,
                            label = { Text("Tenure") },
                            singleLine = true,
                            trailingIcon = { ExposedDropdownMenuDefaults.TrailingIcon(expanded = tenureExpanded) },
                            modifier = Modifier.menuAnchor(MenuAnchorType.PrimaryNotEditable).fillMaxWidth(),
                        )
                        ExposedDropdownMenu(
                            expanded = tenureExpanded,
                            onDismissRequest = { tenureExpanded = false },
                        ) {
                            tenureOptions.forEach { months ->
                                DropdownMenuItem(
                                    text = { Text("$months months") },
                                    onClick = {
                                        tenureMonths = months
                                        tenureExpanded = false
                                    },
                                )
                            }
                        }
                    }
                    Spacer(modifier = Modifier.height(12.dp))

                    OutlinedTextField(
                        value = purpose,
                        onValueChange = { purpose = it },
                        label = { Text("Purpose") },
                        modifier = Modifier.fillMaxWidth(),
                        singleLine = true,
                    )
                    Spacer(modifier = Modifier.height(24.dp))

                    Button(
                        onClick = { step = 1 },
                        modifier = Modifier.fillMaxWidth().height(52.dp),
                        enabled = amount.isNotBlank() && purpose.isNotBlank(),
                    ) { Text("Continue") }
                }

                1 -> {
                    Text("Enter PIN to Confirm", style = MaterialTheme.typography.titleMedium)
                    Spacer(modifier = Modifier.height(8.dp))
                    Text(
                        text = "Loan: ${MoneyFormatter.format(amount, currency)} for $tenureMonths months",
                        style = MaterialTheme.typography.bodyMedium,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                    )
                    Spacer(modifier = Modifier.height(32.dp))
                    PinInput(
                        value = pin,
                        onValueChange = { pin = it },
                        enabled = uiState !is LoanUiState.Loading,
                        onComplete = {
                            viewModel.applyForLoan(amount, currency, tenureMonths, purpose, pin = it)
                        },
                    )
                    Spacer(modifier = Modifier.height(24.dp))
                    LoadingButton(
                        text = "Apply",
                        onClick = {
                            viewModel.applyForLoan(amount, currency, tenureMonths, purpose, pin = pin)
                        },
                        isLoading = uiState is LoanUiState.Loading,
                        enabled = pin.length == 4,
                    )
                }

                2 -> {
                    val result = (uiState as? LoanUiState.ApplicationSuccess)?.result
                    if (result != null) {
                        val isApproved = result.status.name != "REJECTED"

                        Card(
                            modifier = Modifier.fillMaxWidth(),
                            colors = CardDefaults.cardColors(
                                containerColor = if (isApproved)
                                    MaterialTheme.colorScheme.primaryContainer
                                else
                                    MaterialTheme.colorScheme.errorContainer,
                            ),
                        ) {
                            Column(modifier = Modifier.padding(16.dp)) {
                                Text(
                                    text = if (isApproved) "Loan Approved!" else "Loan Rejected",
                                    style = MaterialTheme.typography.headlineSmall,
                                    color = if (isApproved)
                                        MaterialTheme.colorScheme.onPrimaryContainer
                                    else
                                        MaterialTheme.colorScheme.onErrorContainer,
                                )
                                Spacer(modifier = Modifier.height(4.dp))
                                Text(
                                    text = result.message,
                                    style = MaterialTheme.typography.bodyMedium,
                                )
                            }
                        }

                        Spacer(modifier = Modifier.height(16.dp))

                        Card(modifier = Modifier.fillMaxWidth()) {
                            Column(modifier = Modifier.padding(16.dp)) {
                                LoanInfoRow("Reference", result.reference)
                                LoanInfoRow("Principal", MoneyFormatter.format(result.principal.amount, result.principal.currency))
                                LoanInfoRow("Interest Rate", result.interestRate)
                                LoanInfoRow("Monthly Payment", MoneyFormatter.format(result.monthlyPayment.amount, result.monthlyPayment.currency))
                                LoanInfoRow("Tenure", "${result.tenureMonths} months")
                                LoanInfoRow("Credit Score", "${result.creditScore}")
                            }
                        }

                        Spacer(modifier = Modifier.height(24.dp))

                        Button(
                            onClick = { onSuccess(); viewModel.resetState() },
                            modifier = Modifier.fillMaxWidth().height(52.dp),
                        ) { Text("Done") }
                    }
                }
            }
        }
    }
}

@Composable
private fun LoanInfoRow(label: String, value: String) {
    Row(
        modifier = Modifier.fillMaxWidth().padding(vertical = 4.dp),
        horizontalArrangement = Arrangement.SpaceBetween,
    ) {
        Text(text = label, style = MaterialTheme.typography.bodyMedium, color = MaterialTheme.colorScheme.onSurfaceVariant)
        Text(text = value, style = MaterialTheme.typography.bodyMedium)
    }
}
