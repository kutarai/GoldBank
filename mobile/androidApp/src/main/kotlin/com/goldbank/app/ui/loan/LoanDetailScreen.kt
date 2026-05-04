package com.goldbank.app.ui.loan

import androidx.compose.animation.animateContentSize
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.ExpandLess
import androidx.compose.material.icons.filled.ExpandMore
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import com.goldbank.app.viewmodel.LoanViewModel
import com.goldbank.shared.domain.model.LoanScheduleEntry
import com.goldbank.shared.domain.util.MoneyFormatter

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun LoanDetailScreen(
    viewModel: LoanViewModel,
    loanId: String,
    onBack: () -> Unit,
) {
    val loanDetail by viewModel.loanDetail.collectAsState()
    val schedule by viewModel.schedule.collectAsState()
    val isLoading by viewModel.loansLoading.collectAsState()
    var scheduleExpanded by rememberSaveable { mutableStateOf(false) }

    LaunchedEffect(loanId) {
        viewModel.loadLoanDetail(loanId)
        viewModel.loadSchedule(loanId)
    }

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text("Loan Details") },
                navigationIcon = {
                    IconButton(onClick = onBack) {
                        Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = "Back")
                    }
                },
            )
        },
    ) { padding ->
        if (isLoading && loanDetail == null) {
            Box(
                modifier = Modifier.fillMaxSize().padding(padding),
                contentAlignment = Alignment.Center,
            ) {
                CircularProgressIndicator()
            }
        } else {
            val loan = loanDetail ?: return@Scaffold
            Column(
                modifier = Modifier
                    .fillMaxSize()
                    .padding(padding)
                    .verticalScroll(rememberScrollState())
                    .padding(16.dp),
                verticalArrangement = Arrangement.spacedBy(12.dp),
            ) {
                // Header
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.SpaceBetween,
                    verticalAlignment = Alignment.CenterVertically,
                ) {
                    Text(loan.reference, style = MaterialTheme.typography.titleMedium)
                    LoanStatusChip(loan.status)
                }

                // Main details card
                Card(modifier = Modifier.fillMaxWidth()) {
                    Column(modifier = Modifier.padding(16.dp)) {
                        DetailRow("Principal", MoneyFormatter.format(loan.principal.amount, loan.principal.currency))
                        DetailRow("Outstanding", MoneyFormatter.format(loan.outstandingBalance.amount, loan.outstandingBalance.currency))
                        DetailRow("Interest Rate", loan.interestRate)
                        DetailRow("Tenure", "${loan.tenureMonths} months")
                        DetailRow("Monthly Payment", MoneyFormatter.format(loan.monthlyPayment.amount, loan.monthlyPayment.currency))
                        DetailRow("Purpose", loan.purpose)
                        DetailRow("Credit Score", "${loan.creditScore}")
                    }
                }

                // Payment progress
                Card(modifier = Modifier.fillMaxWidth()) {
                    Column(modifier = Modifier.padding(16.dp)) {
                        Text("Payment Progress", style = MaterialTheme.typography.titleSmall)
                        Spacer(modifier = Modifier.height(8.dp))
                        LinearProgressIndicator(
                            progress = {
                                if (loan.totalPayments > 0) loan.paymentsMade.toFloat() / loan.totalPayments
                                else 0f
                            },
                            modifier = Modifier.fillMaxWidth(),
                        )
                        Spacer(modifier = Modifier.height(4.dp))
                        Text(
                            text = "${loan.paymentsMade} of ${loan.totalPayments} payments made",
                            style = MaterialTheme.typography.bodySmall,
                            color = MaterialTheme.colorScheme.onSurfaceVariant,
                        )
                    }
                }

                // Collapsible repayment schedule
                if (schedule.isNotEmpty()) {
                    Card(
                        modifier = Modifier
                            .fillMaxWidth()
                            .animateContentSize(),
                    ) {
                        Column(modifier = Modifier.padding(16.dp)) {
                            Row(
                                modifier = Modifier
                                    .fillMaxWidth()
                                    .clickable { scheduleExpanded = !scheduleExpanded },
                                horizontalArrangement = Arrangement.SpaceBetween,
                                verticalAlignment = Alignment.CenterVertically,
                            ) {
                                Text("Repayment Schedule", style = MaterialTheme.typography.titleSmall)
                                Icon(
                                    imageVector = if (scheduleExpanded) Icons.Default.ExpandLess else Icons.Default.ExpandMore,
                                    contentDescription = if (scheduleExpanded) "Collapse" else "Expand",
                                )
                            }

                            if (scheduleExpanded) {
                                Spacer(modifier = Modifier.height(8.dp))
                                HorizontalDivider()
                                schedule.forEach { entry ->
                                    ScheduleEntryRow(entry)
                                    HorizontalDivider()
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}

@Composable
private fun DetailRow(label: String, value: String) {
    Row(
        modifier = Modifier.fillMaxWidth().padding(vertical = 4.dp),
        horizontalArrangement = Arrangement.SpaceBetween,
    ) {
        Text(text = label, style = MaterialTheme.typography.bodyMedium, color = MaterialTheme.colorScheme.onSurfaceVariant)
        Text(text = value, style = MaterialTheme.typography.bodyMedium)
    }
}

@Composable
private fun ScheduleEntryRow(entry: LoanScheduleEntry) {
    Row(
        modifier = Modifier.fillMaxWidth().padding(vertical = 8.dp),
        horizontalArrangement = Arrangement.SpaceBetween,
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Column {
            Text(
                text = "#${entry.paymentNumber}",
                style = MaterialTheme.typography.labelMedium,
            )
            Text(
                text = MoneyFormatter.format(entry.totalPayment.amount, entry.totalPayment.currency),
                style = MaterialTheme.typography.bodyMedium,
            )
        }
        if (entry.isPaid) {
            SuggestionChip(
                onClick = {},
                label = { Text("Paid", style = MaterialTheme.typography.labelSmall) },
                colors = SuggestionChipDefaults.suggestionChipColors(
                    containerColor = MaterialTheme.colorScheme.tertiary.copy(alpha = 0.12f),
                    labelColor = MaterialTheme.colorScheme.tertiary,
                ),
            )
        } else {
            Text(
                text = "Due",
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
        }
    }
}
