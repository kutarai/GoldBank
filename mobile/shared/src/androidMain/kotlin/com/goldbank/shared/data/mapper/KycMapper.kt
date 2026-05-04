package com.goldbank.shared.data.mapper

import com.goldbank.shared.domain.model.DocumentStatus
import com.goldbank.shared.domain.model.DocumentSummary
import com.goldbank.shared.domain.model.KycStatus
import com.goldbank.shared.domain.model.SelfieUploadResult
import com.goldbank.shared.domain.model.UploadResult
import goldbank.v1.kyc.KycServiceOuterClass as Proto

object KycMapper {

    fun toKycStatus(response: Proto.GetKycStatusResponse) = KycStatus(
        accountId = response.accountId,
        kycLevel = response.kycLevel,
        overallStatus = response.overallStatus,
        documents = response.documentsList.map { toDocumentSummary(it) },
    )

    fun toDocumentSummary(proto: Proto.KycDocumentSummary) = DocumentSummary(
        documentId = proto.documentId,
        documentType = proto.documentType,
        status = proto.status,
        uploadedAt = proto.uploadedAt?.let { "${it.seconds}" } ?: "",
    )

    fun toDocumentStatus(response: Proto.GetDocumentStatusResponse) = DocumentStatus(
        documentId = response.documentId,
        documentType = response.documentType,
        status = response.status,
        message = response.message,
        uploadedAt = response.uploadedAt?.let { "${it.seconds}" } ?: "",
        verifiedAt = response.verifiedAt?.let { "${it.seconds}" } ?: "",
    )

    fun toUploadResult(response: Proto.UploadDocumentResponse) = UploadResult(
        success = response.success,
        documentId = response.documentId,
        status = response.status,
        message = response.message,
    )

    fun toSelfieUploadResult(response: Proto.UploadSelfieResponse) = SelfieUploadResult(
        success = response.success,
        selfieDocumentId = response.selfieDocumentId,
        matchConfidence = response.matchConfidence,
        status = response.status,
        message = response.message,
    )
}
