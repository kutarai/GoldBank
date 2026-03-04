package com.unibank.shared.domain.model

data class QrCode(
    val qrCodeData: String,
    val paymentReference: String,
    val expiresAt: String,
)

data class PaymentResult(
    val success: Boolean,
    val message: String,
    val transactionId: String,
    val reference: String,
    val amount: Money,
    val fee: Money,
    val newBalance: Money,
    val completedAt: String,
    val requiresPin: Boolean,
    val status: String,
)

data class PaymentNotification(
    val notificationId: String,
    val transactionId: String,
    val type: String,
    val title: String,
    val body: String,
    val amount: Money,
    val status: String,
    val reference: String,
    val createdAt: String,
)

data class TokenizeResult(
    val success: Boolean,
    val token: String,
    val tokenReference: String,
    val message: String,
)
