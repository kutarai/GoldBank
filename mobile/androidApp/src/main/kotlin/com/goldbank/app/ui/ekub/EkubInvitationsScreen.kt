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
import androidx.compose.material3.Card
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.Button
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.material3.TopAppBar
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.ArrowBack
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import com.goldbank.app.viewmodel.EkubViewModel

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun EkubInvitationsScreen(
    viewModel: EkubViewModel,
    onBack: () -> Unit,
) {
    val state by viewModel.uiState.collectAsState()
    LaunchedEffect(Unit) { viewModel.loadInvitations() }

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text("Invitations") },
                navigationIcon = {
                    IconButton(onClick = onBack) { Icon(Icons.Default.ArrowBack, contentDescription = "Back") }
                },
            )
        },
    ) { padding ->
        Column(modifier = Modifier.fillMaxSize().padding(padding).padding(16.dp)) {
            if (state.invitations.isEmpty()) {
                Box(modifier = Modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
                    Text(
                        "No pending invitations.",
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                    )
                }
            } else {
                LazyColumn(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                    items(state.invitations, key = { it.id }) { inv ->
                        Card(modifier = Modifier.fillMaxWidth()) {
                            Column(modifier = Modifier.padding(14.dp)) {
                                Text(inv.groupName, style = MaterialTheme.typography.titleMedium)
                                Spacer(Modifier.height(2.dp))
                                Text(
                                    "Invited by ${inv.inviterCustomerId.take(8)}…",
                                    style = MaterialTheme.typography.bodySmall,
                                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                                )
                                Spacer(Modifier.height(10.dp))
                                Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                                    Button(
                                        onClick = { viewModel.respondToInvitation(inv.id, accept = true) },
                                        enabled = !state.isLoading,
                                        modifier = Modifier.weight(1f),
                                    ) { Text("Accept") }
                                    OutlinedButton(
                                        onClick = { viewModel.respondToInvitation(inv.id, accept = false) },
                                        enabled = !state.isLoading,
                                        modifier = Modifier.weight(1f),
                                    ) { Text("Decline") }
                                }
                            }
                        }
                    }
                }
            }
            state.error?.let {
                Spacer(Modifier.height(8.dp))
                Text(it, color = MaterialTheme.colorScheme.error)
            }
            state.flash?.let {
                Spacer(Modifier.height(8.dp))
                Text(it, color = MaterialTheme.colorScheme.primary)
            }
        }
    }
}
