package com.unibank.app.ui.scan

import android.Manifest
import android.content.Intent
import android.net.Uri
import android.provider.Settings
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.unit.dp
import com.google.accompanist.permissions.ExperimentalPermissionsApi
import com.google.accompanist.permissions.PermissionStatus
import com.google.accompanist.permissions.isGranted
import com.google.accompanist.permissions.rememberPermissionState
import com.kashif.cameraK.enums.CameraLens
import com.kashif.cameraK.enums.Directory
import com.kashif.cameraK.enums.FlashMode
import com.kashif.cameraK.enums.ImageFormat
import com.kashif.cameraK.result.ImageCaptureResult
import com.kashif.cameraK.ui.CameraPreview
import com.unibank.app.viewmodel.DocumentScanViewModel
import kotlinx.coroutines.launch

@OptIn(ExperimentalMaterial3Api::class, ExperimentalPermissionsApi::class)
@Composable
fun ChequeScanScreen(
    viewModel: DocumentScanViewModel,
    onDepositComplete: () -> Unit,
    onBack: () -> Unit,
) {
    val uiState by viewModel.uiState.collectAsState()
    val snackbarHostState = remember { SnackbarHostState() }
    val scope = rememberCoroutineScope()
    val context = LocalContext.current

    var step by remember { mutableIntStateOf(0) }
    var cameraController by remember { mutableStateOf<com.kashif.cameraK.controller.CameraController?>(null) }
    var showRationaleDialog by remember { mutableStateOf(false) }

    val cameraPermission = rememberPermissionState(Manifest.permission.CAMERA)

    LaunchedEffect(Unit) {
        if (!cameraPermission.status.isGranted) {
            cameraPermission.launchPermissionRequest()
        }
    }

    LaunchedEffect(cameraPermission.status) {
        if (cameraPermission.status is PermissionStatus.Denied &&
            (cameraPermission.status as PermissionStatus.Denied).shouldShowRationale
        ) {
            showRationaleDialog = true
        }
    }

    // Step 1: trigger OCR once bytes are captured; auto-advance on result
    LaunchedEffect(uiState.chequeFields) {
        if (uiState.chequeFields != null && step == 1) {
            step = 2
        }
    }

    LaunchedEffect(uiState.error) {
        // Error is surfaced in step 1 UI — no separate snackbar needed there
    }

    LaunchedEffect(uiState.depositSubmitted) {
        if (uiState.depositSubmitted) {
            snackbarHostState.showSnackbar("Cheque deposit submitted successfully")
            onDepositComplete()
        }
    }

    if (showRationaleDialog) {
        AlertDialog(
            onDismissRequest = { showRationaleDialog = false },
            title = { Text("Camera Permission Required") },
            text = { Text("Camera access is needed to capture the cheque for deposit.") },
            confirmButton = {
                TextButton(onClick = {
                    showRationaleDialog = false
                    cameraPermission.launchPermissionRequest()
                }) { Text("Grant") }
            },
            dismissButton = {
                TextButton(onClick = { showRationaleDialog = false }) { Text("Cancel") }
            },
        )
    }

    Scaffold(
        topBar = {
            TopAppBar(
                title = {
                    Text(
                        when (step) {
                            0 -> "Scan Cheque"
                            1 -> "Reading Cheque"
                            else -> "Cheque Deposit"
                        }
                    )
                },
                navigationIcon = {
                    IconButton(onClick = {
                        when (step) {
                            0 -> onBack()
                            1 -> { step = 0; viewModel.reset() }
                            else -> { step = 0; viewModel.reset() }
                        }
                    }) {
                        Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = "Back")
                    }
                },
            )
        },
        snackbarHost = { SnackbarHost(snackbarHostState) },
    ) { padding ->

        when {
            !cameraPermission.status.isGranted -> {
                val isPermanentlyDenied = cameraPermission.status is PermissionStatus.Denied &&
                    !(cameraPermission.status as PermissionStatus.Denied).shouldShowRationale

                Box(
                    modifier = Modifier
                        .fillMaxSize()
                        .padding(padding),
                    contentAlignment = Alignment.Center,
                ) {
                    Column(
                        horizontalAlignment = Alignment.CenterHorizontally,
                        modifier = Modifier.padding(24.dp),
                    ) {
                        Text(
                            "Camera permission is required to scan the cheque",
                            style = MaterialTheme.typography.bodyMedium,
                        )
                        Spacer(modifier = Modifier.height(16.dp))
                        if (isPermanentlyDenied) {
                            Button(onClick = {
                                val intent = Intent(Settings.ACTION_APPLICATION_DETAILS_SETTINGS).apply {
                                    data = Uri.fromParts("package", context.packageName, null)
                                }
                                context.startActivity(intent)
                            }) {
                                Text("Go to Settings")
                            }
                        } else {
                            Button(onClick = { cameraPermission.launchPermissionRequest() }) {
                                Text("Grant Permission")
                            }
                        }
                    }
                }
            }

            step == 0 -> {
                // Step 0 — Camera Capture
                Box(modifier = Modifier.fillMaxSize().padding(padding)) {
                    CameraPreview(
                        modifier = Modifier.fillMaxSize(),
                        cameraConfiguration = {
                            setCameraLens(CameraLens.BACK)
                            setFlashMode(FlashMode.OFF)
                            setImageFormat(ImageFormat.JPEG)
                            setDirectory(Directory.PICTURES)
                        },
                        onCameraControllerReady = { controller ->
                            cameraController = controller
                        },
                    )

                    Text(
                        "Position the cheque within the frame",
                        style = MaterialTheme.typography.bodyMedium,
                        color = MaterialTheme.colorScheme.onSurface,
                        modifier = Modifier
                            .align(Alignment.TopCenter)
                            .padding(top = 16.dp),
                    )

                    Button(
                        onClick = {
                            scope.launch {
                                cameraController?.let { controller ->
                                    when (val result = controller.takePictureToFile()) {
                                        is ImageCaptureResult.SuccessWithFile -> {
                                            val bytes = java.io.File(result.filePath).readBytes()
                                            step = 1
                                            viewModel.extractChequeFields(bytes)
                                        }
                                        is ImageCaptureResult.Success -> {
                                            step = 1
                                            viewModel.extractChequeFields(result.byteArray)
                                        }
                                        is ImageCaptureResult.Error -> {
                                            snackbarHostState.showSnackbar(
                                                result.exception.message ?: "Capture failed"
                                            )
                                        }
                                    }
                                }
                            }
                        },
                        modifier = Modifier
                            .align(Alignment.BottomCenter)
                            .padding(bottom = 32.dp)
                            .size(72.dp),
                        shape = MaterialTheme.shapes.extraLarge,
                    ) {
                        Text("Capture", style = MaterialTheme.typography.labelLarge)
                    }
                }
            }

            step == 1 -> {
                // Step 1 — Processing / OCR
                Box(
                    modifier = Modifier
                        .fillMaxSize()
                        .padding(padding),
                    contentAlignment = Alignment.Center,
                ) {
                    when {
                        uiState.error != null -> {
                            Column(
                                horizontalAlignment = Alignment.CenterHorizontally,
                                modifier = Modifier.padding(24.dp),
                            ) {
                                Card(
                                    colors = CardDefaults.cardColors(
                                        containerColor = MaterialTheme.colorScheme.errorContainer,
                                    ),
                                    modifier = Modifier.fillMaxWidth(),
                                ) {
                                    Text(
                                        text = uiState.error!!,
                                        style = MaterialTheme.typography.bodyMedium,
                                        color = MaterialTheme.colorScheme.onErrorContainer,
                                        modifier = Modifier.padding(16.dp),
                                    )
                                }
                                Spacer(modifier = Modifier.height(24.dp))
                                Button(
                                    onClick = { step = 2 },
                                    modifier = Modifier.fillMaxWidth(),
                                ) {
                                    Text("Enter Manually")
                                }
                            }
                        }
                        else -> {
                            Column(
                                horizontalAlignment = Alignment.CenterHorizontally,
                                verticalArrangement = Arrangement.Center,
                            ) {
                                CircularProgressIndicator()
                                Spacer(modifier = Modifier.height(16.dp))
                                Text(
                                    "Reading cheque...",
                                    style = MaterialTheme.typography.bodyMedium,
                                )
                            }
                        }
                    }
                }
            }

            else -> {
                // Step 2 — Confirmation Form
                val chequeFields = uiState.chequeFields

                var chequeNumber by remember { mutableStateOf(chequeFields?.chequeNumber ?: "") }
                var amount by remember { mutableStateOf(chequeFields?.amount ?: "") }
                var amountInWords by remember { mutableStateOf(chequeFields?.amountInWords ?: "") }
                var payee by remember { mutableStateOf(chequeFields?.payee ?: "") }
                var date by remember { mutableStateOf(chequeFields?.date ?: "") }
                var bank by remember { mutableStateOf(chequeFields?.bank ?: "") }
                var branchCode by remember { mutableStateOf(chequeFields?.branchCode ?: "") }
                var accountNumber by remember { mutableStateOf(chequeFields?.accountNumber ?: "") }

                Column(
                    modifier = Modifier
                        .fillMaxSize()
                        .padding(padding)
                        .verticalScroll(rememberScrollState())
                        .padding(horizontal = 16.dp, vertical = 12.dp),
                    verticalArrangement = Arrangement.spacedBy(12.dp),
                ) {
                    if (chequeFields?.amountConsistent == false) {
                        Card(
                            colors = CardDefaults.cardColors(
                                containerColor = Color(0xFFFFF8E1),
                            ),
                            modifier = Modifier.fillMaxWidth(),
                        ) {
                            Text(
                                text = "Amount in words does not match figures — please verify",
                                style = MaterialTheme.typography.bodySmall,
                                color = Color(0xFF795548),
                                modifier = Modifier.padding(12.dp),
                            )
                        }
                    }

                    OutlinedTextField(
                        value = chequeNumber,
                        onValueChange = { chequeNumber = it },
                        label = { Text("Cheque Number") },
                        modifier = Modifier.fillMaxWidth(),
                        singleLine = true,
                    )

                    OutlinedTextField(
                        value = amount,
                        onValueChange = { amount = it },
                        label = { Text("Amount") },
                        modifier = Modifier.fillMaxWidth(),
                        singleLine = true,
                    )

                    OutlinedTextField(
                        value = amountInWords,
                        onValueChange = { amountInWords = it },
                        label = { Text("Amount in Words") },
                        modifier = Modifier.fillMaxWidth(),
                    )

                    OutlinedTextField(
                        value = payee,
                        onValueChange = { payee = it },
                        label = { Text("Payee") },
                        modifier = Modifier.fillMaxWidth(),
                        singleLine = true,
                    )

                    OutlinedTextField(
                        value = date,
                        onValueChange = { date = it },
                        label = { Text("Date") },
                        modifier = Modifier.fillMaxWidth(),
                        singleLine = true,
                    )

                    OutlinedTextField(
                        value = bank,
                        onValueChange = { bank = it },
                        label = { Text("Bank") },
                        modifier = Modifier.fillMaxWidth(),
                        singleLine = true,
                    )

                    OutlinedTextField(
                        value = branchCode,
                        onValueChange = { branchCode = it },
                        label = { Text("Branch Code") },
                        modifier = Modifier.fillMaxWidth(),
                        singleLine = true,
                    )

                    OutlinedTextField(
                        value = accountNumber,
                        onValueChange = { accountNumber = it },
                        label = { Text("Account Number") },
                        modifier = Modifier.fillMaxWidth(),
                        singleLine = true,
                    )

                    Spacer(modifier = Modifier.height(8.dp))

                    Button(
                        onClick = {
                            viewModel.submitChequeDeposit(
                                chequeNumber = chequeNumber,
                                amount = amount,
                                payee = payee,
                                date = date,
                                bank = bank,
                                branchCode = branchCode,
                                accountNumber = accountNumber,
                            )
                        },
                        modifier = Modifier.fillMaxWidth(),
                        enabled = !uiState.isLoading,
                    ) {
                        if (uiState.isLoading) {
                            CircularProgressIndicator(
                                modifier = Modifier.size(20.dp),
                                strokeWidth = 2.dp,
                                color = MaterialTheme.colorScheme.onPrimary,
                            )
                        } else {
                            Text("Submit Deposit")
                        }
                    }
                }
            }
        }
    }
}
