package com.unibank.app.navigation

import kotlinx.serialization.Serializable

sealed interface Route {
    // Auth sub-graph
    @Serializable data object AuthGraph : Route
    @Serializable data object Register : Route
    @Serializable data class Otp(val registrationId: String, val otpLength: Int, val ttlSeconds: Int) : Route
    @Serializable data class CreatePin(val accountId: String) : Route
    @Serializable data object Login : Route
    @Serializable data object DeviceTransfer : Route
    @Serializable data class RegistrationProfile(val accountId: String) : Route
    @Serializable data class RegistrationIdUpload(val accountId: String) : Route
    @Serializable data class RegistrationSelfie(val accountId: String) : Route

    // Main sub-graph (behind auth)
    @Serializable data object MainGraph : Route
    @Serializable data object Home : Route

    // Transactions
    @Serializable data object TransactionList : Route
    @Serializable data class TransactionDetail(val transactionId: String) : Route

    // Payments
    @Serializable data object PaymentGraph : Route
    @Serializable data object QrGenerate : Route
    @Serializable data object QrScan : Route
    @Serializable data class QrPaymentConfirm(val qrData: String) : Route
    @Serializable data object NfcPayment : Route
    @Serializable data class PaymentResult(val transactionId: String) : Route

    // Transfers
    @Serializable data object TransferGraph : Route
    @Serializable data object P2PTransfer : Route
    @Serializable data object CrossBorderTransfer : Route
    @Serializable data class TransferConfirm(
        val transferType: String,
        val recipientPhone: String,
        val amount: String,
        val currency: String,
    ) : Route

    // Bill Pay
    @Serializable data object BillPayGraph : Route
    @Serializable data object ProviderList : Route
    @Serializable data class PayBill(val providerId: String, val providerName: String) : Route
    @Serializable data object SavedBillers : Route

    // Agent
    @Serializable data object AgentGraph : Route
    @Serializable data object CashIn : Route
    @Serializable data object CashOut : Route
    @Serializable data object FloatBalance : Route
    @Serializable data object AgentCommission : Route

    // Loans
    @Serializable data object LoanList : Route
    @Serializable data object LoanApply : Route
    @Serializable data class LoanDetail(val loanId: String) : Route

    // Loan AI
    @Serializable data object LoanEligibility : Route

    // Notifications
    @Serializable data object Notifications : Route

    // KYC
    @Serializable data object KycGraph : Route
    @Serializable data object KycDashboard : Route
    @Serializable data class DocumentUpload(val documentType: String) : Route
    @Serializable data object Selfie : Route
    @Serializable data class KycVerificationResult(val accountId: String) : Route
    @Serializable data object ProofOfAddress : Route

    // Document Scanning
    @Serializable data object ChequeScan : Route
    @Serializable data object BillScan : Route

    // Merchant
    @Serializable data object MerchantGraph : Route
    @Serializable data object MerchantRegister : Route
    @Serializable data object MerchantDashboard : Route
    @Serializable data object MerchantTransactions : Route
    @Serializable data object MerchantSettlements : Route
    @Serializable data object MerchantCommission : Route

    // Profile
    @Serializable data object ProfileGraph : Route
    @Serializable data object Profile : Route
    @Serializable data object EditProfile : Route
    @Serializable data object Settings : Route
    @Serializable data object NotificationSettings : Route

    // Security
    @Serializable data object BiometricPrompt : Route
    @Serializable data object SessionLock : Route
    @Serializable data object SecuritySettings : Route

    // Chat
    @Serializable data object Chat : Route

    // Disputes
    @Serializable data class DisputeWizard(val transactionId: String) : Route
    @Serializable data object DisputeList : Route
    @Serializable data class DisputeDetail(val disputeId: String) : Route

    // Fraud Alerts
    @Serializable data object FraudAlertList : Route
    @Serializable data class FraudAlertDetail(val alertId: String) : Route
}
