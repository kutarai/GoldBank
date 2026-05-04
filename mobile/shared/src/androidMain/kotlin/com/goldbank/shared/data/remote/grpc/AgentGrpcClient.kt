package com.goldbank.shared.data.remote.grpc

import com.google.protobuf.Timestamp
import com.goldbank.shared.data.mapper.AgentMapper
import com.goldbank.shared.data.remote.grpcCall
import com.goldbank.shared.domain.model.CashOperationResult
import com.goldbank.shared.domain.model.CommissionReport
import com.goldbank.shared.domain.model.FloatBalance
import com.goldbank.shared.domain.model.TransactionReceipt
import com.goldbank.shared.domain.util.Result
import io.grpc.ManagedChannel
import goldbank.v1.agents.AgentServiceGrpcKt.AgentServiceCoroutineStub
import goldbank.v1.agents.AgentServiceOuterClass.*
import goldbank.v1.common.Common
import java.time.LocalDate
import java.time.ZoneOffset

class AgentGrpcClient(channel: ManagedChannel) {

    private val stub = AgentServiceCoroutineStub(channel)

    suspend fun cashIn(
        agentId: String,
        customerPhone: String,
        amount: String,
        currency: String,
        agentPin: String,
    ): Result<CashOperationResult> = grpcCall {
        val request = CashInRequest.newBuilder()
            .setAgentId(agentId)
            .setCustomerPhone(customerPhone)
            .setAmount(Common.Money.newBuilder().setAmount(amount).setCurrency(currency).build())
            .setAgentPin(agentPin)
            .build()
        AgentMapper.toCashOperationResult(stub.cashIn(request))
    }

    suspend fun cashOut(
        agentId: String,
        customerAccountId: String,
        amount: String,
        currency: String,
        customerPin: String,
        agentPin: String,
    ): Result<CashOperationResult> = grpcCall {
        val request = CashOutRequest.newBuilder()
            .setAgentId(agentId)
            .setCustomerAccountId(customerAccountId)
            .setAmount(Common.Money.newBuilder().setAmount(amount).setCurrency(currency).build())
            .setCustomerPin(customerPin)
            .setAgentPin(agentPin)
            .build()
        AgentMapper.toCashOperationResult(stub.cashOut(request))
    }

    suspend fun getFloatBalance(agentId: String): Result<FloatBalance> = grpcCall {
        val request = FloatBalanceRequest.newBuilder()
            .setAgentId(agentId)
            .build()
        AgentMapper.toFloatBalance(stub.getFloatBalance(request))
    }

    suspend fun getCommissionReport(agentId: String, startDate: String, endDate: String): Result<CommissionReport> =
        grpcCall {
            val from = LocalDate.parse(startDate).atStartOfDay().toInstant(ZoneOffset.UTC)
            val to = LocalDate.parse(endDate).atStartOfDay().toInstant(ZoneOffset.UTC)
            val request = CommissionReportRequest.newBuilder()
                .setAgentId(agentId)
                .setDateRange(
                    Common.DateRange.newBuilder()
                        .setFrom(Timestamp.newBuilder().setSeconds(from.epochSecond).build())
                        .setTo(Timestamp.newBuilder().setSeconds(to.epochSecond).build())
                        .build()
                )
                .build()
            AgentMapper.toCommissionReport(stub.getCommissionReport(request))
        }

    suspend fun getTransactionReceipt(transactionId: String, agentId: String): Result<TransactionReceipt> =
        grpcCall {
            val request = GetReceiptRequest.newBuilder()
                .setTransactionId(transactionId)
                .setAgentId(agentId)
                .build()
            AgentMapper.toReceipt(stub.getTransactionReceipt(request))
        }
}
