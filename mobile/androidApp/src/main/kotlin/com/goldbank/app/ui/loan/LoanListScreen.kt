package com.goldbank.app.ui.loan

import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.Add
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import com.goldbank.app.viewmodel.LoanViewModel
import com.goldbank.shared.domain.model.LoanStatus
import com.goldbank.shared.domain.model.LoanSummary
import com.goldbank.shared.domain.util.MoneyFormatter

private val tabs = listOf("All" to "", "Active" to "active", "Completed" to "completed")

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun LoanListScreen(
    viewModel: LoanViewModel,
    onApply: () -> Unit,
    onLoanClick: (String) -> Unit,
    onBack: () -> Unit,
) {
    val loans by viewModel.loans.collectAsState()
    val isLoading by viewModel.loansLoading.collectAsState()
    val error by viewModel.loansError.collectAsState()
    var selectedTab by rememberSaveable { mutableIntStateOf(0) }

    LaunchedEffect(selectedTab) {
        viewModel.loadLoans(tabs[selectedTab].second)
    }

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text("My Loans") },
                navigationIcon = {
                    IconButton(onClick = onBack) {
                        Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = "Back")
                    }
                },
            )
        },
        floatingActionButton = {
            FloatingActionButton(onClick = onApply) {
                Icon(Icons.Default.Add, contentDescription = "Apply for Loan")
            }
        },
    ) { padding ->
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(padding),
        ) {
            TabRow(selectedTabIndex = selectedTab) {
                tabs.forEachIndexed { index, (label, _) ->
                    Tab(
                        selected = selectedTab == index,
                        onClick = { selectedTab = index },
                        text = { Text(label) },
                    )
                }
            }

            when {
                isLoading -> {
                    Box(modifier = Modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
                        CircularProgressIndicator()
                    }
                }
                error != null -> {
                    Box(modifier = Modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
                        Column(horizontalAlignment = Alignment.CenterHorizontally) {
                            Text(error ?: "Unknown error", color = MaterialTheme.colorScheme.error)
                            Spacer(modifier = Modifier.height(8.dp))
                            Button(onClick = { viewModel.loadLoans(tabs[selectedTab].second) }) {
                                Text("Retry")
                            }
                        }
                    }
                }
                loans.isEmpty() -> {
                    Box(modifier = Modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
                        Column(horizontalAlignment = Alignment.CenterHorizontally) {
                            Text("No loans found", style = MaterialTheme.typography.bodyLarge)
                            Spacer(modifier = Modifier.height(8.dp))
                            Button(onClick = onApply) { Text("Apply for a Loan") }
                        }
                    }
                }
                else -> {
                    LazyColumn(
                        modifier = Modifier.fillMaxSize(),
                        contentPadding = PaddingValues(16.dp),
                        verticalArrangement = Arrangement.spacedBy(8.dp),
                    ) {
                        items(loans, key = { it.loanId }) { loan ->
                            LoanSummaryCard(loan = loan, onClick = { onLoanClick(loan.loanId) })
                        }
                    }
                }
            }
        }
    }
}

@Composable
private fun LoanSummaryCard(
    loan: LoanSummary,
    onClick: () -> Unit,
) {
    Card(
        modifier = Modifier
            .fillMaxWidth()
            .clickable(onClick = onClick),
    ) {
        Column(modifier = Modifier.padding(16.dp)) {
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically,
            ) {
                Text(loan.reference, style = MaterialTheme.typography.titleSmall)
                LoanStatusChip(loan.status)
            }
            Spacer(modifier = Modifier.height(8.dp))
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
            ) {
                Column {
                    Text("Principal", style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                    Text(MoneyFormatter.format(loan.principal.amount, loan.principal.currency), style = MaterialTheme.typography.bodyMedium)
                }
                Column(horizontalAlignment = Alignment.End) {
                    Text("Outstanding", style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                    Text(MoneyFormatter.format(loan.outstandingBalance.amount, loan.outstandingBalance.currency), style = MaterialTheme.typography.bodyMedium)
                }
            }
            Spacer(modifier = Modifier.height(4.dp))
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
            ) {
                Text(
                    "Monthly: ${MoneyFormatter.format(loan.monthlyPayment.amount, loan.monthlyPayment.currency)}",
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
                Text(
                    "Payments: ${loan.paymentsMade}/${loan.totalPayments}",
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )
            }
        }
    }
}

@Composable
fun LoanStatusChip(status: LoanStatus) {
    val (label, color) = when (status) {
        LoanStatus.PENDING -> "Pending" to MaterialTheme.colorScheme.tertiary
        LoanStatus.APPROVED -> "Approved" to MaterialTheme.colorScheme.primary
        LoanStatus.REJECTED -> "Rejected" to MaterialTheme.colorScheme.error
        LoanStatus.DISBURSED -> "Disbursed" to MaterialTheme.colorScheme.primary
        LoanStatus.REPAYING -> "Repaying" to MaterialTheme.colorScheme.secondary
        LoanStatus.PAID_OFF -> "Paid Off" to MaterialTheme.colorScheme.tertiary
        LoanStatus.DEFAULTED -> "Defaulted" to MaterialTheme.colorScheme.error
        else -> "Unknown" to MaterialTheme.colorScheme.outline
    }
    SuggestionChip(
        onClick = {},
        label = { Text(label, style = MaterialTheme.typography.labelSmall) },
        colors = SuggestionChipDefaults.suggestionChipColors(
            containerColor = color.copy(alpha = 0.12f),
            labelColor = color,
        ),
    )
}
