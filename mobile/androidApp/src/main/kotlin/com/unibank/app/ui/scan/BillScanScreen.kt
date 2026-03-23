package com.unibank.app.ui.scan

import android.Manifest
import android.content.Intent
import android.net.Uri
import android.provider.Settings
import androidx.compose.foundation.layout.*
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
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
import com.unibank.shared.domain.model.BillFields
import kotlinx.coroutines.launch

@OptIn(ExperimentalMaterial3Api::class, ExperimentalPermissionsApi::class)
@Composable
fun BillScanScreen(
    viewModel: DocumentScanViewModel,
    onFieldsExtracted: (BillFields) -> Unit,
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

    // Once bill fields arrive, invoke the callback and let the parent navigate away
    LaunchedEffect(uiState.billFields) {
        val fields = uiState.billFields
        if (fields != null && step == 1) {
            viewModel.reset()
            onFieldsExtracted(fields)
        }
    }

    if (showRationaleDialog) {
        AlertDialog(
            onDismissRequest = { showRationaleDialog = false },
            title = { Text("Camera Permission Required") },
            text = { Text("Camera access is needed to scan the bill.") },
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
                    Text(if (step == 0) "Scan Bill" else "Scanning Bill")
                },
                navigationIcon = {
                    IconButton(onClick = {
                        if (step == 1) {
                            step = 0
                            viewModel.reset()
                        } else {
                            onBack()
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
                            "Camera permission is required to scan the bill",
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
                        "Position the bill within the frame",
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
                                            viewModel.extractBillFields(bytes)
                                        }
                                        is ImageCaptureResult.Success -> {
                                            step = 1
                                            viewModel.extractBillFields(result.byteArray)
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

            else -> {
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
                                        text = "Could not read bill. Please enter details manually.",
                                        style = MaterialTheme.typography.bodyMedium,
                                        color = MaterialTheme.colorScheme.onErrorContainer,
                                        modifier = Modifier.padding(16.dp),
                                    )
                                }
                                Spacer(modifier = Modifier.height(24.dp))
                                Button(
                                    onClick = {
                                        viewModel.reset()
                                        onBack()
                                    },
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
                                    "Scanning bill...",
                                    style = MaterialTheme.typography.bodyMedium,
                                )
                            }
                        }
                    }
                }
            }
        }
    }
}
