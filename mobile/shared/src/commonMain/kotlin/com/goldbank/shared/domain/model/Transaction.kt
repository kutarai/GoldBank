package com.goldbank.shared.domain.model

data class Transaction(
    val transactionId: String,
    val type: TransactionType,
    val amount: Money,
    val fee: Money,
    val status: TransactionStatus,
    val reference: String,
    val description: String,
    val counterpartyName: String,
    val counterpartyPhone: String,
    val balanceAfter: Money,
    val createdAt: String,
    val completedAt: String,
)

enum class TransactionType {
    UNSPECIFIED,
    CASH_IN,
    CASH_OUT,
    P2P_SEND,
    P2P_RECEIVE,
    PAYMENT_NFC,
    PAYMENT_QR,
    BILL_PAYMENT,
    TRANSFER_DOMESTIC,
    TRANSFER_CROSS_BORDER,
    FEE,
    REVERSAL,
    SETTLEMENT,
}

enum class TransactionStatus {
    UNSPECIFIED,
    PENDING,
    PROCESSING,
    COMPLETED,
    FAILED,
    REVERSED,
}
