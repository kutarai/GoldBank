package com.unibank.shared.domain.model

data class BillProvider(
    val providerId: String,
    val name: String,
    val code: String,
    val category: String,
    val requiresMeterNumber: Boolean,
    val requiresAccountNumber: Boolean,
    val minAmount: Money,
    val maxAmount: Money,
)

data class SavedBiller(
    val id: String,
    val providerId: String,
    val providerName: String,
    val billingReference: String,
    val nickname: String,
    val lastPaidAt: String,
)

data class PayBillResult(
    val success: Boolean,
    val message: String,
    val transactionId: String,
    val reference: String,
    val token: String,
    val amount: Money,
    val fee: Money,
    val newBalance: Money,
    val completedAt: String,
)
