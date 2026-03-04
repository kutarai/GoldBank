package com.unibank.app.ui.payment

import androidx.compose.animation.core.*
import androidx.compose.foundation.layout.*
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.Contactless
import androidx.compose.material.icons.filled.Nfc
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.scale
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import com.unibank.app.ui.components.LoadingButton
import com.unibank.app.ui.components.PinInput
import com.unibank.app.viewmodel.PaymentUiState
import com.unibank.app.viewmodel.PaymentViewModel
import kotlinx.coroutines.launch

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun NfcPaymentScreen(
    viewModel: PaymentViewModel,
    onPaymentComplete: () -> Unit,
    onBack: () -> Unit,
) {
    val uiState by viewModel.uiState.collectAsState()
    val snackbarHostState = remember { SnackbarHostState() }
    val scope = rememberCoroutineScope()
    var step by rememberSaveable { mutableIntStateOf(0) } // 0=tokenize, 1=tap, 2=pin (if high-value)
    var transactionId by rememberSaveable { mutableStateOf("") }
    var pin by rememberSaveable { mutableStateOf("") }

    LaunchedEffect(uiState) {
        when (val state = uiState) {
            is PaymentUiState.Tokenized -> {
                step = 1 // Move to "tap" step
            }
            is PaymentUiState.PaymentPending -> {
                // High-value payment requires PIN
                transactionId = state.transactionId
                step = 2
            }
            is PaymentUiState.PaymentComplete -> {
                onPaymentComplete()
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
                title = { Text("NFC Payment") },
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
            when (step) {
                0 -> {
                    // Tokenize step
                    Icon(
                        Icons.Default.Nfc,
                        contentDescription = null,
                        modifier = Modifier.size(80.dp),
                        tint = MaterialTheme.colorScheme.primary,
                    )
                    Spacer(modifier = Modifier.height(24.dp))
                    Text("Set Up NFC Payment", style = MaterialTheme.typography.titleLarge)
                    Spacer(modifier = Modifier.height(8.dp))
                    Text(
                        "Tokenize your card for contactless payments",
                        style = MaterialTheme.typography.bodyMedium,
                    )
                    Spacer(modifier = Modifier.height(32.dp))
                    LoadingButton(
                        text = "Enable NFC Payments",
                        onClick = { viewModel.tokenizeCard() },
                        isLoading = uiState is PaymentUiState.Loading,
                    )
                }
                1 -> {
                    // Tap to pay animation
                    NfcTapAnimation()
                    Spacer(modifier = Modifier.height(24.dp))
                    Text(
                        "Ready to Pay",
                        style = MaterialTheme.typography.headlineMedium,
                        fontWeight = FontWeight.Bold,
                    )
                    Spacer(modifier = Modifier.height(8.dp))
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
                2 -> {
                    // PIN for high-value payment
                    Text("PIN Required", style = MaterialTheme.typography.titleLarge)
                    Spacer(modifier = Modifier.height(8.dp))
                    Text(
                        "This transaction requires PIN verification",
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
