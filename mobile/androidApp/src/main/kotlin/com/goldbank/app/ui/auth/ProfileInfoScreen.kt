package com.goldbank.app.ui.auth

import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import com.goldbank.app.ui.components.ErrorDialog
import com.goldbank.app.ui.components.LoadingButton
import com.goldbank.app.viewmodel.AuthUiState
import com.goldbank.app.viewmodel.AuthViewModel

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun ProfileInfoScreen(
    viewModel: AuthViewModel,
    onProfileUpdated: () -> Unit,
) {
    val uiState by viewModel.uiState.collectAsState()
    var firstName by rememberSaveable { mutableStateOf("") }
    var lastName by rememberSaveable { mutableStateOf("") }
    var nationalId by rememberSaveable { mutableStateOf("") }
    var dateOfBirth by rememberSaveable { mutableStateOf("") }
    var errorMessage by remember { mutableStateOf<String?>(null) }

    var showDatePicker by remember { mutableStateOf(false) }
    val datePickerState = rememberDatePickerState()

    LaunchedEffect(uiState) {
        when (val state = uiState) {
            is AuthUiState.ProfileUpdated -> {
                onProfileUpdated()
                viewModel.resetState()
            }
            is AuthUiState.Error -> {
                errorMessage = state.message
                viewModel.resetState()
            }
            else -> {}
        }
    }

    errorMessage?.let { message ->
        ErrorDialog(
            message = message,
            onDismiss = { errorMessage = null },
        )
    }

    // Display format for user (DD/MM/YYYY), server gets YYYY-MM-DD from dateOfBirth
    val displayDate = remember(dateOfBirth) {
        if (dateOfBirth.matches(Regex("""\d{4}-\d{2}-\d{2}"""))) {
            val parts = dateOfBirth.split("-")
            "${parts[2]}/${parts[1]}/${parts[0]}"
        } else dateOfBirth
    }

    if (showDatePicker) {
        DatePickerDialog(
            onDismissRequest = { showDatePicker = false },
            confirmButton = {
                TextButton(onClick = {
                    datePickerState.selectedDateMillis?.let { millis ->
                        val cal = java.util.Calendar.getInstance().apply { timeInMillis = millis }
                        dateOfBirth = "%04d-%02d-%02d".format(
                            cal.get(java.util.Calendar.YEAR),
                            cal.get(java.util.Calendar.MONTH) + 1,
                            cal.get(java.util.Calendar.DAY_OF_MONTH),
                        )
                    }
                    showDatePicker = false
                }) { Text("OK") }
            },
            dismissButton = {
                TextButton(onClick = { showDatePicker = false }) { Text("Cancel") }
            },
        ) {
            DatePicker(state = datePickerState)
        }
    }

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text("Personal Information") },
            )
        },
    ) { padding ->
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(padding)
                .padding(horizontal = 24.dp),
            horizontalAlignment = Alignment.CenterHorizontally,
        ) {
            Spacer(modifier = Modifier.height(8.dp))

            Text(
                text = "Step 1 of 2",
                style = MaterialTheme.typography.labelMedium,
                color = MaterialTheme.colorScheme.primary,
            )
            Spacer(modifier = Modifier.height(4.dp))
            LinearProgressIndicator(
                progress = { 0.5f },
                modifier = Modifier.fillMaxWidth(),
            )

            Spacer(modifier = Modifier.height(24.dp))

            Text(
                text = "Please provide your details for KYC verification",
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
                textAlign = TextAlign.Center,
            )

            Spacer(modifier = Modifier.height(24.dp))

            OutlinedTextField(
                value = firstName,
                onValueChange = { firstName = it },
                label = { Text("First Name") },
                modifier = Modifier.fillMaxWidth(),
                singleLine = true,
            )
            Spacer(modifier = Modifier.height(12.dp))

            OutlinedTextField(
                value = lastName,
                onValueChange = { lastName = it },
                label = { Text("Last Name") },
                modifier = Modifier.fillMaxWidth(),
                singleLine = true,
            )
            Spacer(modifier = Modifier.height(12.dp))

            OutlinedTextField(
                value = nationalId,
                onValueChange = { nationalId = it },
                label = { Text("National ID") },
                modifier = Modifier.fillMaxWidth(),
                singleLine = true,
            )
            Spacer(modifier = Modifier.height(12.dp))

            Box(modifier = Modifier.fillMaxWidth().clickable { showDatePicker = true }) {
                OutlinedTextField(
                    value = displayDate.ifEmpty { "Select date" },
                    onValueChange = {},
                    readOnly = true,
                    label = { Text("Date of Birth") },
                    modifier = Modifier.fillMaxWidth(),
                    singleLine = true,
                    enabled = false,
                    colors = OutlinedTextFieldDefaults.colors(
                        disabledTextColor = if (dateOfBirth.isNotEmpty())
                            MaterialTheme.colorScheme.onSurface
                        else
                            MaterialTheme.colorScheme.onSurfaceVariant,
                        disabledBorderColor = MaterialTheme.colorScheme.outline,
                        disabledLabelColor = MaterialTheme.colorScheme.onSurfaceVariant,
                    ),
                )
            }

            Spacer(modifier = Modifier.height(32.dp))

            LoadingButton(
                text = "Continue",
                onClick = {
                    viewModel.updateRegistrationProfile(firstName, lastName, nationalId, dateOfBirth)
                },
                isLoading = uiState is AuthUiState.Loading,
                enabled = firstName.isNotBlank() && lastName.isNotBlank()
                    && nationalId.isNotBlank() && dateOfBirth.isNotBlank(),
            )
        }
    }
}
