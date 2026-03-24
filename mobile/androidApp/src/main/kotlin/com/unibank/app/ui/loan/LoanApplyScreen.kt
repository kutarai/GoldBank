package com.unibank.app.ui.loan

import android.Manifest
import android.content.Intent
import android.net.Uri
import android.provider.Settings
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.layout.*
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.CameraAlt
import androidx.compose.material.icons.filled.CheckCircle
import androidx.compose.material.icons.filled.Photo
import androidx.compose.material.icons.filled.Warning
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.unit.dp
import com.google.accompanist.permissions.ExperimentalPermissionsApi
import com.google.accompanist.permissions.PermissionStatus
import com.google.accompanist.permissions.isGranted
import com.google.accompanist.permissions.rememberPermissionState
import com.kashif.cameraK.controller.CameraController
import com.kashif.cameraK.enums.CameraLens
import com.kashif.cameraK.enums.FlashMode
import com.kashif.cameraK.enums.ImageFormat
import com.kashif.cameraK.result.ImageCaptureResult
import com.kashif.cameraK.ui.CameraPreview
import com.unibank.app.ui.components.CurrencyAmountField
import com.unibank.app.ui.components.ErrorDialog
import com.unibank.app.ui.components.LoadingButton
import com.unibank.app.ui.components.PinInput
import com.unibank.app.viewmodel.LoanUiState
import com.unibank.app.viewmodel.LoanViewModel
import com.unibank.shared.domain.util.MoneyFormatter
import kotlinx.coroutines.launch
import kotlin.math.pow

private val tenureOptions = listOf(3, 6, 12, 18, 24)

// Rate is determined by server-side CreditScoringEngine using the configurable rate matrix.
// The mobile app shows the rate from the eligibility check if available, otherwise prompts
// the user to run the eligibility check first.

/**
 * Calculate estimated monthly payment using amortization formula:
 * M = P * [r(1+r)^n] / [(1+r)^n - 1]
 */
private fun calculateMonthlyPayment(principal: Double, annualRate: Double, months: Int): Double {
    val monthlyRate = annualRate / 12.0
    if (monthlyRate == 0.0) return principal / months
    val factor = (1 + monthlyRate).pow(months)
    return principal * (monthlyRate * factor) / (factor - 1)
}

private fun formatAmount(value: Double): String {
    return "%,.2f".format(value)
}

@OptIn(ExperimentalMaterial3Api::class, ExperimentalPermissionsApi::class)
@Composable
fun LoanApplyScreen(
    viewModel: LoanViewModel,
    onSuccess: () -> Unit,
    onBack: () -> Unit,
) {
    val uiState by viewModel.uiState.collectAsState()
    val eligibility by viewModel.eligibility.collectAsState()
    val loanDocVerification by viewModel.loanDocVerification.collectAsState()
    val isVerifyingDoc by viewModel.isVerifyingDoc.collectAsState()
    val docVerificationError by viewModel.docVerificationError.collectAsState()

    // Pre-fill amount from eligibility if available
    var amount by rememberSaveable {
        mutableStateOf(eligibility?.maxAmount?.let {
            it.toDoubleOrNull()?.let { d -> formatAmount(d) } ?: ""
        } ?: "")
    }
    var currency by rememberSaveable { mutableStateOf("ZWG") }
    var tenureMonths by rememberSaveable { mutableIntStateOf(12) }
    var purpose by rememberSaveable { mutableStateOf("") }
    var pin by rememberSaveable { mutableStateOf("") }
    var step by rememberSaveable { mutableIntStateOf(0) } // 0=form, 1=pin, 2=result
    var errorMessage by remember { mutableStateOf<String?>(null) }
    var tenureExpanded by remember { mutableStateOf(false) }

    // Payslip upload state
    var payslipBytes by remember { mutableStateOf<ByteArray?>(null) }
    var showPayslipCamera by rememberSaveable { mutableStateOf(false) }
    var cameraController by remember { mutableStateOf<CameraController?>(null) }
    var showCameraRationaleDialog by remember { mutableStateOf(false) }
    val cameraPermission = rememberPermissionState(Manifest.permission.CAMERA)
    val scope = rememberCoroutineScope()
    val context = LocalContext.current

    // Gallery picker launcher for payslip
    val galleryLauncher = rememberLauncherForActivityResult(
        contract = ActivityResultContracts.GetContent(),
    ) { uri: Uri? ->
        if (uri != null) {
            val bytes = context.contentResolver.openInputStream(uri)?.readBytes()
            if (bytes != null) {
                payslipBytes = bytes
                showPayslipCamera = false
            }
        }
    }

    LaunchedEffect(cameraPermission.status) {
        if (cameraPermission.status is PermissionStatus.Denied &&
            (cameraPermission.status as PermissionStatus.Denied).shouldShowRationale
        ) {
            showCameraRationaleDialog = true
        }
    }

    LaunchedEffect(uiState) {
        when (val state = uiState) {
            is LoanUiState.ApplicationSuccess -> step = 2
            is LoanUiState.Error -> {
                pin = ""
                step = 0
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

    if (showCameraRationaleDialog) {
        AlertDialog(
            onDismissRequest = { showCameraRationaleDialog = false },
            title = { Text("Camera Permission Required") },
            text = { Text("Camera access is needed to capture your payslip.") },
            confirmButton = {
                TextButton(onClick = {
                    showCameraRationaleDialog = false
                    cameraPermission.launchPermissionRequest()
                }) { Text("Grant") }
            },
            dismissButton = {
                TextButton(onClick = { showCameraRationaleDialog = false }) { Text("Cancel") }
            },
        )
    }

    // Camera fullscreen overlay for payslip capture
    if (showPayslipCamera && cameraPermission.status.isGranted) {
        Box(modifier = Modifier.fillMaxSize()) {
            CameraPreview(
                modifier = Modifier.fillMaxSize(),
                cameraConfiguration = {
                    setCameraLens(CameraLens.BACK)
                    setFlashMode(FlashMode.OFF)
                    setImageFormat(ImageFormat.JPEG)
                    setDirectory(com.kashif.cameraK.enums.Directory.PICTURES)
                },
                onCameraControllerReady = { controller ->
                    cameraController = controller
                },
            )

            Text(
                "Position your payslip clearly in frame",
                style = MaterialTheme.typography.bodyMedium,
                color = MaterialTheme.colorScheme.onSurface,
                modifier = Modifier
                    .align(Alignment.TopCenter)
                    .padding(top = 16.dp),
            )

            Row(
                modifier = Modifier
                    .align(Alignment.BottomCenter)
                    .padding(bottom = 32.dp),
                horizontalArrangement = Arrangement.spacedBy(16.dp),
            ) {
                OutlinedButton(onClick = { showPayslipCamera = false }) {
                    Text("Cancel")
                }
                Button(
                    onClick = {
                        scope.launch {
                            cameraController?.let { controller ->
                                when (val result = controller.takePictureToFile()) {
                                    is ImageCaptureResult.SuccessWithFile -> {
                                        payslipBytes = java.io.File(result.filePath).readBytes()
                                        showPayslipCamera = false
                                    }
                                    is ImageCaptureResult.Success -> {
                                        payslipBytes = result.byteArray
                                        showPayslipCamera = false
                                    }
                                    is ImageCaptureResult.Error -> { /* handled silently */ }
                                }
                            }
                        }
                    },
                    modifier = Modifier.size(72.dp),
                    shape = MaterialTheme.shapes.extraLarge,
                ) {
                    Text("Capture", style = MaterialTheme.typography.labelLarge)
                }
            }
        }
        return
    }

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text(if (step == 2) "Loan Result" else "Apply for Loan") },
                navigationIcon = {
                    IconButton(onClick = {
                        when (step) {
                            1 -> { step = 0; pin = "" }
                            2 -> { onSuccess(); viewModel.resetState() }
                            else -> onBack()
                        }
                    }) {
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
            horizontalAlignment = Alignment.CenterHorizontally,
        ) {
            when (step) {
                0 -> {
                    CurrencyAmountField(
                        amount = amount,
                        onAmountChange = { value: String -> amount = value },
                        currency = currency,
                        onCurrencyChange = { code: String -> currency = code },
                        modifier = Modifier.fillMaxWidth(),
                    )
                    Spacer(modifier = Modifier.height(12.dp))

                    ExposedDropdownMenuBox(
                        expanded = tenureExpanded,
                        onExpandedChange = { tenureExpanded = it },
                        modifier = Modifier.fillMaxWidth(),
                    ) {
                        OutlinedTextField(
                            value = "$tenureMonths months",
                            onValueChange = {},
                            readOnly = true,
                            label = { Text("Tenure") },
                            singleLine = true,
                            trailingIcon = { ExposedDropdownMenuDefaults.TrailingIcon(expanded = tenureExpanded) },
                            modifier = Modifier.menuAnchor(MenuAnchorType.PrimaryNotEditable).fillMaxWidth(),
                        )
                        ExposedDropdownMenu(
                            expanded = tenureExpanded,
                            onDismissRequest = { tenureExpanded = false },
                        ) {
                            tenureOptions.forEach { months ->
                                DropdownMenuItem(
                                    text = { Text("$months months") },
                                    onClick = {
                                        tenureMonths = months
                                        tenureExpanded = false
                                    },
                                )
                            }
                        }
                    }
                    Spacer(modifier = Modifier.height(12.dp))

                    OutlinedTextField(
                        value = purpose,
                        onValueChange = { purpose = it },
                        label = { Text("Purpose") },
                        modifier = Modifier.fillMaxWidth(),
                        singleLine = true,
                    )
                    Spacer(modifier = Modifier.height(16.dp))

                    // ── Interest Rate & Monthly Repayment Card ─────────────────
                    val principal = amount.replace(",", "").toDoubleOrNull() ?: 0.0

                    // Use rate from eligibility check (server-side rate matrix)
                    val hasEligibility = eligibility != null
                    val displayRatePercent = eligibility?.let {
                        (it.estimatedRateMin + it.estimatedRateMax) / 2.0
                    }

                    Card(
                        modifier = Modifier.fillMaxWidth(),
                        colors = CardDefaults.cardColors(
                            containerColor = MaterialTheme.colorScheme.secondaryContainer,
                        ),
                    ) {
                        Column(modifier = Modifier.padding(16.dp)) {
                            Text(
                                "Loan Estimate",
                                style = MaterialTheme.typography.titleSmall,
                                color = MaterialTheme.colorScheme.onSecondaryContainer,
                            )
                            Spacer(modifier = Modifier.height(8.dp))
                            if (hasEligibility && displayRatePercent != null) {
                                val annualRate = displayRatePercent / 100.0
                                LoanInfoRow(
                                    "Estimated Interest Rate",
                                    "${"%.1f".format(displayRatePercent)}% — ${"%.1f".format(eligibility!!.estimatedRateMax)}% p.a.",
                                )
                                if (principal > 0) {
                                    val monthlyPayment = calculateMonthlyPayment(principal, annualRate, tenureMonths)
                                    val totalRepayment = monthlyPayment * tenureMonths
                                    val totalInterest = totalRepayment - principal
                                    LoanInfoRow("Monthly Repayment", "$currency ${formatAmount(monthlyPayment)}")
                                    LoanInfoRow("Total Interest", "$currency ${formatAmount(totalInterest)}")
                                    LoanInfoRow("Total Repayment", "$currency ${formatAmount(totalRepayment)}")
                                } else {
                                    Text(
                                        "Enter an amount to see monthly repayment",
                                        style = MaterialTheme.typography.bodySmall,
                                        color = MaterialTheme.colorScheme.onSecondaryContainer.copy(alpha = 0.7f),
                                    )
                                }
                            } else {
                                Text(
                                    "Run an eligibility check first to see your estimated rate and repayment.",
                                    style = MaterialTheme.typography.bodySmall,
                                    color = MaterialTheme.colorScheme.onSecondaryContainer.copy(alpha = 0.7f),
                                )
                            }
                            Spacer(modifier = Modifier.height(4.dp))
                            val rateNote = if (hasEligibility) {
                                "Rate based on your credit score via eligibility assessment"
                            } else {
                                "Actual rate depends on your credit score (18%–36% p.a.)"
                            }
                            Text(
                                rateNote,
                                style = MaterialTheme.typography.labelSmall,
                                color = MaterialTheme.colorScheme.onSecondaryContainer.copy(alpha = 0.7f),
                            )
                        }
                    }

                    Spacer(modifier = Modifier.height(20.dp))

                    // ── Income Verification (Optional) ─────────────────────────
                    HorizontalDivider()
                    Spacer(modifier = Modifier.height(12.dp))
                    Text(
                        "Income Verification (Optional)",
                        style = MaterialTheme.typography.titleSmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                    )
                    Spacer(modifier = Modifier.height(8.dp))
                    Text(
                        "Upload your payslip to speed up approval via AI document verification.",
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                    )
                    Spacer(modifier = Modifier.height(12.dp))

                    // Payslip upload buttons
                    if (payslipBytes == null) {
                        Row(
                            modifier = Modifier.fillMaxWidth(),
                            horizontalArrangement = Arrangement.spacedBy(8.dp),
                        ) {
                            OutlinedButton(
                                onClick = {
                                    if (cameraPermission.status.isGranted) {
                                        showPayslipCamera = true
                                    } else {
                                        cameraPermission.launchPermissionRequest()
                                    }
                                },
                                modifier = Modifier.weight(1f),
                            ) {
                                Icon(
                                    Icons.Filled.CameraAlt,
                                    contentDescription = null,
                                    modifier = Modifier.size(16.dp),
                                )
                                Spacer(modifier = Modifier.width(6.dp))
                                Text("Take Photo")
                            }
                            OutlinedButton(
                                onClick = { galleryLauncher.launch("image/*") },
                                modifier = Modifier.weight(1f),
                            ) {
                                Icon(
                                    Icons.Filled.Photo,
                                    contentDescription = null,
                                    modifier = Modifier.size(16.dp),
                                )
                                Spacer(modifier = Modifier.width(6.dp))
                                Text("From Gallery")
                            }
                        }
                    } else {
                        // Payslip uploaded — show verify / clear options and verification result
                        Card(
                            modifier = Modifier.fillMaxWidth(),
                            colors = CardDefaults.cardColors(
                                containerColor = MaterialTheme.colorScheme.surfaceVariant,
                            ),
                        ) {
                            Column(modifier = Modifier.padding(12.dp)) {
                                Row(
                                    verticalAlignment = Alignment.CenterVertically,
                                    horizontalArrangement = Arrangement.SpaceBetween,
                                    modifier = Modifier.fillMaxWidth(),
                                ) {
                                    Text(
                                        "Payslip selected (${(payslipBytes!!.size / 1024)} KB)",
                                        style = MaterialTheme.typography.bodySmall,
                                    )
                                    TextButton(onClick = {
                                        payslipBytes = null
                                        viewModel.resetDocVerification()
                                    }) {
                                        Text("Remove")
                                    }
                                }

                                // Verification result
                                when {
                                    loanDocVerification != null -> {
                                        val verification = loanDocVerification!!
                                        Spacer(modifier = Modifier.height(8.dp))
                                        HorizontalDivider()
                                        Spacer(modifier = Modifier.height(8.dp))
                                        Text(
                                            "Verification Result",
                                            style = MaterialTheme.typography.labelMedium,
                                        )
                                        Spacer(modifier = Modifier.height(6.dp))
                                        LoanInfoRow("Income", verification.extractedIncome)
                                        LoanInfoRow("Employer", verification.extractedEmployer)
                                        Row(
                                            modifier = Modifier.fillMaxWidth().padding(vertical = 4.dp),
                                            horizontalArrangement = Arrangement.SpaceBetween,
                                            verticalAlignment = Alignment.CenterVertically,
                                        ) {
                                            Text(
                                                "Name Match",
                                                style = MaterialTheme.typography.bodyMedium,
                                                color = MaterialTheme.colorScheme.onSurfaceVariant,
                                            )
                                            Icon(
                                                imageVector = Icons.Filled.CheckCircle,
                                                contentDescription = if (verification.nameMatch) "Matched" else "No match",
                                                tint = if (verification.nameMatch)
                                                    MaterialTheme.colorScheme.primary
                                                else
                                                    MaterialTheme.colorScheme.outline,
                                                modifier = Modifier.size(18.dp),
                                            )
                                        }

                                        // Variance warning
                                        if (verification.incomeVariancePercent > 10.0) {
                                            Spacer(modifier = Modifier.height(6.dp))
                                            Card(
                                                colors = CardDefaults.cardColors(
                                                    containerColor = MaterialTheme.colorScheme.tertiaryContainer,
                                                ),
                                            ) {
                                                Row(
                                                    modifier = Modifier.padding(10.dp),
                                                    verticalAlignment = Alignment.CenterVertically,
                                                    horizontalArrangement = Arrangement.spacedBy(8.dp),
                                                ) {
                                                    Icon(
                                                        imageVector = Icons.Filled.Warning,
                                                        contentDescription = "Warning",
                                                        tint = MaterialTheme.colorScheme.onTertiaryContainer,
                                                        modifier = Modifier.size(18.dp),
                                                    )
                                                    Text(
                                                        "Income declared differs from payslip by ${"%.0f".format(verification.incomeVariancePercent)}%",
                                                        style = MaterialTheme.typography.bodySmall,
                                                        color = MaterialTheme.colorScheme.onTertiaryContainer,
                                                    )
                                                }
                                            }
                                        }
                                    }

                                    docVerificationError != null -> {
                                        Spacer(modifier = Modifier.height(8.dp))
                                        Text(
                                            "Verification failed: $docVerificationError",
                                            style = MaterialTheme.typography.bodySmall,
                                            color = MaterialTheme.colorScheme.error,
                                        )
                                        Spacer(modifier = Modifier.height(8.dp))
                                        LoadingButton(
                                            text = "Retry Verification",
                                            onClick = {
                                                viewModel.verifyLoanDocuments(
                                                    documentBytes = payslipBytes!!,
                                                    declaredIncome = amount,
                                                )
                                            },
                                            isLoading = isVerifyingDoc,
                                            enabled = !isVerifyingDoc,
                                        )
                                    }

                                    else -> {
                                        Spacer(modifier = Modifier.height(8.dp))
                                        LoadingButton(
                                            text = "Verify Payslip",
                                            onClick = {
                                                viewModel.verifyLoanDocuments(
                                                    documentBytes = payslipBytes!!,
                                                    declaredIncome = amount,
                                                )
                                            },
                                            isLoading = isVerifyingDoc,
                                            enabled = !isVerifyingDoc,
                                        )
                                    }
                                }
                            }
                        }
                    }

                    Spacer(modifier = Modifier.height(24.dp))

                    Button(
                        onClick = { step = 1 },
                        modifier = Modifier.fillMaxWidth().height(52.dp),
                        enabled = amount.isNotBlank() && purpose.isNotBlank(),
                    ) { Text("Continue") }
                }

                1 -> {
                    Text("Enter PIN to Confirm", style = MaterialTheme.typography.titleMedium)
                    Spacer(modifier = Modifier.height(8.dp))
                    Text(
                        text = "Loan: ${MoneyFormatter.format(amount, currency)} for $tenureMonths months",
                        style = MaterialTheme.typography.bodyMedium,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                    )
                    Spacer(modifier = Modifier.height(32.dp))
                    PinInput(
                        value = pin,
                        onValueChange = { pin = it },
                        enabled = uiState !is LoanUiState.Loading,
                        onComplete = {
                            viewModel.applyForLoan(amount, currency, tenureMonths, purpose, pin = it)
                        },
                    )
                    Spacer(modifier = Modifier.height(24.dp))
                    LoadingButton(
                        text = "Apply",
                        onClick = {
                            viewModel.applyForLoan(amount, currency, tenureMonths, purpose, pin = pin)
                        },
                        isLoading = uiState is LoanUiState.Loading,
                        enabled = pin.length == 4,
                    )
                }

                2 -> {
                    val result = (uiState as? LoanUiState.ApplicationSuccess)?.result
                    if (result != null) {
                        val isApproved = result.status.name != "REJECTED"

                        Card(
                            modifier = Modifier.fillMaxWidth(),
                            colors = CardDefaults.cardColors(
                                containerColor = if (isApproved)
                                    MaterialTheme.colorScheme.primaryContainer
                                else
                                    MaterialTheme.colorScheme.errorContainer,
                            ),
                        ) {
                            Column(modifier = Modifier.padding(16.dp)) {
                                Text(
                                    text = if (isApproved) "Loan Approved!" else "Loan Rejected",
                                    style = MaterialTheme.typography.headlineSmall,
                                    color = if (isApproved)
                                        MaterialTheme.colorScheme.onPrimaryContainer
                                    else
                                        MaterialTheme.colorScheme.onErrorContainer,
                                )
                                Spacer(modifier = Modifier.height(4.dp))
                                Text(
                                    text = result.message,
                                    style = MaterialTheme.typography.bodyMedium,
                                )
                            }
                        }

                        Spacer(modifier = Modifier.height(16.dp))

                        Card(modifier = Modifier.fillMaxWidth()) {
                            Column(modifier = Modifier.padding(16.dp)) {
                                LoanInfoRow("Reference", result.reference)
                                LoanInfoRow("Principal", MoneyFormatter.format(result.principal.amount, result.principal.currency))
                                LoanInfoRow("Interest Rate", result.interestRate)
                                LoanInfoRow("Monthly Payment", MoneyFormatter.format(result.monthlyPayment.amount, result.monthlyPayment.currency))
                                LoanInfoRow("Tenure", "${result.tenureMonths} months")
                                LoanInfoRow("Credit Score", "${result.creditScore}")
                            }
                        }

                        Spacer(modifier = Modifier.height(24.dp))

                        Button(
                            onClick = { onSuccess(); viewModel.resetState() },
                            modifier = Modifier.fillMaxWidth().height(52.dp),
                        ) { Text("Done") }
                    }
                }
            }
        }
    }
}

@Composable
private fun LoanInfoRow(label: String, value: String) {
    Row(
        modifier = Modifier.fillMaxWidth().padding(vertical = 4.dp),
        horizontalArrangement = Arrangement.SpaceBetween,
    ) {
        Text(text = label, style = MaterialTheme.typography.bodyMedium, color = MaterialTheme.colorScheme.onSurfaceVariant)
        Text(text = value, style = MaterialTheme.typography.bodyMedium)
    }
}
