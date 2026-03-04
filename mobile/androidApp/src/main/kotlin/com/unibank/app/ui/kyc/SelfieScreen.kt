package com.unibank.app.ui.kyc

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
import com.unibank.app.ui.components.LoadingButton
import com.unibank.app.viewmodel.KycUiState
import com.unibank.app.viewmodel.KycViewModel
import kotlinx.coroutines.launch

@OptIn(ExperimentalMaterial3Api::class, ExperimentalPermissionsApi::class)
@Composable
fun SelfieScreen(
    viewModel: KycViewModel,
    onSuccess: () -> Unit,
    onBack: () -> Unit,
) {
    val uiState by viewModel.uiState.collectAsState()
    val snackbarHostState = remember { SnackbarHostState() }
    val scope = rememberCoroutineScope()
    var capturedBytes by remember { mutableStateOf<ByteArray?>(null) }
    var step by rememberSaveable { mutableIntStateOf(0) } // 0=capture, 1=confirm
    var cameraController by remember { mutableStateOf<CameraController?>(null) }

    val cameraPermission = rememberPermissionState(Manifest.permission.CAMERA)

    LaunchedEffect(Unit) {
        if (!cameraPermission.status.isGranted) {
            cameraPermission.launchPermissionRequest()
        }
    }

    LaunchedEffect(uiState) {
        when (val state = uiState) {
            is KycUiState.SelfieUploaded -> {
                if (state.result.success) {
                    onSuccess()
                } else {
                    snackbarHostState.showSnackbar(state.result.message)
                }
                viewModel.resetState()
            }
            is KycUiState.Error -> {
                snackbarHostState.showSnackbar(state.message)
                viewModel.resetState()
            }
            else -> {}
        }
    }

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text("Take Selfie") },
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
        snackbarHost = { SnackbarHost(snackbarHostState) },
    ) { padding ->
        if (!cameraPermission.status.isGranted) {
            Box(
                modifier = Modifier.fillMaxSize().padding(padding),
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
            Box(
                modifier = Modifier.fillMaxSize().padding(padding),
            ) {
                CameraPreview(
                    modifier = Modifier.fillMaxSize(),
                    cameraConfiguration = {
                        setCameraLens(CameraLens.FRONT)
                        setFlashMode(FlashMode.OFF)
                        setImageFormat(ImageFormat.JPEG)
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
                                when (val result = controller.takePicture()) {
                                    is ImageCaptureResult.Success -> {
                                        capturedBytes = result.byteArray
                                        step = 1
                                    }
                                    is ImageCaptureResult.SuccessWithFile -> {
                                        // We prefer byte array; read file if needed
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
        } else {
            // Confirmation step
            Column(
                modifier = Modifier
                    .fillMaxSize()
                    .padding(padding)
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
                    text = "Upload Selfie",
                    onClick = {
                        capturedBytes?.let { bytes ->
                            viewModel.uploadSelfie("image/jpeg", bytes)
                        }
                    },
                    isLoading = uiState is KycUiState.Loading,
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
