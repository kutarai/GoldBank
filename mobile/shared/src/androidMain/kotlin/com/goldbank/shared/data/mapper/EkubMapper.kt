package com.goldbank.shared.data.mapper

import com.goldbank.shared.domain.model.EkubContribution
import com.goldbank.shared.domain.model.EkubGroup
import com.goldbank.shared.domain.model.EkubGroupDetail
import com.goldbank.shared.domain.model.EkubInvitation
import com.goldbank.shared.domain.model.EkubLoan
import com.goldbank.shared.domain.model.EkubLoanVote
import com.goldbank.shared.domain.model.EkubMember
import com.goldbank.shared.domain.model.EkubMyShare
import goldbank.v1.ekub.EkubServiceOuterClass as Proto

object EkubMapper {

    fun toGroup(p: Proto.GroupResponse) = EkubGroup(
        id = p.id,
        name = p.name,
        description = p.description,
        currency = p.currency,
        monthlyContribution = p.monthlyContribution,
        loanInterestRatePercent = p.loanInterestRatePercent,
        status = p.status,
        chairmanCustomerId = p.chairmanCustomerId,
        activeMemberCount = p.activeMemberCount,
        createdAtMillis = (p.createdAt?.seconds ?: 0L) * 1000L,
        activatedAtMillis = p.activatedAt?.let { if (it.seconds > 0L) it.seconds * 1000L else null },
        applyInterestOnContributions = p.applyInterestOnContributions,
    )

    fun toMember(p: Proto.MemberResponse) = EkubMember(
        customerId = p.customerId,
        membershipId = p.membershipId,
        role = p.role,
        firstName = p.firstName,
        lastName = p.lastName,
        phone = p.phone,
        joinedAtMillis = (p.joinedAt?.seconds ?: 0L) * 1000L,
        leftAtMillis = p.leftAt?.let { if (it.seconds > 0L) it.seconds * 1000L else null },
    )

    fun toDetail(p: Proto.GroupDetailResponse) = EkubGroupDetail(
        group = toGroup(p.group),
        members = p.membersList.map { toMember(it) },
        potBalanceAmount = p.potBalance?.amount ?: "0.00",
        potBalanceCurrency = p.potBalance?.currency?.ifEmpty { p.group.currency } ?: p.group.currency,
    )

    fun toInvitation(p: Proto.InvitationResponse) = EkubInvitation(
        id = p.id,
        groupId = p.groupId,
        groupName = p.groupName,
        inviteePhone = p.inviteePhone,
        inviterCustomerId = p.inviterCustomerId,
        status = p.status,
        expiresAtMillis = (p.expiresAt?.seconds ?: 0L) * 1000L,
        createdAtMillis = (p.createdAt?.seconds ?: 0L) * 1000L,
    )

    fun toContribution(p: Proto.ContributionResponse) = EkubContribution(
        id = p.id,
        groupId = p.groupId,
        customerId = p.customerId,
        amount = p.amount?.amount ?: "0.00",
        currency = p.amount?.currency ?: "",
        period = p.period,
        status = p.status,
        confirmedByCustomerId = p.confirmedByCustomerId.ifEmpty { null },
        confirmedAtMillis = p.confirmedAt?.let { if (it.seconds > 0L) it.seconds * 1000L else null },
        createdAtMillis = (p.createdAt?.seconds ?: 0L) * 1000L,
        notes = p.notes,
    )

    fun toMyShare(p: Proto.MyShareResponse) = EkubMyShare(
        customerId = p.customerId,
        groupId = p.groupId,
        myContributionsAmount = p.myContributions?.amount ?: "0.00",
        myInterestEarningsAmount = p.myInterestEarnings?.amount ?: "0.00",
        myShareTotalAmount = p.myShareTotal?.amount ?: "0.00",
        currency = p.myContributions?.currency ?: "",
        mySharePercent = p.mySharePercent,
    )

    fun toLoan(p: Proto.LoanResponse) = EkubLoan(
        id = p.id,
        groupId = p.groupId,
        borrowerCustomerId = p.borrowerCustomerId,
        principal = p.principal?.amount ?: "0.00",
        interestRatePercent = p.interestRatePercent,
        termMonths = p.termMonths,
        installmentAmount = p.installmentAmount?.amount ?: "0.00",
        outstandingBalance = p.outstandingBalance?.amount ?: "0.00",
        currency = p.principal?.currency ?: "",
        status = p.status,
        approvedVotes = p.approvedVotes,
        rejectedVotes = p.rejectedVotes,
        totalEligibleVoters = p.totalEligibleVoters,
        createdAtMillis = (p.createdAt?.seconds ?: 0L) * 1000L,
        disbursedAtMillis = p.disbursedAt?.let { if (it.seconds > 0L) it.seconds * 1000L else null },
        myVote = p.myVote,
    )

    fun toVote(p: Proto.VoteResponse) = EkubLoanVote(
        voterCustomerId = p.voterCustomerId,
        approve = p.approve,
        createdAtMillis = (p.createdAt?.seconds ?: 0L) * 1000L,
    )
}
