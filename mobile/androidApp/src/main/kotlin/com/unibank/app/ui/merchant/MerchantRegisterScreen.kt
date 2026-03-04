package com.unibank.app.ui.merchant

import androidx.compose.foundation.layout.*
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import com.unibank.app.viewmodel.MerchantUiState
import com.unibank.app.viewmodel.MerchantViewModel
import com.unibank.shared.domain.model.MerchantAddress

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun MerchantRegisterScreen(
    viewModel: MerchantViewModel,
    onSuccess: () -> Unit,
    onBack: () -> Unit,
) {
    val uiState by viewModel.uiState.collectAsState()

    var businessName by remember { mutableStateOf("") }
    var businessType by remember { mutableStateOf("") }
    var registrationNumber by remember { mutableStateOf("") }
    var taxId by remember { mutableStateOf("") }
    var categoryCode by remember { mutableStateOf("") }
    var line1 by remember { mutableStateOf("") }
    var city by remember { mutableStateOf("") }
    var province by remember { mutableStateOf("") }
    var postalCode by remember { mutableStateOf("") }
    var isAgent by remember { mutableStateOf(false) }

    LaunchedEffect(uiState) {
        if (uiState is MerchantUiState.Registered) {
            onSuccess()
        }
    }

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text("Register Merchant") },
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
                .padding(horizontal = 16.dp)
                .verticalScroll(rememberScrollState()),
            verticalArrangement = Arrangement.spacedBy(12.dp),
        ) {
            Text("Business Details", style = MaterialTheme.typography.titleMedium)

            OutlinedTextField(
                value = businessName,
                onValueChange = { businessName = it },
                label = { Text("Business Name") },
                modifier = Modifier.fillMaxWidth(),
                singleLine = true,
            )
            OutlinedTextField(
                value = businessType,
                onValueChange = { businessType = it },
                label = { Text("Business Type") },
                modifier = Modifier.fillMaxWidth(),
                singleLine = true,
            )
            OutlinedTextField(
                value = registrationNumber,
                onValueChange = { registrationNumber = it },
                label = { Text("Registration Number") },
                modifier = Modifier.fillMaxWidth(),
                singleLine = true,
            )
            OutlinedTextField(
                value = taxId,
                onValueChange = { taxId = it },
                label = { Text("Tax ID") },
                modifier = Modifier.fillMaxWidth(),
                singleLine = true,
            )
            OutlinedTextField(
                value = categoryCode,
                onValueChange = { categoryCode = it },
                label = { Text("Category Code (MCC)") },
                modifier = Modifier.fillMaxWidth(),
                singleLine = true,
            )

            HorizontalDivider()
            Text("Address", style = MaterialTheme.typography.titleMedium)

            OutlinedTextField(
                value = line1,
                onValueChange = { line1 = it },
                label = { Text("Street Address") },
                modifier = Modifier.fillMaxWidth(),
                singleLine = true,
            )
            Row(Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                OutlinedTextField(
                    value = city,
                    onValueChange = { city = it },
                    label = { Text("City") },
                    modifier = Modifier.weight(1f),
                    singleLine = true,
                )
                OutlinedTextField(
                    value = province,
                    onValueChange = { province = it },
                    label = { Text("Province") },
                    modifier = Modifier.weight(1f),
                    singleLine = true,
                )
            }
            OutlinedTextField(
                value = postalCode,
                onValueChange = { postalCode = it },
                label = { Text("Postal Code") },
                modifier = Modifier.fillMaxWidth(),
                singleLine = true,
            )

            Row(verticalAlignment = androidx.compose.ui.Alignment.CenterVertically) {
                Checkbox(checked = isAgent, onCheckedChange = { isAgent = it })
                Text("Register as Agent", style = MaterialTheme.typography.bodyMedium)
            }

            if (uiState is MerchantUiState.Error) {
                Text(
                    (uiState as MerchantUiState.Error).message,
                    color = MaterialTheme.colorScheme.error,
                    style = MaterialTheme.typography.bodySmall,
                )
            }

            Button(
                onClick = {
                    viewModel.register(
                        businessName = businessName,
                        businessType = businessType,
                        registrationNumber = registrationNumber,
                        taxId = taxId,
                        categoryCode = categoryCode,
                        address = MerchantAddress(
                            line1 = line1,
                            line2 = "",
                            city = city,
                            province = province,
                            postalCode = postalCode,
                            countryCode = "ZW",
                        ),
                        isAgent = isAgent,
                    )
                },
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(vertical = 8.dp),
                enabled = businessName.isNotBlank() && businessType.isNotBlank()
                        && uiState !is MerchantUiState.Loading,
            ) {
                if (uiState is MerchantUiState.Loading) {
                    CircularProgressIndicator(
                        modifier = Modifier.size(20.dp),
                        strokeWidth = 2.dp,
                        color = MaterialTheme.colorScheme.onPrimary,
                    )
                } else {
                    Text("Register")
                }
            }

            Spacer(Modifier.height(16.dp))
        }
    }
}
