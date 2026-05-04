package com.goldbank.shared.data.remote.grpc

import com.goldbank.shared.data.mapper.LoanMapper
import com.goldbank.shared.data.remote.grpcCall
import com.goldbank.shared.domain.model.LoanApplicationResult
import com.goldbank.shared.domain.model.LoanDetail
import com.goldbank.shared.domain.model.LoanScheduleEntry
import com.goldbank.shared.domain.model.LoanSummary
import com.goldbank.shared.domain.util.Result
import io.grpc.ManagedChannel
import goldbank.v1.common.Common
import goldbank.v1.loans.LoanServiceGrpcKt.LoanServiceCoroutineStub
import goldbank.v1.loans.LoanServiceOuterClass.*

class LoanGrpcClient(channel: ManagedChannel) {

    private val stub = LoanServiceCoroutineStub(channel)

    suspend fun applyForLoan(
        accountId: String,
        amount: String,
        currency: String,
        tenureMonths: Int,
        purpose: String,
        pin: String,
        collateralAssetIds: List<String> = emptyList(),
    ): Result<LoanApplicationResult> = grpcCall {
        val request = ApplyForLoanRequest.newBuilder()
            .setAccountId(accountId)
            .setAmount(Common.Money.newBuilder().setAmount(amount).setCurrency(currency).build())
            .setTenureMonths(tenureMonths)
            .setPurpose(purpose)
            .setPin(pin)
            .addAllCollateralAssetIds(collateralAssetIds)
            .build()
        LoanMapper.toApplicationResult(stub.applyForLoan(request))
    }

    suspend fun getLoan(
        loanId: String,
        accountId: String,
    ): Result<LoanDetail> = grpcCall {
        val request = GetLoanRequest.newBuilder()
            .setLoanId(loanId)
            .setAccountId(accountId)
            .build()
        LoanMapper.toLoanDetail(stub.getLoan(request))
    }

    suspend fun listLoans(
        accountId: String,
        statusFilter: String = "",
    ): Result<List<LoanSummary>> = grpcCall {
        val request = ListLoansRequest.newBuilder()
            .setAccountId(accountId)
            .setStatusFilter(statusFilter)
            .build()
        LoanMapper.toLoanSummaries(stub.listLoans(request))
    }

    suspend fun getLoanSchedule(
        loanId: String,
        accountId: String,
    ): Result<List<LoanScheduleEntry>> = grpcCall {
        val request = GetLoanScheduleRequest.newBuilder()
            .setLoanId(loanId)
            .setAccountId(accountId)
            .build()
        LoanMapper.toScheduleEntries(stub.getLoanSchedule(request))
    }
}
