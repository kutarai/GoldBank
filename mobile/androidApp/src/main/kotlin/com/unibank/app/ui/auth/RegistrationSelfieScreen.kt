package com.unibank.app.ui.auth

import android.Manifest
import androidx.compose.foundation.layout.*
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import com.google.accompanist.permissions.ExperimentalPermissionsApi
import com.google.accompanist.permissions.isGranted
import com.google.accompanist.permissions.rememberPermissionState
import com.kashif.cameraK.controller.CameraController
import com.kashif.cameraK.enums.CameraLens
import com.kashif.cameraK.enums.FlashMode
import com.kashif.cameraK.enums.ImageFormat
import com.kashif.cameraK.result.ImageCaptureResult
import com.kashif.cameraK.ui.CameraPreview
import com.unibank.app.ui.components.ErrorDialog
import com.unibank.app.ui.components.LoadingButton
import com.unibank.app.viewmodel.AuthUiState
import com.unibank.app.viewmodel.AuthViewModel
import kotlinx.coroutines.launch

@OptIn(ExperimentalMaterial3Api::class, ExperimentalPermissionsApi::class)
@Composable
fun RegistrationSelfieScreen(
    viewModel: AuthViewModel,
    onComplete: () -> Unit,
    onBack: () -> Unit,
) {
    val uiState by viewModel.uiState.collectAsState()
    var capturedBytes by remember { mutableStateOf<ByteArray?>(null) }
    var step by rememberSaveable { mutableIntStateOf(0) } // 0=capture, 1=confirm
    var cameraController by remember { mutableStateOf<CameraController?>(null) }
    var errorMessage by remember { mutableStateOf<String?>(null) }
    val scope = rememberCoroutineScope()

    val cameraPermission = rememberPermissionState(Manifest.permission.CAMERA)

    LaunchedEffect(Unit) {
        if (!cameraPermission.status.isGranted) {
            cameraPermission.launchPermissionRequest()
        }
    }

    LaunchedEffect(uiState) {
        when (val state = uiState) {
            is AuthUiState.RegistrationComplete -> {
                onComplete()
                viewModel.resetState()
            }
            is AuthUiState.Error -> {
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
                title = { Text("Selfie Verification") },
                navigationIcon = {
                    IconButton(onClick = {
                        if (step == 1) {
                            step = 0; capturedBytes = null
                        } else onBack()
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
                .padding(padding),
        ) {
            // Step indicator
            Column(
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(horizontal = 24.dp, vertical = 8.dp),
                horizontalAlignment = Alignment.CenterHorizontally,
            ) {
                Text(
                    text = "Step 2 of 2",
                    style = MaterialTheme.typography.labelMedium,
                    color = MaterialTheme.colorScheme.primary,
                )
                Spacer(modifier = Modifier.height(4.dp))
                LinearProgressIndicator(
                    progress = { 1f },
                    modifier = Modifier.fillMaxWidth(),
                )
            }

            if (!cameraPermission.status.isGranted) {
                Box(
                    modifier = Modifier.fillMaxSize(),
                    contentAlignment = Alignment.Center,
                ) {
                    Column(horizontalAlignment = Alignment.CenterHorizontally) {
                        Text("Camera permission is required for selfie verification")
                        Spacer(modifier = Modifier.height(16.dp))
                        Button(onClick = { cameraPermission.launchPermissionRequest() }) {
                            Text("Grant Permission")
                        }
                    }
                }
            } else if (step == 0) {
                Box(modifier = Modifier.fillMaxSize()) {
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
                        modifier = Modifier.align(Alignment.TopCenter).padding(top = 16.dp),
                    )

                    Button(
                        onClick = {
                            scope.launch {
                                cameraController?.let { controller ->
                                    when (val result = controller.takePictureToFile()) {
                                        is ImageCaptureResult.SuccessWithFile -> {
                                            capturedBytes = java.io.File(result.filePath).readBytes()
                                            step = 1
                                        }
                                        is ImageCaptureResult.Success -> {
                                            capturedBytes = result.byteArray
                                            step = 1
                                        }
                                        is ImageCaptureResult.Error -> {
                                            errorMessage = result.exception.message ?: "Capture failed"
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
            } else {
                // Confirmation step
                Column(
                    modifier = Modifier
                        .fillMaxSize()
                        .padding(16.dp),
                    horizontalAlignment = Alignment.CenterHorizontally,
                    verticalArrangement = Arrangement.Center,
                ) {
                    Text("Selfie captured!", style = MaterialTheme.typography.titleMedium)
                    Spacer(modifier = Modifier.height(8.dp))
                    Text(
                        "Size: ${capturedBytes?.size?.let { it / 1024 } ?: 0} KB",
                        style = MaterialTheme.typography.bodySmall,
                    )
                    Spacer(modifier = Modifier.height(24.dp))

                    LoadingButton(
                        text = "Upload & Complete Registration",
                        onClick = {
                            capturedBytes?.let { bytes ->
                                viewModel.uploadRegistrationSelfie("image/jpeg", bytes)
                            }
                        },
                        isLoading = uiState is AuthUiState.Loading,
                        enabled = capturedBytes != null,
                    )

                    Spacer(modifier = Modifier.height(12.dp))

                    OutlinedButton(
                        onClick = { step = 0; capturedBytes = null },
                        modifier = Modifier.fillMaxWidth(),
                    ) { Text("Retake") }
                }
            }
        }
    }
}
