package com.unibank.shared.data.mapper

import com.unibank.shared.domain.model.LoanApplicationResult
import com.unibank.shared.domain.model.LoanDetail
import com.unibank.shared.domain.model.LoanScheduleEntry
import com.unibank.shared.domain.model.LoanStatus
import com.unibank.shared.domain.model.LoanSummary
import com.unibank.shared.domain.model.Money
import unibank.v1.loans.LoanServiceOuterClass as Proto

object LoanMapper {

    fun toApplicationResult(response: Proto.LoanApplicationResponse) = LoanApplicationResult(
        success = response.success,
        message = response.message,
        loanId = response.loanId,
        reference = response.reference,
        status = toLoanStatus(response.status),
        principal = toMoney(response.principal),
        interestRate = response.interestRate,
        monthlyPayment = toMoney(response.monthlyPayment),
        tenureMonths = response.tenureMonths,
        creditScore = response.creditScore,
        newBalance = toMoney(response.newBalance),
    )

    fun toLoanDetail(response: Proto.LoanDetailResponse) = LoanDetail(
        loanId = response.loanId,
        reference = response.reference,
        status = toLoanStatus(response.status),
        principal = toMoney(response.principal),
        outstandingBalance = toMoney(response.outstandingBalance),
        interestRate = response.interestRate,
        tenureMonths = response.tenureMonths,
        monthlyPayment = toMoney(response.monthlyPayment),
        purpose = response.purpose,
        paymentsMade = response.paymentsMade,
        totalPayments = response.totalPayments,
        nextPaymentDate = response.nextPaymentDate?.let { "${it.seconds}" } ?: "",
        createdAt = response.createdAt?.let { "${it.seconds}" } ?: "",
        creditScore = response.creditScore,
    )

    fun toLoanSummaries(response: Proto.ListLoansResponse): List<LoanSummary> =
        response.loansList.map { toLoanSummary(it) }

    fun toLoanSummary(proto: Proto.LoanSummary) = LoanSummary(
        loanId = proto.loanId,
        reference = proto.reference,
        status = toLoanStatus(proto.status),
        principal = toMoney(proto.principal),
        outstandingBalance = toMoney(proto.outstandingBalance),
        monthlyPayment = toMoney(proto.monthlyPayment),
        paymentsMade = proto.paymentsMade,
        totalPayments = proto.totalPayments,
        createdAt = proto.createdAt?.let { "${it.seconds}" } ?: "",
    )

    fun toScheduleEntries(response: Proto.LoanScheduleResponse): List<LoanScheduleEntry> =
        response.entriesList.map { toScheduleEntry(it) }

    fun toScheduleEntry(proto: Proto.LoanScheduleEntry) = LoanScheduleEntry(
        paymentNumber = proto.paymentNumber,
        principalAmount = toMoney(proto.principalAmount),
        interestAmount = toMoney(proto.interestAmount),
        totalPayment = toMoney(proto.totalPayment),
        remainingBalance = toMoney(proto.remainingBalance),
        dueDate = proto.dueDate?.let { "${it.seconds}" } ?: "",
        isPaid = proto.isPaid,
    )

    private fun toLoanStatus(proto: Proto.LoanStatus) = when (proto) {
        Proto.LoanStatus.LOAN_STATUS_PENDING -> LoanStatus.PENDING
        Proto.LoanStatus.LOAN_STATUS_APPROVED -> LoanStatus.APPROVED
        Proto.LoanStatus.LOAN_STATUS_REJECTED -> LoanStatus.REJECTED
        Proto.LoanStatus.LOAN_STATUS_DISBURSED -> LoanStatus.DISBURSED
        Proto.LoanStatus.LOAN_STATUS_REPAYING -> LoanStatus.REPAYING
        Proto.LoanStatus.LOAN_STATUS_PAID_OFF -> LoanStatus.PAID_OFF
        Proto.LoanStatus.LOAN_STATUS_DEFAULTED -> LoanStatus.DEFAULTED
        else -> LoanStatus.UNSPECIFIED
    }

    private fun toMoney(proto: unibank.v1.common.Common.Money?): Money {
        if (proto == null) return Money.ZERO_ZWG
        return Money(amount = proto.amount, currency = proto.currency.ifEmpty { "ZWG" })
    }
}
