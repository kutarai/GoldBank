package com.goldbank.app.navigation

import androidx.biometric.BiometricManager
import androidx.compose.animation.AnimatedVisibility
import androidx.compose.animation.slideInVertically
import androidx.compose.animation.slideOutVertically
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.unit.dp
import androidx.navigation.compose.NavHost
import androidx.navigation.compose.composable
import androidx.navigation.compose.rememberNavController
import androidx.navigation.toRoute
import com.goldbank.app.ui.chat.ChatScreen
import com.goldbank.app.ui.components.ChatFAB
import com.goldbank.app.viewmodel.ChatViewModel
import com.goldbank.app.ui.auth.BiometricPromptScreen
import com.goldbank.app.ui.auth.CreatePinScreen
import com.goldbank.app.ui.auth.LoginScreen
import com.goldbank.app.ui.auth.OtpScreen
import com.goldbank.app.ui.auth.ProfileInfoScreen
import com.goldbank.app.ui.auth.RegisterScreen
import com.goldbank.app.ui.auth.RegistrationIdUploadScreen
import com.goldbank.app.ui.auth.RegistrationSelfieScreen
import com.goldbank.app.ui.auth.SessionLockScreen
import com.goldbank.app.viewmodel.SecurityState
import com.goldbank.app.viewmodel.SecurityViewModel
import com.goldbank.app.ui.agent.CashInScreen
import com.goldbank.app.ui.agent.CashOutScreen
import com.goldbank.app.ui.billpay.PayBillScreen
import com.goldbank.app.ui.billpay.ProviderListScreen
import com.goldbank.app.ui.home.HomeScreen
import com.goldbank.app.ui.loan.LoanApplyScreen
import com.goldbank.app.ui.loan.LoanDetailScreen
import com.goldbank.app.ui.loan.LoanListScreen
import com.goldbank.app.ui.home.TransactionDetailScreen
import com.goldbank.app.ui.home.TransactionListScreen
import com.goldbank.app.ui.kyc.DocumentUploadScreen
import com.goldbank.app.ui.kyc.KycDashboardScreen
import com.goldbank.app.ui.kyc.KycVerificationResultScreen
import com.goldbank.app.ui.kyc.ProofOfAddressScreen
import com.goldbank.app.ui.kyc.SelfieScreen
import com.goldbank.app.ui.asset.AssetDetailScreen
import com.goldbank.app.ui.asset.AssetListScreen
import com.goldbank.app.ui.asset.AssetRegisterScreen
import com.goldbank.app.ui.dispute.DisputeDetailScreen
import com.goldbank.app.ui.ekub.CreateEkubGroupScreen
import com.goldbank.app.ui.ekub.EkubGroupDetailScreen
import com.goldbank.app.ui.ekub.EkubGroupListScreen
import com.goldbank.app.ui.ekub.EkubInvitationsScreen
import com.goldbank.app.ui.dispute.DisputeListScreen
import com.goldbank.app.ui.dispute.DisputeWizardScreen
import com.goldbank.app.ui.fraud.FraudAlertDetailScreen
import com.goldbank.app.ui.fraud.FraudAlertListScreen
import com.goldbank.app.ui.notification.NotificationScreen
import com.goldbank.app.ui.scan.BillScanScreen
import com.goldbank.app.ui.scan.ChequeScanScreen
import com.goldbank.app.ui.scan.ReceiptScanScreen
import com.goldbank.app.viewmodel.DisputeViewModel
import com.goldbank.app.viewmodel.DocumentScanViewModel
import com.goldbank.app.viewmodel.FraudAlertViewModel
import com.goldbank.app.ui.merchant.MerchantCommissionScreen
import com.goldbank.app.ui.profile.DeviceTransferScreen
import com.goldbank.app.ui.profile.EditProfileScreen
import com.goldbank.app.ui.profile.NotificationSettingsScreen
import com.goldbank.app.ui.profile.ProfileScreen
import com.goldbank.app.ui.profile.SecuritySettingsScreen
import com.goldbank.app.ui.profile.SettingsScreen
import com.goldbank.app.ui.merchant.MerchantDashboardScreen
import com.goldbank.app.ui.merchant.MerchantRegisterScreen
import com.goldbank.app.ui.merchant.MerchantSettlementsScreen
import com.goldbank.app.ui.merchant.MerchantTransactionsScreen
import com.goldbank.app.ui.payment.NfcPaymentScreen
import com.goldbank.app.ui.payment.QrGenerateScreen
import com.goldbank.app.ui.payment.QrScanScreen
import com.goldbank.app.ui.transfer.P2PTransferScreen
import com.goldbank.app.viewmodel.*
import com.goldbank.shared.data.local.SessionManager
import com.goldbank.shared.domain.model.SessionState
import org.koin.compose.koinInject
import org.koin.compose.viewmodel.koinViewModel

@Composable
fun AppNavGraph(modifier: Modifier = Modifier) {
    val sessionManager: SessionManager = koinInject()
    val sessionState by sessionManager.sessionState.collectAsState()

    when (sessionState) {
        is SessionState.Loading -> {
            Box(modifier = Modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
                CircularProgressIndicator()
            }
        }
        is SessionState.Unauthenticated -> {
            AuthNavHost(modifier = modifier, startAtLogin = false)
        }
        is SessionState.PinRequired -> {
            AuthNavHost(modifier = modifier, startAtLogin = true)
        }
        is SessionState.Authenticated -> {
            val securityViewModel: SecurityViewModel = koinViewModel()
            val securityState by securityViewModel.uiState.collectAsState()
            val biometricManager = BiometricManager.from(LocalContext.current)
            val biometricAvailable = biometricManager.canAuthenticate(
                BiometricManager.Authenticators.BIOMETRIC_STRONG
            ) == BiometricManager.BIOMETRIC_SUCCESS

            LaunchedEffect(biometricAvailable) {
                securityViewModel.checkLockState(biometricAvailable)
            }

            when (securityState.securityState) {
                SecurityState.Loading -> {
                    Box(Modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
                        CircularProgressIndicator()
                    }
                }
                SecurityState.BiometricRequired -> {
                    BiometricPromptScreen(
                        viewModel = securityViewModel,
                        onFallbackToPin = {
                            // Force to PIN by failing biometric 3 times
                            repeat(3) { securityViewModel.onBiometricFailed() }
                        },
                    )
                }
                SecurityState.PinRequired -> {
                    SessionLockScreen(
                        viewModel = securityViewModel,
                        onVerifyPin = { pin ->
                            securityViewModel.onPinVerified()
                        },
                        onBiometricRetry = if (biometricAvailable && securityState.biometricEnabled) {
                            { securityViewModel.checkLockState(true) }
                        } else null,
                    )
                }
                SecurityState.Unlocked -> {
                    MainNavHost(modifier = modifier)
                }
            }
        }
    }
}

@Composable
private fun AuthNavHost(modifier: Modifier, startAtLogin: Boolean) {
    val navController = rememberNavController()
    val authViewModel: AuthViewModel = koinViewModel()
    val sessionManager: SessionManager = koinInject()

    NavHost(
        navController = navController,
        startDestination = if (startAtLogin) Route.Login else Route.Register,
        modifier = modifier,
    ) {
        composable<Route.Register> {
            RegisterScreen(
                viewModel = authViewModel,
                onOtpSent = { registrationId, otpLength, ttlSeconds ->
                    navController.navigate(Route.Otp(registrationId, otpLength, ttlSeconds))
                },
                onLoginClick = { navController.navigate(Route.Login) },
            )
        }
        composable<Route.Otp> { backStackEntry ->
            val route = backStackEntry.toRoute<Route.Otp>()
            OtpScreen(
                viewModel = authViewModel,
                otpLength = route.otpLength,
                ttlSeconds = route.ttlSeconds,
                onVerified = { accountId ->
                    navController.navigate(Route.CreatePin(accountId)) {
                        popUpTo(Route.Register) { inclusive = false }
                    }
                },
                onBack = { navController.popBackStack() },
            )
        }
        composable<Route.CreatePin> { backStackEntry ->
            val route = backStackEntry.toRoute<Route.CreatePin>()
            CreatePinScreen(
                viewModel = authViewModel,
                accountId = route.accountId,
                onAuthenticated = {
                    navController.navigate(Route.RegistrationProfile(route.accountId)) {
                        popUpTo(Route.Register) { inclusive = true }
                    }
                },
                onBack = { navController.popBackStack() },
            )
        }
        composable<Route.RegistrationProfile> { backStackEntry ->
            val route = backStackEntry.toRoute<Route.RegistrationProfile>()
            ProfileInfoScreen(
                viewModel = authViewModel,
                onProfileUpdated = {
                    navController.navigate(Route.RegistrationIdUpload(route.accountId)) {
                        popUpTo(Route.RegistrationProfile(route.accountId)) { inclusive = true }
                    }
                },
            )
        }
        composable<Route.RegistrationIdUpload> { backStackEntry ->
            val route = backStackEntry.toRoute<Route.RegistrationIdUpload>()
            RegistrationIdUploadScreen(
                viewModel = authViewModel,
                onSuccess = {
                    navController.navigate(Route.RegistrationSelfie(route.accountId)) {
                        popUpTo(Route.RegistrationIdUpload(route.accountId)) { inclusive = true }
                    }
                },
                onBack = { navController.popBackStack() },
            )
        }
        composable<Route.RegistrationSelfie> {
            RegistrationSelfieScreen(
                viewModel = authViewModel,
                onComplete = {
                    // After selfie, session is cleared — navigate to login
                    navController.navigate(Route.Login) {
                        popUpTo(Route.Register) { inclusive = true }
                    }
                },
                onBack = { navController.popBackStack() },
            )
        }
        composable<Route.Login> {
            val storedPhone = sessionManager.getPhoneNumber()
            LoginScreen(
                viewModel = authViewModel,
                onAuthenticated = {
                    // SessionManager will switch state to Authenticated
                },
                onRegisterClick = {
                    navController.navigate(Route.Register) {
                        popUpTo(Route.Login) { inclusive = true }
                    }
                },
                showPhoneField = storedPhone.isNullOrBlank(),
                initialPhoneNumber = storedPhone.orEmpty(),
            )
        }
    }
}

@Composable
private fun MainNavHost(modifier: Modifier) {
    val navController = rememberNavController()
    var showChat by remember { mutableStateOf(false) }
    val chatViewModel: ChatViewModel = org.koin.compose.viewmodel.koinViewModel()

    Box(modifier = modifier.fillMaxSize()) {
        NavHost(
            navController = navController,
            startDestination = Route.Home,
            modifier = Modifier.fillMaxSize(),
        ) {
        // Home + Transactions (Phase 3)
        composable<Route.Home> {
            val homeViewModel: HomeViewModel = koinViewModel()
            HomeScreen(
                viewModel = homeViewModel,
                onTransactionClick = { txnId ->
                    navController.navigate(Route.TransactionDetail(txnId))
                },
                onViewAllTransactions = {
                    navController.navigate(Route.TransactionList)
                },
                onQuickAction = { routeKey ->
                    when (routeKey) {
                        "p2p_transfer" -> navController.navigate(Route.P2PTransfer)
                        "qr_scan" -> navController.navigate(Route.QrScan)
                        "qr_generate" -> navController.navigate(Route.QrGenerate)
                        "nfc_payment" -> navController.navigate(Route.NfcPayment)
                        "bill_pay" -> navController.navigate(Route.ProviderList)
                        "cash_in" -> navController.navigate(Route.CashIn)
                        "cash_out" -> navController.navigate(Route.CashOut)
                        "loan" -> navController.navigate(Route.LoanList)
                        "cheque_deposit" -> navController.navigate(Route.ChequeScan)
                        "assets" -> navController.navigate(Route.AssetList)
                        "ekub" -> navController.navigate(Route.EkubGroupList)
                    }
                },
                onProfileClick = { navController.navigate(Route.Profile) },
                onNotificationsClick = { navController.navigate(Route.Notifications) },
                onAssetsClick = { navController.navigate(Route.AssetList) },
                onLogout = { homeViewModel.logout() },
            )
        }
        composable<Route.TransactionList> {
            val homeViewModel: HomeViewModel = koinViewModel()
            TransactionListScreen(
                viewModel = homeViewModel,
                onTransactionClick = { txnId ->
                    navController.navigate(Route.TransactionDetail(txnId))
                },
                onBack = { navController.popBackStack() },
            )
        }
        composable<Route.TransactionDetail> { backStackEntry ->
            val homeViewModel: HomeViewModel = koinViewModel()
            val route = backStackEntry.toRoute<Route.TransactionDetail>()
            TransactionDetailScreen(
                viewModel = homeViewModel,
                transactionId = route.transactionId,
                onBack = { navController.popBackStack() },
                onDispute = { txnId -> navController.navigate(Route.DisputeWizard(txnId)) },
                onAttachReceipt = { /* Sprint 18 */ },
            )
        }

        // QR Payments (Phase 4)
        composable<Route.QrGenerate> {
            val paymentViewModel: PaymentViewModel = koinViewModel()
            QrGenerateScreen(viewModel = paymentViewModel, onBack = { navController.popBackStack() })
        }
        composable<Route.QrScan> {
            val paymentViewModel: PaymentViewModel = koinViewModel()
            QrScanScreen(
                viewModel = paymentViewModel,
                onPaymentComplete = { navController.popBackStack() },
                onBack = { navController.popBackStack() },
            )
        }

        // P2P Transfer (Phase 5)
        composable<Route.P2PTransfer> {
            val transferViewModel: TransferViewModel = koinViewModel()
            P2PTransferScreen(
                viewModel = transferViewModel,
                onSuccess = { navController.popBackStack() },
                onBack = { navController.popBackStack() },
            )
        }

        // Bill Pay (Phase 6)
        composable<Route.ProviderList> {
            val billPayViewModel: BillPayViewModel = koinViewModel()
            ProviderListScreen(
                viewModel = billPayViewModel,
                onProviderSelected = { id, name ->
                    navController.navigate(Route.PayBill(id, name))
                },
                onBack = { navController.popBackStack() },
            )
        }
        composable<Route.PayBill> { backStackEntry ->
            val billPayViewModel: BillPayViewModel = koinViewModel()
            val route = backStackEntry.toRoute<Route.PayBill>()
            val savedStateHandle = backStackEntry.savedStateHandle
            val prefilledProvider by savedStateHandle.getStateFlow<String?>("prefilledProvider", null).collectAsState()
            val prefilledAccountNumber by savedStateHandle.getStateFlow<String?>("prefilledAccountNumber", null).collectAsState()
            val prefilledAmount by savedStateHandle.getStateFlow<String?>("prefilledAmount", null).collectAsState()
            PayBillScreen(
                viewModel = billPayViewModel,
                providerId = route.providerId,
                providerName = route.providerName,
                onSuccess = { navController.popBackStack() },
                onBack = { navController.popBackStack() },
                onScanBill = { navController.navigate(Route.BillScan) },
                prefilledProvider = prefilledProvider,
                prefilledAccountNumber = prefilledAccountNumber,
                prefilledAmount = prefilledAmount,
            )
        }

        // Agent (Phase 7)
        composable<Route.CashIn> {
            val agentViewModel: AgentViewModel = koinViewModel()
            CashInScreen(
                viewModel = agentViewModel,
                onSuccess = { navController.popBackStack() },
                onBack = { navController.popBackStack() },
            )
        }
        composable<Route.CashOut> {
            val agentViewModel: AgentViewModel = koinViewModel()
            CashOutScreen(
                viewModel = agentViewModel,
                onSuccess = { navController.popBackStack() },
                onBack = { navController.popBackStack() },
            )
        }

        // Loans
        composable<Route.LoanList> {
            val loanViewModel: LoanViewModel = koinViewModel()
            LoanListScreen(
                viewModel = loanViewModel,
                onApply = { navController.navigate(Route.LoanApply) },
                onLoanClick = { loanId -> navController.navigate(Route.LoanDetail(loanId)) },
                onBack = { navController.popBackStack() },
            )
        }
        composable<Route.LoanApply> {
            val loanViewModel: LoanViewModel = koinViewModel()
            val assetViewModel: AssetViewModel = koinViewModel()
            LoanApplyScreen(
                viewModel = loanViewModel,
                assetViewModel = assetViewModel,
                onSuccess = { navController.popBackStack() },
                onBack = { navController.popBackStack() },
            )
        }
        composable<Route.LoanDetail> { backStackEntry ->
            val loanViewModel: LoanViewModel = koinViewModel()
            val route = backStackEntry.toRoute<Route.LoanDetail>()
            LoanDetailScreen(
                viewModel = loanViewModel,
                loanId = route.loanId,
                onBack = { navController.popBackStack() },
            )
        }

        // KYC (Phase 8)
        composable<Route.KycDashboard> {
            val kycViewModel: KycViewModel = koinViewModel()
            KycDashboardScreen(
                viewModel = kycViewModel,
                onUploadDocument = { docType ->
                    navController.navigate(Route.DocumentUpload(docType))
                },
                onTakeSelfie = { navController.navigate(Route.Selfie) },
                onBack = { navController.popBackStack() },
            )
        }
        composable<Route.DocumentUpload> { backStackEntry ->
            val kycViewModel: KycViewModel = koinViewModel()
            val route = backStackEntry.toRoute<Route.DocumentUpload>()
            DocumentUploadScreen(
                viewModel = kycViewModel,
                documentType = route.documentType,
                onSuccess = { navController.popBackStack() },
                onBack = { navController.popBackStack() },
            )
        }
        composable<Route.Selfie> {
            val kycViewModel: KycViewModel = koinViewModel()
            SelfieScreen(
                viewModel = kycViewModel,
                onSuccess = { navController.popBackStack() },
                onBack = { navController.popBackStack() },
                onVerificationComplete = { accountId ->
                    navController.navigate(Route.KycVerificationResult(accountId))
                },
            )
        }

        // NFC Payment (Phase 9)
        composable<Route.NfcPayment> {
            val paymentViewModel: PaymentViewModel = koinViewModel()
            NfcPaymentScreen(
                viewModel = paymentViewModel,
                onPaymentComplete = { navController.popBackStack() },
                onBack = { navController.popBackStack() },
            )
        }

        // Merchant (Phase 10)
        composable<Route.MerchantRegister> {
            val merchantViewModel: MerchantViewModel = koinViewModel()
            MerchantRegisterScreen(
                viewModel = merchantViewModel,
                onSuccess = {
                    navController.navigate(Route.MerchantDashboard) {
                        popUpTo(Route.MerchantRegister) { inclusive = true }
                    }
                },
                onBack = { navController.popBackStack() },
            )
        }
        composable<Route.MerchantDashboard> {
            val merchantViewModel: MerchantViewModel = koinViewModel()
            MerchantDashboardScreen(
                viewModel = merchantViewModel,
                merchantId = "",
                onTransactions = { navController.navigate(Route.MerchantTransactions) },
                onSettlements = { navController.navigate(Route.MerchantSettlements) },
                onCommission = { navController.navigate(Route.MerchantCommission) },
                onBack = { navController.popBackStack() },
            )
        }
        composable<Route.MerchantTransactions> {
            val merchantViewModel: MerchantViewModel = koinViewModel()
            MerchantTransactionsScreen(
                viewModel = merchantViewModel,
                onBack = { navController.popBackStack() },
            )
        }
        composable<Route.MerchantSettlements> {
            val merchantViewModel: MerchantViewModel = koinViewModel()
            MerchantSettlementsScreen(
                viewModel = merchantViewModel,
                onBack = { navController.popBackStack() },
            )
        }
        composable<Route.MerchantCommission> {
            val merchantViewModel: MerchantViewModel = koinViewModel()
            MerchantCommissionScreen(
                viewModel = merchantViewModel,
                onBack = { navController.popBackStack() },
            )
        }

        // Profile (Phase 11)
        composable<Route.Profile> {
            val profileViewModel: ProfileViewModel = koinViewModel()
            ProfileScreen(
                viewModel = profileViewModel,
                onEditProfile = { navController.navigate(Route.EditProfile) },
                onSettings = { navController.navigate(Route.Settings) },
                onNotifications = { navController.navigate(Route.NotificationSettings) },
                onDeviceTransfer = { navController.navigate(Route.DeviceTransfer) },
                onSecurityClick = { navController.navigate(Route.SecuritySettings) },
                onBack = { navController.popBackStack() },
            )
        }
        composable<Route.EditProfile> {
            val profileViewModel: ProfileViewModel = koinViewModel()
            EditProfileScreen(
                viewModel = profileViewModel,
                onSuccess = { navController.popBackStack() },
                onBack = { navController.popBackStack() },
            )
        }
        composable<Route.Settings> {
            val profileViewModel: ProfileViewModel = koinViewModel()
            SettingsScreen(
                viewModel = profileViewModel,
                onBack = { navController.popBackStack() },
            )
        }
        composable<Route.NotificationSettings> {
            val profileViewModel: ProfileViewModel = koinViewModel()
            NotificationSettingsScreen(
                viewModel = profileViewModel,
                onBack = { navController.popBackStack() },
            )
        }
        composable<Route.DeviceTransfer> {
            val profileViewModel: ProfileViewModel = koinViewModel()
            DeviceTransferScreen(
                viewModel = profileViewModel,
                onComplete = { navController.popBackStack() },
                onBack = { navController.popBackStack() },
            )
        }

        // Security Settings (Sprint 15)
        composable<Route.SecuritySettings> {
            val securityViewModel: SecurityViewModel = koinViewModel()
            SecuritySettingsScreen(
                viewModel = securityViewModel,
                onBack = { navController.popBackStack() },
            )
        }

        // KYC result + proof of address (Sprint 16)
        composable<Route.KycVerificationResult> { backStackEntry ->
            val route = backStackEntry.toRoute<Route.KycVerificationResult>()
            val kycViewModel: KycViewModel = koinViewModel()
            KycVerificationResultScreen(
                viewModel = kycViewModel,
                accountId = route.accountId,
                onHome = { navController.navigate(Route.Home) { popUpTo(Route.Home) { inclusive = true } } },
                onBack = { navController.popBackStack() },
            )
        }

        composable<Route.ProofOfAddress> {
            val kycViewModel: KycViewModel = koinViewModel()
            ProofOfAddressScreen(
                viewModel = kycViewModel,
                onSuccess = { navController.popBackStack() },
                onBack = { navController.popBackStack() },
            )
        }

        // Document scanning (Sprint 16)
        composable<Route.ChequeScan> {
            val docScanViewModel: DocumentScanViewModel = koinViewModel()
            ChequeScanScreen(
                viewModel = docScanViewModel,
                onDepositComplete = { navController.navigate(Route.Home) { popUpTo(Route.Home) { inclusive = true } } },
                onBack = { navController.popBackStack() },
            )
        }

        composable<Route.BillScan> {
            val docScanViewModel: DocumentScanViewModel = koinViewModel()
            BillScanScreen(
                viewModel = docScanViewModel,
                onFieldsExtracted = { billFields ->
                    navController.previousBackStackEntry?.savedStateHandle?.apply {
                        set("prefilledProvider", billFields.provider)
                        set("prefilledAccountNumber", billFields.accountNumber)
                        set("prefilledAmount", billFields.amount)
                    }
                    navController.popBackStack()
                },
                onBack = { navController.popBackStack() },
            )
        }

        composable<Route.ReceiptScan> {
            val docScanViewModel: DocumentScanViewModel = koinViewModel()
            ReceiptScanScreen(
                viewModel = docScanViewModel,
                onFieldsExtracted = { _ ->
                    navController.popBackStack()
                },
                onBack = { navController.popBackStack() },
            )
        }

        // Notifications (Sprint 18)
        composable<Route.Notifications> {
            NotificationScreen(
                onNotificationClick = { _, _ -> },
                onBack = { navController.popBackStack() },
            )
        }

        // Disputes (Sprint 17)
        composable<Route.DisputeWizard> { backStackEntry ->
            val route = backStackEntry.toRoute<Route.DisputeWizard>()
            val disputeViewModel: DisputeViewModel = koinViewModel()
            DisputeWizardScreen(
                viewModel = disputeViewModel,
                transactionId = route.transactionId,
                onComplete = { navController.popBackStack() },
                onBack = { navController.popBackStack() },
            )
        }

        composable<Route.DisputeList> {
            val disputeViewModel: DisputeViewModel = koinViewModel()
            DisputeListScreen(
                viewModel = disputeViewModel,
                onDisputeClick = { navController.navigate(Route.DisputeDetail(it)) },
                onBack = { navController.popBackStack() },
            )
        }

        composable<Route.DisputeDetail> { backStackEntry ->
            val route = backStackEntry.toRoute<Route.DisputeDetail>()
            val disputeViewModel: DisputeViewModel = koinViewModel()
            DisputeDetailScreen(
                viewModel = disputeViewModel,
                disputeId = route.disputeId,
                onBack = { navController.popBackStack() },
            )
        }

        // Fraud Alerts (Sprint 17)
        composable<Route.FraudAlertList> {
            val fraudViewModel: FraudAlertViewModel = koinViewModel()
            FraudAlertListScreen(
                viewModel = fraudViewModel,
                onAlertClick = { navController.navigate(Route.FraudAlertDetail(it)) },
                onBack = { navController.popBackStack() },
            )
        }

        composable<Route.FraudAlertDetail> { backStackEntry ->
            val route = backStackEntry.toRoute<Route.FraudAlertDetail>()
            val fraudViewModel: FraudAlertViewModel = koinViewModel()
            FraudAlertDetailScreen(
                viewModel = fraudViewModel,
                alertId = route.alertId,
                onDisputeCreated = { navController.popBackStack() },
                onBack = { navController.popBackStack() },
            )
        }

        // Assets (STORY-141, STORY-142)
        composable<Route.AssetList> {
            val assetViewModel: AssetViewModel = koinViewModel()
            AssetListScreen(
                viewModel = assetViewModel,
                onAssetClick = { assetId -> navController.navigate(Route.AssetDetail(assetId)) },
                onRegister = { navController.navigate(Route.AssetRegister) },
                onBack = { navController.popBackStack() },
            )
        }

        composable<Route.AssetDetail> { backStackEntry ->
            val route = backStackEntry.toRoute<Route.AssetDetail>()
            val assetViewModel: AssetViewModel = koinViewModel()
            AssetDetailScreen(
                viewModel = assetViewModel,
                assetId = route.assetId,
                onBack = { navController.popBackStack() },
            )
        }

        composable<Route.AssetRegister> {
            val assetViewModel: AssetViewModel = koinViewModel()
            AssetRegisterScreen(
                viewModel = assetViewModel,
                onComplete = {
                    navController.navigate(Route.AssetList) {
                        popUpTo(Route.AssetRegister) { inclusive = true }
                    }
                },
                onBack = { navController.popBackStack() },
            )
        }

        // ── Ekub (group savings + lending) ────────────────────────────────
        composable<Route.EkubGroupList> {
            val ekubViewModel: EkubViewModel = koinViewModel()
            EkubGroupListScreen(
                viewModel = ekubViewModel,
                onBack = { navController.popBackStack() },
                onCreateGroup = { navController.navigate(Route.EkubCreateGroup) },
                onInvitations = { navController.navigate(Route.EkubInvitations) },
                onOpenGroup = { id -> navController.navigate(Route.EkubGroupDetail(id)) },
            )
        }
        composable<Route.EkubCreateGroup> {
            val ekubViewModel: EkubViewModel = koinViewModel()
            CreateEkubGroupScreen(
                viewModel = ekubViewModel,
                onBack = { navController.popBackStack() },
                onCreated = { id ->
                    navController.navigate(Route.EkubGroupDetail(id)) {
                        popUpTo(Route.EkubCreateGroup) { inclusive = true }
                    }
                },
            )
        }
        composable<Route.EkubInvitations> {
            val ekubViewModel: EkubViewModel = koinViewModel()
            EkubInvitationsScreen(
                viewModel = ekubViewModel,
                onBack = { navController.popBackStack() },
            )
        }
        composable<Route.EkubGroupDetail> { backStackEntry ->
            val route = backStackEntry.toRoute<Route.EkubGroupDetail>()
            val ekubViewModel: EkubViewModel = koinViewModel()
            EkubGroupDetailScreen(
                viewModel = ekubViewModel,
                groupId = route.groupId,
                onBack = { navController.popBackStack() },
            )
        }
        }

        // ChatFAB overlay — only visible when chat is not open
        if (!showChat) {
            ChatFAB(
                onClick = { showChat = true },
                modifier = Modifier
                    .align(Alignment.BottomEnd)
                    .padding(16.dp),
            )
        }

        // Fullscreen chat overlay
        AnimatedVisibility(
            visible = showChat,
            enter = slideInVertically { it },
            exit = slideOutVertically { it },
            modifier = Modifier.fillMaxSize(),
        ) {
            ChatScreen(
                viewModel = chatViewModel,
                onBack = { showChat = false },
            )
        }
    }
}
