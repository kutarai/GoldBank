package com.unibank.app.ui.kyc

import androidx.compose.foundation.layout.*
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.Cancel
import androidx.compose.material.icons.filled.CheckCircle
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.unit.dp
import com.unibank.app.viewmodel.KycViewModel
import com.unibank.shared.domain.model.KycVerificationResult

private val GreenMatch = Color(0xFF2E7D32)
private val AmberMatch = Color(0xFFF57F17)
private val RedMatch = Color(0xFFC62828)

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun KycVerificationResultScreen(
    viewModel: KycViewModel,
    accountId: String,
    onHome: () -> Unit,
    onBack: () -> Unit,
) {
    val state by viewModel.verificationUiState.collectAsState()

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text("Verification Result") },
                navigationIcon = {
                    IconButton(onClick = onBack) {
                        Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = "Back")
                    }
                },
            )
        },
    ) { padding ->
        Box(
            modifier = Modifier
                .fillMaxSize()
                .padding(padding),
        ) {
            when {
                state.isVerifying -> {
                    Column(
                        modifier = Modifier.align(Alignment.Center),
                        horizontalAlignment = Alignment.CenterHorizontally,
                        verticalArrangement = Arrangement.spacedBy(16.dp),
                    ) {
                        CircularProgressIndicator()
                        Text(
                            text = "Verifying identity...",
                            style = MaterialTheme.typography.bodyMedium,
                            color = MaterialTheme.colorScheme.onSurfaceVariant,
                        )
                    }
                }

                state.verificationError != null -> {
                    Card(
                        modifier = Modifier
                            .fillMaxWidth()
                            .padding(16.dp)
                            .align(Alignment.Center),
                        colors = CardDefaults.cardColors(
                            containerColor = MaterialTheme.colorScheme.errorContainer,
                        ),
                    ) {
                        Column(
                            modifier = Modifier.padding(16.dp),
                            verticalArrangement = Arrangement.spacedBy(12.dp),
                        ) {
                            Text(
                                text = state.verificationError!!,
                                style = MaterialTheme.typography.bodyMedium,
                                color = MaterialTheme.colorScheme.onErrorContainer,
                            )
                            Button(
                                onClick = onHome,
                                modifier = Modifier.fillMaxWidth(),
                            ) {
                                Text("Return Home")
                            }
                        }
                    }
                }

                state.verificationResult != null -> {
                    VerificationResultContent(
                        result = state.verificationResult!!,
                        onHome = onHome,
                    )
                }

                else -> {
                    // No result yet and not verifying — placeholder while waiting
                    Column(
                        modifier = Modifier.align(Alignment.Center),
                        horizontalAlignment = Alignment.CenterHorizontally,
                    ) {
                        Text(
                            text = "No verification result available.",
                            style = MaterialTheme.typography.bodyMedium,
                            color = MaterialTheme.colorScheme.onSurfaceVariant,
                        )
                    }
                }
            }
        }
    }
}

@Composable
private fun VerificationResultContent(
    result: KycVerificationResult,
    onHome: () -> Unit,
) {
    Column(
        modifier = Modifier
            .fillMaxSize()
            .verticalScroll(rememberScrollState())
            .padding(16.dp),
        verticalArrangement = Arrangement.spacedBy(16.dp),
    ) {
        // ── Face Match Score ──────────────────────────────────────────────────
        Card(modifier = Modifier.fillMaxWidth()) {
            Column(modifier = Modifier.padding(16.dp)) {
                val scorePercent = (result.faceMatchScore * 100).toInt()
                val progressColor = when {
                    result.faceMatchScore > 0.8 -> GreenMatch
                    result.faceMatchScore >= 0.5 -> AmberMatch
                    else -> RedMatch
                }

                Text(
                    text = "Face Match: $scorePercent%",
                    style = MaterialTheme.typography.titleSmall,
                )
                Spacer(modifier = Modifier.height(8.dp))
                LinearProgressIndicator(
                    progress = { result.faceMatchScore.toFloat().coerceIn(0f, 1f) },
                    modifier = Modifier.fillMaxWidth(),
                    color = progressColor,
                    trackColor = progressColor.copy(alpha = 0.2f),
                )
            }
        }

        // ── Field Comparison Table ────────────────────────────────────────────
        Card(modifier = Modifier.fillMaxWidth()) {
            Column(modifier = Modifier.padding(vertical = 8.dp)) {
                Text(
                    text = "Document Fields",
                    style = MaterialTheme.typography.titleSmall,
                    modifier = Modifier.padding(horizontal = 16.dp, vertical = 4.dp),
                )

                FieldComparisonRow(
                    fieldName = "Name",
                    extractedValue = result.extractedName ?: "—",
                    isMatch = result.nameMatch,
                )
                HorizontalDivider(modifier = Modifier.padding(horizontal = 16.dp))
                FieldComparisonRow(
                    fieldName = "ID Number",
                    extractedValue = result.extractedIdNumber ?: "—",
                    isMatch = result.idNumberMatch,
                )
                HorizontalDivider(modifier = Modifier.padding(horizontal = 16.dp))
                FieldComparisonRow(
                    fieldName = "Date of Birth",
                    extractedValue = result.extractedDob ?: "—",
                    isMatch = result.dobMatch,
                )
            }
        }

        // ── Decision Card ─────────────────────────────────────────────────────
        val decisionLower = result.decision.lowercase()
        when {
            decisionLower.contains("approved") || decisionLower.contains("auto") -> {
                Card(
                    modifier = Modifier.fillMaxWidth(),
                    colors = CardDefaults.cardColors(
                        containerColor = GreenMatch.copy(alpha = 0.12f),
                    ),
                ) {
                    Column(
                        modifier = Modifier.padding(16.dp),
                        verticalArrangement = Arrangement.spacedBy(12.dp),
                    ) {
                        Row(
                            verticalAlignment = Alignment.CenterVertically,
                            horizontalArrangement = Arrangement.spacedBy(8.dp),
                        ) {
                            Icon(
                                imageVector = Icons.Default.CheckCircle,
                                contentDescription = null,
                                tint = GreenMatch,
                            )
                            Text(
                                text = "Auto-approved",
                                style = MaterialTheme.typography.titleMedium,
                                color = GreenMatch,
                            )
                        }
                        Button(
                            onClick = onHome,
                            modifier = Modifier.fillMaxWidth(),
                        ) {
                            Text("Continue")
                        }
                    }
                }
            }

            decisionLower.contains("review") || decisionLower.contains("manual") -> {
                Card(
                    modifier = Modifier.fillMaxWidth(),
                    colors = CardDefaults.cardColors(
                        containerColor = AmberMatch.copy(alpha = 0.12f),
                    ),
                ) {
                    Column(
                        modifier = Modifier.padding(16.dp),
                        verticalArrangement = Arrangement.spacedBy(8.dp),
                    ) {
                        Row(
                            verticalAlignment = Alignment.CenterVertically,
                            horizontalArrangement = Arrangement.spacedBy(8.dp),
                        ) {
                            Icon(
                                imageVector = Icons.Default.CheckCircle,
                                contentDescription = null,
                                tint = AmberMatch,
                            )
                            Text(
                                text = "Under Manual Review",
                                style = MaterialTheme.typography.titleMedium,
                                color = AmberMatch,
                            )
                        }
                        Text(
                            text = "We'll notify you when verified",
                            style = MaterialTheme.typography.bodyMedium,
                            color = MaterialTheme.colorScheme.onSurfaceVariant,
                        )
                    }
                }
            }

            decisionLower.contains("reject") -> {
                Card(
                    modifier = Modifier.fillMaxWidth(),
                    colors = CardDefaults.cardColors(
                        containerColor = MaterialTheme.colorScheme.errorContainer,
                    ),
                ) {
                    Column(
                        modifier = Modifier.padding(16.dp),
                        verticalArrangement = Arrangement.spacedBy(8.dp),
                    ) {
                        Row(
                            verticalAlignment = Alignment.CenterVertically,
                            horizontalArrangement = Arrangement.spacedBy(8.dp),
                        ) {
                            Icon(
                                imageVector = Icons.Default.Cancel,
                                contentDescription = null,
                                tint = MaterialTheme.colorScheme.error,
                            )
                            Text(
                                text = "Rejected",
                                style = MaterialTheme.typography.titleMedium,
                                color = MaterialTheme.colorScheme.onErrorContainer,
                            )
                        }
                        val reason = result.rejectionReason
                        if (!reason.isNullOrBlank()) {
                            Text(
                                text = reason,
                                style = MaterialTheme.typography.bodyMedium,
                                color = MaterialTheme.colorScheme.onErrorContainer,
                            )
                        }
                    }
                }
            }

            else -> {
                // Fallback for unexpected decision values
                Card(modifier = Modifier.fillMaxWidth()) {
                    Text(
                        text = "Decision: ${result.decision}",
                        style = MaterialTheme.typography.bodyMedium,
                        modifier = Modifier.padding(16.dp),
                    )
                }
            }
        }
    }
}

@Composable
private fun FieldComparisonRow(
    fieldName: String,
    extractedValue: String,
    isMatch: Boolean,
) {
    ListItem(
        headlineContent = { Text(fieldName) },
        supportingContent = { Text(extractedValue) },
        leadingContent = {
            Icon(
                imageVector = if (isMatch) Icons.Default.CheckCircle else Icons.Default.Cancel,
                contentDescription = if (isMatch) "Match" else "Mismatch",
                tint = if (isMatch) GreenMatch else RedMatch,
            )
        },
    )
}
