package com.unibank.app.ui.billpay

import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.Receipt
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import com.unibank.app.viewmodel.BillPayViewModel

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun ProviderListScreen(
    viewModel: BillPayViewModel,
    onProviderSelected: (providerId: String, providerName: String) -> Unit,
    onBack: () -> Unit,
) {
    val providers by viewModel.providers.collectAsState()
    val isLoading by viewModel.providersLoading.collectAsState()
    val error by viewModel.providersError.collectAsState()

    LaunchedEffect(Unit) {
        viewModel.loadProviders()
        viewModel.loadSavedBillers()
    }

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text("Bill Payments") },
                navigationIcon = {
                    IconButton(onClick = onBack) {
                        Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = "Back")
                    }
                },
            )
        },
    ) { padding ->
        when {
            isLoading -> {
                Box(
                    modifier = Modifier.fillMaxSize().padding(padding),
                    contentAlignment = Alignment.Center,
                ) {
                    CircularProgressIndicator()
                }
            }
            error != null -> {
                Box(
                    modifier = Modifier.fillMaxSize().padding(padding),
                    contentAlignment = Alignment.Center,
                ) {
                    Column(horizontalAlignment = Alignment.CenterHorizontally) {
                        Text(
                            error ?: "Failed to load providers",
                            style = MaterialTheme.typography.bodyMedium,
                            color = MaterialTheme.colorScheme.error,
                        )
                        Spacer(modifier = Modifier.height(16.dp))
                        Button(onClick = { viewModel.loadProviders() }) {
                            Text("Retry")
                        }
                    }
                }
            }
            providers.isEmpty() -> {
                Box(
                    modifier = Modifier.fillMaxSize().padding(padding),
                    contentAlignment = Alignment.Center,
                ) {
                    Text("No providers available", style = MaterialTheme.typography.bodyMedium)
                }
            }
            else -> {
                LazyColumn(
                    modifier = Modifier.fillMaxSize().padding(padding),
                ) {
                    items(providers, key = { it.providerId }) { provider ->
                        ListItem(
                            headlineContent = { Text(provider.name) },
                            supportingContent = { Text(provider.category) },
                            leadingContent = {
                                Icon(Icons.Default.Receipt, contentDescription = null)
                            },
                            modifier = Modifier.clickable {
                                onProviderSelected(provider.providerId, provider.name)
                            },
                        )
                        HorizontalDivider()
                    }
                }
            }
        }
    }
}
