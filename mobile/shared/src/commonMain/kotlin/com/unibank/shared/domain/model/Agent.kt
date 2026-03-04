package com.unibank.shared.domain.model

data class CashOperationResult(
    val success: Boolean,
    val message: String,
    val transactionId: String,
    val reference: String,
    val amount: Money,
    val commission: Money,
    val newFloatBalance: Money,
    val completedAt: String,
)

data class FloatBalance(
    val agentId: String,
    val floatBalance: Money,
    val floatLimit: Money,
    val availableFloat: Money,
)

data class CommissionReport(
    val agentId: String,
    val totalCommission: Money,
    val totalTransactions: Int,
    val items: List<CommissionLineItem>,
)

data class CommissionLineItem(
    val transactionType: String,
    val count: Int,
    val totalAmount: Money,
    val totalCommission: Money,
)

data class TransactionReceipt(
    val receiptNumber: String,
    val transactionType: String,
    val customerPhone: String,
    val amount: Money,
    val commission: Money,
    val netAmount: Money,
    val agentName: String,
    val reference: String,
    val timestamp: String,
    val status: String,
)
