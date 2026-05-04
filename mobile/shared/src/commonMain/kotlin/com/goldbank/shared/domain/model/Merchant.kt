package com.goldbank.shared.domain.model

data class MerchantProfile(
    val merchantId: String,
    val businessName: String,
    val businessType: String,
    val categoryCode: String,
    val address: MerchantAddress,
    val status: String,
    val commissionRate: String,
    val settlementFrequency: String,
    val createdAt: String,
)

data class MerchantAddress(
    val line1: String,
    val line2: String,
    val city: String,
    val province: String,
    val postalCode: String,
    val countryCode: String,
)

data class MerchantRegistrationResult(
    val success: Boolean,
    val message: String,
    val merchantId: String,
    val status: String,
)

data class MerchantStatusInfo(
    val merchantId: String,
    val businessName: String,
    val status: String,
    val kycStatus: String,
    val isAgent: Boolean,
    val lastUpdated: String,
)

data class MerchantTransaction(
    val transactionId: String,
    val amount: Money,
    val fee: Money,
    val reference: String,
    val paymentMethod: String,
    val terminalId: String,
    val createdAt: String,
)

data class Settlement(
    val settlementId: String,
    val amount: Money,
    val transactionCount: Int,
    val status: String,
    val settlementDate: String,
    val paidAt: String,
)

data class SettlementDetail(
    val settlementId: String,
    val merchantId: String,
    val periodStart: String,
    val periodEnd: String,
    val totalTransactions: Int,
    val grossAmount: Money,
    val totalFees: Money,
    val netAmount: Money,
    val status: String,
    val paidAt: String,
    val reference: String,
)

data class MerchantCommissionReport(
    val merchantId: String,
    val lineItems: List<MerchantCommissionLineItem>,
    val totalCommission: Money,
    val totalTransactions: Int,
)

data class BusinessDocumentUploadResult(
    val documentId: String,
    val status: String,
    val message: String,
)

data class MerchantCommissionLineItem(
    val transactionType: String,
    val transactionCount: Int,
    val totalTransactionAmount: Money,
    val commissionRate: String,
    val commissionAmount: Money,
)
