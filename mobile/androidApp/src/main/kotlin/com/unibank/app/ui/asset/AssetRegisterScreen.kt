package com.unibank.app.ui.asset

import android.Manifest
import android.content.Intent
import android.net.Uri
import android.provider.Settings
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.ArrowDropDown
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.DropdownMenuItem
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.ExposedDropdownMenuBox
import androidx.compose.material3.ExposedDropdownMenuDefaults
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Scaffold
import androidx.compose.material3.SnackbarHost
import androidx.compose.material3.SnackbarHostState
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.TopAppBar
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.input.KeyboardType
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
import com.unibank.app.viewmodel.AssetViewModel
import kotlinx.coroutines.launch

private val assetTypes = listOf("GoldCoin", "GoldBar", "Silver", "Platinum", "PreciousStone", "Other")
private val unitOptions = listOf("coins", "bars", "grams", "oz", "carats")

@OptIn(ExperimentalMaterial3Api::class, ExperimentalPermissionsApi::class)
@Composable
fun AssetRegisterScreen(
    viewModel: AssetViewModel,
    onComplete: () -> Unit,
    onBack: () -> Unit,
) {
    val uiState by viewModel.uiState.collectAsState()
    val snackbarHostState = remember { SnackbarHostState() }
    val scope = rememberCoroutineScope()
    val context = LocalContext.current

    var step by remember { mutableIntStateOf(0) }
    var cameraController by remember { mutableStateOf<com.kashif.cameraK.controller.CameraController?>(null) }
    var capturedImagePath by remember { mutableStateOf("") }
    var capturedImageBytes by remember { mutableStateOf<ByteArray?>(null) }
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

    // Auto-advance to step 2 when OCR completes
    LaunchedEffect(uiState.receiptOcr) {
        if (uiState.receiptOcr != null && step == 1) {
            step = 2
        }
    }

    // On OCR error in step 1, allow manual fallback
    LaunchedEffect(uiState.error) {
        if (uiState.error != null && step == 1) {
            // Error shown inline; no separate snackbar needed
        }
    }

    // Navigate away on successful registration
    LaunchedEffect(uiState.isRegistering) {
        // isRegistering transitions false→false on success with a new asset in the list
        // We detect success by checking if isRegistering just became false and no error
    }

    // Watch for registration completion (isRegistering goes false with no error)
    var wasRegistering by remember { mutableStateOf(false) }
    LaunchedEffect(uiState.isRegistering, uiState.error) {
        if (wasRegistering && !uiState.isRegistering && uiState.error == null) {
            onComplete()
        }
        wasRegistering = uiState.isRegistering
    }

    if (showRationaleDialog) {
        AlertDialog(
            onDismissRequest = { showRationaleDialog = false },
            title = { Text("Camera Permission Required") },
            text = { Text("Camera access is needed to capture the deposit receipt.") },
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
                            0 -> "Capture Receipt"
                            1 -> "Reading Receipt"
                            else -> "Confirm Asset Details"
                        }
                    )
                },
                navigationIcon = {
                    IconButton(onClick = {
                        when (step) {
                            0 -> onBack()
                            1 -> { step = 0; viewModel.clearOcr() }
                            else -> { step = 0; viewModel.clearOcr() }
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
            !cameraPermission.status.isGranted && step == 0 -> {
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
                            "Camera permission is required to capture the deposit receipt",
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
                        Spacer(modifier = Modifier.height(8.dp))
                        TextButton(onClick = { step = 2 }) {
                            Text("Enter Details Manually")
                        }
                    }
                }
            }

            step == 0 -> {
                // Step 0 — Camera Capture
                Box(
                    modifier = Modifier
                        .fillMaxSize()
                        .padding(padding),
                ) {
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
                        "Take a photo of your deposit receipt",
                        style = MaterialTheme.typography.bodyMedium,
                        color = MaterialTheme.colorScheme.onSurface,
                        modifier = Modifier
                            .align(Alignment.TopCenter)
                            .padding(top = 16.dp),
                    )

                    Column(
                        modifier = Modifier
                            .align(Alignment.BottomCenter)
                            .padding(bottom = 32.dp),
                        horizontalAlignment = Alignment.CenterHorizontally,
                        verticalArrangement = Arrangement.spacedBy(12.dp),
                    ) {
                        Button(
                            onClick = {
                                scope.launch {
                                    cameraController?.let { controller ->
                                        when (val result = controller.takePictureToFile()) {
                                            is ImageCaptureResult.SuccessWithFile -> {
                                                val file = java.io.File(result.filePath)
                                                capturedImagePath = result.filePath
                                                capturedImageBytes = file.readBytes()
                                                step = 1
                                                viewModel.extractReceipt(file.readBytes())
                                            }
                                            is ImageCaptureResult.Success -> {
                                                capturedImageBytes = result.byteArray
                                                step = 1
                                                viewModel.extractReceipt(result.byteArray)
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
                            modifier = Modifier.size(72.dp),
                            shape = MaterialTheme.shapes.extraLarge,
                        ) {
                            Text("Capture", style = MaterialTheme.typography.labelLarge)
                        }
                        TextButton(onClick = { step = 2 }) {
                            Text("Skip — Enter Manually")
                        }
                    }
                }
            }

            step == 1 -> {
                // Step 1 — OCR Processing
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
                                verticalArrangement = Arrangement.spacedBy(16.dp),
                            ) {
                                androidx.compose.material3.Card(
                                    colors = androidx.compose.material3.CardDefaults.cardColors(
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
                                Button(
                                    onClick = { step = 2 },
                                    modifier = Modifier.fillMaxWidth(),
                                ) {
                                    Text("Enter Details Manually")
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
                                    "Reading receipt...",
                                    style = MaterialTheme.typography.bodyMedium,
                                )
                            }
                        }
                    }
                }
            }

            else -> {
                // Step 2 — Confirm / Edit Form
                AssetDetailsForm(
                    viewModel = viewModel,
                    capturedImagePath = capturedImagePath,
                    modifier = Modifier
                        .fillMaxSize()
                        .padding(padding),
                )
            }
        }
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun AssetDetailsForm(
    viewModel: AssetViewModel,
    capturedImagePath: String,
    modifier: Modifier = Modifier,
) {
    val uiState by viewModel.uiState.collectAsState()
    val ocr = uiState.receiptOcr

    var assetType by remember { mutableStateOf(ocr?.let { guessAssetType(it.description) } ?: "GoldCoin") }
    var description by remember { mutableStateOf(ocr?.description ?: "") }
    var quantity by remember { mutableStateOf(ocr?.quantity ?: "") }
    var unit by remember { mutableStateOf("coins") }
    var weightGrams by remember { mutableStateOf(ocr?.weight ?: "") }
    var purity by remember { mutableStateOf(ocr?.purity ?: "") }
    var depositHouse by remember { mutableStateOf(ocr?.depositHouse ?: "") }
    var receiptNumber by remember { mutableStateOf(ocr?.receiptNumber ?: "") }
    var receiptDate by remember { mutableStateOf(ocr?.date ?: "") }

    var assetTypeExpanded by remember { mutableStateOf(false) }
    var unitExpanded by remember { mutableStateOf(false) }

    Column(
        modifier = modifier
            .verticalScroll(rememberScrollState())
            .padding(horizontal = 16.dp, vertical = 12.dp),
        verticalArrangement = Arrangement.spacedBy(12.dp),
    ) {
        if (ocr != null) {
            androidx.compose.material3.Card(
                colors = androidx.compose.material3.CardDefaults.cardColors(
                    containerColor = MaterialTheme.colorScheme.secondaryContainer,
                ),
                modifier = Modifier.fillMaxWidth(),
            ) {
                Text(
                    text = "Details pre-filled from receipt — please verify before submitting.",
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSecondaryContainer,
                    modifier = Modifier.padding(12.dp),
                )
            }
        }

        // Asset Type dropdown
        ExposedDropdownMenuBox(
            expanded = assetTypeExpanded,
            onExpandedChange = { assetTypeExpanded = it },
        ) {
            OutlinedTextField(
                value = assetType,
                onValueChange = {},
                readOnly = true,
                label = { Text("Asset Type") },
                trailingIcon = {
                    ExposedDropdownMenuDefaults.TrailingIcon(expanded = assetTypeExpanded)
                },
                modifier = Modifier
                    .fillMaxWidth()
                    .menuAnchor(),
            )
            ExposedDropdownMenu(
                expanded = assetTypeExpanded,
                onDismissRequest = { assetTypeExpanded = false },
            ) {
                assetTypes.forEach { type ->
                    DropdownMenuItem(
                        text = { Text(type) },
                        onClick = {
                            assetType = type
                            assetTypeExpanded = false
                        },
                    )
                }
            }
        }

        OutlinedTextField(
            value = description,
            onValueChange = { description = it },
            label = { Text("Description") },
            modifier = Modifier.fillMaxWidth(),
            singleLine = true,
        )

        OutlinedTextField(
            value = quantity,
            onValueChange = { quantity = it },
            label = { Text("Quantity") },
            modifier = Modifier.fillMaxWidth(),
            singleLine = true,
            keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Decimal),
        )

        // Unit dropdown
        ExposedDropdownMenuBox(
            expanded = unitExpanded,
            onExpandedChange = { unitExpanded = it },
        ) {
            OutlinedTextField(
                value = unit,
                onValueChange = {},
                readOnly = true,
                label = { Text("Unit") },
                trailingIcon = {
                    ExposedDropdownMenuDefaults.TrailingIcon(expanded = unitExpanded)
                },
                modifier = Modifier
                    .fillMaxWidth()
                    .menuAnchor(),
            )
            ExposedDropdownMenu(
                expanded = unitExpanded,
                onDismissRequest = { unitExpanded = false },
            ) {
                unitOptions.forEach { opt ->
                    DropdownMenuItem(
                        text = { Text(opt) },
                        onClick = {
                            unit = opt
                            unitExpanded = false
                        },
                    )
                }
            }
        }

        OutlinedTextField(
            value = weightGrams,
            onValueChange = { weightGrams = it },
            label = { Text("Weight (grams)") },
            modifier = Modifier.fillMaxWidth(),
            singleLine = true,
            keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Decimal),
        )

        OutlinedTextField(
            value = purity,
            onValueChange = { purity = it },
            label = { Text("Purity (e.g. 0.999)") },
            modifier = Modifier.fillMaxWidth(),
            singleLine = true,
            keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Decimal),
        )

        OutlinedTextField(
            value = depositHouse,
            onValueChange = { depositHouse = it },
            label = { Text("Deposit House") },
            modifier = Modifier.fillMaxWidth(),
            singleLine = true,
        )

        OutlinedTextField(
            value = receiptNumber,
            onValueChange = { receiptNumber = it },
            label = { Text("Receipt Number") },
            modifier = Modifier.fillMaxWidth(),
            singleLine = true,
        )

        OutlinedTextField(
            value = receiptDate,
            onValueChange = { receiptDate = it },
            label = { Text("Receipt Date") },
            modifier = Modifier.fillMaxWidth(),
            singleLine = true,
            placeholder = { Text("e.g. 2024-03-15") },
        )

        if (uiState.error != null) {
            androidx.compose.material3.Card(
                colors = androidx.compose.material3.CardDefaults.cardColors(
                    containerColor = MaterialTheme.colorScheme.errorContainer,
                ),
                modifier = Modifier.fillMaxWidth(),
            ) {
                Text(
                    text = uiState.error!!,
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onErrorContainer,
                    modifier = Modifier.padding(12.dp),
                )
            }
        }

        Spacer(modifier = Modifier.height(8.dp))

        Button(
            onClick = {
                viewModel.registerAsset(
                    receiptNumber = receiptNumber,
                    assetType = assetType,
                    description = description,
                    quantity = quantity,
                    unit = unit,
                    weightGrams = weightGrams,
                    purity = purity,
                    receiptImagePath = capturedImagePath,
                    depositHouseId = depositHouse,
                )
            },
            modifier = Modifier.fillMaxWidth(),
            enabled = !uiState.isRegistering,
        ) {
            if (uiState.isRegistering) {
                CircularProgressIndicator(
                    modifier = Modifier.size(20.dp),
                    strokeWidth = 2.dp,
                    color = MaterialTheme.colorScheme.onPrimary,
                )
            } else {
                Text("Register Asset")
            }
        }

        Spacer(modifier = Modifier.height(16.dp))
    }
}

private fun guessAssetType(description: String): String {
    val lower = description.lowercase()
    return when {
        "gold coin" in lower || "goldcoin" in lower -> "GoldCoin"
        "gold bar" in lower || "goldbar" in lower -> "GoldBar"
        "silver" in lower -> "Silver"
        "platinum" in lower -> "Platinum"
        "diamond" in lower || "ruby" in lower || "emerald" in lower || "stone" in lower -> "PreciousStone"
        else -> "Other"
    }
}
