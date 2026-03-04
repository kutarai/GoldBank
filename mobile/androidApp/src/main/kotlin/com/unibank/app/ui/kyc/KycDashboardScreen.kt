package com.unibank.app.ui.kyc

import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.CameraAlt
import androidx.compose.material.icons.filled.CheckCircle
import androidx.compose.material.icons.filled.Description
import androidx.compose.material.icons.filled.HourglassBottom
import androidx.compose.material.icons.filled.Warning
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.unit.dp
import com.unibank.app.viewmodel.KycUiState
import com.unibank.app.viewmodel.KycViewModel
import com.unibank.shared.domain.model.DocumentSummary

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun KycDashboardScreen(
    viewModel: KycViewModel,
    onUploadDocument: (documentType: String) -> Unit,
    onTakeSelfie: () -> Unit,
    onBack: () -> Unit,
) {
    val uiState by viewModel.uiState.collectAsState()
    val kycStatus by viewModel.kycStatus.collectAsState()

    LaunchedEffect(Unit) { viewModel.loadKycStatus() }

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text("KYC Verification") },
                navigationIcon = {
                    IconButton(onClick = onBack) {
                        Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = "Back")
                    }
                },
            )
        },
    ) { padding ->
        when {
            uiState is KycUiState.Loading && kycStatus == null -> {
                Box(
                    modifier = Modifier.fillMaxSize().padding(padding),
                    contentAlignment = Alignment.Center,
                ) { CircularProgressIndicator() }
            }
            else -> {
                LazyColumn(
                    modifier = Modifier.fillMaxSize().padding(padding).padding(16.dp),
                    verticalArrangement = Arrangement.spacedBy(12.dp),
                ) {
                    // Status card
                    item {
                        KycStatusCard(
                            level = kycStatus?.kycLevel ?: 0,
                            overallStatus = kycStatus?.overallStatus ?: "pending",
                        )
                    }

                    // Document upload options
                    item {
                        Text(
                            "Required Documents",
                            style = MaterialTheme.typography.titleMedium,
                            modifier = Modifier.padding(top = 8.dp),
                        )
                    }

                    val docTypes = listOf(
                        "national_id" to "National ID",
                        "passport" to "Passport",
                        "drivers_license" to "Driver's License",
                    )

                    items(docTypes) { (type, label) ->
                        val existing = kycStatus?.documents?.find { it.documentType == type }
                        DocumentUploadCard(
                            label = label,
                            status = existing?.status,
                            onClick = { onUploadDocument(type) },
                        )
                    }

                    // Selfie section
                    item {
                        Text(
                            "Selfie Verification",
                            style = MaterialTheme.typography.titleMedium,
                            modifier = Modifier.padding(top = 8.dp),
                        )
                    }

                    item {
                        val selfieDoc = kycStatus?.documents?.find { it.documentType == "selfie" }
                        Card(
                            modifier = Modifier.fillMaxWidth().clickable { onTakeSelfie() },
                        ) {
                            ListItem(
                                headlineContent = { Text("Take Selfie") },
                                supportingContent = {
                                    Text(selfieDoc?.status?.replaceFirstChar { it.uppercase() } ?: "Not uploaded")
                                },
                                leadingContent = {
                                    Icon(Icons.Default.CameraAlt, contentDescription = null)
                                },
                                trailingContent = {
                                    StatusIcon(selfieDoc?.status)
                                },
                            )
                        }
                    }
                }
            }
        }
    }
}

@Composable
private fun KycStatusCard(level: Int, overallStatus: String) {
    val statusColor = when (overallStatus) {
        "approved" -> Color(0xFF4CAF50)
        "rejected" -> MaterialTheme.colorScheme.error
        else -> Color(0xFFFFA000)
    }
    Card(
        modifier = Modifier.fillMaxWidth(),
        colors = CardDefaults.cardColors(containerColor = statusColor.copy(alpha = 0.1f)),
    ) {
        Column(modifier = Modifier.padding(16.dp)) {
            Text(
                "KYC Level $level",
                style = MaterialTheme.typography.headlineSmall,
            )
            Spacer(modifier = Modifier.height(4.dp))
            Text(
                "Status: ${overallStatus.replaceFirstChar { it.uppercase() }}",
                style = MaterialTheme.typography.bodyMedium,
                color = statusColor,
            )
        }
    }
}

@Composable
private fun DocumentUploadCard(label: String, status: String?, onClick: () -> Unit) {
    Card(
        modifier = Modifier.fillMaxWidth().clickable(onClick = onClick),
    ) {
        ListItem(
            headlineContent = { Text(label) },
            supportingContent = {
                Text(status?.replaceFirstChar { it.uppercase() } ?: "Not uploaded")
            },
            leadingContent = {
                Icon(Icons.Default.Description, contentDescription = null)
            },
            trailingContent = { StatusIcon(status) },
        )
    }
}

@Composable
private fun StatusIcon(status: String?) {
    when (status) {
        "approved" -> Icon(Icons.Default.CheckCircle, contentDescription = "Approved", tint = Color(0xFF4CAF50))
        "pending", "pending_review" -> Icon(Icons.Default.HourglassBottom, contentDescription = "Pending", tint = Color(0xFFFFA000))
        "rejected" -> Icon(Icons.Default.Warning, contentDescription = "Rejected", tint = MaterialTheme.colorScheme.error)
        else -> {}
    }
}
