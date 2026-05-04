package com.goldbank.app.ui.ekub

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material3.Badge
import androidx.compose.material3.BadgedBox
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.TopAppBar
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.ArrowBack
import androidx.compose.material.icons.filled.Mail
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
fun EkubGroupListScreen(
    viewModel: EkubViewModel,
    onBack: () -> Unit,
    onCreateGroup: () -> Unit,
    onInvitations: () -> Unit,
    onOpenGroup: (groupId: String) -> Unit,
) {
    val state by viewModel.uiState.collectAsState()

    LaunchedEffect(Unit) { viewModel.loadHome() }

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text("Ekub") },
                navigationIcon = {
                    IconButton(onClick = onBack) {
                        Icon(Icons.Default.ArrowBack, contentDescription = "Back")
                    }
                },
                actions = {
                    BadgedBox(badge = {
                        if (state.invitations.isNotEmpty()) {
                            Badge { Text(state.invitations.size.toString()) }
                        }
                    }) {
                        IconButton(onClick = onInvitations) {
                            Icon(Icons.Default.Mail, contentDescription = "Invitations")
                        }
                    }
                },
            )
        },
    ) { padding ->
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(padding)
                .padding(horizontal = 16.dp),
        ) {
            Spacer(Modifier.height(8.dp))
            Text(
                "Group savings + lending",
                style = MaterialTheme.typography.titleMedium,
            )
            Text(
                "Save together. Borrow against the pot. Interest grows everyone's share.",
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
            Spacer(Modifier.height(12.dp))

            Button(onClick = onCreateGroup, modifier = Modifier.fillMaxWidth()) {
                Text("Create new group")
            }

            Spacer(Modifier.height(12.dp))

            if (state.groups.isEmpty()) {
                Box(modifier = Modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
                    Text(
                        "You're not in any Ekub groups yet.\nCreate one or accept an invitation.",
                        textAlign = androidx.compose.ui.text.style.TextAlign.Center,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                    )
                }
            } else {
                LazyColumn(verticalArrangement = Arrangement.spacedBy(8.dp)) {
                    items(state.groups, key = { it.id }) { group ->
                        Card(
                            onClick = { onOpenGroup(group.id) },
                            modifier = Modifier.fillMaxWidth(),
                        ) {
                            Column(modifier = Modifier.padding(14.dp)) {
                                Text(group.name, style = MaterialTheme.typography.titleMedium)
                                Spacer(Modifier.height(2.dp))
                                Text(
                                    "${group.status} · ${group.activeMemberCount} members · ${group.currency} ${group.monthlyContribution}/mo",
                                    style = MaterialTheme.typography.bodySmall,
                                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                                )
                                if (group.description.isNotEmpty()) {
                                    Spacer(Modifier.height(6.dp))
                                    Text(
                                        group.description,
                                        style = MaterialTheme.typography.bodyMedium,
                                    )
                                }
                            }
                        }
                    }
                }
            }

            state.error?.let {
                Spacer(Modifier.height(8.dp))
                Text(it, color = MaterialTheme.colorScheme.error)
                TextButton(onClick = viewModel::clearError) { Text("Dismiss") }
            }
        }
    }
}
