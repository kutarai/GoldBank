package com.unibank.app.ui.payment

import androidx.compose.animation.core.*
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.Contactless
import androidx.compose.material.icons.filled.CreditCard
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.draw.scale
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import com.unibank.app.ui.components.LoadingButton
import com.unibank.app.ui.components.PinInput
import com.unibank.app.viewmodel.PaymentUiState
import com.unibank.app.viewmodel.PaymentViewModel
import com.unibank.shared.domain.model.AccountSummary

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun NfcPaymentScreen(
    viewModel: PaymentViewModel,
    onPaymentComplete: () -> Unit,
    onBack: () -> Unit,
) {
    val uiState by viewModel.uiState.collectAsState()
    val accounts by viewModel.accounts.collectAsState()
    val snackbarHostState = remember { SnackbarHostState() }
    // 0=select account, 1=PIN, 2=tokenize+tap, 3=high-value PIN
    var step by rememberSaveable { mutableIntStateOf(0) }
    var selectedAccount by remember { mutableStateOf<AccountSummary?>(null) }
    var pin by rememberSaveable { mutableStateOf("") }
    var transactionId by rememberSaveable { mutableStateOf("") }

    LaunchedEffect(Unit) {
        viewModel.loadAccounts()
    }

    // Auto-select if only one account
    LaunchedEffect(accounts) {
        if (accounts.size == 1 && selectedAccount == null) {
            selectedAccount = accounts[0]
        }
    }

    LaunchedEffect(uiState) {
        when (val state = uiState) {
            is PaymentUiState.Tokenized -> {
                step = 2 // Move to "tap" step
            }
            is PaymentUiState.PaymentPending -> {
                transactionId = state.transactionId
                step = 3 // High-value PIN
            }
            is PaymentUiState.PaymentComplete -> {
                onPaymentComplete()
                viewModel.resetState()
            }
            is PaymentUiState.Error -> {
                snackbarHostState.showSnackbar(state.message)
                if (step == 1) pin = ""
                viewModel.resetState()
            }
            else -> {}
        }
    }

    Scaffold(
        topBar = {
            TopAppBar(
                title = {
                    Text(
                        when (step) {
                            0 -> "Select Account"
                            1 -> "Enter PIN"
                            2 -> "Tap to Pay"
                            else -> "Confirm Payment"
                        }
                    )
                },
                navigationIcon = {
                    IconButton(onClick = {
                        when (step) {
                            0 -> onBack()
                            1 -> { step = 0; pin = "" }
                            2 -> { step = 1; pin = "" }
                            3 -> { step = 2; pin = "" }
                        }
                    }) {
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
            when (step) {
                // ── Step 0: Select Account ──
                0 -> {
                    Icon(
                        Icons.Default.CreditCard,
                        contentDescription = null,
                        modifier = Modifier.size(64.dp),
                        tint = MaterialTheme.colorScheme.primary,
                    )
                    Spacer(modifier = Modifier.height(16.dp))
                    Text("Choose Account for NFC Payment", style = MaterialTheme.typography.titleMedium)
                    Spacer(modifier = Modifier.height(24.dp))

                    if (accounts.isEmpty()) {
                        CircularProgressIndicator()
                        Spacer(modifier = Modifier.height(8.dp))
                        Text("Loading accounts...", style = MaterialTheme.typography.bodySmall)
                    } else {
                        accounts.forEach { account ->
                            val isSelected = account.accountId == selectedAccount?.accountId
                            Card(
                                modifier = Modifier
                                    .fillMaxWidth()
                                    .padding(vertical = 4.dp)
                                    .clickable { selectedAccount = account },
                                colors = CardDefaults.cardColors(
                                    containerColor = if (isSelected)
                                        MaterialTheme.colorScheme.primaryContainer
                                    else
                                        MaterialTheme.colorScheme.surfaceVariant,
                                ),
                                shape = RoundedCornerShape(12.dp),
                            ) {
                                Row(
                                    modifier = Modifier
                                        .fillMaxWidth()
                                        .padding(16.dp),
                                    horizontalArrangement = Arrangement.SpaceBetween,
                                    verticalAlignment = Alignment.CenterVertically,
                                ) {
                                    Column {
                                        Text(
                                            text = "${account.currency} Account",
                                            style = MaterialTheme.typography.titleSmall,
                                            fontWeight = FontWeight.Bold,
                                        )
                                        if (account.cardPanLast4.isNotEmpty()) {
                                            Text(
                                                text = "**** **** **** ${account.cardPanLast4}",
                                                style = MaterialTheme.typography.bodySmall,
                                                color = MaterialTheme.colorScheme.onSurfaceVariant,
                                            )
                                        }
                                    }
                                    Text(
                                        text = "${account.currency} ${account.availableBalance.amount}",
                                        style = MaterialTheme.typography.bodyMedium,
                                        fontWeight = FontWeight.SemiBold,
                                    )
                                }
                            }
                        }

                        Spacer(modifier = Modifier.height(24.dp))

                        Button(
                            onClick = { step = 1 },
                            modifier = Modifier.fillMaxWidth().height(52.dp),
                            enabled = selectedAccount != null,
                        ) {
                            Text("Continue")
                        }
                    }
                }

                // ── Step 1: PIN Entry ──
                1 -> {
                    Text("Enter PIN to authorize", style = MaterialTheme.typography.titleMedium)
                    selectedAccount?.let { acct ->
                        Spacer(modifier = Modifier.height(8.dp))
                        Text(
                            "${acct.currency} Account (**** ${acct.cardPanLast4})",
                            style = MaterialTheme.typography.bodyMedium,
                            color = MaterialTheme.colorScheme.primary,
                            fontWeight = FontWeight.SemiBold,
                        )
                    }
                    Spacer(modifier = Modifier.height(32.dp))
                    PinInput(
                        value = pin,
                        onValueChange = { pin = it },
                        enabled = uiState !is PaymentUiState.Loading,
                        onComplete = {
                            // PIN entered — tokenize the selected account's virtual card
                            selectedAccount?.let { acct ->
                                viewModel.tokenizeForAccount(acct.accountId)
                            }
                        },
                    )
                    Spacer(modifier = Modifier.height(24.dp))
                    LoadingButton(
                        text = "Activate NFC",
                        onClick = {
                            selectedAccount?.let { acct ->
                                viewModel.tokenizeForAccount(acct.accountId)
                            }
                        },
                        isLoading = uiState is PaymentUiState.Loading,
                        enabled = pin.length == 4,
                    )
                }

                // ── Step 2: Tap to Pay ──
                2 -> {
                    NfcTapAnimation()
                    Spacer(modifier = Modifier.height(24.dp))
                    Text(
                        "Ready to Pay",
                        style = MaterialTheme.typography.headlineMedium,
                        fontWeight = FontWeight.Bold,
                    )
                    Spacer(modifier = Modifier.height(8.dp))
                    selectedAccount?.let { acct ->
                        Text(
                            "${acct.currency} Account (**** ${acct.cardPanLast4})",
                            style = MaterialTheme.typography.bodyMedium,
                            color = MaterialTheme.colorScheme.primary,
                        )
                        Spacer(modifier = Modifier.height(4.dp))
                    }
                    Text(
                        "Hold your phone near the payment terminal",
                        style = MaterialTheme.typography.bodyMedium,
                    )
                    Spacer(modifier = Modifier.height(32.dp))
                    if (uiState is PaymentUiState.Loading) {
                        CircularProgressIndicator()
                        Spacer(modifier = Modifier.height(8.dp))
                        Text("Processing payment...", style = MaterialTheme.typography.bodySmall)
                    }
                }

                // ── Step 3: High-value PIN confirmation ──
                3 -> {
                    pin = "" // Reset PIN for re-entry
                    Text("PIN Required", style = MaterialTheme.typography.titleLarge)
                    Spacer(modifier = Modifier.height(8.dp))
                    Text(
                        "This transaction requires additional PIN verification",
                        style = MaterialTheme.typography.bodyMedium,
                    )
                    Spacer(modifier = Modifier.height(32.dp))
                    PinInput(
                        value = pin,
                        onValueChange = { pin = it },
                        enabled = uiState !is PaymentUiState.Loading,
                        onComplete = { viewModel.confirmNfcPayment(transactionId, it) },
                    )
                    Spacer(modifier = Modifier.height(24.dp))
                    LoadingButton(
                        text = "Confirm Payment",
                        onClick = { viewModel.confirmNfcPayment(transactionId, pin) },
                        isLoading = uiState is PaymentUiState.Loading,
                        enabled = pin.length == 4,
                    )
                }
            }
        }
    }
}

@Composable
private fun NfcTapAnimation() {
    val infiniteTransition = rememberInfiniteTransition(label = "nfc_pulse")
    val scale by infiniteTransition.animateFloat(
        initialValue = 0.9f,
        targetValue = 1.1f,
        animationSpec = infiniteRepeatable(
            animation = tween(1000, easing = EaseInOutSine),
            repeatMode = RepeatMode.Reverse,
        ),
        label = "pulse_scale",
    )
    val alpha by infiniteTransition.animateFloat(
        initialValue = 0.5f,
        targetValue = 1f,
        animationSpec = infiniteRepeatable(
            animation = tween(1000, easing = EaseInOutSine),
            repeatMode = RepeatMode.Reverse,
        ),
        label = "pulse_alpha",
    )

    Icon(
        Icons.Default.Contactless,
        contentDescription = "Tap to pay",
        modifier = Modifier.size(120.dp).scale(scale),
        tint = MaterialTheme.colorScheme.primary.copy(alpha = alpha),
    )
}

private val EaseInOutSine: Easing = CubicBezierEasing(0.37f, 0f, 0.63f, 1f)
