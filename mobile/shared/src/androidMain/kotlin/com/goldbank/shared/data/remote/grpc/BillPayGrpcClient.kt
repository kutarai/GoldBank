package com.goldbank.shared.data.remote.grpc

import com.goldbank.shared.data.mapper.BillPayMapper
import com.goldbank.shared.data.remote.grpcCall
import com.goldbank.shared.domain.model.BillProvider
import com.goldbank.shared.domain.model.PayBillResult
import com.goldbank.shared.domain.model.SavedBiller
import com.goldbank.shared.domain.util.Result
import io.grpc.ManagedChannel
import goldbank.v1.billpay.BillPayServiceGrpcKt.BillPayServiceCoroutineStub
import goldbank.v1.billpay.BillpayService.*
import goldbank.v1.common.Common

class BillPayGrpcClient(channel: ManagedChannel) {

    private val stub = BillPayServiceCoroutineStub(channel)

    suspend fun listProviders(category: String = "", countryCode: String = "ZW"): Result<List<BillProvider>> =
        grpcCall {
            val request = ListProvidersRequest.newBuilder()
                .setCategory(category)
                .setCountryCode(countryCode)
                .build()
            BillPayMapper.toProviders(stub.listProviders(request))
        }

    suspend fun payBill(
        accountId: String,
        providerId: String,
        billingReference: String,
        amount: String,
        currency: String,
        pin: String,
    ): Result<PayBillResult> = grpcCall {
        val request = PayBillRequest.newBuilder()
            .setAccountId(accountId)
            .setProviderId(providerId)
            .setBillingReference(billingReference)
            .setAmount(Common.Money.newBuilder().setAmount(amount).setCurrency(currency).build())
            .setPin(pin)
            .build()
        BillPayMapper.toPayBillResult(stub.payBill(request))
    }

    suspend fun saveBiller(
        accountId: String,
        providerId: String,
        billingReference: String,
        nickname: String,
    ): Result<Unit> = grpcCall {
        val request = SaveBillerRequest.newBuilder()
            .setAccountId(accountId)
            .setProviderId(providerId)
            .setBillingReference(billingReference)
            .setNickname(nickname)
            .build()
        stub.saveBiller(request)
        Unit
    }

    suspend fun getSavedBillers(accountId: String): Result<List<SavedBiller>> = grpcCall {
        val request = GetSavedBillersRequest.newBuilder()
            .setAccountId(accountId)
            .build()
        BillPayMapper.toSavedBillers(stub.getSavedBillers(request))
    }
}
