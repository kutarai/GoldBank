package com.unibank.app.viewmodel

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.unibank.shared.data.local.SessionManager
import com.unibank.shared.data.remote.grpc.BillPayGrpcClient
import com.unibank.shared.domain.model.BillProvider
import com.unibank.shared.domain.model.PayBillResult
import com.unibank.shared.domain.model.SavedBiller
import com.unibank.shared.domain.util.Result
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch

class BillPayViewModel(
    private val billPayClient: BillPayGrpcClient,
    private val sessionManager: SessionManager,
) : ViewModel() {

    private val _providers = MutableStateFlow<List<BillProvider>>(emptyList())
    val providers: StateFlow<List<BillProvider>> = _providers.asStateFlow()

    private val _providersLoading = MutableStateFlow(false)
    val providersLoading: StateFlow<Boolean> = _providersLoading.asStateFlow()

    private val _providersError = MutableStateFlow<String?>(null)
    val providersError: StateFlow<String?> = _providersError.asStateFlow()

    private val _savedBillers = MutableStateFlow<List<SavedBiller>>(emptyList())
    val savedBillers: StateFlow<List<SavedBiller>> = _savedBillers.asStateFlow()

    private val _uiState = MutableStateFlow<BillPayUiState>(BillPayUiState.Idle)
    val uiState: StateFlow<BillPayUiState> = _uiState.asStateFlow()

    fun loadProviders(category: String = "") {
        _providersLoading.value = true
        _providersError.value = null
        viewModelScope.launch {
            when (val result = billPayClient.listProviders(category)) {
                is Result.Success -> _providers.value = result.data
                is Result.Failure -> _providersError.value = result.error.message
            }
            _providersLoading.value = false
        }
    }

    fun loadSavedBillers() {
        viewModelScope.launch {
            val accountId = sessionManager.getAccountId() ?: return@launch
            when (val result = billPayClient.getSavedBillers(accountId)) {
                is Result.Success -> _savedBillers.value = result.data
                is Result.Failure -> {}
            }
        }
    }

    fun payBill(providerId: String, billingReference: String, amount: String, currency: String = "ZWG", pin: String) {
        _uiState.value = BillPayUiState.Loading
        viewModelScope.launch {
            val accountId = sessionManager.getAccountId() ?: return@launch
            when (val result = billPayClient.payBill(accountId, providerId, billingReference, amount, currency, pin)) {
                is Result.Success -> {
                    if (result.data.success) {
                        _uiState.value = BillPayUiState.Success(result.data)
                    } else {
                        _uiState.value = BillPayUiState.Error(result.data.message)
                    }
                }
                is Result.Failure -> _uiState.value = BillPayUiState.Error("Payment failed")
            }
        }
    }

    fun saveBiller(providerId: String, billingReference: String, nickname: String) {
        viewModelScope.launch {
            val accountId = sessionManager.getAccountId() ?: return@launch
            billPayClient.saveBiller(accountId, providerId, billingReference, nickname)
            loadSavedBillers()
        }
    }

    fun resetState() { _uiState.value = BillPayUiState.Idle }
}

sealed interface BillPayUiState {
    data object Idle : BillPayUiState
    data object Loading : BillPayUiState
    data class Success(val result: PayBillResult) : BillPayUiState
    data class Error(val message: String) : BillPayUiState
}
