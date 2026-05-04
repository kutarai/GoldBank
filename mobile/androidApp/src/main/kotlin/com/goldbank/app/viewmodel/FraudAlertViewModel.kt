package com.goldbank.app.viewmodel

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.goldbank.shared.data.local.SessionManager
import com.goldbank.shared.data.remote.grpc.AccountGrpcClient
import com.goldbank.shared.domain.model.FraudAlertDetail
import com.goldbank.shared.domain.model.FraudAlertSummary
import com.goldbank.shared.domain.util.Result
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch

data class FraudAlertUiState(
    val alerts: List<FraudAlertSummary> = emptyList(),
    val selectedAlert: FraudAlertDetail? = null,
    val unreadCount: Int = 0,
    val isLoading: Boolean = false,
    val error: String? = null,
    val actionResult: String? = null,
)

class FraudAlertViewModel(
    private val accountClient: AccountGrpcClient,
    private val sessionManager: SessionManager,
) : ViewModel() {

    private val _uiState = MutableStateFlow(FraudAlertUiState())
    val uiState: StateFlow<FraudAlertUiState> = _uiState.asStateFlow()

    fun loadAlerts() {
        _uiState.update { it.copy(isLoading = true, error = null) }
        viewModelScope.launch {
            val accountId = sessionManager.getAccountId() ?: run {
                _uiState.update { it.copy(isLoading = false, error = "Not authenticated") }
                return@launch
            }
            when (val result = accountClient.listMyFraudAlerts(accountId)) {
                is Result.Success -> _uiState.update {
                    it.copy(
                        alerts = result.data.first,
                        unreadCount = result.data.second,
                        isLoading = false,
                    )
                }
                is Result.Failure -> _uiState.update { it.copy(error = result.error.message, isLoading = false) }
            }
        }
    }

    fun loadAlertDetail(alertId: String) {
        _uiState.update { it.copy(isLoading = true, error = null) }
        viewModelScope.launch {
            val accountId = sessionManager.getAccountId() ?: run {
                _uiState.update { it.copy(isLoading = false, error = "Not authenticated") }
                return@launch
            }
            when (val result = accountClient.getFraudAlertDetail(accountId, alertId)) {
                is Result.Success -> _uiState.update { it.copy(selectedAlert = result.data, isLoading = false) }
                is Result.Failure -> _uiState.update { it.copy(error = result.error.message, isLoading = false) }
            }
        }
    }

    fun confirmTransaction(alertId: String) {
        viewModelScope.launch {
            val accountId = sessionManager.getAccountId() ?: return@launch
            when (val result = accountClient.confirmTransaction(accountId, alertId)) {
                is Result.Success -> _uiState.update {
                    it.copy(actionResult = if (result.data) "Transaction confirmed successfully." else "Confirmation failed.")
                }
                is Result.Failure -> _uiState.update { it.copy(error = result.error.message) }
            }
        }
    }

    fun reportFraud(alertId: String, description: String) {
        viewModelScope.launch {
            val accountId = sessionManager.getAccountId() ?: return@launch
            when (val result = accountClient.reportFraud(accountId, alertId, description)) {
                is Result.Success -> _uiState.update { it.copy(actionResult = "Fraud reported. Reference: ${result.data}") }
                is Result.Failure -> _uiState.update { it.copy(error = result.error.message) }
            }
        }
    }

    fun clearActionResult() {
        _uiState.update { it.copy(actionResult = null) }
    }
}
