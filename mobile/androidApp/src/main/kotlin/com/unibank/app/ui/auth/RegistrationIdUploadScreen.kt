package com.unibank.app.ui.auth

import android.Manifest
import android.net.Uri
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.Image
import androidx.compose.foundation.layout.*
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.CameraAlt
import androidx.compose.material.icons.filled.CloudUpload
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import coil3.compose.rememberAsyncImagePainter
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
fun RegistrationIdUploadScreen(
    viewModel: AuthViewModel,
    onSuccess: () -> Unit,
    onBack: () -> Unit,
) {
    val uiState by viewModel.uiState.collectAsState()
    val context = LocalContext.current
    val scope = rememberCoroutineScope()
    val snackbarHostState = remember { SnackbarHostState() }

    // 0=choose method, 1=camera, 2=preview+upload
    var step by rememberSaveable { mutableIntStateOf(0) }
    var capturedBytes by remember { mutableStateOf<ByteArray?>(null) }
    var selectedUri by rememberSaveable { mutableStateOf<String?>(null) }
    var errorMessage by remember { mutableStateOf<String?>(null) }
    var cameraController by remember { mutableStateOf<CameraController?>(null) }

    val cameraPermission = rememberPermissionState(Manifest.permission.CAMERA)

    val galleryLauncher = rememberLauncherForActivityResult(
        contract = ActivityResultContracts.GetContent(),
    ) { uri: Uri? ->
        uri?.let {
            selectedUri = it.toString()
            context.contentResolver.openInputStream(it)?.use { stream ->
                capturedBytes = stream.readBytes()
            }
            step = 2
        }
    }

    LaunchedEffect(uiState) {
        when (val state = uiState) {
            is AuthUiState.IdUploaded -> {
                onSuccess()
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
                title = { Text(when (step) {
                    1 -> "Take Photo of ID"
                    2 -> "Confirm ID Photo"
                    else -> "National ID"
                }) },
                navigationIcon = {
                    IconButton(onClick = {
                        when (step) {
                            0 -> onBack()
                            1 -> step = 0
                            2 -> { step = 0; capturedBytes = null; selectedUri = null }
                        }
                    }) {
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
            when (step) {
                // ── Step 0: Choose method ──
                0 -> {
                    Spacer(modifier = Modifier.height(8.dp))
                    Text(
                        text = "Step 2 of 2",
                        style = MaterialTheme.typography.labelMedium,
                        color = MaterialTheme.colorScheme.primary,
                    )
                    Spacer(modifier = Modifier.height(4.dp))
                    LinearProgressIndicator(
                        progress = { 0.75f },
                        modifier = Modifier.fillMaxWidth(),
                    )
                    Spacer(modifier = Modifier.height(24.dp))

                    Text(
                        "Upload a clear photo of your National ID",
                        style = MaterialTheme.typography.bodyMedium,
                        textAlign = TextAlign.Center,
                    )
                    Spacer(modifier = Modifier.height(32.dp))

                    Button(
                        onClick = {
                            if (cameraPermission.status.isGranted) {
                                step = 1
                            } else {
                                cameraPermission.launchPermissionRequest()
                            }
                        },
                        modifier = Modifier.fillMaxWidth().height(56.dp),
                    ) {
                        Icon(Icons.Default.CameraAlt, contentDescription = null)
                        Spacer(modifier = Modifier.width(12.dp))
                        Text("Take Photo")
                    }

                    Spacer(modifier = Modifier.height(16.dp))

                    OutlinedButton(
                        onClick = { galleryLauncher.launch("image/*") },
                        modifier = Modifier.fillMaxWidth().height(56.dp),
                    ) {
                        Icon(Icons.Default.CloudUpload, contentDescription = null)
                        Spacer(modifier = Modifier.width(12.dp))
                        Text("Upload from Gallery")
                    }
                }

                // ── Step 1: Camera capture ──
                1 -> {
                    Box(modifier = Modifier.fillMaxSize()) {
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
                            "Position your ID card within the frame",
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
                                                step = 2
                                            }
                                            is ImageCaptureResult.Success -> {
                                                capturedBytes = result.byteArray
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

                // ── Step 2: Preview and upload ──
                2 -> {
                    Text("ID Photo captured!", style = MaterialTheme.typography.titleMedium)
                    Spacer(modifier = Modifier.height(8.dp))
                    Text(
                        "Size: ${capturedBytes?.size?.let { it / 1024 } ?: 0} KB",
                        style = MaterialTheme.typography.bodySmall,
                    )
                    Spacer(modifier = Modifier.height(16.dp))

                    if (selectedUri != null) {
                        Card(modifier = Modifier.fillMaxWidth().height(240.dp)) {
                            Image(
                                painter = rememberAsyncImagePainter(Uri.parse(selectedUri)),
                                contentDescription = "ID document",
                                modifier = Modifier.fillMaxSize(),
                                contentScale = ContentScale.Fit,
                            )
                        }
                        Spacer(modifier = Modifier.height(16.dp))
                    }

                    LoadingButton(
                        text = "Upload ID",
                        onClick = {
                            capturedBytes?.let { bytes ->
                                viewModel.uploadRegistrationId("image/jpeg", bytes)
                            }
                        },
                        isLoading = uiState is AuthUiState.Loading,
                        enabled = capturedBytes != null,
                    )

                    Spacer(modifier = Modifier.height(12.dp))

                    OutlinedButton(
                        onClick = { step = 0; capturedBytes = null; selectedUri = null },
                        modifier = Modifier.fillMaxWidth(),
                    ) { Text("Retake") }
                }
            }
        }
    }
}
