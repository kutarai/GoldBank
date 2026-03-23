package com.unibank.shared.data.remote.grpc

import com.google.protobuf.ByteString
import com.unibank.shared.data.mapper.AiMapper
import com.unibank.shared.data.remote.grpcCall
import com.unibank.shared.domain.model.BillFields
import com.unibank.shared.domain.model.ChequeFields
import com.unibank.shared.domain.model.DisputeTriage
import com.unibank.shared.domain.model.DocumentFields
import com.unibank.shared.domain.model.FraudExplanation
import com.unibank.shared.domain.model.KycVerificationResult
import com.unibank.shared.domain.model.LoanDocVerification
import com.unibank.shared.domain.model.ModelStatus
import com.unibank.shared.domain.model.ReceiptFields
import com.unibank.shared.domain.model.SpendingInsightsResult
import com.unibank.shared.domain.util.Result
import io.grpc.ManagedChannel
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.map
import unibank.v1.ai.AIServiceGrpcKt.AIServiceCoroutineStub
import unibank.v1.ai.AiService
import unibank.v1.ai.AiService.DocumentType

class AiGrpcClient(channel: ManagedChannel) {

    private val stub = AIServiceCoroutineStub(channel)

    suspend fun verifyIdentity(
        accountId: String,
        selfieImage: ByteArray,
        idDocumentImage: ByteArray,
    ): Result<KycVerificationResult> = grpcCall {
        val request = AiService.VerifyIdentityRequest.newBuilder()
            .setAccountId(accountId)
            .setSelfieImage(ByteString.copyFrom(selfieImage))
            .setIdDocumentImage(ByteString.copyFrom(idDocumentImage))
            .build()
        AiMapper.toKycVerificationResult(stub.verifyIdentity(request))
    }

    suspend fun extractDocumentFields(
        documentImage: ByteArray,
        documentType: DocumentType,
    ): Result<DocumentFields> = grpcCall {
        val request = AiService.ExtractDocumentFieldsRequest.newBuilder()
            .setDocumentImage(ByteString.copyFrom(documentImage))
            .setDocumentType(documentType)
            .build()
        AiMapper.toDocumentFields(stub.extractDocumentFields(request))
    }

    suspend fun verifyProofOfAddress(
        accountId: String,
        documentImage: ByteArray,
    ): Result<KycVerificationResult> = grpcCall {
        val request = AiService.VerifyProofOfAddressRequest.newBuilder()
            .setAccountId(accountId)
            .setDocumentImage(ByteString.copyFrom(documentImage))
            .build()
        val response = stub.verifyProofOfAddress(request)
        KycVerificationResult(
            faceMatchScore = 0.0,
            decision = response.decision.name,
            extractedName = response.extractedFields?.name?.takeIf { it.isNotEmpty() },
            extractedIdNumber = null,
            extractedDob = null,
            nameMatch = response.nameMatch == AiService.FieldMatch.FIELD_MATCH_MATCH,
            idNumberMatch = false,
            dobMatch = false,
            rejectionReason = if (!response.success) response.message.takeIf { it.isNotEmpty() } else null,
        )
    }

    suspend fun extractChequeFields(
        accountId: String,
        chequeImage: ByteArray,
    ): Result<ChequeFields> = grpcCall {
        val request = AiService.ExtractChequeFieldsRequest.newBuilder()
            .setAccountId(accountId)
            .setChequeImage(ByteString.copyFrom(chequeImage))
            .build()
        AiMapper.toChequeFields(stub.extractChequeFields(request))
    }

    suspend fun extractBillFields(
        accountId: String,
        billImage: ByteArray,
    ): Result<BillFields> = grpcCall {
        val request = AiService.ExtractBillFieldsRequest.newBuilder()
            .setAccountId(accountId)
            .setBillImage(ByteString.copyFrom(billImage))
            .build()
        AiMapper.toBillFields(stub.extractBillFields(request))
    }

    fun chat(
        accountId: String,
        message: String,
        history: List<com.unibank.shared.domain.model.ChatMessage>,
    ): Flow<com.unibank.shared.domain.model.ChatResponse> {
        val protoHistory = history.map { msg ->
            AiService.ChatMessage.newBuilder()
                .setRole(msg.role)
                .setContent(msg.content)
                .build()
        }
        val request = AiService.ChatRequest.newBuilder()
            .setAccountId(accountId)
            .setMessage(message)
            .addAllHistory(protoHistory)
            .build()
        return stub.chat(request).map { AiMapper.toChatResponse(it) }
    }

    suspend fun extractReceiptFields(
        accountId: String,
        transactionId: String,
        receiptImage: ByteArray,
    ): Result<ReceiptFields> = grpcCall {
        val request = AiService.ExtractReceiptFieldsRequest.newBuilder()
            .setAccountId(accountId)
            .setTransactionId(transactionId)
            .setReceiptImage(ByteString.copyFrom(receiptImage))
            .build()
        AiMapper.toReceiptFields(stub.extractReceiptFields(request))
    }

    suspend fun getSpendingInsights(accountId: String): Result<SpendingInsightsResult> = grpcCall {
        val request = AiService.GetSpendingInsightsRequest.newBuilder()
            .setAccountId(accountId)
            .build()
        AiMapper.toSpendingInsightsResult(stub.getSpendingInsights(request))
    }

    suspend fun checkLoanEligibility(
        accountId: String,
        desiredAmount: String,
        currency: String,
        tenureMonths: Int,
        purpose: String,
    ): Result<com.unibank.shared.domain.model.LoanEligibility> = grpcCall {
        val request = AiService.CheckLoanEligibilityRequest.newBuilder()
            .setAccountId(accountId)
            .setDesiredAmount(desiredAmount)
            .setCurrency(currency)
            .setTenureMonths(tenureMonths)
            .setPurpose(purpose)
            .build()
        AiMapper.toLoanEligibility(stub.checkLoanEligibility(request))
    }

    suspend fun verifyLoanDocuments(
        accountId: String,
        loanApplicationId: String,
        documentImage: ByteArray,
        documentType: DocumentType,
        declaredIncome: String,
    ): Result<LoanDocVerification> = grpcCall {
        val request = AiService.VerifyLoanDocumentsRequest.newBuilder()
            .setAccountId(accountId)
            .setLoanApplicationId(loanApplicationId)
            .setDocumentImage(ByteString.copyFrom(documentImage))
            .setDocumentType(documentType)
            .setDeclaredIncome(declaredIncome)
            .build()
        AiMapper.toLoanDocVerification(stub.verifyLoanDocuments(request))
    }

    /** Convenience wrapper that hardcodes DOCUMENT_TYPE_PAYSLIP for the loan apply flow. */
    suspend fun verifyPayslip(
        accountId: String,
        documentImage: ByteArray,
        declaredIncome: String,
    ): Result<LoanDocVerification> = verifyLoanDocuments(
        accountId = accountId,
        loanApplicationId = "",
        documentImage = documentImage,
        documentType = DocumentType.DOCUMENT_TYPE_PAYSLIP,
        declaredIncome = declaredIncome,
    )

    suspend fun triageDispute(
        accountId: String,
        transactionId: String,
        description: String,
        evidenceImage: ByteArray?,
    ): Result<DisputeTriage> = grpcCall {
        val builder = AiService.TriageDisputeRequest.newBuilder()
            .setAccountId(accountId)
            .setTransactionId(transactionId)
            .setDescription(description)
        evidenceImage?.let { builder.setEvidenceImage(ByteString.copyFrom(it)) }
        AiMapper.toDisputeTriage(stub.triageDispute(builder.build()))
    }

    suspend fun explainFraudAlert(
        accountId: String,
        transactionId: String,
        fraudRulesTriggered: String,
        riskScore: Double,
    ): Result<FraudExplanation> = grpcCall {
        val request = AiService.ExplainFraudAlertRequest.newBuilder()
            .setAccountId(accountId)
            .setTransactionId(transactionId)
            .setFraudRulesTriggered(fraudRulesTriggered)
            .setRiskScore(riskScore)
            .build()
        AiMapper.toFraudExplanation(stub.explainFraudAlert(request))
    }

    suspend fun getModelStatus(): Result<ModelStatus> = grpcCall {
        val request = AiService.GetModelStatusRequest.newBuilder().build()
        AiMapper.toModelStatus(stub.getModelStatus(request))
    }
}
