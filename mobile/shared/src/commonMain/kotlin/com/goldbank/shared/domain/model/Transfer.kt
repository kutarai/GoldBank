package com.goldbank.shared.domain.model

data class TransferResult(
    val success: Boolean,
    val message: String,
    val transactionId: String,
    val reference: String,
    val amountSent: Money,
    val amountReceived: Money,
    val fee: Money,
    val exchangeRate: String,
    val newBalance: Money,
    val status: TransferStatus,
    val estimatedDelivery: String,
)

enum class TransferStatus {
    UNSPECIFIED, PENDING, PROCESSING, COMPLETED, FAILED,
}
