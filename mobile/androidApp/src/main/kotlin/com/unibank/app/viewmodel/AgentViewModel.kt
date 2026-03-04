package com.unibank.app.viewmodel

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.unibank.shared.data.local.SessionManager
import com.unibank.shared.data.remote.grpc.AgentGrpcClient
import com.unibank.shared.domain.model.CashOperationResult
import com.unibank.shared.domain.model.CommissionReport
import com.unibank.shared.domain.model.FloatBalance
import com.unibank.shared.domain.util.Result
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch

class AgentViewModel(
    private val agentClient: AgentGrpcClient,
    private val sessionManager: SessionManager,
) : ViewModel() {

    private val _uiState = MutableStateFlow<AgentUiState>(AgentUiState.Idle)
    val uiState: StateFlow<AgentUiState> = _uiState.asStateFlow()

    private val _floatBalance = MutableStateFlow<FloatBalance?>(null)
    val floatBalance: StateFlow<FloatBalance?> = _floatBalance.asStateFlow()

    private val _commissionReport = MutableStateFlow<CommissionReport?>(null)
    val commissionReport: StateFlow<CommissionReport?> = _commissionReport.asStateFlow()

    private val agentId: String get() = sessionManager.getAccountId() ?: ""

    fun loadFloatBalance() {
        viewModelScope.launch {
            when (val result = agentClient.getFloatBalance(agentId)) {
                is Result.Success -> _floatBalance.value = result.data
                is Result.Failure -> {}
            }
        }
    }

    fun loadCommissionReport(startDate: String, endDate: String) {
        viewModelScope.launch {
            when (val result = agentClient.getCommissionReport(agentId, startDate, endDate)) {
                is Result.Success -> _commissionReport.value = result.data
                is Result.Failure -> {}
            }
        }
    }

    fun cashIn(customerPhone: String, amount: String, currency: String = "ZWG", agentPin: String) {
        _uiState.value = AgentUiState.Loading
        viewModelScope.launch {
            when (val result = agentClient.cashIn(agentId, customerPhone, amount, currency, agentPin)) {
                is Result.Success -> {
                    if (result.data.success) {
                        _uiState.value = AgentUiState.CashSuccess(result.data)
                        loadFloatBalance()
                    } else {
                        _uiState.value = AgentUiState.Error(result.data.message)
                    }
                }
                is Result.Failure -> _uiState.value = AgentUiState.Error("Cash-in failed")
            }
        }
    }

    fun cashOut(customerAccountId: String, amount: String, currency: String = "ZWG", customerPin: String, agentPin: String) {
        _uiState.value = AgentUiState.Loading
        viewModelScope.launch {
            when (val result = agentClient.cashOut(agentId, customerAccountId, amount, currency, customerPin, agentPin)) {
                is Result.Success -> {
                    if (result.data.success) {
                        _uiState.value = AgentUiState.CashSuccess(result.data)
                        loadFloatBalance()
                    } else {
                        _uiState.value = AgentUiState.Error(result.data.message)
                    }
                }
                is Result.Failure -> _uiState.value = AgentUiState.Error("Cash-out failed")
            }
        }
    }

    fun resetState() { _uiState.value = AgentUiState.Idle }
}

sealed interface AgentUiState {
    data object Idle : AgentUiState
    data object Loading : AgentUiState
    data class CashSuccess(val result: CashOperationResult) : AgentUiState
    data class Error(val message: String) : AgentUiState
}
