package com.goldbank.app.ui.kyc

import android.Manifest
import android.content.Intent
import android.graphics.BitmapFactory
import android.net.Uri
import android.provider.Settings
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
import com.goldbank.app.ui.components.LoadingButton
import com.goldbank.app.viewmodel.KycViewModel
import kotlinx.coroutines.launch

@OptIn(ExperimentalMaterial3Api::class, ExperimentalPermissionsApi::class)
@Composable
fun SelfieScreen(
    viewModel: KycViewModel,
    onSuccess: () -> Unit,
    onBack: () -> Unit,
    onVerificationComplete: (String) -> Unit,
) {
    val verificationUiState by viewModel.verificationUiState.collectAsState()
    val snackbarHostState = remember { SnackbarHostState() }
    val scope = rememberCoroutineScope()
    val context = LocalContext.current

    // step: 0=capture selfie, 1=confirm selfie, 2=capture ID, 3=confirm ID + verify
    var step by rememberSaveable { mutableIntStateOf(0) }
    var selfieBytes by remember { mutableStateOf<ByteArray?>(null) }
    var idDocBytes by remember { mutableStateOf<ByteArray?>(null) }
    var cameraController by remember { mutableStateOf<CameraController?>(null) }
    var showRationaleDialog by remember { mutableStateOf(false) }

    val cameraPermission = rememberPermissionState(Manifest.permission.CAMERA)

    LaunchedEffect(Unit) {
        if (!cameraPermission.status.isGranted) {
            cameraPermission.launchPermissionRequest()
        }
    }

    // Handle rationale dialog trigger after denial
    LaunchedEffect(cameraPermission.status) {
        if (cameraPermission.status is PermissionStatus.Denied &&
            (cameraPermission.status as PermissionStatus.Denied).shouldShowRationale
        ) {
            showRationaleDialog = true
        }
    }

    // Observe verification results
    LaunchedEffect(verificationUiState.verificationResult) {
        val result = verificationUiState.verificationResult
        if (result != null) {
            // Use the account ID from session; navigate with decision info
            onVerificationComplete(result.decision)
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
            text = { Text("Camera access is needed to capture your selfie and ID document for identity verification.") },
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
        0 -> "Take Selfie"
        1 -> "Confirm Selfie"
        2 -> "Capture ID Document"
        else -> "Confirm & Verify"
    }

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text(topBarTitle) },
                navigationIcon = {
                    IconButton(onClick = {
                        when (step) {
                            0 -> onBack()
                            1 -> { step = 0; selfieBytes = null }
                            2 -> { step = 1 }
                            else -> { step = 2; idDocBytes = null }
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
                            "Camera permission is required for selfie verification",
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
                // Step 0: Capture selfie (front camera)
                Box(modifier = Modifier.fillMaxSize().padding(padding)) {
                    CameraPreview(
                        modifier = Modifier.fillMaxSize(),
                        cameraConfiguration = {
                            setCameraLens(CameraLens.FRONT)
                            setFlashMode(FlashMode.OFF)
                            setImageFormat(ImageFormat.JPEG)
                            setDirectory(com.kashif.cameraK.enums.Directory.PICTURES)
                        },
                        onCameraControllerReady = { controller ->
                            cameraController = controller
                        },
                    )

                    Text(
                        "Position your face in the center",
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
                                            selfieBytes = java.io.File(result.filePath).readBytes()
                                            step = 1
                                        }
                                        is ImageCaptureResult.Success -> {
                                            selfieBytes = result.byteArray
                                            step = 1
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
                            .height(72.dp).widthIn(min = 220.dp),
                        shape = MaterialTheme.shapes.extraLarge,
                    ) {
                        Text("Capture", style = MaterialTheme.typography.labelLarge)
                    }
                }
            }

            step == 1 -> {
                // Step 1: Confirm selfie preview
                Column(
                    modifier = Modifier
                        .fillMaxSize()
                        .padding(padding)
                        .padding(16.dp),
                    horizontalAlignment = Alignment.CenterHorizontally,
                    verticalArrangement = Arrangement.Center,
                ) {
                    Text("Selfie Preview", style = MaterialTheme.typography.titleMedium)
                    Spacer(modifier = Modifier.height(16.dp))

                    selfieBytes?.let { bytes ->
                        val bitmap = BitmapFactory.decodeByteArray(bytes, 0, bytes.size)
                        if (bitmap != null) {
                            Image(
                                bitmap = bitmap.asImageBitmap(),
                                contentDescription = "Selfie preview",
                                modifier = Modifier
                                    .size(240.dp)
                                    .padding(8.dp),
                                contentScale = ContentScale.Crop,
                            )
                        }
                    }

                    Spacer(modifier = Modifier.height(24.dp))

                    Button(
                        onClick = {
                            selfieBytes?.let { bytes ->
                                viewModel.setSelfieBytes(bytes)
                            }
                            step = 2
                        },
                        modifier = Modifier.fillMaxWidth(),
                        enabled = selfieBytes != null,
                    ) {
                        Text("Continue to ID Photo")
                    }

                    Spacer(modifier = Modifier.height(12.dp))

                    OutlinedButton(
                        onClick = { step = 0; selfieBytes = null },
                        modifier = Modifier.fillMaxWidth(),
                    ) {
                        Text("Retake")
                    }
                }
            }

            step == 2 -> {
                // Step 2: Capture ID document (back camera)
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
                        "Take a clear photo of your national ID card",
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
                                            idDocBytes = java.io.File(result.filePath).readBytes()
                                            step = 3
                                        }
                                        is ImageCaptureResult.Success -> {
                                            idDocBytes = result.byteArray
                                            step = 3
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
                            .height(72.dp).widthIn(min = 220.dp),
                        shape = MaterialTheme.shapes.extraLarge,
                    ) {
                        Text("Capture", style = MaterialTheme.typography.labelLarge)
                    }
                }
            }

            else -> {
                // Step 3: Confirm ID + Verify
                Column(
                    modifier = Modifier
                        .fillMaxSize()
                        .padding(padding)
                        .padding(16.dp),
                    horizontalAlignment = Alignment.CenterHorizontally,
                    verticalArrangement = Arrangement.Center,
                ) {
                    Text("Review & Verify", style = MaterialTheme.typography.titleMedium)
                    Spacer(modifier = Modifier.height(16.dp))

                    Row(
                        modifier = Modifier.fillMaxWidth(),
                        horizontalArrangement = Arrangement.spacedBy(12.dp),
                        verticalAlignment = Alignment.CenterVertically,
                    ) {
                        // Selfie thumbnail (smaller)
                        selfieBytes?.let { bytes ->
                            val bitmap = BitmapFactory.decodeByteArray(bytes, 0, bytes.size)
                            if (bitmap != null) {
                                Column(
                                    horizontalAlignment = Alignment.CenterHorizontally,
                                    modifier = Modifier.weight(0.4f),
                                ) {
                                    Text("Selfie", style = MaterialTheme.typography.labelSmall)
                                    Spacer(modifier = Modifier.height(4.dp))
                                    Image(
                                        bitmap = bitmap.asImageBitmap(),
                                        contentDescription = "Selfie thumbnail",
                                        modifier = Modifier
                                            .size(96.dp),
                                        contentScale = ContentScale.Crop,
                                    )
                                }
                            }
                        }

                        // ID document thumbnail (larger)
                        idDocBytes?.let { bytes ->
                            val bitmap = BitmapFactory.decodeByteArray(bytes, 0, bytes.size)
                            if (bitmap != null) {
                                Column(
                                    horizontalAlignment = Alignment.CenterHorizontally,
                                    modifier = Modifier.weight(0.6f),
                                ) {
                                    Text("ID Document", style = MaterialTheme.typography.labelSmall)
                                    Spacer(modifier = Modifier.height(4.dp))
                                    Image(
                                        bitmap = bitmap.asImageBitmap(),
                                        contentDescription = "ID document thumbnail",
                                        modifier = Modifier
                                            .fillMaxWidth()
                                            .height(140.dp),
                                        contentScale = ContentScale.Crop,
                                    )
                                }
                            }
                        }
                    }

                    Spacer(modifier = Modifier.height(24.dp))

                    LoadingButton(
                        text = "Verify Identity",
                        onClick = {
                            idDocBytes?.let { bytes ->
                                viewModel.setIdDocumentBytes(bytes)
                            }
                            viewModel.verifyIdentity()
                        },
                        isLoading = verificationUiState.isVerifying,
                        enabled = idDocBytes != null && !verificationUiState.isVerifying,
                    )

                    Spacer(modifier = Modifier.height(12.dp))

                    OutlinedButton(
                        onClick = { step = 2; idDocBytes = null },
                        modifier = Modifier.fillMaxWidth(),
                        enabled = !verificationUiState.isVerifying,
                    ) {
                        Text("Retake ID")
                    }
                }
            }
        }
    }
}
