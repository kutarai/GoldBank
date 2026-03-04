package com.unibank.shared.data.remote.grpc

import com.google.protobuf.ByteString
import com.unibank.shared.data.mapper.KycMapper
import com.unibank.shared.data.remote.grpcCall
import com.unibank.shared.domain.model.DocumentStatus
import com.unibank.shared.domain.model.KycStatus
import com.unibank.shared.domain.model.SelfieUploadResult
import com.unibank.shared.domain.model.UploadResult
import com.unibank.shared.domain.util.Result
import io.grpc.ManagedChannel
import kotlinx.coroutines.flow.flow
import unibank.v1.kyc.KycServiceGrpcKt.KycServiceCoroutineStub
import unibank.v1.kyc.KycServiceOuterClass.*

class KycGrpcClient(channel: ManagedChannel) {

    private val stub = KycServiceCoroutineStub(channel)

    companion object {
        private const val CHUNK_SIZE = 32 * 1024 // 32 KB
    }

    suspend fun uploadDocument(
        accountId: String,
        documentType: String,
        fileName: String,
        contentType: String,
        fileBytes: ByteArray,
    ): Result<UploadResult> = grpcCall {
        val requestFlow = flow {
            // First message: metadata
            emit(
                UploadDocumentRequest.newBuilder()
                    .setMetadata(
                        DocumentMetadata.newBuilder()
                            .setAccountId(accountId)
                            .setDocumentType(documentType)
                            .setFileName(fileName)
                            .setContentType(contentType)
                            .setFileSize(fileBytes.size.toLong())
                            .build()
                    )
                    .build()
            )
            // Subsequent messages: file chunks
            var offset = 0
            while (offset < fileBytes.size) {
                val end = minOf(offset + CHUNK_SIZE, fileBytes.size)
                emit(
                    UploadDocumentRequest.newBuilder()
                        .setChunk(ByteString.copyFrom(fileBytes, offset, end - offset))
                        .build()
                )
                offset = end
            }
        }
        KycMapper.toUploadResult(stub.uploadDocument(requestFlow))
    }

    suspend fun uploadSelfie(
        accountId: String,
        contentType: String,
        fileBytes: ByteArray,
        livenessToken: String = "",
    ): Result<SelfieUploadResult> = grpcCall {
        val requestFlow = flow {
            // First message: metadata
            emit(
                UploadSelfieRequest.newBuilder()
                    .setMetadata(
                        SelfieMetadata.newBuilder()
                            .setAccountId(accountId)
                            .setContentType(contentType)
                            .setFileSize(fileBytes.size.toLong())
                            .setLivenessToken(livenessToken)
                            .build()
                    )
                    .build()
            )
            // Subsequent messages: file chunks
            var offset = 0
            while (offset < fileBytes.size) {
                val end = minOf(offset + CHUNK_SIZE, fileBytes.size)
                emit(
                    UploadSelfieRequest.newBuilder()
                        .setChunk(ByteString.copyFrom(fileBytes, offset, end - offset))
                        .build()
                )
                offset = end
            }
        }
        KycMapper.toSelfieUploadResult(stub.uploadSelfie(requestFlow))
    }

    suspend fun getKycStatus(accountId: String): Result<KycStatus> = grpcCall {
        val request = GetKycStatusRequest.newBuilder()
            .setAccountId(accountId)
            .build()
        KycMapper.toKycStatus(stub.getKycStatus(request))
    }

    suspend fun getDocumentStatus(documentId: String): Result<DocumentStatus> = grpcCall {
        val request = GetDocumentStatusRequest.newBuilder()
            .setDocumentId(documentId)
            .build()
        KycMapper.toDocumentStatus(stub.getDocumentStatus(request))
    }
}
