package com.unibank.shared.data.remote.grpc

import com.unibank.shared.data.mapper.PaymentMapper
import com.unibank.shared.data.remote.grpcCall
import com.unibank.shared.domain.model.PaymentNotification
import com.unibank.shared.domain.model.PaymentResult
import com.unibank.shared.domain.model.QrCode
import com.unibank.shared.domain.model.TokenizeResult
import com.unibank.shared.domain.util.Result
import io.grpc.ManagedChannel
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.map
import unibank.v1.common.Common
import unibank.v1.payments.PaymentServiceGrpcKt.PaymentServiceCoroutineStub
import unibank.v1.payments.PaymentServiceOuterClass.*

class PaymentGrpcClient(channel: ManagedChannel) {

    private val stub = PaymentServiceCoroutineStub(channel)

    suspend fun generateQrCode(
        merchantId: String,
        terminalId: String,
        amount: String,
        currency: String,
        description: String,
        ttlSeconds: Int,
    ): Result<QrCode> = grpcCall {
        val request = QRCodeRequest.newBuilder()
            .setMerchantId(merchantId)
            .setTerminalId(terminalId)
            .setAmount(Common.Money.newBuilder().setAmount(amount).setCurrency(currency).build())
            .setDescription(description)
            .setTtlSeconds(ttlSeconds)
            .build()
        PaymentMapper.toQrCode(stub.generateQRCode(request))
    }

    suspend fun processQrPayment(
        accountId: String,
        qrCodeData: String,
        pin: String,
    ): Result<PaymentResult> = grpcCall {
        val request = QRPaymentRequest.newBuilder()
            .setAccountId(accountId)
            .setQrCodeData(qrCodeData)
            .setPin(pin)
            .build()
        PaymentMapper.toPaymentResult(stub.processQRPayment(request))
    }

    suspend fun initiateNfcPayment(
        accountId: String,
        merchantId: String,
        terminalId: String,
        amount: String,
        currency: String,
        pin: String,
        nfcData: String,
    ): Result<PaymentResult> = grpcCall {
        val request = NFCPaymentRequest.newBuilder()
            .setAccountId(accountId)
            .setMerchantId(merchantId)
            .setTerminalId(terminalId)
            .setAmount(Common.Money.newBuilder().setAmount(amount).setCurrency(currency).build())
            .setPin(pin)
            .setNfcData(nfcData)
            .build()
        PaymentMapper.toPaymentResult(stub.initiateNFCPayment(request))
    }

    suspend fun confirmNfcPayment(transactionId: String, pin: String): Result<PaymentResult> =
        grpcCall {
            val request = ConfirmNFCPaymentRequest.newBuilder()
                .setTransactionId(transactionId)
                .setPin(pin)
                .build()
            PaymentMapper.toPaymentResult(stub.confirmNFCPayment(request))
        }

    suspend fun tokenizeCard(
        accountId: String,
        cardPan: String,
        deviceId: String,
    ): Result<TokenizeResult> = grpcCall {
        val request = TokenizeCardRequest.newBuilder()
            .setAccountId(accountId)
            .setCardPan(cardPan)
            .setDeviceId(deviceId)
            .build()
        PaymentMapper.toTokenizeResult(stub.tokenizeCard(request))
    }

    fun streamPaymentNotifications(accountId: String): Flow<PaymentNotification> {
        val request = StreamNotificationsRequest.newBuilder()
            .setAccountId(accountId)
            .build()
        return stub.streamPaymentNotifications(request)
            .map { PaymentMapper.toPaymentNotification(it) }
    }
}
