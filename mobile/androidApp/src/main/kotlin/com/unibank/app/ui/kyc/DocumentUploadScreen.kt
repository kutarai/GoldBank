package com.unibank.app.ui.kyc

import android.net.Uri
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.Image
import androidx.compose.foundation.layout.*
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.CloudUpload
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.layout.ContentScale
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.unit.dp
import coil3.compose.rememberAsyncImagePainter
import com.unibank.app.ui.components.LoadingButton
import com.unibank.app.viewmodel.KycUiState
import com.unibank.app.viewmodel.KycViewModel

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun DocumentUploadScreen(
    viewModel: KycViewModel,
    documentType: String,
    onSuccess: () -> Unit,
    onBack: () -> Unit,
) {
    val uiState by viewModel.uiState.collectAsState()
    val snackbarHostState = remember { SnackbarHostState() }
    val context = LocalContext.current
    var selectedUri by rememberSaveable { mutableStateOf<String?>(null) }
    var selectedBytes by remember { mutableStateOf<ByteArray?>(null) }
    var fileName by rememberSaveable { mutableStateOf("") }
    var contentType by rememberSaveable { mutableStateOf("") }

    val documentLabel = when (documentType) {
        "national_id" -> "National ID"
        "passport" -> "Passport"
        "drivers_license" -> "Driver's License"
        else -> documentType
    }

    val launcher = rememberLauncherForActivityResult(
        contract = ActivityResultContracts.GetContent(),
    ) { uri: Uri? ->
        uri?.let {
            selectedUri = it.toString()
            context.contentResolver.openInputStream(it)?.use { stream ->
                selectedBytes = stream.readBytes()
            }
            val mimeType = context.contentResolver.getType(it) ?: "image/jpeg"
            contentType = mimeType
            val name = it.lastPathSegment ?: "document"
            fileName = name
        }
    }

    LaunchedEffect(uiState) {
        when (val state = uiState) {
            is KycUiState.DocumentUploaded -> {
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
                title = { Text("Upload $documentLabel") },
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
            Text(
                "Upload a clear photo or scan of your $documentLabel",
                style = MaterialTheme.typography.bodyMedium,
            )
            Spacer(modifier = Modifier.height(24.dp))

            if (selectedUri != null) {
                Card(modifier = Modifier.fillMaxWidth().height(240.dp)) {
                    Image(
                        painter = rememberAsyncImagePainter(Uri.parse(selectedUri)),
                        contentDescription = "Selected document",
                        modifier = Modifier.fillMaxSize(),
                        contentScale = ContentScale.Fit,
                    )
                }
                Spacer(modifier = Modifier.height(8.dp))
                Text(fileName, style = MaterialTheme.typography.bodySmall)
                Spacer(modifier = Modifier.height(16.dp))
            }

            OutlinedButton(
                onClick = { launcher.launch("image/*") },
                modifier = Modifier.fillMaxWidth(),
            ) {
                Icon(Icons.Default.CloudUpload, contentDescription = null)
                Spacer(modifier = Modifier.width(8.dp))
                Text(if (selectedUri == null) "Choose File" else "Choose Different File")
            }

            Spacer(modifier = Modifier.height(24.dp))

            LoadingButton(
                text = "Upload Document",
                onClick = {
                    selectedBytes?.let { bytes ->
                        viewModel.uploadDocument(documentType, fileName, contentType, bytes)
                    }
                },
                isLoading = uiState is KycUiState.Loading,
                enabled = selectedBytes != null,
            )
        }
    }
}
