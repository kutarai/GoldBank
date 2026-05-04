package com.goldbank.shared.data.remote.grpc

import com.goldbank.shared.data.mapper.EkubMapper
import com.goldbank.shared.data.remote.grpcCall
import com.goldbank.shared.domain.model.EkubContribution
import com.goldbank.shared.domain.model.EkubGroup
import com.goldbank.shared.domain.model.EkubGroupDetail
import com.goldbank.shared.domain.model.EkubInvitation
import com.goldbank.shared.domain.model.EkubLoan
import com.goldbank.shared.domain.model.EkubLoanVote
import com.goldbank.shared.domain.model.EkubMyShare
import com.goldbank.shared.domain.util.Result
import io.grpc.ManagedChannel
import goldbank.v1.ekub.EkubServiceGrpcKt.EkubServiceCoroutineStub
import goldbank.v1.ekub.EkubServiceOuterClass as Proto

class EkubGrpcClient(channel: ManagedChannel) {

    private val stub = EkubServiceCoroutineStub(channel)

    // -- Group lifecycle ------------------------------------------------------

    suspend fun createGroup(
        customerId: String,
        name: String,
        description: String,
        currency: String,
        monthlyContribution: String,
        loanInterestRatePercent: String,
        applyInterestOnContributions: Boolean,
    ): Result<EkubGroup> = grpcCall {
        val req = Proto.CreateGroupRequest.newBuilder()
            .setCustomerId(customerId)
            .setName(name)
            .setDescription(description)
            .setCurrency(currency)
            .setMonthlyContribution(monthlyContribution)
            .setLoanInterestRatePercent(loanInterestRatePercent)
            .setApplyInterestOnContributions(applyInterestOnContributions)
            .build()
        EkubMapper.toGroup(stub.createGroup(req))
    }

    suspend fun assignRole(groupId: String, requesterCustomerId: String, targetCustomerId: String, role: String): Result<EkubGroup> = grpcCall {
        val req = Proto.AssignRoleRequest.newBuilder()
            .setGroupId(groupId)
            .setRequesterCustomerId(requesterCustomerId)
            .setTargetCustomerId(targetCustomerId)
            .setRole(role)
            .build()
        EkubMapper.toGroup(stub.assignRole(req))
    }

    // -- Membership / invitations --------------------------------------------

    suspend fun inviteMember(groupId: String, inviterCustomerId: String, inviteePhone: String): Result<EkubInvitation> = grpcCall {
        val req = Proto.InviteMemberRequest.newBuilder()
            .setGroupId(groupId)
            .setInviterCustomerId(inviterCustomerId)
            .setInviteePhone(inviteePhone)
            .build()
        EkubMapper.toInvitation(stub.inviteMember(req))
    }

    suspend fun listMyInvitations(customerId: String): Result<List<EkubInvitation>> = grpcCall {
        val req = Proto.ListMyInvitationsRequest.newBuilder().setCustomerId(customerId).build()
        stub.listMyInvitations(req).invitationsList.map { EkubMapper.toInvitation(it) }
    }

    suspend fun respondToInvitation(invitationId: String, customerId: String, accept: Boolean): Result<Boolean> = grpcCall {
        val req = Proto.RespondToInvitationRequest.newBuilder()
            .setInvitationId(invitationId)
            .setCustomerId(customerId)
            .setAccept(accept)
            .build()
        stub.respondToInvitation(req).success
    }

    suspend fun kickMember(groupId: String, requesterCustomerId: String, targetCustomerId: String, reason: String): Result<Boolean> = grpcCall {
        val req = Proto.KickMemberRequest.newBuilder()
            .setGroupId(groupId)
            .setRequesterCustomerId(requesterCustomerId)
            .setTargetCustomerId(targetCustomerId)
            .setReason(reason)
            .build()
        stub.kickMember(req).success
    }

    // -- Contributions -------------------------------------------------------

    suspend fun recordContribution(groupId: String, customerId: String, amount: String, period: String, notes: String): Result<EkubContribution> = grpcCall {
        val req = Proto.RecordContributionRequest.newBuilder()
            .setGroupId(groupId)
            .setCustomerId(customerId)
            .setAmount(amount)
            .setPeriod(period)
            .setNotes(notes)
            .build()
        EkubMapper.toContribution(stub.recordContribution(req))
    }

    suspend fun confirmContribution(contributionId: String, treasurerCustomerId: String, approve: Boolean, notes: String): Result<EkubContribution> = grpcCall {
        val req = Proto.ConfirmContributionRequest.newBuilder()
            .setContributionId(contributionId)
            .setTreasurerCustomerId(treasurerCustomerId)
            .setApprove(approve)
            .setNotes(notes)
            .build()
        EkubMapper.toContribution(stub.confirmContribution(req))
    }

    suspend fun listGroupContributions(groupId: String, requesterCustomerId: String, period: String, statusFilter: String): Result<List<EkubContribution>> = grpcCall {
        val req = Proto.ListGroupContributionsRequest.newBuilder()
            .setGroupId(groupId)
            .setRequesterCustomerId(requesterCustomerId)
            .setPeriod(period)
            .setStatusFilter(statusFilter)
            .build()
        stub.listGroupContributions(req).contributionsList.map { EkubMapper.toContribution(it) }
    }

    // -- Reads ---------------------------------------------------------------

    suspend fun listMyGroups(customerId: String, statusFilter: String = ""): Result<List<EkubGroup>> = grpcCall {
        val req = Proto.ListMyGroupsRequest.newBuilder()
            .setCustomerId(customerId)
            .setStatusFilter(statusFilter)
            .build()
        stub.listMyGroups(req).groupsList.map { EkubMapper.toGroup(it) }
    }

    suspend fun getGroupDetail(groupId: String, requesterCustomerId: String): Result<EkubGroupDetail> = grpcCall {
        val req = Proto.GetGroupDetailRequest.newBuilder()
            .setGroupId(groupId)
            .setRequesterCustomerId(requesterCustomerId)
            .build()
        EkubMapper.toDetail(stub.getGroupDetail(req))
    }

    suspend fun getMyShare(groupId: String, customerId: String): Result<EkubMyShare> = grpcCall {
        val req = Proto.GetMyShareRequest.newBuilder()
            .setGroupId(groupId)
            .setCustomerId(customerId)
            .build()
        EkubMapper.toMyShare(stub.getMyShare(req))
    }

    // -- Loans (v2) ----------------------------------------------------------

    suspend fun applyForLoan(groupId: String, borrowerCustomerId: String, principal: String, termMonths: Int, purpose: String): Result<EkubLoan> = grpcCall {
        val req = Proto.ApplyForLoanRequest.newBuilder()
            .setGroupId(groupId)
            .setBorrowerCustomerId(borrowerCustomerId)
            .setPrincipal(principal)
            .setTermMonths(termMonths)
            .setPurpose(purpose)
            .build()
        EkubMapper.toLoan(stub.applyForLoan(req))
    }

    suspend fun voteOnLoan(loanId: String, voterCustomerId: String, approve: Boolean): Result<EkubLoan> = grpcCall {
        val req = Proto.VoteOnLoanRequest.newBuilder()
            .setLoanId(loanId)
            .setVoterCustomerId(voterCustomerId)
            .setApprove(approve)
            .build()
        EkubMapper.toLoan(stub.voteOnLoan(req))
    }

    suspend fun confirmLoanByTreasurer(loanId: String, treasurerCustomerId: String, approve: Boolean, notes: String): Result<EkubLoan> = grpcCall {
        val req = Proto.ConfirmLoanRequest.newBuilder()
            .setLoanId(loanId)
            .setTreasurerCustomerId(treasurerCustomerId)
            .setApprove(approve)
            .setNotes(notes)
            .build()
        EkubMapper.toLoan(stub.confirmLoanByTreasurer(req))
    }

    suspend fun recordLoanRepayment(loanId: String, treasurerCustomerId: String, amount: String): Result<EkubLoan> = grpcCall {
        val req = Proto.RecordLoanRepaymentRequest.newBuilder()
            .setLoanId(loanId)
            .setTreasurerCustomerId(treasurerCustomerId)
            .setAmount(amount)
            .build()
        EkubMapper.toLoan(stub.recordLoanRepayment(req))
    }

    suspend fun listGroupLoans(groupId: String, requesterCustomerId: String, statusFilter: String = ""): Result<List<EkubLoan>> = grpcCall {
        val req = Proto.ListGroupLoansRequest.newBuilder()
            .setGroupId(groupId)
            .setRequesterCustomerId(requesterCustomerId)
            .setStatusFilter(statusFilter)
            .build()
        stub.listGroupLoans(req).loansList.map { EkubMapper.toLoan(it) }
    }

    suspend fun listMyLoans(customerId: String): Result<List<EkubLoan>> = grpcCall {
        val req = Proto.ListMyLoansRequest.newBuilder().setCustomerId(customerId).build()
        stub.listMyLoans(req).loansList.map { EkubMapper.toLoan(it) }
    }

    suspend fun getLoanDetail(loanId: String, requesterCustomerId: String): Result<Pair<EkubLoan, List<EkubLoanVote>>> = grpcCall {
        val req = Proto.GetLoanDetailRequest.newBuilder()
            .setLoanId(loanId)
            .setRequesterCustomerId(requesterCustomerId)
            .build()
        val resp = stub.getLoanDetail(req)
        EkubMapper.toLoan(resp.loan) to resp.votesList.map { EkubMapper.toVote(it) }
    }
}
