package com.unibank.app.ui.kyc

import android.Manifest
import android.content.Intent
import android.graphics.BitmapFactory
import android.net.Uri
import android.provider.Settings
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.Image
import androidx.compose.foundation.layout.*
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.CameraAlt
import androidx.compose.material.icons.filled.CheckCircle
import androidx.compose.material.icons.filled.Photo
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.asImageBitmap
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.unit.dp
import com.google.accompanist.permissions.ExperimentalPermissionsApi
import com.google.accompanist.permissions.PermissionStatus
import com.google.accompanist.permissions.isGranted
import com.google.accompanist.permissions.rememberPermissionState
import com.kashif.cameraK.controller.CameraController
import com.kashif.cameraK.enums.CameraLens
import com.kashif.cameraK.enums.FlashMode
import com.kashif.cameraK.enums.ImageFormat
import com.kashif.cameraK.result.ImageCaptureResult
import com.kashif.cameraK.ui.CameraPreview
import com.unibank.app.ui.components.LoadingButton
import com.unibank.app.viewmodel.KycViewModel
import kotlinx.coroutines.launch

@OptIn(ExperimentalMaterial3Api::class, ExperimentalPermissionsApi::class)
@Composable
fun ProofOfAddressScreen(
    viewModel: KycViewModel,
    onSuccess: () -> Unit,
    onBack: () -> Unit,
) {
    val verificationUiState by viewModel.verificationUiState.collectAsState()
    val snackbarHostState = remember { SnackbarHostState() }
    val scope = rememberCoroutineScope()
    val context = LocalContext.current

    // step: 0=choose method, 1=camera/gallery capture, 2=preview + verify
    var step by rememberSaveable { mutableIntStateOf(0) }
    var useCamera by rememberSaveable { mutableStateOf(false) }
    var documentBytes by remember { mutableStateOf<ByteArray?>(null) }
    var cameraController by remember { mutableStateOf<CameraController?>(null) }
    var showRationaleDialog by remember { mutableStateOf(false) }

    val cameraPermission = rememberPermissionState(Manifest.permission.CAMERA)

    // Gallery picker launcher
    val galleryLauncher = rememberLauncherForActivityResult(
        contract = ActivityResultContracts.GetContent(),
    ) { uri: Uri? ->
        if (uri != null) {
            val bytes = context.contentResolver.openInputStream(uri)?.readBytes()
            if (bytes != null) {
                documentBytes = bytes
                step = 2
            } else {
                scope.launch { snackbarHostState.showSnackbar("Failed to read image from gallery.") }
            }
        }
    }

    // Handle rationale dialog after camera permission denial
    LaunchedEffect(cameraPermission.status) {
        if (cameraPermission.status is PermissionStatus.Denied &&
            (cameraPermission.status as PermissionStatus.Denied).shouldShowRationale
        ) {
            showRationaleDialog = true
        }
    }

    // Observe proof-of-address result
    LaunchedEffect(verificationUiState.proofOfAddressResult) {
        if (verificationUiState.proofOfAddressResult != null) {
            step = 2 // stay on preview step to show result card
        }
    }

    LaunchedEffect(verificationUiState.verificationError) {
        val error = verificationUiState.verificationError
        if (error != null) {
            snackbarHostState.showSnackbar(error)
        }
    }

    if (showRationaleDialog) {
        AlertDialog(
            onDismissRequest = { showRationaleDialog = false },
            title = { Text("Camera Permission Required") },
            text = { Text("Camera access is needed to capture your proof of address document.") },
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

    val topBarTitle = when (step) {
        0 -> "Proof of Address"
        1 -> if (useCamera) "Capture Document" else "Choose Image"
        else -> "Verify Document"
    }

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text(topBarTitle) },
                navigationIcon = {
                    IconButton(onClick = {
                        when (step) {
                            0 -> onBack()
                            1 -> { step = 0 }
                            else -> {
                                documentBytes = null
                                step = 0
                            }
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
            // ─── Step 0: Choose Capture Method ──────────────────────────────────
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
                        "Proof of Address",
                        style = MaterialTheme.typography.headlineSmall,
                    )
                    Spacer(modifier = Modifier.height(12.dp))
                    Text(
                        "Upload a utility bill, bank statement, or official letter showing your address",
                        style = MaterialTheme.typography.bodyMedium,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                    )
                    Spacer(modifier = Modifier.height(40.dp))

                    Button(
                        onClick = {
                            useCamera = true
                            if (cameraPermission.status.isGranted) {
                                step = 1
                            } else {
                                cameraPermission.launchPermissionRequest()
                            }
                        },
                        modifier = Modifier
                            .fillMaxWidth()
                            .height(56.dp),
                    ) {
                        Icon(
                            Icons.Filled.CameraAlt,
                            contentDescription = null,
                            modifier = Modifier.size(20.dp),
                        )
                        Spacer(modifier = Modifier.width(8.dp))
                        Text("Take Photo")
                    }

                    Spacer(modifier = Modifier.height(16.dp))

                    OutlinedButton(
                        onClick = {
                            useCamera = false
                            galleryLauncher.launch("image/*")
                        },
                        modifier = Modifier
                            .fillMaxWidth()
                            .height(56.dp),
                    ) {
                        Icon(
                            Icons.Filled.Photo,
                            contentDescription = null,
                            modifier = Modifier.size(20.dp),
                        )
                        Spacer(modifier = Modifier.width(8.dp))
                        Text("Choose from Gallery")
                    }
                }
            }

            // ─── Step 1: Camera Capture ──────────────────────────────────────────
            1 -> {
                if (!cameraPermission.status.isGranted) {
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
                                "Camera permission is required to capture the document",
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
                } else {
                    Box(modifier = Modifier.fillMaxSize().padding(padding)) {
                        CameraPreview(
                            modifier = Modifier.fillMaxSize(),
                            cameraConfiguration = {
                                setCameraLens(CameraLens.BACK)
                                setFlashMode(FlashMode.OFF)
                                setImageFormat(ImageFormat.JPEG)
                                setDirectory(com.kashif.cameraK.enums.Directory.PICTURES)
                            },
                            onCameraControllerReady = { controller ->
                                cameraController = controller
                            },
                        )

                        Text(
                            "Position the document clearly in frame",
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
                                                documentBytes = java.io.File(result.filePath).readBytes()
                                                step = 2
                                            }
                                            is ImageCaptureResult.Success -> {
                                                documentBytes = result.byteArray
                                                step = 2
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
            }

            // ─── Step 2: Preview + Verify ────────────────────────────────────────
            else -> {
                val result = verificationUiState.proofOfAddressResult
                val hasError = verificationUiState.verificationError != null &&
                    !verificationUiState.isVerifying &&
                    result == null

                Column(
                    modifier = Modifier
                        .fillMaxSize()
                        .padding(padding)
                        .padding(16.dp),
                    horizontalAlignment = Alignment.CenterHorizontally,
                ) {
                    // Document preview
                    documentBytes?.let { bytes ->
                        val bitmap = BitmapFactory.decodeByteArray(bytes, 0, bytes.size)
                        if (bitmap != null) {
                            Image(
                                bitmap = bitmap.asImageBitmap(),
                                contentDescription = "Document preview",
                                modifier = Modifier
                                    .fillMaxWidth()
                                    .height(220.dp),
                                contentScale = ContentScale.Fit,
                            )
                        }
                    }

                    Spacer(modifier = Modifier.height(24.dp))

                    when {
                        // ── Verification result ──────────────────────────────
                        result != null -> {
                            Card(
                                modifier = Modifier.fillMaxWidth(),
                                colors = CardDefaults.cardColors(
                                    containerColor = MaterialTheme.colorScheme.surfaceVariant,
                                ),
                            ) {
                                Column(modifier = Modifier.padding(16.dp)) {
                                    Text(
                                        "Verification Result",
                                        style = MaterialTheme.typography.titleMedium,
                                    )
                                    Spacer(modifier = Modifier.height(12.dp))

                                    // Extracted name
                                    val extractedName = result.extractedName
                                    if (extractedName != null) {
                                        VerificationRow(
                                            label = "Name",
                                            value = extractedName,
                                            matched = result.nameMatch,
                                        )
                                        Spacer(modifier = Modifier.height(8.dp))
                                    }

                                    // Decision
                                    val isApproved = result.decision.contains("APPROVE", ignoreCase = true) ||
                                        result.decision.contains("VERIFIED", ignoreCase = true)
                                    Row(verticalAlignment = Alignment.CenterVertically) {
                                        Text(
                                            "Decision: ",
                                            style = MaterialTheme.typography.bodyMedium,
                                        )
                                        Text(
                                            result.decision,
                                            style = MaterialTheme.typography.bodyMedium,
                                            color = if (isApproved)
                                                MaterialTheme.colorScheme.primary
                                            else
                                                MaterialTheme.colorScheme.error,
                                        )
                                    }
                                }
                            }

                            Spacer(modifier = Modifier.height(24.dp))

                            Button(
                                onClick = onSuccess,
                                modifier = Modifier.fillMaxWidth().height(52.dp),
                                shape = MaterialTheme.shapes.medium,
                            ) {
                                Text("Continue", style = MaterialTheme.typography.labelLarge)
                            }
                        }

                        // ── Verification error ───────────────────────────────
                        hasError -> {
                            Text(
                                "Verification failed. Please try again.",
                                style = MaterialTheme.typography.bodyMedium,
                                color = MaterialTheme.colorScheme.error,
                            )
                            Spacer(modifier = Modifier.height(16.dp))
                            Button(
                                onClick = {
                                    documentBytes = null
                                    step = 0
                                },
                                modifier = Modifier.fillMaxWidth().height(52.dp),
                                shape = MaterialTheme.shapes.medium,
                            ) {
                                Text("Retry", style = MaterialTheme.typography.labelLarge)
                            }
                        }

                        // ── Pending / not yet verified ───────────────────────
                        else -> {
                            LoadingButton(
                                text = "Verify Document",
                                onClick = {
                                    documentBytes?.let { bytes ->
                                        viewModel.verifyProofOfAddress(bytes)
                                    }
                                },
                                isLoading = verificationUiState.isVerifying,
                                enabled = documentBytes != null && !verificationUiState.isVerifying,
                            )

                            Spacer(modifier = Modifier.height(12.dp))

                            OutlinedButton(
                                onClick = {
                                    documentBytes = null
                                    step = 0
                                },
                                modifier = Modifier.fillMaxWidth(),
                                enabled = !verificationUiState.isVerifying,
                            ) {
                                Text("Retake / Choose Again")
                            }
                        }
                    }
                }
            }
        }
    }
}

@Composable
private fun VerificationRow(
    label: String,
    value: String,
    matched: Boolean,
) {
    Row(
        modifier = Modifier.fillMaxWidth(),
        verticalAlignment = Alignment.CenterVertically,
        horizontalArrangement = Arrangement.SpaceBetween,
    ) {
        Column(modifier = Modifier.weight(1f)) {
            Text(label, style = MaterialTheme.typography.labelSmall)
            Text(value, style = MaterialTheme.typography.bodyMedium)
        }
        Icon(
            imageVector = Icons.Filled.CheckCircle,
            contentDescription = if (matched) "Matched" else "No match",
            tint = if (matched)
                MaterialTheme.colorScheme.primary
            else
                MaterialTheme.colorScheme.outline,
            modifier = Modifier.size(20.dp),
        )
    }
}
