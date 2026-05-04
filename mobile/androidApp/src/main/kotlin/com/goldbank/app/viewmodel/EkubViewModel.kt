package com.goldbank.app.viewmodel

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.goldbank.shared.data.local.SessionManager
import com.goldbank.shared.data.remote.grpc.EkubGrpcClient
import com.goldbank.shared.domain.model.EkubContribution
import com.goldbank.shared.domain.model.EkubGroup
import com.goldbank.shared.domain.model.EkubGroupDetail
import com.goldbank.shared.domain.model.EkubInvitation
import com.goldbank.shared.domain.model.EkubLoan
import com.goldbank.shared.domain.model.EkubLoanVote
import com.goldbank.shared.domain.model.EkubMyShare
import com.goldbank.shared.domain.util.Result
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch

data class EkubUiState(
    val groups: List<EkubGroup> = emptyList(),
    val invitations: List<EkubInvitation> = emptyList(),
    val selectedDetail: EkubGroupDetail? = null,
    val selectedContributions: List<EkubContribution> = emptyList(),
    val selectedMyShare: EkubMyShare? = null,
    val selectedLoans: List<EkubLoan> = emptyList(),
    val selectedLoanVotes: List<EkubLoanVote> = emptyList(),
    val isLoading: Boolean = false,
    val error: String? = null,
    val flash: String? = null,
)

class EkubViewModel(
    private val ekubClient: EkubGrpcClient,
    private val sessionManager: SessionManager,
) : ViewModel() {

    private val _uiState = MutableStateFlow(EkubUiState())
    val uiState: StateFlow<EkubUiState> = _uiState.asStateFlow()

    private val customerId: String get() = sessionManager.getCustomerId() ?: ""

    fun loadHome() {
        loadMyGroups()
        loadInvitations()
    }

    fun loadMyGroups() = launchWithLoading {
        when (val r = ekubClient.listMyGroups(customerId)) {
            is Result.Success -> _uiState.value = _uiState.value.copy(groups = r.data, isLoading = false)
            is Result.Failure -> fail("load groups", r.error.message)
        }
    }

    fun loadInvitations() = viewModelScope.launch {
        when (val r = ekubClient.listMyInvitations(customerId)) {
            is Result.Success -> _uiState.value = _uiState.value.copy(invitations = r.data)
            is Result.Failure -> fail("load invitations", r.error.message)
        }
    }

    fun createGroup(
        name: String,
        description: String,
        currency: String,
        monthlyContribution: String,
        interestRate: String,
        applyInterestOnContributions: Boolean,
        onCreated: (EkubGroup) -> Unit,
    ) =
        launchWithLoading {
            when (val r = ekubClient.createGroup(customerId, name, description, currency, monthlyContribution, interestRate, applyInterestOnContributions)) {
                is Result.Success -> {
                    _uiState.value = _uiState.value.copy(
                        groups = _uiState.value.groups + r.data,
                        isLoading = false,
                        flash = "Group created — invite at least 2 more members to activate.",
                    )
                    onCreated(r.data)
                }
                is Result.Failure -> fail("create group", r.error.message)
            }
        }

    fun respondToInvitation(invitationId: String, accept: Boolean) = launchWithLoading {
        when (val r = ekubClient.respondToInvitation(invitationId, customerId, accept)) {
            is Result.Success -> {
                loadHome()
                _uiState.value = _uiState.value.copy(
                    isLoading = false,
                    flash = if (accept) "Joined the group." else "Invitation declined.",
                )
            }
            is Result.Failure -> fail("respond", r.error.message)
        }
    }

    fun inviteMember(groupId: String, phone: String) = launchWithLoading {
        when (val r = ekubClient.inviteMember(groupId, customerId, phone)) {
            is Result.Success -> _uiState.value = _uiState.value.copy(isLoading = false, flash = "Invitation sent to $phone.")
            is Result.Failure -> fail("invite", r.error.message)
        }
    }

    fun loadGroupDetail(groupId: String) = launchWithLoading {
        val detail = ekubClient.getGroupDetail(groupId, customerId)
        val share = ekubClient.getMyShare(groupId, customerId)
        val contributions = ekubClient.listGroupContributions(groupId, customerId, "", "")
        val loans = ekubClient.listGroupLoans(groupId, customerId, "")
        _uiState.value = _uiState.value.copy(
            selectedDetail = (detail as? Result.Success)?.data,
            selectedMyShare = (share as? Result.Success)?.data,
            selectedContributions = (contributions as? Result.Success)?.data ?: emptyList(),
            selectedLoans = (loans as? Result.Success)?.data ?: emptyList(),
            isLoading = false,
            error = listOfNotNull(
                (detail as? Result.Failure)?.error?.message,
                (share as? Result.Failure)?.error?.message,
            ).firstOrNull(),
        )
    }

    fun recordContribution(groupId: String, amount: String, period: String, notes: String) = launchWithLoading {
        when (val r = ekubClient.recordContribution(groupId, customerId, amount, period, notes)) {
            is Result.Success -> {
                loadGroupDetail(groupId)
                _uiState.value = _uiState.value.copy(flash = "Contribution submitted, awaiting treasurer.")
            }
            is Result.Failure -> fail("record contribution", r.error.message)
        }
    }

    fun confirmContribution(contributionId: String, groupId: String, approve: Boolean) = launchWithLoading {
        when (val r = ekubClient.confirmContribution(contributionId, customerId, approve, "")) {
            is Result.Success -> {
                loadGroupDetail(groupId)
                _uiState.value = _uiState.value.copy(flash = if (approve) "Contribution confirmed." else "Contribution rejected.")
            }
            is Result.Failure -> fail("confirm", r.error.message)
        }
    }

    fun applyForLoan(groupId: String, principal: String, termMonths: Int, purpose: String) = launchWithLoading {
        when (val r = ekubClient.applyForLoan(groupId, customerId, principal, termMonths, purpose)) {
            is Result.Success -> {
                loadGroupDetail(groupId)
                _uiState.value = _uiState.value.copy(flash = "Loan submitted — vote in progress.")
            }
            is Result.Failure -> fail("apply", r.error.message)
        }
    }

    fun voteOnLoan(loanId: String, groupId: String, approve: Boolean) = launchWithLoading {
        when (val r = ekubClient.voteOnLoan(loanId, customerId, approve)) {
            is Result.Success -> {
                loadGroupDetail(groupId)
                _uiState.value = _uiState.value.copy(flash = if (approve) "Vote recorded: approve." else "Vote recorded: reject.")
            }
            is Result.Failure -> fail("vote", r.error.message)
        }
    }

    fun confirmLoan(loanId: String, groupId: String, approve: Boolean) = launchWithLoading {
        when (val r = ekubClient.confirmLoanByTreasurer(loanId, customerId, approve, "")) {
            is Result.Success -> {
                loadGroupDetail(groupId)
                _uiState.value = _uiState.value.copy(flash = if (approve) "Loan confirmed and disbursed." else "Loan rejected by treasurer.")
            }
            is Result.Failure -> fail("treasurer confirm", r.error.message)
        }
    }

    fun recordRepayment(loanId: String, groupId: String, amount: String) = launchWithLoading {
        when (val r = ekubClient.recordLoanRepayment(loanId, customerId, amount)) {
            is Result.Success -> {
                loadGroupDetail(groupId)
                _uiState.value = _uiState.value.copy(flash = "Repayment recorded.")
            }
            is Result.Failure -> fail("repayment", r.error.message)
        }
    }

    fun clearFlash() { _uiState.value = _uiState.value.copy(flash = null) }
    fun clearError() { _uiState.value = _uiState.value.copy(error = null) }

    private fun launchWithLoading(block: suspend () -> Unit) {
        viewModelScope.launch {
            _uiState.value = _uiState.value.copy(isLoading = true, error = null)
            block()
        }
    }

    private fun fail(op: String, msg: String?) {
        _uiState.value = _uiState.value.copy(isLoading = false, error = msg ?: "Failed to $op")
    }
}
