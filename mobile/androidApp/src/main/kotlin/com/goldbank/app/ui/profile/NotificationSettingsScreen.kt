package com.goldbank.app.ui.profile

import androidx.compose.foundation.layout.*
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import com.goldbank.app.viewmodel.ProfileViewModel

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun NotificationSettingsScreen(
    viewModel: ProfileViewModel,
    onBack: () -> Unit,
) {
    val notificationsEnabled by viewModel.notificationsEnabled.collectAsState()

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text("Notifications") },
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
            Text("Push Notifications", style = MaterialTheme.typography.titleMedium)

            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically,
            ) {
                Column {
                    Text("Enable Notifications", style = MaterialTheme.typography.bodyLarge)
                    Text(
                        "Receive push notifications for transactions",
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                    )
                }
                Switch(
                    checked = notificationsEnabled,
                    onCheckedChange = { viewModel.setNotifications(it) },
                )
            }

            HorizontalDivider()
            Text("Notification Types", style = MaterialTheme.typography.titleMedium)

            NotificationTypeRow("Transaction Alerts", "Get notified for all transactions", true)
            NotificationTypeRow("Security Alerts", "Login attempts and security events", true)
            NotificationTypeRow("Promotions", "Special offers and updates", false)
        }
    }
}

@Composable
private fun NotificationTypeRow(
    title: String,
    subtitle: String,
    defaultEnabled: Boolean,
) {
    var enabled by remember { mutableStateOf(defaultEnabled) }
    Row(
        modifier = Modifier.fillMaxWidth(),
        horizontalArrangement = Arrangement.SpaceBetween,
        verticalAlignment = Alignment.CenterVertically,
    ) {
        Column(Modifier.weight(1f)) {
            Text(title, style = MaterialTheme.typography.bodyLarge)
            Text(
                subtitle,
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
            )
        }
        Switch(checked = enabled, onCheckedChange = { enabled = it })
    }
}
