package com.unibank.shared.domain.model

data class LoanApplicationResult(
    val success: Boolean,
    val message: String,
    val loanId: String,
    val reference: String,
    val status: LoanStatus,
    val principal: Money,
    val interestRate: String,
    val monthlyPayment: Money,
    val tenureMonths: Int,
    val creditScore: Int,
    val newBalance: Money,
)

data class LoanDetail(
    val loanId: String,
    val reference: String,
    val status: LoanStatus,
    val principal: Money,
    val outstandingBalance: Money,
    val interestRate: String,
    val tenureMonths: Int,
    val monthlyPayment: Money,
    val purpose: String,
    val paymentsMade: Int,
    val totalPayments: Int,
    val nextPaymentDate: String,
    val createdAt: String,
    val creditScore: Int,
)

data class LoanSummary(
    val loanId: String,
    val reference: String,
    val status: LoanStatus,
    val principal: Money,
    val outstandingBalance: Money,
    val monthlyPayment: Money,
    val paymentsMade: Int,
    val totalPayments: Int,
    val createdAt: String,
)

data class LoanScheduleEntry(
    val paymentNumber: Int,
    val principalAmount: Money,
    val interestAmount: Money,
    val totalPayment: Money,
    val remainingBalance: Money,
    val dueDate: String,
    val isPaid: Boolean,
)

enum class LoanStatus {
    UNSPECIFIED, PENDING, APPROVED, REJECTED, DISBURSED, REPAYING, PAID_OFF, DEFAULTED,
}
