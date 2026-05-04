package com.goldbank.shared.data.remote.grpc

import com.google.protobuf.ByteString
import com.google.protobuf.Timestamp
import com.goldbank.shared.data.mapper.MerchantMapper
import com.goldbank.shared.domain.model.BusinessDocumentUploadResult
import com.goldbank.shared.domain.model.MerchantAddress
import com.goldbank.shared.domain.model.MerchantCommissionReport
import com.goldbank.shared.domain.model.MerchantProfile
import com.goldbank.shared.domain.model.MerchantRegistrationResult
import com.goldbank.shared.domain.model.MerchantStatusInfo
import com.goldbank.shared.domain.model.MerchantTransaction
import com.goldbank.shared.domain.model.Settlement
import com.goldbank.shared.domain.model.SettlementDetail
import io.grpc.ManagedChannel
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.flow
import kotlinx.coroutines.flow.map
import goldbank.v1.merchants.MerchantServiceGrpcKt
import goldbank.v1.merchants.MerchantServiceOuterClass as Proto
import java.time.LocalDate
import java.time.ZoneOffset

class MerchantGrpcClient(channel: ManagedChannel) {

    private val stub = MerchantServiceGrpcKt.MerchantServiceCoroutineStub(channel)

    companion object {
        private const val CHUNK_SIZE = 32 * 1024 // 32KB
    }

    suspend fun register(
        accountId: String,
        businessName: String,
        businessType: String,
        registrationNumber: String,
        taxId: String,
        categoryCode: String,
        address: MerchantAddress,
        isAgent: Boolean,
    ): MerchantRegistrationResult {
        val request = Proto.MerchantRegisterRequest.newBuilder()
            .setAccountId(accountId)
            .setBusinessName(businessName)
            .setBusinessType(businessType)
            .setRegistrationNumber(registrationNumber)
            .setTaxId(taxId)
            .setCategoryCode(categoryCode)
            .setAddress(
                Proto.MerchantAddress.newBuilder()
                    .setLine1(address.line1)
                    .setLine2(address.line2)
                    .setCity(address.city)
                    .setProvince(address.province)
                    .setPostalCode(address.postalCode)
                    .setCountryCode(address.countryCode)
                    .build()
            )
            .setIsAgent(isAgent)
            .setAgentTermsAccepted(isAgent)
            .build()
        return MerchantMapper.toRegistrationResult(stub.register(request))
    }

    suspend fun getProfile(merchantId: String): MerchantProfile {
        val request = Proto.MerchantProfileRequest.newBuilder()
            .setMerchantId(merchantId)
            .build()
        return MerchantMapper.toProfile(stub.getProfile(request))
    }

    suspend fun updateProfile(
        merchantId: String,
        businessName: String,
        categoryCode: String,
        address: MerchantAddress,
        commissionRate: String,
        settlementFrequency: String,
    ): MerchantProfile {
        val request = Proto.UpdateMerchantProfileRequest.newBuilder()
            .setMerchantId(merchantId)
            .setBusinessName(businessName)
            .setCategoryCode(categoryCode)
            .setAddress(
                Proto.MerchantAddress.newBuilder()
                    .setLine1(address.line1)
                    .setLine2(address.line2)
                    .setCity(address.city)
                    .setProvince(address.province)
                    .setPostalCode(address.postalCode)
                    .setCountryCode(address.countryCode)
                    .build()
            )
            .setCommissionRate(commissionRate)
            .setSettlementFrequency(settlementFrequency)
            .build()
        return MerchantMapper.toProfile(stub.updateProfile(request))
    }

    fun getTransactions(
        merchantId: String,
        startDate: String,
        endDate: String,
    ): Flow<MerchantTransaction> {
        val from = LocalDate.parse(startDate).atStartOfDay().toInstant(ZoneOffset.UTC)
        val to = LocalDate.parse(endDate).atStartOfDay().toInstant(ZoneOffset.UTC)
        val request = Proto.MerchantTransactionsRequest.newBuilder()
            .setMerchantId(merchantId)
            .setDateRange(
                goldbank.v1.common.Common.DateRange.newBuilder()
                    .setFrom(Timestamp.newBuilder().setSeconds(from.epochSecond).build())
                    .setTo(Timestamp.newBuilder().setSeconds(to.epochSecond).build())
                    .build()
            )
            .build()
        return stub.getTransactions(request).map { MerchantMapper.toTransaction(it) }
    }

    suspend fun getSettlements(
        merchantId: String,
        startDate: String,
        endDate: String,
    ): List<Settlement> {
        val from = LocalDate.parse(startDate).atStartOfDay().toInstant(ZoneOffset.UTC)
        val to = LocalDate.parse(endDate).atStartOfDay().toInstant(ZoneOffset.UTC)
        val request = Proto.MerchantSettlementsRequest.newBuilder()
            .setMerchantId(merchantId)
            .setDateRange(
                goldbank.v1.common.Common.DateRange.newBuilder()
                    .setFrom(Timestamp.newBuilder().setSeconds(from.epochSecond).build())
                    .setTo(Timestamp.newBuilder().setSeconds(to.epochSecond).build())
                    .build()
            )
            .build()
        return MerchantMapper.toSettlements(stub.getSettlements(request))
    }

    suspend fun uploadBusinessDocument(
        merchantId: String,
        documentType: String,
        fileName: String,
        contentType: String,
        fileBytes: ByteArray,
    ): BusinessDocumentUploadResult {
        val requestFlow = flow {
            emit(
                Proto.UploadBusinessDocumentRequest.newBuilder()
                    .setMetadata(
                        Proto.BusinessDocumentMetadata.newBuilder()
                            .setMerchantId(merchantId)
                            .setDocumentType(documentType)
                            .setFileName(fileName)
                            .setContentType(contentType)
                            .setFileSize(fileBytes.size.toLong())
                            .build()
                    )
                    .build()
            )
            var offset = 0
            while (offset < fileBytes.size) {
                val end = minOf(offset + CHUNK_SIZE, fileBytes.size)
                emit(
                    Proto.UploadBusinessDocumentRequest.newBuilder()
                        .setChunk(ByteString.copyFrom(fileBytes, offset, end - offset))
                        .build()
                )
                offset = end
            }
        }
        val response = stub.uploadBusinessDocument(requestFlow)
        return BusinessDocumentUploadResult(
            documentId = response.documentId,
            status = response.status,
            message = response.message,
        )
    }

    suspend fun getMerchantStatus(merchantId: String): MerchantStatusInfo {
        val request = Proto.GetMerchantStatusRequest.newBuilder()
            .setMerchantId(merchantId)
            .build()
        return MerchantMapper.toMerchantStatus(stub.getMerchantStatus(request))
    }

    suspend fun getSettlement(
        merchantId: String,
        periodStart: String,
        periodEnd: String,
        currency: String = "ZWG",
    ): SettlementDetail {
        val from = LocalDate.parse(periodStart).atStartOfDay().toInstant(ZoneOffset.UTC)
        val to = LocalDate.parse(periodEnd).atStartOfDay().toInstant(ZoneOffset.UTC)
        val request = Proto.GetSettlementRequest.newBuilder()
            .setMerchantId(merchantId)
            .setPeriodStart(Timestamp.newBuilder().setSeconds(from.epochSecond).build())
            .setPeriodEnd(Timestamp.newBuilder().setSeconds(to.epochSecond).build())
            .setCurrency(currency)
            .build()
        return MerchantMapper.toSettlementDetail(stub.getSettlement(request))
    }

    fun getTransactionHistory(
        merchantId: String,
        startDate: String,
        endDate: String,
        typeFilter: String = "",
    ): Flow<MerchantTransaction> {
        val from = LocalDate.parse(startDate).atStartOfDay().toInstant(ZoneOffset.UTC)
        val to = LocalDate.parse(endDate).atStartOfDay().toInstant(ZoneOffset.UTC)
        val request = Proto.MerchantTransactionHistoryRequest.newBuilder()
            .setMerchantId(merchantId)
            .setDateRange(
                goldbank.v1.common.Common.DateRange.newBuilder()
                    .setFrom(Timestamp.newBuilder().setSeconds(from.epochSecond).build())
                    .setTo(Timestamp.newBuilder().setSeconds(to.epochSecond).build())
                    .build()
            )
            .setTypeFilter(typeFilter)
            .build()
        return stub.getTransactionHistory(request).map { MerchantMapper.toTransaction(it) }
    }

    suspend fun getCommissionReport(
        merchantId: String,
        startDate: String,
        endDate: String,
    ): MerchantCommissionReport {
        val from = LocalDate.parse(startDate).atStartOfDay().toInstant(ZoneOffset.UTC)
        val to = LocalDate.parse(endDate).atStartOfDay().toInstant(ZoneOffset.UTC)
        val request = Proto.MerchantCommissionRequest.newBuilder()
            .setMerchantId(merchantId)
            .setDateRange(
                goldbank.v1.common.Common.DateRange.newBuilder()
                    .setFrom(Timestamp.newBuilder().setSeconds(from.epochSecond).build())
                    .setTo(Timestamp.newBuilder().setSeconds(to.epochSecond).build())
                    .build()
            )
            .build()
        return MerchantMapper.toCommissionReport(stub.getCommissionReport(request))
    }
}
