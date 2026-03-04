package com.unibank.shared.data.remote.grpc

import com.unibank.shared.data.mapper.TransferMapper
import com.unibank.shared.data.remote.grpcCall
import com.unibank.shared.domain.model.TransferResult
import com.unibank.shared.domain.util.Result
import io.grpc.ManagedChannel
import unibank.v1.common.Common
import unibank.v1.transfers.TransferServiceGrpcKt.TransferServiceCoroutineStub
import unibank.v1.transfers.TransferServiceOuterClass.*

class TransferGrpcClient(channel: ManagedChannel) {

    private val stub = TransferServiceCoroutineStub(channel)

    suspend fun sendP2P(
        senderAccountId: String,
        recipientPhone: String,
        amount: String,
        currency: String,
        description: String,
        pin: String,
    ): Result<TransferResult> = grpcCall {
        val request = P2PTransferRequest.newBuilder()
            .setSenderAccountId(senderAccountId)
            .setRecipientPhone(recipientPhone)
            .setAmount(Common.Money.newBuilder().setAmount(amount).setCurrency(currency).build())
            .setDescription(description)
            .setPin(pin)
            .build()
        TransferMapper.toTransferResult(stub.sendP2P(request))
    }

    suspend fun sendCrossBorder(
        senderAccountId: String,
        recipientPhone: String,
        recipientName: String,
        recipientCountry: String,
        sendAmount: String,
        sendCurrency: String,
        receiveCurrency: String,
        corridorId: String,
        pin: String,
    ): Result<TransferResult> = grpcCall {
        val request = CrossBorderTransferRequest.newBuilder()
            .setSenderAccountId(senderAccountId)
            .setRecipientPhone(recipientPhone)
            .setRecipientName(recipientName)
            .setRecipientCountry(recipientCountry)
            .setSendAmount(Common.Money.newBuilder().setAmount(sendAmount).setCurrency(sendCurrency).build())
            .setReceiveCurrency(receiveCurrency)
            .setCorridorId(corridorId)
            .setPin(pin)
            .build()
        TransferMapper.toTransferResult(stub.sendCrossBorder(request))
    }
}
