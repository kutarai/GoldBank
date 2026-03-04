package com.unibank.app.ui.merchant

import androidx.compose.foundation.layout.*
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.Assessment
import androidx.compose.material.icons.filled.History
import androidx.compose.material.icons.filled.Payments
import androidx.compose.material.icons.filled.Store
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import com.unibank.app.viewmodel.MerchantUiState
import com.unibank.app.viewmodel.MerchantViewModel

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun MerchantDashboardScreen(
    viewModel: MerchantViewModel,
    merchantId: String,
    onTransactions: () -> Unit,
    onSettlements: () -> Unit,
    onCommission: () -> Unit,
    onBack: () -> Unit,
) {
    val uiState by viewModel.uiState.collectAsState()

    LaunchedEffect(merchantId) {
        viewModel.loadStatus(merchantId)
    }

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text("Merchant Dashboard") },
                navigationIcon = {
                    IconButton(onClick = onBack) {
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
                .padding(16.dp),
            verticalArrangement = Arrangement.spacedBy(16.dp),
        ) {
            when (val state = uiState) {
                is MerchantUiState.Loading -> {
                    Box(Modifier.fillMaxWidth(), contentAlignment = Alignment.Center) {
                        CircularProgressIndicator()
                    }
                }
                is MerchantUiState.StatusLoaded -> {
                    // Status Card
                    Card(
                        modifier = Modifier.fillMaxWidth(),
                        colors = CardDefaults.cardColors(
                            containerColor = MaterialTheme.colorScheme.primaryContainer,
                        ),
                    ) {
                        Column(Modifier.padding(16.dp)) {
                            Row(
                                modifier = Modifier.fillMaxWidth(),
                                horizontalArrangement = Arrangement.SpaceBetween,
                                verticalAlignment = Alignment.CenterVertically,
                            ) {
                                Icon(Icons.Default.Store, contentDescription = null, modifier = Modifier.size(32.dp))
                                AssistChip(
                                    onClick = {},
                                    label = { Text(state.status.status) },
                                )
                            }
                            Spacer(Modifier.height(8.dp))
                            Text(
                                state.status.businessName,
                                style = MaterialTheme.typography.titleLarge,
                            )
                            Text(
                                "ID: ${state.status.merchantId}",
                                style = MaterialTheme.typography.bodySmall,
                                color = MaterialTheme.colorScheme.onSurfaceVariant,
                            )
                            if (state.status.isAgent) {
                                Spacer(Modifier.height(4.dp))
                                Text(
                                    "Agent Account",
                                    style = MaterialTheme.typography.labelMedium,
                                    color = MaterialTheme.colorScheme.tertiary,
                                )
                            }
                            Spacer(Modifier.height(4.dp))
                            Text(
                                "KYC: ${state.status.kycStatus}",
                                style = MaterialTheme.typography.bodySmall,
                            )
                        }
                    }

                    // Action Cards
                    DashboardActionCard(
                        icon = Icons.Default.History,
                        title = "Transactions",
                        subtitle = "View merchant transaction history",
                        onClick = onTransactions,
                    )
                    DashboardActionCard(
                        icon = Icons.Default.Payments,
                        title = "Settlements",
                        subtitle = "View settlement reports and payouts",
                        onClick = onSettlements,
                    )
                    DashboardActionCard(
                        icon = Icons.Default.Assessment,
                        title = "Commission Report",
                        subtitle = "View commission earnings breakdown",
                        onClick = onCommission,
                    )
                }
                is MerchantUiState.Error -> {
                    Text(
                        state.message,
                        color = MaterialTheme.colorScheme.error,
                        style = MaterialTheme.typography.bodyMedium,
                    )
                    Button(onClick = { viewModel.loadStatus(merchantId) }) {
                        Text("Retry")
                    }
                }
                else -> {}
            }
        }
    }
}

@Composable
private fun DashboardActionCard(
    icon: androidx.compose.ui.graphics.vector.ImageVector,
    title: String,
    subtitle: String,
    onClick: () -> Unit,
) {
    Card(
        onClick = onClick,
        modifier = Modifier.fillMaxWidth(),
    ) {
        Row(
            modifier = Modifier.padding(16.dp),
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.spacedBy(16.dp),
        ) {
            Icon(
                icon,
                contentDescription = null,
                modifier = Modifier.size(40.dp),
                tint = MaterialTheme.colorScheme.primary,
            )
            Column(Modifier.weight(1f)) {
                Text(title, style = MaterialTheme.typography.titleMedium)
                Text(
                    subtitle,
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
            }
        }
    }
}
