package com.unibank.shared.data.mapper

import com.unibank.shared.domain.model.*
import unibank.v1.ai.AiService as Proto

object AiMapper {

    fun toKycVerificationResult(response: Proto.VerifyIdentityResponse) = KycVerificationResult(
        faceMatchScore = response.faceMatchScore,
        decision = response.decision.name,
        extractedName = response.extractedFields?.fullName?.takeIf { it.isNotEmpty() },
        extractedIdNumber = response.extractedFields?.idNumber?.takeIf { it.isNotEmpty() },
        extractedDob = response.extractedFields?.dateOfBirth?.takeIf { it.isNotEmpty() },
        nameMatch = response.nameMatch == Proto.FieldMatch.FIELD_MATCH_MATCH,
        idNumberMatch = response.idNumberMatch == Proto.FieldMatch.FIELD_MATCH_MATCH,
        dobMatch = response.dobMatch == Proto.FieldMatch.FIELD_MATCH_MATCH,
        rejectionReason = response.rejectionReason.takeIf { it.isNotEmpty() },
    )

    fun toSpendingInsightsResult(response: Proto.GetSpendingInsightsResponse) = SpendingInsightsResult(
        insights = response.insightsList.map { text ->
            SpendingInsight(
                summary = text,
                category = "",
                percentageChange = 0.0,
                period = "",
            )
        },
        generatedAt = response.generatedAt?.seconds ?: 0L,
    )

    fun toChatResponse(response: Proto.ChatResponse) = ChatResponse(
        token = response.token,
        isComplete = response.done,
        sessionId = "",
    )

    fun toChequeFields(response: Proto.ExtractChequeFieldsResponse) = ChequeFields(
        chequeNumber = response.fields?.chequeNumber ?: "",
        amount = response.fields?.amountFigures ?: "",
        amountInWords = response.fields?.amountWords ?: "",
        payee = response.fields?.payee ?: "",
        date = response.fields?.date ?: "",
        bank = response.fields?.bankName ?: "",
        branchCode = response.fields?.branchCode ?: "",
        accountNumber = "",
        amountConsistent = response.amountConsistent,
    )

    fun toBillFields(response: Proto.ExtractBillFieldsResponse) = BillFields(
        provider = response.fields?.provider ?: "",
        accountNumber = response.fields?.accountNumber ?: "",
        amount = response.fields?.amountDue ?: "",
        dueDate = response.fields?.dueDate ?: "",
        customerName = "",
        providerMatchConfidence = response.matchedProviderId.takeIf { it.isNotEmpty() } ?: "",
    )

    fun toReceiptFields(response: Proto.ExtractReceiptFieldsResponse) = ReceiptFields(
        merchant = response.fields?.merchantName ?: "",
        totalAmount = response.fields?.totalAmount ?: "",
        currency = response.fields?.currency ?: "",
        date = response.fields?.date ?: "",
        category = response.fields?.category ?: "",
        items = response.fields?.itemsList ?: emptyList(),
    )

    fun toLoanEligibility(response: Proto.CheckLoanEligibilityResponse) = LoanEligibility(
        eligibility = response.likelihood.name,
        estimatedRateMin = response.estimatedRateMin.toDoubleOrNull() ?: 0.0,
        estimatedRateMax = response.estimatedRateMax.toDoubleOrNull() ?: 0.0,
        maxAmount = "",
        assessmentText = response.assessment,
    )

    fun toLoanDocVerification(response: Proto.VerifyLoanDocumentsResponse) = LoanDocVerification(
        extractedIncome = response.extractedIncome,
        extractedEmployer = response.extractedFields?.employer ?: "",
        incomeVariancePercent = response.variancePercentage,
        nameMatch = response.nameMatch == Proto.FieldMatch.FIELD_MATCH_MATCH,
        assessmentText = response.message,
    )

    fun toDisputeTriage(response: Proto.TriageDisputeResponse) = DisputeTriage(
        reference = response.reference,
        classification = response.classification.name,
        priority = response.priority.name,
        assignedTeam = response.assignedTeam,
        summary = response.summary,
        recommendedAction = response.message,
        expectedResolutionDays = response.expectedResolution.filter { it.isDigit() }.toIntOrNull() ?: 0,
    )

    fun toFraudExplanation(response: Proto.ExplainFraudAlertResponse) = FraudExplanation(
        explanation = response.explanation,
        riskScore = 0.0,
        triggeredRules = response.suggestedActionsList,
    )

    fun toDocumentFields(response: Proto.ExtractDocumentFieldsResponse): DocumentFields {
        val fields = mutableMapOf<String, String>()
        if (response.hasIdFields()) {
            val id = response.idFields
            fields["full_name"] = id.fullName
            fields["id_number"] = id.idNumber
            fields["date_of_birth"] = id.dateOfBirth
            fields["nationality"] = id.nationality
            fields["gender"] = id.gender
            fields["expiry_date"] = id.expiryDate
            fields["document_type"] = id.documentType
        }
        if (response.hasChequeFields()) {
            val c = response.chequeFields
            fields["cheque_number"] = c.chequeNumber
            fields["amount_figures"] = c.amountFigures
            fields["amount_words"] = c.amountWords
            fields["payee"] = c.payee
            fields["drawer"] = c.drawer
            fields["bank_name"] = c.bankName
            fields["branch_code"] = c.branchCode
            fields["date"] = c.date
            fields["currency"] = c.currency
        }
        if (response.hasBillFields()) {
            val b = response.billFields
            fields["provider"] = b.provider
            fields["account_number"] = b.accountNumber
            fields["amount_due"] = b.amountDue
            fields["due_date"] = b.dueDate
            fields["reference"] = b.reference
            fields["currency"] = b.currency
        }
        if (response.hasReceiptFields()) {
            val r = response.receiptFields
            fields["merchant_name"] = r.merchantName
            fields["date"] = r.date
            fields["total_amount"] = r.totalAmount
            fields["currency"] = r.currency
            fields["category"] = r.category
        }
        if (response.hasPayslipFields()) {
            val p = response.payslipFields
            fields["employer"] = p.employer
            fields["employee_name"] = p.employeeName
            fields["gross_salary"] = p.grossSalary
            fields["net_salary"] = p.netSalary
            fields["currency"] = p.currency
            fields["pay_period"] = p.payPeriod
        }
        if (response.hasProofOfAddressFields()) {
            val poa = response.proofOfAddressFields
            fields["name"] = poa.name
            fields["address"] = poa.address
            fields["document_date"] = poa.documentDate
            fields["document_type"] = poa.documentType
        }
        return DocumentFields(
            fields = fields,
            confidence = emptyMap(),
            documentType = response.message,
        )
    }

    fun toModelStatus(response: Proto.GetModelStatusResponse) = ModelStatus(
        modelName = response.visionModel,
        isAvailable = response.ollamaHealthy && response.faceModelLoaded,
        inferenceTimeMs = 0L,
    )
}
