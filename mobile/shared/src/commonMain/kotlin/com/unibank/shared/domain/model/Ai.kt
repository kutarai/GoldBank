package com.unibank.shared.domain.model

data class ChatMessage(
    val role: String,
    val content: String,
    val timestamp: Long,
)

data class ChatResponse(
    val token: String,
    val isComplete: Boolean,
    val sessionId: String,
)

data class SpendingInsight(
    val summary: String,
    val category: String,
    val percentageChange: Double,
    val period: String,
)

data class SpendingInsightsResult(
    val insights: List<SpendingInsight>,
    val generatedAt: Long,
)

data class KycVerificationResult(
    val faceMatchScore: Double,
    val decision: String,
    val extractedName: String?,
    val extractedIdNumber: String?,
    val extractedDob: String?,
    val nameMatch: Boolean,
    val idNumberMatch: Boolean,
    val dobMatch: Boolean,
    val rejectionReason: String?,
)

data class DocumentFields(
    val fields: Map<String, String>,
    val confidence: Map<String, String>,
    val documentType: String,
)

data class ChequeFields(
    val chequeNumber: String,
    val amount: String,
    val amountInWords: String,
    val payee: String,
    val date: String,
    val bank: String,
    val branchCode: String,
    val accountNumber: String,
    val amountConsistent: Boolean,
)

data class BillFields(
    val provider: String,
    val accountNumber: String,
    val amount: String,
    val dueDate: String,
    val customerName: String,
    val providerMatchConfidence: String,
)

data class ReceiptFields(
    val merchant: String,
    val totalAmount: String,
    val currency: String,
    val date: String,
    val category: String,
    val items: List<String>,
)

data class LoanEligibility(
    val eligibility: String,
    val estimatedRateMin: Double,
    val estimatedRateMax: Double,
    val maxAmount: String,
    val assessmentText: String,
)

data class LoanDocVerification(
    val extractedIncome: String,
    val extractedEmployer: String,
    val incomeVariancePercent: Double,
    val nameMatch: Boolean,
    val assessmentText: String,
)

data class DisputeTriage(
    val reference: String,
    val classification: String,
    val priority: String,
    val assignedTeam: String,
    val summary: String,
    val recommendedAction: String,
    val expectedResolutionDays: Int,
)

data class FraudExplanation(
    val explanation: String,
    val riskScore: Double,
    val triggeredRules: List<String>,
)

data class ModelStatus(
    val modelName: String,
    val isAvailable: Boolean,
    val inferenceTimeMs: Long,
)
