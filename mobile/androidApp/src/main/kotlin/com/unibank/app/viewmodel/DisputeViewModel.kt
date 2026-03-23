package com.unibank.app.viewmodel

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.unibank.shared.data.local.SessionManager
import com.unibank.shared.data.remote.grpc.AccountGrpcClient
import com.unibank.shared.data.remote.grpc.AiGrpcClient
import com.unibank.shared.domain.model.DisputeDetail
import com.unibank.shared.domain.model.DisputeSummary
import com.unibank.shared.domain.model.DisputeTriage
import com.unibank.shared.domain.util.Result
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch

data class DisputeUiState(
    val disputes: List<DisputeSummary> = emptyList(),
    val selectedDispute: DisputeDetail? = null,
    val triageResult: DisputeTriage? = null,
    val isLoading: Boolean = false,
    val isSubmitting: Boolean = false,
    val statusFilter: String = "all",
    val error: String? = null,
    // Wizard state
    val wizardStep: Int = 0,
    val wizardDescription: String = "",
    val wizardEvidenceBytes: ByteArray? = null,
)

class DisputeViewModel(
    private val accountClient: AccountGrpcClient,
    private val aiClient: AiGrpcClient,
    private val sessionManager: SessionManager,
) : ViewModel() {

    private val _uiState = MutableStateFlow(DisputeUiState())
    val uiState: StateFlow<DisputeUiState> = _uiState.asStateFlow()

    fun loadDisputes() {
        _uiState.update { it.copy(isLoading = true, error = null) }
        viewModelScope.launch {
            val accountId = sessionManager.getAccountId() ?: run {
                _uiState.update { it.copy(isLoading = false, error = "Not authenticated") }
                return@launch
            }
            when (val result = accountClient.listMyDisputes(accountId, _uiState.value.statusFilter)) {
                is Result.Success -> _uiState.update { it.copy(disputes = result.data, isLoading = false) }
                is Result.Failure -> _uiState.update { it.copy(error = result.error.message, isLoading = false) }
            }
        }
    }

    fun loadDisputeDetail(disputeId: String) {
        _uiState.update { it.copy(isLoading = true, error = null) }
        viewModelScope.launch {
            val accountId = sessionManager.getAccountId() ?: run {
                _uiState.update { it.copy(isLoading = false, error = "Not authenticated") }
                return@launch
            }
            when (val result = accountClient.getDisputeDetail(accountId, disputeId)) {
                is Result.Success -> _uiState.update { it.copy(selectedDispute = result.data, isLoading = false) }
                is Result.Failure -> _uiState.update { it.copy(error = result.error.message, isLoading = false) }
            }
        }
    }

    fun setStatusFilter(filter: String) {
        _uiState.update { it.copy(statusFilter = filter) }
        loadDisputes()
    }

    fun setWizardDescription(text: String) {
        _uiState.update { it.copy(wizardDescription = text) }
    }

    fun setWizardEvidence(bytes: ByteArray) {
        _uiState.update { it.copy(wizardEvidenceBytes = bytes) }
    }

    fun submitDispute(transactionId: String) {
        _uiState.update { it.copy(isSubmitting = true, error = null) }
        viewModelScope.launch {
            val accountId = sessionManager.getAccountId() ?: run {
                _uiState.update { it.copy(isSubmitting = false, error = "Not authenticated") }
                return@launch
            }
            val state = _uiState.value
            when (val result = aiClient.triageDispute(
                accountId = accountId,
                transactionId = transactionId,
                description = state.wizardDescription,
                evidenceImage = state.wizardEvidenceBytes,
            )) {
                is Result.Success -> _uiState.update { it.copy(triageResult = result.data, isSubmitting = false) }
                is Result.Failure -> _uiState.update { it.copy(error = result.error.message, isSubmitting = false) }
            }
        }
    }

    fun nextWizardStep() {
        _uiState.update { it.copy(wizardStep = it.wizardStep + 1) }
    }

    fun prevWizardStep() {
        _uiState.update { it.copy(wizardStep = maxOf(0, it.wizardStep - 1)) }
    }

    fun resetWizard() {
        _uiState.update {
            it.copy(
                wizardStep = 0,
                wizardDescription = "",
                wizardEvidenceBytes = null,
                triageResult = null,
                error = null,
            )
        }
    }
}
