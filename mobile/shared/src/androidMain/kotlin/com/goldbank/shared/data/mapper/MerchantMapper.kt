package com.goldbank.shared.data.mapper

import com.goldbank.shared.domain.model.*
import goldbank.v1.merchants.MerchantServiceOuterClass as Proto

object MerchantMapper {

    fun toRegistrationResult(response: Proto.MerchantRegisterResponse) = MerchantRegistrationResult(
        success = response.success,
        message = response.message,
        merchantId = response.merchantId,
        status = response.status.name,
    )

    fun toProfile(response: Proto.MerchantProfileResponse) = MerchantProfile(
        merchantId = response.merchantId,
        businessName = response.businessName,
        businessType = response.businessType,
        categoryCode = response.categoryCode,
        address = toAddress(response.address),
        status = response.status.name,
        commissionRate = response.commissionRate,
        settlementFrequency = response.settlementFrequency,
        createdAt = response.createdAt?.let { "${it.seconds}" } ?: "",
    )

    fun toAddress(proto: Proto.MerchantAddress?) = MerchantAddress(
        line1 = proto?.line1 ?: "",
        line2 = proto?.line2 ?: "",
        city = proto?.city ?: "",
        province = proto?.province ?: "",
        postalCode = proto?.postalCode ?: "",
        countryCode = proto?.countryCode ?: "",
    )

    fun toTransaction(proto: Proto.MerchantTransactionResponse) = MerchantTransaction(
        transactionId = proto.transactionId,
        amount = toMoney(proto.amount),
        fee = toMoney(proto.fee),
        reference = proto.reference,
        paymentMethod = proto.paymentMethod,
        terminalId = proto.terminalId,
        createdAt = proto.createdAt?.let { "${it.seconds}" } ?: "",
    )

    fun toSettlements(response: Proto.MerchantSettlementsResponse): List<Settlement> =
        response.settlementsList.map { toSettlement(it) }

    fun toSettlement(proto: Proto.Settlement) = Settlement(
        settlementId = proto.settlementId,
        amount = toMoney(proto.amount),
        transactionCount = proto.transactionCount,
        status = proto.status,
        settlementDate = proto.settlementDate?.let { "${it.seconds}" } ?: "",
        paidAt = proto.paidAt?.let { "${it.seconds}" } ?: "",
    )

    fun toMerchantStatus(response: Proto.GetMerchantStatusResponse) = MerchantStatusInfo(
        merchantId = response.merchantId,
        businessName = response.businessName,
        status = response.status.name,
        kycStatus = response.kycStatus,
        isAgent = response.isAgent,
        lastUpdated = response.lastUpdated?.let { "${it.seconds}" } ?: "",
    )

    fun toSettlementDetail(response: Proto.SettlementResponse) = SettlementDetail(
        settlementId = response.settlementId,
        merchantId = response.merchantId,
        periodStart = response.periodStart?.let { "${it.seconds}" } ?: "",
        periodEnd = response.periodEnd?.let { "${it.seconds}" } ?: "",
        totalTransactions = response.totalTransactions,
        grossAmount = toMoney(response.grossAmount),
        totalFees = toMoney(response.totalFees),
        netAmount = toMoney(response.netAmount),
        status = response.status,
        paidAt = response.paidAt?.let { "${it.seconds}" } ?: "",
        reference = response.reference,
    )

    fun toCommissionReport(response: Proto.MerchantCommissionResponse) = MerchantCommissionReport(
        merchantId = response.merchantId,
        lineItems = response.lineItemsList.map { toCommissionLineItem(it) },
        totalCommission = toMoney(response.totalCommission),
        totalTransactions = response.totalTransactions,
    )

    fun toCommissionLineItem(proto: Proto.CommissionLineItem) = MerchantCommissionLineItem(
        transactionType = proto.transactionType,
        transactionCount = proto.transactionCount,
        totalTransactionAmount = toMoney(proto.totalTransactionAmount),
        commissionRate = proto.commissionRate,
        commissionAmount = toMoney(proto.commissionAmount),
    )

    private fun toMoney(proto: goldbank.v1.common.Common.Money?): Money {
        if (proto == null) return Money.ZERO_ZWG
        return Money(amount = proto.amount, currency = proto.currency.ifEmpty { "ZWG" })
    }
}
