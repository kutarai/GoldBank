package com.goldbank.app.ui.home

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.PaddingValues
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.layout.ExperimentalLayoutApi
import androidx.compose.foundation.layout.FlowRow
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.Logout
import androidx.compose.material.icons.filled.Diamond
import androidx.compose.material.icons.filled.Notifications
import androidx.compose.material.icons.filled.Person
import androidx.compose.foundation.layout.WindowInsets
import androidx.compose.foundation.layout.statusBars
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.SuggestionChip
import androidx.compose.material3.SuggestionChipDefaults
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.TopAppBar
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import com.goldbank.app.ui.components.BalanceCard
import com.goldbank.app.ui.components.QuickActionGrid
import com.goldbank.app.ui.components.TransactionItem
import com.goldbank.app.viewmodel.HomeViewModel
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.ui.draw.clip
import androidx.compose.ui.text.font.FontWeight

@OptIn(ExperimentalMaterial3Api::class, ExperimentalLayoutApi::class)
@Composable
fun HomeScreen(
    viewModel: HomeViewModel,
    onTransactionClick: (String) -> Unit,
    onViewAllTransactions: () -> Unit,
    onQuickAction: (String) -> Unit,
    onProfileClick: () -> Unit,
    onNotificationsClick: () -> Unit = {},
    onAssetsClick: () -> Unit = {},
    onLogout: () -> Unit,
) {
    val uiState by viewModel.uiState.collectAsState()

    Column(modifier = Modifier.fillMaxSize()) {
        TopAppBar(
            title = {
                Column(modifier = Modifier.padding(top = 4.dp)) {
                    Text(
                        text = "Welcome back,",
                        style = MaterialTheme.typography.labelMedium,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                    )
                    Text(
                        text = uiState.profile?.firstName ?: "User",
                        style = MaterialTheme.typography.titleMedium,
                    )
                }
            },
            actions = {
                IconButton(onClick = onNotificationsClick) {
                    Icon(Icons.Default.Notifications, contentDescription = "Notifications")
                }
                IconButton(onClick = onProfileClick) {
                    Icon(Icons.Default.Person, contentDescription = "Profile")
                }
                IconButton(onClick = onLogout) {
                    Icon(Icons.AutoMirrored.Filled.Logout, contentDescription = "Logout")
                }
            },
            windowInsets = WindowInsets.statusBars,
        )

        LazyColumn(
            modifier = Modifier.fillMaxSize(),
            contentPadding = PaddingValues(16.dp),
            verticalArrangement = Arrangement.spacedBy(16.dp),
        ) {
            // Account selector (ZWG | USD)
            if (uiState.accounts.size > 1) {
                item {
                    Row(
                        modifier = Modifier
                            .fillMaxWidth()
                            .clip(RoundedCornerShape(12.dp))
                            .background(MaterialTheme.colorScheme.surfaceVariant),
                        horizontalArrangement = Arrangement.Center,
                    ) {
                        uiState.accounts.forEach { account ->
                            val isSelected = account.accountId == (uiState.selectedAccount?.accountId)
                            Row(
                                modifier = Modifier
                                    .weight(1f)
                                    .clip(RoundedCornerShape(12.dp))
                                    .background(
                                        if (isSelected) MaterialTheme.colorScheme.primary
                                        else MaterialTheme.colorScheme.surfaceVariant
                                    )
                                    .clickable { viewModel.selectAccount(account) }
                                    .padding(vertical = 12.dp),
                                horizontalArrangement = Arrangement.Center,
                                verticalAlignment = Alignment.CenterVertically,
                            ) {
                                Text(
                                    text = account.currency,
                                    style = MaterialTheme.typography.titleSmall,
                                    fontWeight = if (isSelected) FontWeight.Bold else FontWeight.Normal,
                                    color = if (isSelected) MaterialTheme.colorScheme.onPrimary
                                            else MaterialTheme.colorScheme.onSurfaceVariant,
                                )
                                if (account.cardPanLast4.isNotEmpty()) {
                                    Text(
                                        text = "  ****${account.cardPanLast4}",
                                        style = MaterialTheme.typography.labelSmall,
                                        color = if (isSelected)
                                            MaterialTheme.colorScheme.onPrimary.copy(alpha = 0.7f)
                                        else
                                            MaterialTheme.colorScheme.onSurfaceVariant.copy(alpha = 0.7f),
                                    )
                                }
                            }
                        }
                    }
                }
            }

            item {
                BalanceCard(
                    balance = uiState.balance,
                    isLoading = uiState.isBalanceLoading,
                    error = uiState.balanceError,
                )
            }

            // Spending insights section — non-critical, hidden when empty and not loading
            if (uiState.isInsightsLoading || uiState.spendingInsights.isNotEmpty()) {
                item {
                    Row(
                        modifier = Modifier.fillMaxWidth(),
                        horizontalArrangement = Arrangement.SpaceBetween,
                        verticalAlignment = Alignment.CenterVertically,
                    ) {
                        Text(
                            text = "Insights",
                            style = MaterialTheme.typography.titleSmall,
                        )
                        Text(
                            text = "Powered by AI",
                            style = MaterialTheme.typography.labelSmall,
                            color = MaterialTheme.colorScheme.outline,
                        )
                    }
                }
                if (uiState.isInsightsLoading) {
                    item {
                        Card(
                            modifier = Modifier
                                .fillMaxWidth()
                                .height(60.dp),
                            colors = CardDefaults.cardColors(
                                containerColor = MaterialTheme.colorScheme.surfaceVariant,
                            ),
                        ) {}
                    }
                } else {
                    items(
                        items = uiState.spendingInsights,
                        key = { "${it.category}_${it.period}" },
                    ) { insight ->
                        Card(
                            modifier = Modifier.fillMaxWidth(),
                            colors = CardDefaults.cardColors(
                                containerColor = MaterialTheme.colorScheme.secondaryContainer,
                            ),
                        ) {
                            Column(modifier = Modifier.padding(12.dp)) {
                                Text(
                                    text = insight.summary,
                                    style = MaterialTheme.typography.bodyMedium,
                                    color = MaterialTheme.colorScheme.onSecondaryContainer,
                                )
                                Spacer(modifier = Modifier.height(4.dp))
                                Text(
                                    text = "${insight.category} \u2022 ${insight.period}",
                                    style = MaterialTheme.typography.labelSmall,
                                    color = MaterialTheme.colorScheme.onSecondaryContainer.copy(alpha = 0.7f),
                                )
                            }
                        }
                    }
                }
            }

            // Asset Portfolio card — only shown when portfolio data is loaded and has assets
            val portfolio = uiState.portfolioValue
            if (portfolio != null && portfolio.assetsByType.isNotEmpty()) {
                item {
                    Card(
                        modifier = Modifier.fillMaxWidth(),
                        colors = CardDefaults.cardColors(
                            containerColor = MaterialTheme.colorScheme.surfaceVariant,
                        ),
                    ) {
                        Column(modifier = Modifier.padding(16.dp)) {
                            Row(
                                modifier = Modifier.fillMaxWidth(),
                                horizontalArrangement = Arrangement.SpaceBetween,
                                verticalAlignment = Alignment.CenterVertically,
                            ) {
                                Row(
                                    verticalAlignment = Alignment.CenterVertically,
                                    horizontalArrangement = Arrangement.spacedBy(6.dp),
                                ) {
                                    Icon(
                                        imageVector = Icons.Default.Diamond,
                                        contentDescription = null,
                                        tint = MaterialTheme.colorScheme.primary,
                                    )
                                    Text(
                                        text = "Asset Portfolio",
                                        style = MaterialTheme.typography.titleSmall,
                                        fontWeight = FontWeight.SemiBold,
                                    )
                                }
                                TextButton(onClick = onAssetsClick) {
                                    Text("View All")
                                }
                            }
                            Spacer(modifier = Modifier.height(4.dp))
                            Text(
                                text = "$${portfolio.totalValueUsd}",
                                style = MaterialTheme.typography.headlineMedium,
                                fontWeight = FontWeight.Bold,
                                color = MaterialTheme.colorScheme.onSurface,
                            )
                            Text(
                                text = "ZWG ${portfolio.totalValueZwg}",
                                style = MaterialTheme.typography.bodySmall,
                                color = MaterialTheme.colorScheme.onSurfaceVariant,
                            )
                            Spacer(modifier = Modifier.height(8.dp))
                            FlowRow(
                                horizontalArrangement = Arrangement.spacedBy(6.dp),
                                verticalArrangement = Arrangement.spacedBy(4.dp),
                            ) {
                                portfolio.assetsByType.forEach { typeSummary ->
                                    SuggestionChip(
                                        onClick = onAssetsClick,
                                        label = {
                                            Text(
                                                text = "${typeSummary.assetType}: \$${typeSummary.totalValueUsd}",
                                                style = MaterialTheme.typography.labelSmall,
                                            )
                                        },
                                        colors = SuggestionChipDefaults.suggestionChipColors(
                                            containerColor = MaterialTheme.colorScheme.surface,
                                        ),
                                    )
                                }
                            }
                        }
                    }
                }
            }

            item {
                QuickActionGrid(onActionClick = onQuickAction)
            }

            item {
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.SpaceBetween,
                    verticalAlignment = Alignment.CenterVertically,
                ) {
                    Text(
                        text = "Recent Transactions",
                        style = MaterialTheme.typography.titleSmall,
                    )
                    TextButton(onClick = onViewAllTransactions) {
                        Text("View All")
                    }
                }
            }

            if (uiState.recentTransactions.isEmpty() && !uiState.isTransactionsLoading) {
                item {
                    Text(
                        text = uiState.transactionsError ?: "No transactions yet",
                        style = MaterialTheme.typography.bodyMedium,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                        modifier = Modifier.padding(vertical = 16.dp),
                    )
                }
            }

            items(
                items = uiState.recentTransactions.take(10),
                key = { it.transactionId },
            ) { transaction ->
                TransactionItem(
                    transaction = transaction,
                    onClick = { onTransactionClick(transaction.transactionId) },
                )
                HorizontalDivider(modifier = Modifier.padding(horizontal = 16.dp))
            }
        }
    }
}
