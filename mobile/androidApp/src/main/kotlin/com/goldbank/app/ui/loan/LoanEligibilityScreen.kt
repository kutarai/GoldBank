package com.goldbank.app.ui.loan

import androidx.compose.foundation.layout.*
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.unit.dp
import com.goldbank.app.viewmodel.LoanViewModel

private val tenureEligibilityOptions = listOf(6, 12, 24, 36)

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun LoanEligibilityScreen(
    viewModel: LoanViewModel,
    onApplyNow: () -> Unit,
    onBack: () -> Unit,
) {
    val eligibility by viewModel.eligibility.collectAsState()
    val isChecking by viewModel.isCheckingEligibility.collectAsState()
    val eligibilityError by viewModel.eligibilityError.collectAsState()

    var amount by rememberSaveable { mutableStateOf("") }
    var tenureMonths by rememberSaveable { mutableIntStateOf(12) }
    var purpose by rememberSaveable { mutableStateOf("") }

    val scrollState = rememberScrollState()

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text("Loan Eligibility Check") },
                navigationIcon = {
                    IconButton(onClick = {
                        viewModel.resetEligibility()
                        onBack()
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
                .padding(padding)
                .padding(horizontal = 16.dp)
                .verticalScroll(scrollState),
            verticalArrangement = Arrangement.spacedBy(12.dp),
        ) {
            Spacer(modifier = Modifier.height(4.dp))

            // Amount field
            OutlinedTextField(
                value = amount,
                onValueChange = { amount = it },
                label = { Text("Desired Amount (ZWG)") },
                keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number),
                singleLine = true,
                modifier = Modifier.fillMaxWidth(),
            )

            // Tenure chip row
            Text(
                text = "Tenure",
                style = MaterialTheme.typography.labelMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
            Row(
                horizontalArrangement = Arrangement.spacedBy(8.dp),
                modifier = Modifier.fillMaxWidth(),
            ) {
                tenureEligibilityOptions.forEach { months ->
                    FilterChip(
                        selected = tenureMonths == months,
                        onClick = { tenureMonths = months },
                        label = { Text("${months}mo") },
                    )
                }
            }

            // Purpose field
            OutlinedTextField(
                value = purpose,
                onValueChange = { purpose = it },
                label = { Text("Purpose") },
                singleLine = true,
                modifier = Modifier.fillMaxWidth(),
            )

            // Check Eligibility button
            Button(
                onClick = {
                    viewModel.checkEligibility(amount, tenureMonths, purpose)
                },
                modifier = Modifier
                    .fillMaxWidth()
                    .height(52.dp),
                enabled = amount.isNotBlank() && purpose.isNotBlank() && !isChecking,
            ) {
                Text("Check Eligibility")
            }

            // Loading state
            if (isChecking) {
                Card(
                    modifier = Modifier.fillMaxWidth(),
                    colors = CardDefaults.cardColors(
                        containerColor = MaterialTheme.colorScheme.surfaceVariant,
                    ),
                ) {
                    Row(
                        modifier = Modifier
                            .fillMaxWidth()
                            .padding(16.dp),
                        verticalAlignment = Alignment.CenterVertically,
                        horizontalArrangement = Arrangement.spacedBy(12.dp),
                    ) {
                        CircularProgressIndicator(modifier = Modifier.size(24.dp))
                        Text(
                            text = "Assessing eligibility...",
                            style = MaterialTheme.typography.bodyMedium,
                        )
                    }
                }
            }

            // Error state
            if (eligibilityError != null && !isChecking) {
                Card(
                    modifier = Modifier.fillMaxWidth(),
                    colors = CardDefaults.cardColors(
                        containerColor = MaterialTheme.colorScheme.errorContainer,
                    ),
                ) {
                    Column(modifier = Modifier.padding(16.dp)) {
                        Text(
                            text = "Pre-check unavailable. You can still apply directly.",
                            style = MaterialTheme.typography.bodyMedium,
                            color = MaterialTheme.colorScheme.onErrorContainer,
                        )
                        Spacer(modifier = Modifier.height(12.dp))
                        OutlinedButton(
                            onClick = onApplyNow,
                            modifier = Modifier.fillMaxWidth(),
                        ) {
                            Text("Apply Directly")
                        }
                    }
                }
            }

            // Result state
            if (eligibility != null && !isChecking) {
                val result = eligibility!!

                // Likelihood gauge card
                val (gaugeColor, gaugeLabel) = when {
                    result.eligibility.equals("HIGH", ignoreCase = true) ->
                        Color(0xFF2E7D32) to "High likelihood of approval"
                    result.eligibility.equals("MEDIUM", ignoreCase = true) ->
                        Color(0xFFF57F17) to "Moderate likelihood of approval"
                    else ->
                        MaterialTheme.colorScheme.error to "Low likelihood of approval"
                }

                Card(
                    modifier = Modifier.fillMaxWidth(),
                    colors = CardDefaults.cardColors(containerColor = gaugeColor.copy(alpha = 0.12f)),
                ) {
                    Column(modifier = Modifier.padding(16.dp)) {
                        Text(
                            text = "Eligibility",
                            style = MaterialTheme.typography.labelMedium,
                            color = MaterialTheme.colorScheme.onSurfaceVariant,
                        )
                        Spacer(modifier = Modifier.height(4.dp))
                        Row(verticalAlignment = Alignment.CenterVertically) {
                            Surface(
                                modifier = Modifier.size(12.dp),
                                shape = MaterialTheme.shapes.small,
                                color = gaugeColor,
                            ) {}
                            Spacer(modifier = Modifier.width(8.dp))
                            Text(
                                text = gaugeLabel,
                                style = MaterialTheme.typography.bodyLarge,
                                color = gaugeColor,
                            )
                        }
                    }
                }

                // Rate range and max amount
                Card(modifier = Modifier.fillMaxWidth()) {
                    Column(modifier = Modifier.padding(16.dp)) {
                        LoanInfoRow(
                            label = "Estimated Rate Range",
                            value = "${"%.1f".format(result.estimatedRateMin)}% - ${"%.1f".format(result.estimatedRateMax)}%",
                        )
                        Spacer(modifier = Modifier.height(4.dp))
                        LoanInfoRow(
                            label = "Max Amount",
                            value = result.maxAmount,
                        )
                    }
                }

                // AI assessment text
                Card(
                    modifier = Modifier.fillMaxWidth(),
                    colors = CardDefaults.cardColors(
                        containerColor = MaterialTheme.colorScheme.secondaryContainer,
                    ),
                ) {
                    Column(modifier = Modifier.padding(16.dp)) {
                        Text(
                            text = "AI Assessment",
                            style = MaterialTheme.typography.titleSmall,
                            color = MaterialTheme.colorScheme.onSecondaryContainer,
                        )
                        Spacer(modifier = Modifier.height(8.dp))
                        Text(
                            text = result.assessmentText,
                            style = MaterialTheme.typography.bodyMedium,
                            color = MaterialTheme.colorScheme.onSecondaryContainer,
                        )
                    }
                }

                // Disclaimer
                Text(
                    text = "This is an estimate only. Actual terms may vary.",
                    style = MaterialTheme.typography.labelSmall,
                    color = MaterialTheme.colorScheme.outline,
                )

                // Apply Now button
                Button(
                    onClick = onApplyNow,
                    modifier = Modifier
                        .fillMaxWidth()
                        .height(52.dp),
                ) {
                    Text("Apply Now")
                }
            }

            Spacer(modifier = Modifier.height(16.dp))
        }
    }
}

@Composable
private fun LoanInfoRow(label: String, value: String) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .padding(vertical = 4.dp),
        horizontalArrangement = Arrangement.SpaceBetween,
    ) {
        Text(
            text = label,
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
        Text(
            text = value,
            style = MaterialTheme.typography.bodyMedium,
        )
    }
}
