package com.goldbank.app.ui.ekub

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.AssistChip
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.Divider
import androidx.compose.material3.ElevatedCard
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Tab
import androidx.compose.material3.TabRow
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.TopAppBar
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.ArrowBack
import androidx.compose.material.icons.filled.PersonAdd
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.unit.dp
import com.goldbank.app.viewmodel.EkubViewModel
import com.goldbank.shared.data.local.SessionManager
import com.goldbank.shared.domain.model.EkubContribution
import com.goldbank.shared.domain.model.EkubLoan
import com.goldbank.shared.domain.model.EkubMember
import org.koin.compose.koinInject

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun EkubGroupDetailScreen(
    viewModel: EkubViewModel,
    groupId: String,
    onBack: () -> Unit,
) {
    val state by viewModel.uiState.collectAsState()
    val sessionManager: SessionManager = koinInject()
    val myCustomerId = sessionManager.getCustomerId() ?: ""

    LaunchedEffect(groupId) { viewModel.loadGroupDetail(groupId) }

    val detail = state.selectedDetail
    val myMembership = detail?.members?.firstOrNull { it.customerId == myCustomerId && it.leftAtMillis == null }
    val isChairmanOrSecretary = myMembership?.role in setOf("Chairman", "Secretary")
    val isTreasurer = myMembership?.role == "Treasurer"

    var showInviteDialog by rememberSaveable { mutableStateOf(false) }
    var showContributeDialog by rememberSaveable { mutableStateOf(false) }
    var showApplyLoanDialog by rememberSaveable { mutableStateOf(false) }
    var tab by rememberSaveable { mutableStateOf(0) }

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text(detail?.group?.name ?: "Group") },
                navigationIcon = {
                    IconButton(onClick = onBack) { Icon(Icons.Default.ArrowBack, contentDescription = "Back") }
                },
                actions = {
                    if (isChairmanOrSecretary) {
                        IconButton(onClick = { showInviteDialog = true }) {
                            Icon(Icons.Default.PersonAdd, contentDescription = "Invite member")
                        }
                    }
                },
            )
        },
    ) { padding ->
        if (detail == null) {
            Column(modifier = Modifier.padding(padding).padding(16.dp)) { Text("Loading…") }
            return@Scaffold
        }

        Column(modifier = Modifier.fillMaxSize().padding(padding)) {
            HeaderCard(detail.group.status, detail.group.currency, detail.group.monthlyContribution, detail.group.loanInterestRatePercent, detail.potBalanceAmount)
            MyShareCard(state.selectedMyShare?.myContributionsAmount, state.selectedMyShare?.myInterestEarningsAmount, state.selectedMyShare?.myShareTotalAmount, state.selectedMyShare?.currency, state.selectedMyShare?.mySharePercent)

            state.error?.let { msg ->
                Card(
                    modifier = Modifier.fillMaxWidth().padding(horizontal = 16.dp).padding(bottom = 8.dp),
                    colors = androidx.compose.material3.CardDefaults.cardColors(
                        containerColor = MaterialTheme.colorScheme.errorContainer,
                    ),
                ) {
                    Row(
                        modifier = Modifier.padding(12.dp),
                        verticalAlignment = androidx.compose.ui.Alignment.CenterVertically,
                    ) {
                        Text(
                            text = msg,
                            style = MaterialTheme.typography.bodySmall,
                            color = MaterialTheme.colorScheme.onErrorContainer,
                            modifier = Modifier.weight(1f),
                        )
                        TextButton(onClick = { viewModel.clearError() }) { Text("Dismiss") }
                    }
                }
            }
            state.flash?.let { msg ->
                Card(
                    modifier = Modifier.fillMaxWidth().padding(horizontal = 16.dp).padding(bottom = 8.dp),
                ) {
                    Row(
                        modifier = Modifier.padding(12.dp),
                        verticalAlignment = androidx.compose.ui.Alignment.CenterVertically,
                    ) {
                        Text(
                            text = msg,
                            style = MaterialTheme.typography.bodySmall,
                            color = MaterialTheme.colorScheme.primary,
                            modifier = Modifier.weight(1f),
                        )
                        TextButton(onClick = { viewModel.clearFlash() }) { Text("OK") }
                    }
                }
            }

            TabRow(selectedTabIndex = tab) {
                Tab(selected = tab == 0, onClick = { tab = 0 }, text = { Text("Members") })
                Tab(selected = tab == 1, onClick = { tab = 1 }, text = { Text("Contributions") })
                Tab(selected = tab == 2, onClick = { tab = 2 }, text = { Text("Loans") })
            }

            // Bound the tab content so the inner LazyColumn has a finite max height.
            Box(modifier = Modifier.fillMaxSize().weight(1f)) {
                when (tab) {
                0 -> MembersTab(detail.members)
                1 -> ContributionsTab(
                    contributions = state.selectedContributions,
                    members = detail.members,
                    isTreasurer = isTreasurer,
                    isActive = detail.group.status == "Active",
                    onSubmit = { showContributeDialog = true },
                    onConfirm = { id, approve -> viewModel.confirmContribution(id, groupId, approve) },
                )
                2 -> LoansTab(
                    loans = state.selectedLoans,
                    members = detail.members,
                    myCustomerId = myCustomerId,
                    isTreasurer = isTreasurer,
                    isActive = detail.group.status == "Active",
                    onApply = { showApplyLoanDialog = true },
                    onVote = { loanId, approve -> viewModel.voteOnLoan(loanId, groupId, approve) },
                    onConfirm = { loanId, approve -> viewModel.confirmLoan(loanId, groupId, approve) },
                    onRepay = { loanId, amount -> viewModel.recordRepayment(loanId, groupId, amount) },
                )
                }
            }
        }
    }

    if (showInviteDialog) {
        InviteDialog(
            onDismiss = { showInviteDialog = false },
            onSend = { phone ->
                viewModel.inviteMember(groupId, phone)
                showInviteDialog = false
            },
        )
    }
    if (showContributeDialog) {
        AmountDialog(
            title = "Record contribution",
            label = "Amount (${detail?.group?.currency ?: ""})",
            actionLabel = "Submit",
            onDismiss = { showContributeDialog = false },
            onConfirm = { amount ->
                viewModel.recordContribution(groupId, amount, "", "")
                showContributeDialog = false
            },
        )
    }
    if (showApplyLoanDialog) {
        ApplyLoanDialog(
            currency = detail?.group?.currency ?: "",
            interestRatePercent = detail?.group?.loanInterestRatePercent?.toDoubleOrNull() ?: 0.0,
            applyInterestOnContributions = detail?.group?.applyInterestOnContributions ?: true,
            myContributions = state.selectedMyShare?.myContributionsAmount?.toDoubleOrNull() ?: 0.0,
            potBalance = detail?.potBalanceAmount?.toDoubleOrNull() ?: 0.0,
            onDismiss = { showApplyLoanDialog = false },
            onConfirm = { principal, term, purpose ->
                viewModel.applyForLoan(groupId, principal, term, purpose)
                showApplyLoanDialog = false
            },
        )
    }
}

@Composable
private fun HeaderCard(status: String, currency: String, monthly: String, rate: String, pot: String) {
    ElevatedCard(modifier = Modifier.fillMaxWidth().padding(16.dp)) {
        Column(modifier = Modifier.padding(16.dp)) {
            Row(verticalAlignment = androidx.compose.ui.Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                AssistChip(onClick = {}, label = { Text(status) })
                Text("· $currency $monthly /month", color = MaterialTheme.colorScheme.onSurfaceVariant)
            }
            Spacer(Modifier.height(8.dp))
            Text("Pot balance", style = MaterialTheme.typography.labelMedium)
            Text("$currency $pot", style = MaterialTheme.typography.headlineMedium, fontWeight = FontWeight.Bold)
            Spacer(Modifier.height(4.dp))
            Text("Loan rate $rate% per year", style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
        }
    }
}

@Composable
private fun MyShareCard(contrib: String?, interest: String?, total: String?, currency: String?, percent: String?) {
    Card(modifier = Modifier.fillMaxWidth().padding(horizontal = 16.dp).padding(bottom = 8.dp)) {
        Column(modifier = Modifier.padding(16.dp)) {
            Text("Your share", style = MaterialTheme.typography.labelMedium)
            Spacer(Modifier.height(4.dp))
            Text(
                "$currency ${total ?: "0.00"}",
                style = MaterialTheme.typography.titleLarge,
                fontWeight = FontWeight.Bold,
            )
            Spacer(Modifier.height(2.dp))
            Text(
                "${currency ?: ""} ${contrib ?: "0.00"} contributed · ${currency ?: ""} ${interest ?: "0.00"} interest · ${percent ?: "0.00"}% of pot",
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
        }
    }
}

@Composable
private fun MembersTab(members: List<EkubMember>) {
    val active = members.filter { it.leftAtMillis == null }
    LazyColumn(
        modifier = Modifier.fillMaxSize(),
        contentPadding = androidx.compose.foundation.layout.PaddingValues(16.dp),
        verticalArrangement = Arrangement.spacedBy(8.dp),
    ) {
        items(active, key = { it.membershipId }) { m ->
            Card(modifier = Modifier.fillMaxWidth()) {
                Column(modifier = Modifier.padding(12.dp)) {
                    Text("${m.firstName} ${m.lastName}".trim().ifEmpty { m.phone }, style = MaterialTheme.typography.titleSmall)
                    Text("${m.role} · ${m.phone}", style = MaterialTheme.typography.bodySmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                }
            }
        }
    }
}

@Composable
private fun ContributionsTab(
    contributions: List<EkubContribution>,
    members: List<EkubMember>,
    isTreasurer: Boolean,
    isActive: Boolean,
    onSubmit: () -> Unit,
    onConfirm: (id: String, approve: Boolean) -> Unit,
) {
    val byCustomer = members.associateBy { it.customerId }
    LazyColumn(
        modifier = Modifier.fillMaxSize(),
        contentPadding = androidx.compose.foundation.layout.PaddingValues(16.dp),
        verticalArrangement = Arrangement.spacedBy(8.dp),
    ) {
        item {
            if (isActive) {
                Button(onClick = onSubmit, modifier = Modifier.fillMaxWidth()) { Text("Record my contribution") }
                Spacer(Modifier.height(8.dp))
            }
        }
        items(contributions, key = { it.id }) { c ->
            val who = byCustomer[c.customerId]
            Card(modifier = Modifier.fillMaxWidth()) {
                Column(modifier = Modifier.padding(12.dp)) {
                    Text("${c.currency} ${c.amount}", style = MaterialTheme.typography.titleMedium, fontWeight = FontWeight.SemiBold)
                    Text(
                        "${who?.let { "${it.firstName} ${it.lastName}".trim().ifEmpty { it.phone } } ?: "Member"} · ${c.period} · ${c.status}",
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                    )
                    if (isTreasurer && c.status == "Pending") {
                        Spacer(Modifier.height(8.dp))
                        Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                            Button(onClick = { onConfirm(c.id, true) }, modifier = Modifier.weight(1f)) { Text("Confirm") }
                            OutlinedButton(onClick = { onConfirm(c.id, false) }, modifier = Modifier.weight(1f)) { Text("Reject") }
                        }
                    }
                }
            }
        }
    }
}

@Composable
private fun LoansTab(
    loans: List<EkubLoan>,
    members: List<EkubMember>,
    myCustomerId: String,
    isTreasurer: Boolean,
    isActive: Boolean,
    onApply: () -> Unit,
    onVote: (loanId: String, approve: Boolean) -> Unit,
    onConfirm: (loanId: String, approve: Boolean) -> Unit,
    onRepay: (loanId: String, amount: String) -> Unit,
) {
    val byCustomer = members.associateBy { it.customerId }
    LazyColumn(
        modifier = Modifier.fillMaxSize(),
        contentPadding = androidx.compose.foundation.layout.PaddingValues(16.dp),
        verticalArrangement = Arrangement.spacedBy(8.dp),
    ) {
        item {
            if (isActive) {
                Button(onClick = onApply, modifier = Modifier.fillMaxWidth()) { Text("Apply for a loan") }
                Spacer(Modifier.height(8.dp))
            }
        }
        items(loans, key = { it.id }) { loan ->
            val who = byCustomer[loan.borrowerCustomerId]
            val isBorrower = loan.borrowerCustomerId == myCustomerId
            Card(modifier = Modifier.fillMaxWidth()) {
                Column(modifier = Modifier.padding(12.dp)) {
                    Text("${loan.currency} ${loan.principal}", style = MaterialTheme.typography.titleMedium, fontWeight = FontWeight.SemiBold)
                    Text(
                        "${who?.let { "${it.firstName} ${it.lastName}".trim().ifEmpty { it.phone } } ?: "Member"} · ${loan.termMonths}m @ ${loan.interestRatePercent}% · ${loan.status}",
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                    )
                    if (loan.status == "Voting") {
                        Text(
                            "Votes: ${loan.approvedVotes} / ${loan.totalEligibleVoters} approve · ${loan.rejectedVotes} reject",
                            style = MaterialTheme.typography.bodySmall,
                        )
                    }
                    if (loan.status in setOf("Disbursed", "Repaying")) {
                        Text("Outstanding: ${loan.currency} ${loan.outstandingBalance} · installment ${loan.installmentAmount}",
                            style = MaterialTheme.typography.bodySmall)
                    }

                    Spacer(Modifier.height(6.dp))
                    when {
                        loan.status == "Voting" && !isBorrower && loan.myVote.isBlank() -> {
                            Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                                Button(onClick = { onVote(loan.id, true) }, modifier = Modifier.weight(1f)) { Text("Approve") }
                                OutlinedButton(onClick = { onVote(loan.id, false) }, modifier = Modifier.weight(1f)) { Text("Reject") }
                            }
                        }
                        loan.status == "Voting" && loan.myVote.isNotBlank() -> {
                            Text(
                                "You voted: ${loan.myVote}",
                                style = MaterialTheme.typography.bodySmall,
                                color = MaterialTheme.colorScheme.primary,
                            )
                        }
                        loan.status == "AwaitingTreasurer" && isTreasurer -> {
                            Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                                Button(onClick = { onConfirm(loan.id, true) }, modifier = Modifier.weight(1f)) { Text("Confirm & disburse") }
                                OutlinedButton(onClick = { onConfirm(loan.id, false) }, modifier = Modifier.weight(1f)) { Text("Reject") }
                            }
                        }
                        loan.status in setOf("Disbursed", "Repaying") && isTreasurer -> {
                            var amount by remember { mutableStateOf("") }
                            Row(verticalAlignment = androidx.compose.ui.Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                                OutlinedTextField(
                                    value = amount,
                                    onValueChange = { amount = it.filter { ch -> ch.isDigit() || ch == '.' } },
                                    placeholder = { Text("Repayment amount") },
                                    singleLine = true,
                                    modifier = Modifier.weight(1f),
                                    keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Decimal),
                                )
                                Button(onClick = { if (amount.isNotBlank()) { onRepay(loan.id, amount); amount = "" } }) {
                                    Text("Record")
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
private fun InviteDialog(onDismiss: () -> Unit, onSend: (phone: String) -> Unit) {
    var phone by rememberSaveable { mutableStateOf("+263") }
    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text("Invite member") },
        text = {
            OutlinedTextField(
                value = phone, onValueChange = { phone = it },
                label = { Text("Phone (E.164)") },
                singleLine = true,
                modifier = Modifier.fillMaxWidth(),
            )
        },
        confirmButton = { TextButton(onClick = { onSend(phone.trim()) }, enabled = phone.length >= 10) { Text("Send invitation") } },
        dismissButton = { TextButton(onClick = onDismiss) { Text("Cancel") } },
    )
}

@Composable
private fun AmountDialog(title: String, label: String, actionLabel: String, onDismiss: () -> Unit, onConfirm: (amount: String) -> Unit) {
    var amount by rememberSaveable { mutableStateOf("") }
    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text(title) },
        text = {
            OutlinedTextField(
                value = amount,
                onValueChange = { amount = it.filter { ch -> ch.isDigit() || ch == '.' } },
                label = { Text(label) },
                singleLine = true,
                keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Decimal),
                modifier = Modifier.fillMaxWidth(),
            )
        },
        confirmButton = { TextButton(onClick = { onConfirm(amount) }, enabled = amount.isNotBlank()) { Text(actionLabel) } },
        dismissButton = { TextButton(onClick = onDismiss) { Text("Cancel") } },
    )
}

@Composable
private fun ApplyLoanDialog(
    currency: String,
    interestRatePercent: Double,
    applyInterestOnContributions: Boolean,
    myContributions: Double,
    potBalance: Double,
    onDismiss: () -> Unit,
    onConfirm: (principal: String, termMonths: Int, purpose: String) -> Unit,
) {
    var principal by rememberSaveable { mutableStateOf("") }
    var term by rememberSaveable { mutableStateOf("6") }
    var purpose by rememberSaveable { mutableStateOf("") }

    // Live projection — mirrors EkubGrpcService.ApplyForLoan so the borrower
    // sees exactly what the server will compute on submit.
    val principalD = principal.toDoubleOrNull() ?: 0.0
    val termI      = term.toIntOrNull() ?: 0
    val rateFrac   = interestRatePercent / 100.0
    val interestable = when {
        applyInterestOnContributions -> principalD
        else -> (principalD - myContributions).coerceAtLeast(0.0)
    }
    val totalInterest = if (termI > 0) interestable * rateFrac * termI / 12.0 else 0.0
    val totalRepayable = principalD + totalInterest
    val monthlyRepayment = if (termI > 0) totalRepayable / termI else 0.0
    val showProjection   = principalD > 0.0 && termI > 0
    val exceedsPot       = principalD > potBalance
    val canSubmit        = principal.isNotBlank() && termI > 0 && principalD > 0.0 && !exceedsPot

    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text("Apply for a loan") },
        text = {
            Column(verticalArrangement = Arrangement.spacedBy(8.dp), modifier = Modifier.verticalScroll(rememberScrollState())) {
                Text(
                    "Pot available: $currency ${"%,.2f".format(potBalance)}",
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                )

                OutlinedTextField(
                    value = principal, onValueChange = { principal = it.filter { ch -> ch.isDigit() || ch == '.' } },
                    label = { Text("Principal ($currency)") }, singleLine = true,
                    isError = exceedsPot,
                    supportingText = if (exceedsPot) {
                        { Text("Cannot exceed pot balance ($currency ${"%,.2f".format(potBalance)})") }
                    } else null,
                    keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Decimal),
                    modifier = Modifier.fillMaxWidth(),
                )
                OutlinedTextField(
                    value = term, onValueChange = { term = it.filter { ch -> ch.isDigit() } },
                    label = { Text("Term (months)") }, singleLine = true,
                    keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number),
                    modifier = Modifier.fillMaxWidth(),
                )
                OutlinedTextField(
                    value = purpose, onValueChange = { purpose = it },
                    label = { Text("Purpose") }, singleLine = true,
                    modifier = Modifier.fillMaxWidth(),
                )

                if (showProjection) {
                    Spacer(Modifier.height(4.dp))
                    Card(modifier = Modifier.fillMaxWidth()) {
                        Column(modifier = Modifier.padding(12.dp), verticalArrangement = Arrangement.spacedBy(2.dp)) {
                            ProjectionRow("Interest rate", String.format("%.2f%% per year", interestRatePercent))
                            if (!applyInterestOnContributions && myContributions > 0.0) {
                                ProjectionRow(
                                    "Your contributions",
                                    "$currency ${"%,.2f".format(myContributions)}",
                                )
                                ProjectionRow(
                                    "Interest charged on",
                                    "$currency ${"%,.2f".format(interestable)}",
                                    subtitle = if (interestable == 0.0)
                                        "Loan is within your contributions — no interest"
                                    else
                                        "Only the portion above your contributions",
                                )
                            }
                            ProjectionRow("Total interest", "$currency ${"%,.2f".format(totalInterest)}")
                            ProjectionRow(
                                "Monthly repayment",
                                "$currency ${"%,.2f".format(monthlyRepayment)}",
                                emphasised = true,
                            )
                            ProjectionRow("Total repayable", "$currency ${"%,.2f".format(totalRepayable)}")
                        }
                    }
                }
            }
        },
        confirmButton = {
            TextButton(
                onClick = { onConfirm(principal, term.toIntOrNull() ?: 6, purpose) },
                enabled = canSubmit,
            ) { Text("Submit") }
        },
        dismissButton = { TextButton(onClick = onDismiss) { Text("Cancel") } },
    )
}

@Composable
private fun ProjectionRow(label: String, value: String, subtitle: String? = null, emphasised: Boolean = false) {
    Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.SpaceBetween) {
        Text(
            label,
            style = MaterialTheme.typography.bodySmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
        Text(
            value,
            style = if (emphasised) MaterialTheme.typography.titleMedium else MaterialTheme.typography.bodyMedium,
            fontWeight = if (emphasised) FontWeight.Bold else FontWeight.Normal,
        )
    }
    if (subtitle != null) {
        Text(
            subtitle,
            style = MaterialTheme.typography.labelSmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant,
        )
    }
}
