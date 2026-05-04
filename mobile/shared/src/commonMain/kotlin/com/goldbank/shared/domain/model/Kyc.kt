package com.goldbank.shared.domain.model

data class KycStatus(
    val accountId: String,
    val kycLevel: Int,
    val overallStatus: String, // "pending", "approved", "rejected"
    val documents: List<DocumentSummary>,
)

data class DocumentSummary(
    val documentId: String,
    val documentType: String,
    val status: String,
    val uploadedAt: String,
)

data class DocumentStatus(
    val documentId: String,
    val documentType: String,
    val status: String,
    val message: String,
    val uploadedAt: String,
    val verifiedAt: String,
)

data class UploadResult(
    val success: Boolean,
    val documentId: String,
    val status: String,
    val message: String,
)

data class SelfieUploadResult(
    val success: Boolean,
    val selfieDocumentId: String,
    val matchConfidence: Double,
    val status: String,
    val message: String,
)
