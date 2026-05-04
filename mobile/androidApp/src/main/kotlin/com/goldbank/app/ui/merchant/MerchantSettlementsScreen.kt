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
import androidx.compose.ui.unit.dp
import com.goldbank.app.viewmodel.MerchantUiState
import com.goldbank.app.viewmodel.MerchantViewModel
import com.goldbank.shared.domain.model.Settlement

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun MerchantSettlementsScreen(
    viewModel: MerchantViewModel,
    onBack: () -> Unit,
) {
    val uiState by viewModel.uiState.collectAsState()

    LaunchedEffect(Unit) {
        viewModel.loadSettlements()
    }

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text("Settlements") },
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
            is MerchantUiState.SettlementsLoaded -> {
                if (state.settlements.isEmpty()) {
                    Box(Modifier.fillMaxSize().padding(padding), contentAlignment = Alignment.Center) {
                        Text("No settlements found", style = MaterialTheme.typography.bodyLarge)
                    }
                } else {
                    LazyColumn(
                        modifier = Modifier.fillMaxSize().padding(padding),
                        contentPadding = PaddingValues(16.dp),
                        verticalArrangement = Arrangement.spacedBy(8.dp),
                    ) {
                        items(state.settlements) { settlement ->
                            SettlementItem(settlement)
                        }
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
                    Button(onClick = { viewModel.loadSettlements() }) { Text("Retry") }
                }
            }
            else -> {}
        }
    }
}

@Composable
private fun SettlementItem(settlement: Settlement) {
    Card(modifier = Modifier.fillMaxWidth()) {
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .padding(16.dp),
        ) {
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically,
            ) {
                Text(
                    "Settlement #${settlement.settlementId.takeLast(8)}",
                    style = MaterialTheme.typography.titleSmall,
                )
                AssistChip(
                    onClick = {},
                    label = { Text(settlement.status) },
                )
            }
            Spacer(Modifier.height(8.dp))
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
            ) {
                Column {
                    Text(
                        "Amount",
                        style = MaterialTheme.typography.labelSmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                    )
                    Text(
                        "${settlement.amount.currency} ${settlement.amount.amount}",
                        style = MaterialTheme.typography.titleMedium,
                        color = MaterialTheme.colorScheme.primary,
                    )
                }
                Column(horizontalAlignment = Alignment.End) {
                    Text(
                        "Transactions",
                        style = MaterialTheme.typography.labelSmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                    )
                    Text(
                        "${settlement.transactionCount}",
                        style = MaterialTheme.typography.titleMedium,
                    )
                }
            }
        }
    }
}
