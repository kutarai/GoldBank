package com.goldbank.shared.data.mapper

import com.goldbank.shared.domain.model.Money
import com.goldbank.shared.domain.model.TransferResult
import com.goldbank.shared.domain.model.TransferStatus
import goldbank.v1.transfers.TransferServiceOuterClass as Proto

object TransferMapper {

    fun toTransferResult(response: Proto.TransferResponse) = TransferResult(
        success = response.success,
        message = response.message,
        transactionId = response.transactionId,
        reference = response.reference,
        amountSent = toMoney(response.amountSent),
        amountReceived = toMoney(response.amountReceived),
        fee = toMoney(response.fee),
        exchangeRate = response.exchangeRate,
        newBalance = toMoney(response.newBalance),
        status = toTransferStatus(response.status),
        estimatedDelivery = response.estimatedDelivery?.let { "${it.seconds}" } ?: "",
    )

    private fun toTransferStatus(proto: Proto.TransferStatus) = when (proto) {
        Proto.TransferStatus.TRANSFER_STATUS_PENDING -> TransferStatus.PENDING
        Proto.TransferStatus.TRANSFER_STATUS_PROCESSING -> TransferStatus.PROCESSING
        Proto.TransferStatus.TRANSFER_STATUS_COMPLETED -> TransferStatus.COMPLETED
        Proto.TransferStatus.TRANSFER_STATUS_FAILED -> TransferStatus.FAILED
        else -> TransferStatus.UNSPECIFIED
    }

    private fun toMoney(proto: goldbank.v1.common.Common.Money?): Money {
        if (proto == null) return Money.ZERO_ZWG
        return Money(amount = proto.amount, currency = proto.currency.ifEmpty { "ZWG" })
    }
}
