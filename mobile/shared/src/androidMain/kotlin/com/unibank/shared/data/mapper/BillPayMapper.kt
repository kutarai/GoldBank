package com.unibank.shared.data.mapper

import com.unibank.shared.domain.model.BillProvider
import com.unibank.shared.domain.model.Money
import com.unibank.shared.domain.model.PayBillResult
import com.unibank.shared.domain.model.SavedBiller
import unibank.v1.billpay.BillpayService as Proto

object BillPayMapper {

    fun toProviders(response: Proto.ListProvidersResponse): List<BillProvider> =
        response.providersList.map { toProvider(it) }

    fun toProvider(proto: Proto.BillProvider) = BillProvider(
        providerId = proto.providerId,
        name = proto.name,
        code = proto.code,
        category = proto.category,
        requiresMeterNumber = proto.requiresMeterNumber,
        requiresAccountNumber = proto.requiresAccountNumber,
        minAmount = toMoney(proto.minAmount),
        maxAmount = toMoney(proto.maxAmount),
    )

    fun toPayBillResult(response: Proto.PayBillResponse) = PayBillResult(
        success = response.success,
        message = response.message,
        transactionId = response.transactionId,
        reference = response.reference,
        token = response.token,
        amount = toMoney(response.amount),
        fee = toMoney(response.fee),
        newBalance = toMoney(response.newBalance),
        completedAt = response.completedAt?.let { "${it.seconds}" } ?: "",
    )

    fun toSavedBillers(response: Proto.GetSavedBillersResponse): List<SavedBiller> =
        response.billersList.map { toSavedBiller(it) }

    fun toSavedBiller(proto: Proto.SavedBiller) = SavedBiller(
        id = proto.id,
        providerId = proto.providerId,
        providerName = proto.providerName,
        billingReference = proto.billingReference,
        nickname = proto.nickname,
        lastPaidAt = proto.lastPaidAt?.let { "${it.seconds}" } ?: "",
    )

    private fun toMoney(proto: unibank.v1.common.Common.Money?): Money {
        if (proto == null) return Money.ZERO_ZWG
        return Money(amount = proto.amount, currency = proto.currency.ifEmpty { "ZWG" })
    }
}
