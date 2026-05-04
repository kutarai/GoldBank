package com.goldbank.app.ui.ekub

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.Button
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.FilterChip
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Switch
import androidx.compose.material3.Text
import androidx.compose.material3.TopAppBar
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.ArrowBack
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.ui.unit.dp
import com.goldbank.app.viewmodel.EkubViewModel

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun CreateEkubGroupScreen(
    viewModel: EkubViewModel,
    onBack: () -> Unit,
    onCreated: (groupId: String) -> Unit,
) {
    val state by viewModel.uiState.collectAsState()

    var name by rememberSaveable { mutableStateOf("") }
    var description by rememberSaveable { mutableStateOf("") }
    var currency by rememberSaveable { mutableStateOf("USD") }
    var monthly by rememberSaveable { mutableStateOf("") }
    var rate by rememberSaveable { mutableStateOf("8.0") }
    var applyInterestOnContributions by rememberSaveable { mutableStateOf(true) }

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text("New Ekub group") },
                navigationIcon = {
                    IconButton(onClick = onBack) { Icon(Icons.Default.ArrowBack, contentDescription = "Back") }
                },
            )
        },
    ) { padding ->
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(padding)
                .padding(16.dp),
            verticalArrangement = Arrangement.spacedBy(12.dp),
        ) {
            OutlinedTextField(
                value = name,
                onValueChange = { name = it },
                label = { Text("Group name") },
                singleLine = true,
                modifier = Modifier.fillMaxWidth(),
            )

            OutlinedTextField(
                value = description,
                onValueChange = { description = it },
                label = { Text("Description (optional)") },
                modifier = Modifier.fillMaxWidth(),
                minLines = 2,
            )

            Text("Currency", style = MaterialTheme.typography.labelLarge)
            Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                FilterChip(
                    selected = currency == "ZWG",
                    onClick = { currency = "ZWG" },
                    label = { Text("ZWG") },
                )
                FilterChip(
                    selected = currency == "USD",
                    onClick = { currency = "USD" },
                    label = { Text("USD") },
                )
            }

            OutlinedTextField(
                value = monthly,
                onValueChange = { monthly = it.filter { ch -> ch.isDigit() || ch == '.' } },
                label = { Text("Monthly contribution ($currency)") },
                singleLine = true,
                keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Decimal, imeAction = ImeAction.Next),
                modifier = Modifier.fillMaxWidth(),
            )

            OutlinedTextField(
                value = rate,
                onValueChange = { rate = it.filter { ch -> ch.isDigit() || ch == '.' } },
                label = { Text("Loan interest rate (% per year)") },
                singleLine = true,
                keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Decimal, imeAction = ImeAction.Done),
                modifier = Modifier.fillMaxWidth(),
            )

            Row(
                modifier = Modifier.fillMaxWidth(),
                verticalAlignment = androidx.compose.ui.Alignment.CenterVertically,
            ) {
                Column(modifier = Modifier.weight(1f)) {
                    Text("Charge interest on member contributions", style = MaterialTheme.typography.bodyMedium)
                    Text(
                        if (applyInterestOnContributions)
                            "Interest applies to the full loan amount."
                        else
                            "Borrowers pay interest only on the portion of a loan above their own contributions.",
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                    )
                }
                Switch(
                    checked = applyInterestOnContributions,
                    onCheckedChange = { applyInterestOnContributions = it },
                )
            }

            Spacer(Modifier.height(8.dp))

            Button(
                onClick = {
                    viewModel.createGroup(
                        name = name.trim(),
                        description = description.trim(),
                        currency = currency,
                        monthlyContribution = monthly.ifBlank { "0" },
                        interestRate = rate.ifBlank { "0" },
                        applyInterestOnContributions = applyInterestOnContributions,
                        onCreated = { onCreated(it.id) },
                    )
                },
                enabled = name.isNotBlank() && monthly.isNotBlank() && !state.isLoading,
                modifier = Modifier.fillMaxWidth(),
            ) {
                Text(if (state.isLoading) "Creating…" else "Create group")
            }

            state.error?.let {
                Text(it, color = MaterialTheme.colorScheme.error)
            }
            Text(
                "You'll be the chairman. Invite at least 2 more members to activate the group.",
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
        }
    }
}
