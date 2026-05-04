package com.goldbank.app.ui.merchant

import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import com.goldbank.app.viewmodel.MerchantUiState
import com.goldbank.app.viewmodel.MerchantViewModel
import com.goldbank.shared.domain.model.MerchantCommissionLineItem

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun MerchantCommissionScreen(
    viewModel: MerchantViewModel,
    onBack: () -> Unit,
) {
    val uiState by viewModel.uiState.collectAsState()

    LaunchedEffect(Unit) {
        viewModel.loadCommissionReport()
    }

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text("Commission Report") },
                navigationIcon = {
                    IconButton(onClick = onBack) {
                        Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = "Back")
                    }
                },
            )
        },
    ) { padding ->
        when (val state = uiState) {
            is MerchantUiState.Loading -> {
                Box(Modifier.fillMaxSize().padding(padding), contentAlignment = Alignment.Center) {
                    CircularProgressIndicator()
                }
            }
            is MerchantUiState.CommissionLoaded -> {
                LazyColumn(
                    modifier = Modifier.fillMaxSize().padding(padding),
                    contentPadding = PaddingValues(16.dp),
                    verticalArrangement = Arrangement.spacedBy(12.dp),
                ) {
                    // Summary card
                    item {
                        Card(
                            modifier = Modifier.fillMaxWidth(),
                            colors = CardDefaults.cardColors(
                                containerColor = MaterialTheme.colorScheme.primaryContainer,
                            ),
                        ) {
                            Column(Modifier.padding(16.dp)) {
                                Text(
                                    "Total Commission",
                                    style = MaterialTheme.typography.labelMedium,
                                    color = MaterialTheme.colorScheme.onPrimaryContainer,
                                )
                                Text(
                                    "${state.report.totalCommission.currency} ${state.report.totalCommission.amount}",
                                    style = MaterialTheme.typography.headlineMedium,
                                    fontWeight = FontWeight.Bold,
                                    color = MaterialTheme.colorScheme.onPrimaryContainer,
                                )
                                Spacer(Modifier.height(8.dp))
                                Text(
                                    "${state.report.totalTransactions} total transactions",
                                    style = MaterialTheme.typography.bodySmall,
                                    color = MaterialTheme.colorScheme.onPrimaryContainer,
                                )
                            }
                        }
                    }

                    // Line items header
                    item {
                        Text(
                            "Breakdown by Type",
                            style = MaterialTheme.typography.titleMedium,
                            modifier = Modifier.padding(top = 8.dp),
                        )
                    }

                    // Commission line items
                    items(state.report.lineItems) { item ->
                        CommissionLineItemCard(item)
                    }
                }
            }
            is MerchantUiState.Error -> {
                Column(
                    Modifier.fillMaxSize().padding(padding),
                    horizontalAlignment = Alignment.CenterHorizontally,
                    verticalArrangement = Arrangement.Center,
                ) {
                    Text(state.message, color = MaterialTheme.colorScheme.error)
                    Spacer(Modifier.height(8.dp))
                    Button(onClick = { viewModel.loadCommissionReport() }) { Text("Retry") }
                }
            }
            else -> {}
        }
    }
}

@Composable
private fun CommissionLineItemCard(item: MerchantCommissionLineItem) {
    Card(modifier = Modifier.fillMaxWidth()) {
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .padding(16.dp),
        ) {
            Text(
                item.transactionType,
                style = MaterialTheme.typography.titleSmall,
            )
            Spacer(Modifier.height(8.dp))
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
            ) {
                InfoColumn(
                    label = "Transactions",
                    value = "${item.transactionCount}",
                )
                InfoColumn(
                    label = "Volume",
                    value = "${item.totalTransactionAmount.currency} ${item.totalTransactionAmount.amount}",
                )
                InfoColumn(
                    label = "Rate",
                    value = item.commissionRate,
                )
            }
            Spacer(Modifier.height(8.dp))
            HorizontalDivider()
            Spacer(Modifier.height(8.dp))
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically,
            ) {
                Text("Commission", style = MaterialTheme.typography.bodyMedium)
                Text(
                    "${item.commissionAmount.currency} ${item.commissionAmount.amount}",
                    style = MaterialTheme.typography.titleMedium,
                    color = MaterialTheme.colorScheme.primary,
                    fontWeight = FontWeight.Bold,
                )
            }
        }
    }
}

@Composable
private fun InfoColumn(label: String, value: String) {
    Column {
        Text(
            label,
            style = MaterialTheme.typography.labelSmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
        Text(value, style = MaterialTheme.typography.bodyMedium)
    }
}
