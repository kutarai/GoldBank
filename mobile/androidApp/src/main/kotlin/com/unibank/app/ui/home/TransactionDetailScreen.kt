package com.unibank.app.ui.home

import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.material3.TopAppBar
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.remember
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import com.unibank.app.viewmodel.HomeViewModel
import com.unibank.shared.domain.model.Transaction
import com.unibank.shared.domain.util.MoneyFormatter

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun TransactionDetailScreen(
    viewModel: HomeViewModel,
    transactionId: String,
    onBack: () -> Unit,
) {
    val uiState by viewModel.uiState.collectAsState()
    val transaction = remember(transactionId, uiState.recentTransactions) {
        uiState.recentTransactions.find { it.transactionId == transactionId }
    }

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text("Transaction Details") },
                navigationIcon = {
                    IconButton(onClick = onBack) {
                        Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = "Back")
                    }
                },
            )
        },
    ) { padding ->
        if (transaction == null) {
            Text(
                text = "Transaction not found",
                modifier = Modifier
                    .fillMaxSize()
                    .padding(padding)
                    .padding(16.dp),
            )
            return@Scaffold
        }

        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(padding)
                .padding(16.dp),
        ) {
            Text(
                text = MoneyFormatter.format(transaction.amount.amount, transaction.amount.currency),
                style = MaterialTheme.typography.headlineLarge,
                fontWeight = FontWeight.Bold,
            )
            Spacer(modifier = Modifier.height(4.dp))
            Text(
                text = transaction.status.name.lowercase().replaceFirstChar { it.uppercase() },
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.tertiary,
            )

            Spacer(modifier = Modifier.height(24.dp))
            HorizontalDivider()
            Spacer(modifier = Modifier.height(16.dp))

            DetailRow("Type", transaction.type.name.replace("_", " ").lowercase()
                .replaceFirstChar { it.uppercase() })
            if (transaction.description.isNotEmpty()) {
                DetailRow("Description", transaction.description)
            }
            if (transaction.counterpartyName.isNotEmpty()) {
                DetailRow("Counterparty", transaction.counterpartyName)
            }
            if (transaction.counterpartyPhone.isNotEmpty()) {
                DetailRow("Phone", transaction.counterpartyPhone)
            }
            if (transaction.reference.isNotEmpty()) {
                DetailRow("Reference", transaction.reference)
            }
            if (transaction.fee.amount != "0" && transaction.fee.amount != "0.00") {
                DetailRow("Fee", MoneyFormatter.format(transaction.fee.amount, transaction.fee.currency))
            }
            DetailRow(
                "Balance After",
                MoneyFormatter.format(transaction.balanceAfter.amount, transaction.balanceAfter.currency),
            )
            if (transaction.createdAt.isNotEmpty()) {
                DetailRow("Created", transaction.createdAt)
            }
            if (transaction.completedAt.isNotEmpty()) {
                DetailRow("Completed", transaction.completedAt)
            }
            DetailRow("Transaction ID", transaction.transactionId)
        }
    }
}

@Composable
private fun DetailRow(label: String, value: String) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .padding(vertical = 8.dp),
    ) {
        Text(
            text = label,
            style = MaterialTheme.typography.bodyMedium,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
            modifier = Modifier.weight(0.4f),
        )
        Text(
            text = value,
            style = MaterialTheme.typography.bodyMedium,
            fontWeight = FontWeight.Medium,
            modifier = Modifier.weight(0.6f),
        )
    }
}
