package com.goldbank.shared.domain.model

data class EkubGroup(
    val id: String,
    val name: String,
    val description: String,
    val currency: String,
    val monthlyContribution: String,
    val loanInterestRatePercent: String,
    val status: String,
    val chairmanCustomerId: String,
    val activeMemberCount: Int,
    val createdAtMillis: Long,
    val activatedAtMillis: Long?,
    /** When false, members aren't charged interest on the portion of any loan
     *  that is ≤ their own confirmed contributions. */
    val applyInterestOnContributions: Boolean = true,
)

data class EkubMember(
    val customerId: String,
    val membershipId: String,
    val role: String,
    val firstName: String,
    val lastName: String,
    val phone: String,
    val joinedAtMillis: Long,
    val leftAtMillis: Long?,
)

data class EkubGroupDetail(
    val group: EkubGroup,
    val members: List<EkubMember>,
    val potBalanceAmount: String,
    val potBalanceCurrency: String,
)

data class EkubInvitation(
    val id: String,
    val groupId: String,
    val groupName: String,
    val inviteePhone: String,
    val inviterCustomerId: String,
    val status: String,
    val expiresAtMillis: Long,
    val createdAtMillis: Long,
)

data class EkubContribution(
    val id: String,
    val groupId: String,
    val customerId: String,
    val amount: String,
    val currency: String,
    val period: String,
    val status: String,
    val confirmedByCustomerId: String?,
    val confirmedAtMillis: Long?,
    val createdAtMillis: Long,
    val notes: String,
)

data class EkubMyShare(
    val customerId: String,
    val groupId: String,
    val myContributionsAmount: String,
    val myInterestEarningsAmount: String,
    val myShareTotalAmount: String,
    val currency: String,
    val mySharePercent: String,
)

// v2 — loans
data class EkubLoan(
    val id: String,
    val groupId: String,
    val borrowerCustomerId: String,
    val principal: String,
    val interestRatePercent: String,
    val termMonths: Int,
    val installmentAmount: String,
    val outstandingBalance: String,
    val currency: String,
    val status: String,
    val approvedVotes: Int,
    val rejectedVotes: Int,
    val totalEligibleVoters: Int,
    val createdAtMillis: Long,
    val disbursedAtMillis: Long?,
    /** Requester's own vote: "Approve", "Reject" or "" (not yet voted). */
    val myVote: String = "",
)

data class EkubLoanVote(
    val voterCustomerId: String,
    val approve: Boolean,
    val createdAtMillis: Long,
)
