package com.unibank.app.ui.payment

import android.app.Activity
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.compose.foundation.layout.*
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import com.journeyapps.barcodescanner.ScanContract
import com.journeyapps.barcodescanner.ScanOptions
import com.unibank.app.ui.components.LoadingButton
import com.unibank.app.ui.components.PinInput
import com.unibank.app.viewmodel.PaymentUiState
import com.unibank.app.viewmodel.PaymentViewModel

/**
 * Parsed EMV QR code fields for the confirmation screen.
 */
private data class EmvQrInfo(
    val merchantName: String,
    val merchantId: String,
    val amount: String,
    val currency: String,
    val paymentReference: String,
    val rawData: String,
)

/**
 * Parses EMV QR Code TLV data to extract merchant name, amount, and currency.
 * Tags: 54=amount, 53=currency numeric, 59=merchant name.
 * Tag 26=merchant account info (sub-tag 02=merchant ID, sub-tag 04=payment reference).
 * Tag 62=additional data (sub-tag 05=reference label).
 */
private fun parseEmvQrCode(qrData: String): EmvQrInfo? {
    if (qrData.length < 10) return null
    try {
        val fields = parseTlv(qrData)
        val amount = fields["54"] ?: "0.00"
        val currencyNumeric = fields["53"] ?: "924"
        val merchantName = fields["59"] ?: "Merchant"

        var merchantId = ""
        var paymentReference = ""
        fields["26"]?.let { merchantInfo ->
            val sub = parseTlv(merchantInfo)
            merchantId = sub["02"] ?: ""
            paymentReference = sub["04"] ?: ""
        }

        if (paymentReference.isEmpty()) {
            fields["62"]?.let { additional ->
                val sub = parseTlv(additional)
                paymentReference = sub["05"] ?: ""
            }
        }

        val currency = when (currencyNumeric) {
            "924" -> "ZWG"; "840" -> "USD"; "978" -> "EUR"
            "826" -> "GBP"; "710" -> "ZAR"; else -> currencyNumeric
        }

        return EmvQrInfo(merchantName, merchantId, amount, currency, paymentReference, qrData)
    } catch (_: Exception) {
        return null
    }
}

private fun parseTlv(data: String): Map<String, String> {
    val fields = mutableMapOf<String, String>()
    var i = 0
    while (i + 4 <= data.length) {
        val tag = data.substring(i, i + 2)
        val length = data.substring(i + 2, i + 4).toIntOrNull() ?: break
        if (i + 4 + length > data.length) break
        fields[tag] = data.substring(i + 4, i + 4 + length)
        i += 4 + length
    }
    return fields
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun QrScanScreen(
    viewModel: PaymentViewModel,
    onPaymentComplete: (String) -> Unit,
    onBack: () -> Unit,
) {
    val uiState by viewModel.uiState.collectAsState()
    val snackbarHostState = remember { SnackbarHostState() }

    // 0=scan, 1=confirm, 2=pin
    var step by rememberSaveable { mutableIntStateOf(0) }
    var scannedQrData by rememberSaveable { mutableStateOf("") }
    var qrInfo by remember { mutableStateOf<EmvQrInfo?>(null) }
    var pin by rememberSaveable { mutableStateOf("") }

    // ZXing barcode scanner launcher
    val scanLauncher = rememberLauncherForActivityResult(ScanContract()) { result ->
        if (result.contents != null) {
            scannedQrData = result.contents
            qrInfo = parseEmvQrCode(result.contents)
            step = 1
        }
        // If cancelled (result.contents == null), stay on step 0
    }

    // Launch scanner immediately on first composition
    LaunchedEffect(step) {
        if (step == 0 && scannedQrData.isEmpty()) {
            val options = ScanOptions().apply {
                setDesiredBarcodeFormats(ScanOptions.QR_CODE)
                setPrompt("Point camera at merchant's EMVCo QR code")
                setCameraId(0)
                setBeepEnabled(false)
                setOrientationLocked(true)
            }
            scanLauncher.launch(options)
        }
    }

    LaunchedEffect(uiState) {
        when (val state = uiState) {
            is PaymentUiState.PaymentComplete -> {
                onPaymentComplete(state.result.transactionId)
                viewModel.resetState()
            }
            is PaymentUiState.Error -> {
                snackbarHostState.showSnackbar(state.message)
                if (step == 2) pin = ""
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
                            0 -> "Scan QR Code"
                            1 -> "Confirm Payment"
                            else -> "Enter PIN"
                        }
                    )
                },
                navigationIcon = {
                    IconButton(onClick = {
                        when (step) {
                            0 -> onBack()
                            1 -> { step = 0; scannedQrData = ""; qrInfo = null }
                            2 -> { step = 1; pin = "" }
                        }
                    }) {
                        Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = "Back")
                    }
                },
            )
        },
        snackbarHost = { SnackbarHost(snackbarHostState) },
    ) { padding ->
        when (step) {
            // ── Step 0: Waiting / Scan Again ──
            0 -> {
                Column(
                    modifier = Modifier
                        .fillMaxSize()
                        .padding(padding)
                        .padding(24.dp),
                    horizontalAlignment = Alignment.CenterHorizontally,
                    verticalArrangement = Arrangement.Center,
                ) {
                    Text(
                        "Scan a merchant's EMVCo QR code to pay",
                        style = MaterialTheme.typography.bodyLarge,
                    )
                    Spacer(modifier = Modifier.height(24.dp))
                    Button(onClick = {
                        val options = ScanOptions().apply {
                            setDesiredBarcodeFormats(ScanOptions.QR_CODE)
                            setPrompt("Point camera at merchant's EMVCo QR code")
                            setCameraId(0)
                            setBeepEnabled(false)
                            setOrientationLocked(true)
                        }
                        scanLauncher.launch(options)
                    }) {
                        Text("Open Scanner")
                    }
                }
            }

            // ── Step 1: Confirmation Screen ──
            1 -> {
                Column(
                    modifier = Modifier
                        .fillMaxSize()
                        .padding(padding)
                        .padding(24.dp),
                    horizontalAlignment = Alignment.CenterHorizontally,
                ) {
                    Spacer(modifier = Modifier.height(32.dp))

                    Text(
                        "Payment Details",
                        style = MaterialTheme.typography.headlineSmall,
                        fontWeight = FontWeight.Bold,
                    )

                    Spacer(modifier = Modifier.height(32.dp))

                    Card(
                        modifier = Modifier.fillMaxWidth(),
                        colors = CardDefaults.cardColors(
                            containerColor = MaterialTheme.colorScheme.surfaceVariant,
                        ),
                    ) {
                        Column(
                            modifier = Modifier.padding(20.dp),
                            verticalArrangement = Arrangement.spacedBy(16.dp),
                        ) {
                            val info = qrInfo
                            if (info != null) {
                                PaymentDetailRow("Merchant", info.merchantName)
                                if (info.merchantId.isNotEmpty()) {
                                    PaymentDetailRow("Merchant ID", info.merchantId)
                                }
                                HorizontalDivider()
                                PaymentDetailRow(
                                    "Amount",
                                    "${info.currency} ${info.amount}",
                                    highlight = true,
                                )
                                if (info.paymentReference.isNotEmpty()) {
                                    PaymentDetailRow("Reference", info.paymentReference)
                                }
                            } else {
                                Text(
                                    "QR code scanned. Tap Pay to continue.",
                                    style = MaterialTheme.typography.bodyMedium,
                                )
                            }
                        }
                    }

                    Spacer(modifier = Modifier.weight(1f))

                    Button(
                        onClick = { step = 2 },
                        modifier = Modifier.fillMaxWidth().height(52.dp),
                    ) {
                        Text("Pay", style = MaterialTheme.typography.titleMedium)
                    }

                    Spacer(modifier = Modifier.height(12.dp))

                    OutlinedButton(
                        onClick = {
                            step = 0
                            scannedQrData = ""
                            qrInfo = null
                            val options = ScanOptions().apply {
                                setDesiredBarcodeFormats(ScanOptions.QR_CODE)
                                setPrompt("Point camera at merchant's EMVCo QR code")
                                setCameraId(0)
                                setBeepEnabled(false)
                                setOrientationLocked(true)
                            }
                            scanLauncher.launch(options)
                        },
                        modifier = Modifier.fillMaxWidth(),
                    ) {
                        Text("Scan Again")
                    }

                    Spacer(modifier = Modifier.height(16.dp))
                }
            }

            // ── Step 2: PIN Entry ──
            2 -> {
                Column(
                    modifier = Modifier
                        .fillMaxSize()
                        .padding(padding)
                        .padding(24.dp),
                    horizontalAlignment = Alignment.CenterHorizontally,
                    verticalArrangement = Arrangement.Center,
                ) {
                    Text(
                        "Enter PIN to confirm",
                        style = MaterialTheme.typography.titleMedium,
                    )

                    qrInfo?.let { info ->
                        Spacer(modifier = Modifier.height(8.dp))
                        Text(
                            "${info.currency} ${info.amount} to ${info.merchantName}",
                            style = MaterialTheme.typography.bodyLarge,
                            fontWeight = FontWeight.SemiBold,
                            color = MaterialTheme.colorScheme.primary,
                        )
                    }

                    Spacer(modifier = Modifier.height(32.dp))

                    PinInput(
                        value = pin,
                        onValueChange = { pin = it },
                        enabled = uiState !is PaymentUiState.Loading,
                        onComplete = { viewModel.processQrPayment(scannedQrData, it) },
                    )

                    Spacer(modifier = Modifier.height(24.dp))

                    LoadingButton(
                        text = "Confirm Payment",
                        onClick = { viewModel.processQrPayment(scannedQrData, pin) },
                        isLoading = uiState is PaymentUiState.Loading,
                        enabled = pin.length == 4,
                    )
                }
            }
        }
    }
}

@Composable
private fun PaymentDetailRow(
    label: String,
    value: String,
    highlight: Boolean = false,
) {
    Row(
        modifier = Modifier.fillMaxWidth(),
        horizontalArrangement = Arrangement.SpaceBetween,
    ) {
        Text(
            label,
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
        Text(
            value,
            style = if (highlight)
                MaterialTheme.typography.titleMedium.copy(fontWeight = FontWeight.Bold)
            else
                MaterialTheme.typography.bodyMedium.copy(fontWeight = FontWeight.SemiBold),
            color = if (highlight)
                MaterialTheme.colorScheme.primary
            else
                MaterialTheme.colorScheme.onSurface,
        )
    }
}
