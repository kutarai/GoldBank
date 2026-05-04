package com.goldbank.shared.data.mapper

import com.goldbank.shared.domain.model.CashOperationResult
import com.goldbank.shared.domain.model.CommissionLineItem
import com.goldbank.shared.domain.model.CommissionReport
import com.goldbank.shared.domain.model.FloatBalance
import com.goldbank.shared.domain.model.Money
import com.goldbank.shared.domain.model.TransactionReceipt
import goldbank.v1.agents.AgentServiceOuterClass as Proto

object AgentMapper {

    fun toCashOperationResult(response: Proto.CashOperationResponse) = CashOperationResult(
        success = response.success,
        message = response.message,
        transactionId = response.transactionId,
        reference = response.reference,
        amount = toMoney(response.amount),
        commission = toMoney(response.commission),
        newFloatBalance = toMoney(response.newFloatBalance),
        completedAt = response.completedAt?.let { "${it.seconds}" } ?: "",
    )

    fun toFloatBalance(response: Proto.FloatBalanceResponse) = FloatBalance(
        agentId = response.agentId,
        floatBalance = toMoney(response.floatBalance),
        floatLimit = toMoney(response.floatLimit),
        availableFloat = toMoney(response.availableFloat),
    )

    fun toCommissionReport(response: Proto.CommissionReportResponse) = CommissionReport(
        agentId = response.agentId,
        totalCommission = toMoney(response.totalCommission),
        totalTransactions = response.totalTransactions,
        items = response.itemsList.map { toCommissionLineItem(it) },
    )

    fun toCommissionLineItem(proto: Proto.CommissionLineItem) = CommissionLineItem(
        transactionType = proto.transactionType,
        count = proto.count,
        totalAmount = toMoney(proto.totalAmount),
        totalCommission = toMoney(proto.totalCommission),
    )

    fun toReceipt(response: Proto.TransactionReceiptResponse) = TransactionReceipt(
        receiptNumber = response.receiptNumber,
        transactionType = response.transactionType,
        customerPhone = response.customerPhone,
        amount = toMoney(response.amount),
        commission = toMoney(response.commission),
        netAmount = toMoney(response.netAmount),
        agentName = response.agentName,
        reference = response.reference,
        timestamp = response.timestamp?.let { "${it.seconds}" } ?: "",
        status = response.status,
    )

    private fun toMoney(proto: goldbank.v1.common.Common.Money?): Money {
        if (proto == null) return Money.ZERO_ZWG
        return Money(amount = proto.amount, currency = proto.currency.ifEmpty { "ZWG" })
    }
}
