package com.goldbank.shared.domain.model

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

// --- Dispute Models (Sprint 17) ---
data class DisputeSummary(
    val disputeId: String,
    val reference: String,
    val transactionId: String,
    val disputeType: String,
    val priority: String,
    val status: String,
    val amount: String,
    val currency: String,
    val summary: String,
    val createdAt: Long,
    val resolvedAt: Long?,
)

data class DisputeDetail(
    val disputeId: String,
    val reference: String,
    val transactionId: String,
    val disputeType: String,
    val priority: String,
    val status: String,
    val assignedTeam: String,
    val amount: String,
    val currency: String,
    val userDescription: String,
    val aiSummary: String,
    val aiRecommendedAction: String,
    val classificationConfidence: Double,
    val resolutionNotes: String?,
    val expectedResolution: String,
    val createdAt: Long,
    val resolvedAt: Long?,
)

// --- Fraud Alert Models (Sprint 17) ---
data class FraudAlertSummary(
    val alertId: String,
    val transactionId: String,
    val transactionDescription: String,
    val amount: String,
    val currency: String,
    val riskScore: Double,
    val riskLevel: String,
    val isRead: Boolean,
    val createdAt: Long,
)

data class FraudAlertDetail(
    val alertId: String,
    val transactionId: String,
    val transactionDescription: String,
    val amount: String,
    val currency: String,
    val riskScore: Double,
    val riskLevel: String,
    val aiExplanation: String,
    val triggeredRules: List<String>,
    val counterparty: String,
    val location: String,
    val transactionAt: Long,
    val alertAt: Long,
)

// --- Asset Custody Models (EPIC-020) ---
data class AssetSummary(
    val assetId: String,
    val receiptNumber: String,
    val assetType: String,
    val description: String,
    val quantity: Double,
    val unit: String,
    val currentValueAmount: String,
    val currentValueCurrency: String,
    val verificationStatus: String,
    val status: String,
    val depositHouseName: String,
    val receiptDate: Long,
)

data class AssetDetail(
    val assetId: String,
    val receiptNumber: String,
    val assetType: String,
    val description: String,
    val quantity: Double,
    val unit: String,
    val weightGrams: Double?,
    val purity: Double?,
    val currentValueAmount: String,
    val currentValueCurrency: String,
    val verificationStatus: String,
    val status: String,
    val depositHouseName: String,
    val depositHouseAddress: String,
    val receiptDate: Long,
    val valuations: List<ValuationEntry>,
)

data class ValuationEntry(
    val amount: String,
    val currency: String,
    val valuerName: String,
    val date: Long,
)

data class PortfolioValue(
    val totalValueZwg: String,
    val totalValueUsd: String,
    val assetsByType: List<AssetTypeSummary>,
)

data class AssetTypeSummary(
    val assetType: String,
    val count: Int,
    val totalValueUsd: String,
)

data class DailyPriceEntry(
    val assetType: String,
    val pricePerGramUsd: Double,
    val pricePerOzUsd: Double,
    val date: String,
    val source: String,
)

data class DepositReceiptOcr(
    val depositHouse: String,
    val receiptNumber: String,
    val date: String,
    val depositorName: String,
    val description: String,
    val quantity: String,
    val weight: String,
    val purity: String,
)
