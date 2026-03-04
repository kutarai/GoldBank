package com.unibank.app.ui.payment

import android.graphics.Bitmap
import androidx.compose.foundation.Image
import androidx.compose.foundation.layout.*
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.asImageBitmap
import androidx.compose.ui.unit.dp
import com.google.zxing.BarcodeFormat
import com.google.zxing.qrcode.QRCodeWriter
import com.unibank.app.ui.components.CurrencyAmountField
import com.unibank.app.ui.components.LoadingButton
import com.unibank.app.viewmodel.PaymentUiState
import com.unibank.app.viewmodel.PaymentViewModel

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun QrGenerateScreen(
    viewModel: PaymentViewModel,
    onBack: () -> Unit,
) {
    val uiState by viewModel.uiState.collectAsState()
    var amount by rememberSaveable { mutableStateOf("") }
    var currency by rememberSaveable { mutableStateOf("ZWG") }
    var description by rememberSaveable { mutableStateOf("") }
    val snackbarHostState = remember { SnackbarHostState() }

    LaunchedEffect(uiState) {
        if (uiState is PaymentUiState.Error) {
            snackbarHostState.showSnackbar((uiState as PaymentUiState.Error).message)
            viewModel.resetState()
        }
    }

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text("My QR Code") },
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
        ) {
            val qrState = uiState
            if (qrState is PaymentUiState.QrGenerated) {
                val bitmap = remember(qrState.qrCode.qrCodeData) {
                    generateQrBitmap(qrState.qrCode.qrCodeData, 512)
                }
                if (bitmap != null) {
                    Image(
                        bitmap = bitmap.asImageBitmap(),
                        contentDescription = "Payment QR Code",
                        modifier = Modifier.size(256.dp),
                    )
                }
                Spacer(modifier = Modifier.height(16.dp))
                Text("Ref: ${qrState.qrCode.paymentReference}", style = MaterialTheme.typography.bodySmall)
            } else {
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
                LoadingButton(
                    text = "Generate QR Code",
                    onClick = { viewModel.generateQrCode(amount, currency, description = description) },
                    isLoading = uiState is PaymentUiState.Loading,
                    enabled = amount.isNotBlank(),
                )
            }
        }
    }
}

private fun generateQrBitmap(data: String, size: Int): Bitmap? {
    return try {
        val writer = QRCodeWriter()
        val bitMatrix = writer.encode(data, BarcodeFormat.QR_CODE, size, size)
        val bitmap = Bitmap.createBitmap(size, size, Bitmap.Config.RGB_565)
        for (x in 0 until size) {
            for (y in 0 until size) {
                bitmap.setPixel(x, y, if (bitMatrix[x, y]) android.graphics.Color.BLACK else android.graphics.Color.WHITE)
            }
        }
        bitmap
    } catch (_: Exception) {
        null
    }
}
