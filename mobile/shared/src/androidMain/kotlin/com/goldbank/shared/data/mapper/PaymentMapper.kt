package com.goldbank.shared.data.mapper

import com.goldbank.shared.domain.model.Money
import com.goldbank.shared.domain.model.PaymentNotification
import com.goldbank.shared.domain.model.PaymentResult
import com.goldbank.shared.domain.model.QrCode
import com.goldbank.shared.domain.model.TokenizeResult
import goldbank.v1.payments.PaymentServiceOuterClass as Proto

object PaymentMapper {

    fun toQrCode(response: Proto.QRCodeResponse) = QrCode(
        qrCodeData = response.qrCodeData,
        paymentReference = response.paymentReference,
        expiresAt = response.expiresAt?.let { "${it.seconds}" } ?: "",
    )

    fun toPaymentResult(response: Proto.PaymentResponse) = PaymentResult(
        success = response.success,
        message = response.message,
        transactionId = response.transactionId,
        reference = response.reference,
        amount = toMoney(response.amount),
        fee = toMoney(response.fee),
        newBalance = toMoney(response.newBalance),
        completedAt = response.completedAt?.let { "${it.seconds}" } ?: "",
        requiresPin = response.requiresPin,
        status = response.status,
    )

    fun toTokenizeResult(response: Proto.TokenizeCardResponse) = TokenizeResult(
        success = response.success,
        token = response.token,
        tokenReference = response.tokenReference,
        message = response.message,
    )

    fun toPaymentNotification(response: Proto.PaymentNotification) = PaymentNotification(
        notificationId = response.notificationId,
        transactionId = response.transactionId,
        type = response.type,
        title = response.title,
        body = response.body,
        amount = toMoney(response.amount),
        status = response.status,
        reference = response.reference,
        createdAt = response.createdAt?.let { "${it.seconds}" } ?: "",
    )

    private fun toMoney(proto: goldbank.v1.common.Common.Money?): Money {
        if (proto == null) return Money.ZERO_ZWG
        return Money(amount = proto.amount, currency = proto.currency.ifEmpty { "ZWG" })
    }
}
