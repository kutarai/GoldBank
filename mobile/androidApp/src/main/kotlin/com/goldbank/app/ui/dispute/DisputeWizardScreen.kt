package com.goldbank.app.ui.dispute

import android.Manifest
import android.content.Intent
import android.graphics.BitmapFactory
import android.net.Uri
import android.provider.Settings
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.Image
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.asImageBitmap
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import com.google.accompanist.permissions.ExperimentalPermissionsApi
import com.google.accompanist.permissions.PermissionStatus
import com.google.accompanist.permissions.isGranted
import com.google.accompanist.permissions.rememberPermissionState
import com.kashif.cameraK.controller.CameraController
import com.kashif.cameraK.enums.CameraLens
import com.kashif.cameraK.enums.Directory
import com.kashif.cameraK.enums.FlashMode
import com.kashif.cameraK.enums.ImageFormat
import com.kashif.cameraK.result.ImageCaptureResult
import com.kashif.cameraK.ui.CameraPreview
import com.goldbank.app.viewmodel.DisputeViewModel
import kotlinx.coroutines.launch

@OptIn(ExperimentalMaterial3Api::class, ExperimentalPermissionsApi::class)
@Composable
fun DisputeWizardScreen(
    viewModel: DisputeViewModel,
    transactionId: String,
    onComplete: () -> Unit,
    onBack: () -> Unit,
) {
    val uiState by viewModel.uiState.collectAsState()
    val snackbarHostState = remember { SnackbarHostState() }
    val scope = rememberCoroutineScope()
    val context = LocalContext.current

    val step = uiState.wizardStep

    // Camera state for step 2
    var showCamera by remember { mutableStateOf(false) }
    var cameraController by remember { mutableStateOf<CameraController?>(null) }
    var capturedBytes by remember { mutableStateOf<ByteArray?>(null) }
    var showRationaleDialog by remember { mutableStateOf(false) }

    val cameraPermission = rememberPermissionState(Manifest.permission.CAMERA)

    // Gallery launcher
    val galleryLauncher = rememberLauncherForActivityResult(
        contract = ActivityResultContracts.GetContent(),
    ) { uri: Uri? ->
        uri?.let {
            val bytes = context.contentResolver.openInputStream(it)?.use { stream ->
                stream.readBytes()
            }
            if (bytes != null) {
                capturedBytes = bytes
            }
        }
    }

    // Step 3: trigger AI triage when wizard reaches step 3
    LaunchedEffect(step) {
        if (step == 3) {
            viewModel.submitDispute(transactionId)
        }
    }

    if (showRationaleDialog) {
        AlertDialog(
            onDismissRequest = { showRationaleDialog = false },
            title = { Text("Camera Permission Required") },
            text = { Text("Camera access is needed to capture evidence for your dispute.") },
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

    // Full-screen camera overlay for step 2
    if (showCamera) {
        if (!cameraPermission.status.isGranted) {
            val isPermanentlyDenied = cameraPermission.status is PermissionStatus.Denied &&
                !(cameraPermission.status as PermissionStatus.Denied).shouldShowRationale

            Box(
                modifier = Modifier.fillMaxSize(),
                contentAlignment = Alignment.Center,
            ) {
                Column(
                    horizontalAlignment = Alignment.CenterHorizontally,
                    modifier = Modifier.padding(24.dp),
                ) {
                    Text(
                        "Camera permission is required to capture evidence",
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
                    Spacer(modifier = Modifier.height(12.dp))
                    OutlinedButton(onClick = { showCamera = false }) {
                        Text("Cancel")
                    }
                }
            }
        } else {
            Box(modifier = Modifier.fillMaxSize()) {
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
                    "Capture evidence for your dispute",
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.onSurface,
                    modifier = Modifier
                        .align(Alignment.TopCenter)
                        .padding(top = 16.dp),
                )

                Row(
                    modifier = Modifier
                        .align(Alignment.BottomCenter)
                        .padding(bottom = 32.dp),
                    horizontalArrangement = Arrangement.spacedBy(16.dp),
                    verticalAlignment = Alignment.CenterVertically,
                ) {
                    OutlinedButton(onClick = { showCamera = false }) {
                        Text("Cancel")
                    }
                    Button(
                        onClick = {
                            scope.launch {
                                cameraController?.let { controller ->
                                    when (val result = controller.takePictureToFile()) {
                                        is ImageCaptureResult.SuccessWithFile -> {
                                            capturedBytes = java.io.File(result.filePath).readBytes()
                                            showCamera = false
                                        }
                                        is ImageCaptureResult.Success -> {
                                            capturedBytes = result.byteArray
                                            showCamera = false
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
                        modifier = Modifier.height(72.dp).widthIn(min = 220.dp),
                        shape = MaterialTheme.shapes.extraLarge,
                    ) {
                        Text("Capture", style = MaterialTheme.typography.labelLarge)
                    }
                }
            }
        }
        return
    }

    Scaffold(
        topBar = {
            TopAppBar(
                title = {
                    Column {
                        Text("Dispute Transaction")
                        Text(
                            text = "Step ${step + 1} of 4",
                            style = MaterialTheme.typography.labelSmall,
                            color = MaterialTheme.colorScheme.onSurfaceVariant,
                        )
                    }
                },
                navigationIcon = {
                    IconButton(onClick = {
                        if (step == 0) {
                            onBack()
                        } else {
                            viewModel.prevWizardStep()
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
                .padding(padding),
        ) {
            LinearProgressIndicator(
                progress = { (step + 1) / 4f },
                modifier = Modifier.fillMaxWidth(),
            )

            when (step) {
                0 -> Step0TransactionContext(
                    transactionId = transactionId,
                    onNext = { viewModel.nextWizardStep() },
                )

                1 -> Step1DescribeIssue(
                    initialDescription = uiState.wizardDescription,
                    onNext = { description ->
                        viewModel.setWizardDescription(description)
                        viewModel.nextWizardStep()
                    },
                    onBack = { viewModel.prevWizardStep() },
                )

                2 -> Step2AttachEvidence(
                    capturedBytes = capturedBytes,
                    onTakePhoto = {
                        if (!cameraPermission.status.isGranted) {
                            if (cameraPermission.status is PermissionStatus.Denied &&
                                (cameraPermission.status as PermissionStatus.Denied).shouldShowRationale
                            ) {
                                showRationaleDialog = true
                            } else {
                                cameraPermission.launchPermissionRequest()
                            }
                        } else {
                            showCamera = true
                        }
                    },
                    onChooseFromGallery = { galleryLauncher.launch("image/*") },
                    onRemoveImage = { capturedBytes = null },
                    onNext = {
                        capturedBytes?.let { bytes -> viewModel.setWizardEvidence(bytes) }
                        viewModel.nextWizardStep()
                    },
                    onSkip = { viewModel.nextWizardStep() },
                    onBack = { viewModel.prevWizardStep() },
                )

                else -> Step3AiTriageResult(
                    isSubmitting = uiState.isSubmitting,
                    triageResult = uiState.triageResult,
                    error = uiState.error,
                    onDone = onComplete,
                )
            }
        }
    }
}

@Composable
private fun Step0TransactionContext(
    transactionId: String,
    onNext: () -> Unit,
) {
    Column(
        modifier = Modifier
            .fillMaxSize()
            .verticalScroll(rememberScrollState())
            .padding(horizontal = 16.dp, vertical = 24.dp),
        verticalArrangement = Arrangement.spacedBy(16.dp),
    ) {
        Text(
            text = "Transaction Details",
            style = MaterialTheme.typography.titleMedium,
        )

        Card(modifier = Modifier.fillMaxWidth()) {
            Column(
                modifier = Modifier.padding(16.dp),
                verticalArrangement = Arrangement.spacedBy(8.dp),
            ) {
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.SpaceBetween,
                ) {
                    Text(
                        text = "Transaction ID",
                        style = MaterialTheme.typography.labelMedium,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                    )
                    Text(
                        text = transactionId,
                        style = MaterialTheme.typography.bodyMedium,
                        fontWeight = FontWeight.Medium,
                    )
                }
                HorizontalDivider()
                Text(
                    text = "You are disputing transaction $transactionId",
                    style = MaterialTheme.typography.bodyMedium,
                )
            }
        }

        Spacer(modifier = Modifier.weight(1f))

        Button(
            onClick = onNext,
            modifier = Modifier.fillMaxWidth(),
        ) {
            Text("Next")
        }
    }
}

@Composable
private fun Step1DescribeIssue(
    initialDescription: String,
    onNext: (String) -> Unit,
    onBack: () -> Unit,
) {
    var description by remember { mutableStateOf(initialDescription) }
    val maxChars = 2000

    Column(
        modifier = Modifier
            .fillMaxSize()
            .verticalScroll(rememberScrollState())
            .padding(horizontal = 16.dp, vertical = 24.dp),
        verticalArrangement = Arrangement.spacedBy(16.dp),
    ) {
        Text(
            text = "Describe the Issue",
            style = MaterialTheme.typography.titleMedium,
        )

        OutlinedTextField(
            value = description,
            onValueChange = { if (it.length <= maxChars) description = it },
            label = { Text("Description") },
            placeholder = { Text("Please describe what happened with this transaction...") },
            modifier = Modifier.fillMaxWidth(),
            minLines = 5,
            maxLines = 8,
            supportingText = {
                Text(
                    text = "${description.length}/$maxChars",
                    modifier = Modifier.fillMaxWidth(),
                    style = MaterialTheme.typography.labelSmall,
                    color = if (description.length >= maxChars) {
                        MaterialTheme.colorScheme.error
                    } else {
                        MaterialTheme.colorScheme.onSurfaceVariant
                    },
                )
            },
        )

        Spacer(modifier = Modifier.weight(1f))

        Row(
            modifier = Modifier.fillMaxWidth(),
            horizontalArrangement = Arrangement.spacedBy(12.dp),
        ) {
            OutlinedButton(
                onClick = onBack,
                modifier = Modifier.weight(1f),
            ) {
                Text("Back")
            }
            Button(
                onClick = { onNext(description) },
                modifier = Modifier.weight(1f),
                enabled = description.isNotBlank(),
            ) {
                Text("Next")
            }
        }
    }
}

@Composable
private fun Step2AttachEvidence(
    capturedBytes: ByteArray?,
    onTakePhoto: () -> Unit,
    onChooseFromGallery: () -> Unit,
    onRemoveImage: () -> Unit,
    onNext: () -> Unit,
    onSkip: () -> Unit,
    onBack: () -> Unit,
) {
    Column(
        modifier = Modifier
            .fillMaxSize()
            .verticalScroll(rememberScrollState())
            .padding(horizontal = 16.dp, vertical = 24.dp),
        verticalArrangement = Arrangement.spacedBy(16.dp),
    ) {
        Text(
            text = "Attach Evidence (Optional)",
            style = MaterialTheme.typography.titleMedium,
        )

        Text(
            text = "You may attach a photo as supporting evidence for your dispute.",
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )

        if (capturedBytes != null) {
            val bitmap = BitmapFactory.decodeByteArray(capturedBytes, 0, capturedBytes.size)
            if (bitmap != null) {
                Card(modifier = Modifier.fillMaxWidth()) {
                    Column(
                        modifier = Modifier.padding(12.dp),
                        horizontalAlignment = Alignment.CenterHorizontally,
                        verticalArrangement = Arrangement.spacedBy(8.dp),
                    ) {
                        Image(
                            bitmap = bitmap.asImageBitmap(),
                            contentDescription = "Evidence preview",
                            modifier = Modifier
                                .fillMaxWidth()
                                .height(200.dp),
                            contentScale = ContentScale.Crop,
                        )
                        TextButton(
                            onClick = onRemoveImage,
                            colors = ButtonDefaults.textButtonColors(
                                contentColor = MaterialTheme.colorScheme.error,
                            ),
                        ) {
                            Text("Remove")
                        }
                    }
                }
            }
        } else {
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.spacedBy(12.dp),
            ) {
                OutlinedButton(
                    onClick = onTakePhoto,
                    modifier = Modifier.weight(1f),
                ) {
                    Text("Take Photo")
                }
                OutlinedButton(
                    onClick = onChooseFromGallery,
                    modifier = Modifier.weight(1f),
                ) {
                    Text("Choose from Gallery")
                }
            }
        }

        Spacer(modifier = Modifier.weight(1f))

        Row(
            modifier = Modifier.fillMaxWidth(),
            horizontalArrangement = Arrangement.spacedBy(12.dp),
            verticalAlignment = Alignment.CenterVertically,
        ) {
            OutlinedButton(
                onClick = onBack,
                modifier = Modifier.weight(1f),
            ) {
                Text("Back")
            }
            if (capturedBytes == null) {
                TextButton(
                    onClick = onSkip,
                    modifier = Modifier.weight(1f),
                ) {
                    Text("Skip")
                }
            } else {
                Button(
                    onClick = onNext,
                    modifier = Modifier.weight(1f),
                ) {
                    Text("Next")
                }
            }
        }
    }
}

@Composable
private fun Step3AiTriageResult(
    isSubmitting: Boolean,
    triageResult: com.goldbank.shared.domain.model.DisputeTriage?,
    error: String?,
    onDone: () -> Unit,
) {
    Column(
        modifier = Modifier
            .fillMaxSize()
            .verticalScroll(rememberScrollState())
            .padding(horizontal = 16.dp, vertical = 24.dp),
        verticalArrangement = Arrangement.spacedBy(16.dp),
        horizontalAlignment = Alignment.CenterHorizontally,
    ) {
        when {
            isSubmitting -> {
                Spacer(modifier = Modifier.weight(1f))
                CircularProgressIndicator()
                Spacer(modifier = Modifier.height(16.dp))
                Text(
                    text = "Classifying dispute...",
                    style = MaterialTheme.typography.bodyMedium,
                )
                Spacer(modifier = Modifier.weight(1f))
            }

            triageResult != null -> {
                Text(
                    text = "Dispute Filed",
                    style = MaterialTheme.typography.titleMedium,
                )

                Card(modifier = Modifier.fillMaxWidth()) {
                    Column(
                        modifier = Modifier.padding(16.dp),
                        verticalArrangement = Arrangement.spacedBy(12.dp),
                    ) {
                        // Reference number
                        Text(
                            text = triageResult.reference,
                            style = MaterialTheme.typography.titleSmall,
                            fontWeight = FontWeight.Bold,
                        )

                        HorizontalDivider()

                        // Classification badge
                        Row(
                            modifier = Modifier.fillMaxWidth(),
                            horizontalArrangement = Arrangement.SpaceBetween,
                            verticalAlignment = Alignment.CenterVertically,
                        ) {
                            Text(
                                text = "Classification",
                                style = MaterialTheme.typography.labelMedium,
                                color = MaterialTheme.colorScheme.onSurfaceVariant,
                            )
                            SuggestionChip(
                                onClick = {},
                                label = { Text(triageResult.classification) },
                            )
                        }

                        // Priority chip
                        Row(
                            modifier = Modifier.fillMaxWidth(),
                            horizontalArrangement = Arrangement.SpaceBetween,
                            verticalAlignment = Alignment.CenterVertically,
                        ) {
                            Text(
                                text = "Priority",
                                style = MaterialTheme.typography.labelMedium,
                                color = MaterialTheme.colorScheme.onSurfaceVariant,
                            )
                            val priorityColors = when (triageResult.priority.lowercase()) {
                                "high" -> AssistChipDefaults.elevatedAssistChipColors(
                                    containerColor = MaterialTheme.colorScheme.errorContainer,
                                    labelColor = MaterialTheme.colorScheme.onErrorContainer,
                                )
                                "medium" -> AssistChipDefaults.elevatedAssistChipColors(
                                    containerColor = MaterialTheme.colorScheme.tertiaryContainer,
                                    labelColor = MaterialTheme.colorScheme.onTertiaryContainer,
                                )
                                else -> AssistChipDefaults.elevatedAssistChipColors(
                                    containerColor = MaterialTheme.colorScheme.primaryContainer,
                                    labelColor = MaterialTheme.colorScheme.onPrimaryContainer,
                                )
                            }
                            ElevatedAssistChip(
                                onClick = {},
                                label = { Text(triageResult.priority.replaceFirstChar { it.uppercase() }) },
                                colors = priorityColors,
                            )
                        }

                        // Assigned team
                        Row(
                            modifier = Modifier.fillMaxWidth(),
                            horizontalArrangement = Arrangement.SpaceBetween,
                        ) {
                            Text(
                                text = "Assigned Team",
                                style = MaterialTheme.typography.labelMedium,
                                color = MaterialTheme.colorScheme.onSurfaceVariant,
                            )
                            Text(
                                text = triageResult.assignedTeam,
                                style = MaterialTheme.typography.bodyMedium,
                            )
                        }

                        HorizontalDivider()

                        // AI summary
                        Text(
                            text = "AI Summary",
                            style = MaterialTheme.typography.labelMedium,
                            color = MaterialTheme.colorScheme.onSurfaceVariant,
                        )
                        Text(
                            text = triageResult.summary,
                            style = MaterialTheme.typography.bodyMedium,
                        )

                        HorizontalDivider()

                        // Expected resolution
                        Row(
                            modifier = Modifier.fillMaxWidth(),
                            horizontalArrangement = Arrangement.SpaceBetween,
                        ) {
                            Text(
                                text = "Expected Resolution",
                                style = MaterialTheme.typography.labelMedium,
                                color = MaterialTheme.colorScheme.onSurfaceVariant,
                            )
                            Text(
                                text = "${triageResult.expectedResolutionDays} day(s)",
                                style = MaterialTheme.typography.bodyMedium,
                            )
                        }
                    }
                }

                Spacer(modifier = Modifier.weight(1f))

                Button(
                    onClick = onDone,
                    modifier = Modifier.fillMaxWidth(),
                ) {
                    Text("Done")
                }
            }

            else -> {
                // Error fallback: dispute filed but classification pending
                Spacer(modifier = Modifier.weight(1f))

                Card(
                    modifier = Modifier.fillMaxWidth(),
                    colors = CardDefaults.cardColors(
                        containerColor = MaterialTheme.colorScheme.surfaceVariant,
                    ),
                ) {
                    Column(
                        modifier = Modifier.padding(16.dp),
                        horizontalAlignment = Alignment.CenterHorizontally,
                        verticalArrangement = Arrangement.spacedBy(8.dp),
                    ) {
                        Text(
                            text = "Dispute filed successfully. Classification pending.",
                            style = MaterialTheme.typography.bodyMedium,
                        )
                    }
                }

                Spacer(modifier = Modifier.weight(1f))

                Button(
                    onClick = onDone,
                    modifier = Modifier.fillMaxWidth(),
                ) {
                    Text("Done")
                }
            }
        }
    }
}
